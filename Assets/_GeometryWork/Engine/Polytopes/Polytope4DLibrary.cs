using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>The six convex regular 4-polytopes.</summary>
    public enum RegularPolytope4D { Cell5, Cell8, Cell16, Cell24, Cell120, Cell600 }

    /// <summary>One regular 4-polytope: vertices on the unit 3-sphere, edges, and 2-faces.</summary>
    public sealed class Polytope4D
    {
        public RegularPolytope4D kind;
        /// <summary>Vertices, all at distance 1 from the origin.</summary>
        public Vector4[] vertices;
        /// <summary>Edges as vertex index pairs, a &lt; b.</summary>
        public Vector2Int[] edges;
        /// <summary>2-faces as cyclically ordered vertex index loops (triangles, squares or pentagons).</summary>
        public int[][] faces;
        /// <summary>Edge length on the unit sphere.</summary>
        public float edgeLength;

        /// <summary>3-cells. Not enumerated; this is the known count, with V − E + F − C = 0 (χ of S³).</summary>
        public int CellCount => vertices.Length == 0 ? 0 : faces.Length - edges.Length + vertices.Length;

        public int Degree => vertices.Length == 0 ? 0 : 2 * edges.Length / vertices.Length;
    }

    /// <summary>
    /// The regular 4-polytopes as plain data, so the swarms, the wire builders and the tests all
    /// read one definition.
    ///
    /// | Polytope | V   | E    | F    | C   | face     |
    /// |----------|-----|------|------|-----|----------|
    /// | 5-cell   | 5   | 10   | 10   | 5   | triangle |
    /// | 8-cell   | 16  | 32   | 24   | 8   | square   |
    /// | 16-cell  | 8   | 24   | 32   | 16  | triangle |
    /// | 24-cell  | 24  | 96   | 96   | 24  | triangle |
    /// | 120-cell | 600 | 1200 | 720  | 120 | pentagon |
    /// | 600-cell | 120 | 720  | 1200 | 600 | triangle |
    ///
    /// Vertices come from coordinate orbits. Every orbit applies sign changes to all four
    /// coordinates — the earlier 600-cell built in <c>Polytope4DSwarm</c> flipped only the first
    /// three, which silently dropped 36 of the 120 vertices and 660 of the 720 edges.
    ///
    /// Edges are the vertex pairs at the shortest distance; faces are the chordless, planar edge
    /// cycles of the face length. Neither is hand-written. Results are cached, since the 120-cell's
    /// 180,000 pair tests are cheap once but not free per frame.
    /// </summary>
    public static class Polytope4DLibrary
    {
        static readonly Polytope4D[] cache = new Polytope4D[6];

        public static string Label(RegularPolytope4D p)
        {
            switch (p)
            {
                case RegularPolytope4D.Cell5: return "5-cell";
                case RegularPolytope4D.Cell8: return "8-cell";
                case RegularPolytope4D.Cell16: return "16-cell";
                case RegularPolytope4D.Cell24: return "24-cell";
                case RegularPolytope4D.Cell600: return "600-cell";
                default: return "120-cell";
            }
        }

        /// <summary>Vertices per 2-face.</summary>
        public static int FaceSize(RegularPolytope4D p) =>
            p == RegularPolytope4D.Cell8 ? 4 : p == RegularPolytope4D.Cell120 ? 5 : 3;

        /// <summary>The full polytope, built once and cached. Do not mutate the result.</summary>
        public static Polytope4D Get(RegularPolytope4D p)
        {
            int i = (int)p;
            if (cache[i] != null) return cache[i];
            var poly = new Polytope4D { kind = p, vertices = Vertices(p) };
            poly.edges = Edges(poly.vertices, out poly.edgeLength);
            poly.faces = Faces(poly.vertices, poly.edges, FaceSize(p));
            return cache[i] = poly;
        }

        // ---- vertices -------------------------------------------------------------

        /// <summary>Fresh vertex array on the unit 3-sphere.</summary>
        public static Vector4[] Vertices(RegularPolytope4D p)
        {
            var list = new List<Vector4>();
            float phi = (1f + Mathf.Sqrt(5f)) * .5f;
            switch (p)
            {
                case RegularPolytope4D.Cell5:
                {
                    float s = 1f / Mathf.Sqrt(5f);
                    list.Add(new Vector4(1, 1, 1, -s));
                    list.Add(new Vector4(1, -1, -1, -s));
                    list.Add(new Vector4(-1, 1, -1, -s));
                    list.Add(new Vector4(-1, -1, 1, -s));
                    list.Add(new Vector4(0, 0, 0, 4f * s));
                    break;
                }
                case RegularPolytope4D.Cell8:
                    Orbit(list, AllPerm, 1, 1, 1, 1);
                    break;
                case RegularPolytope4D.Cell16:
                    Orbit(list, AllPerm, 1, 0, 0, 0);
                    break;
                case RegularPolytope4D.Cell24:
                    Orbit(list, AllPerm, 1, 1, 0, 0);
                    break;
                case RegularPolytope4D.Cell600:
                    // 8 + 16 (a 24-cell of radius 1) + 96 even permutations of (φ, 1, 1/φ, 0) / 2.
                    Orbit(list, AllPerm, 1, 0, 0, 0);
                    Orbit(list, AllPerm, .5f, .5f, .5f, .5f);
                    Orbit(list, EvenPerm, phi * .5f, .5f, .5f / phi, 0);
                    break;
                default:
                {
                    float r5 = Mathf.Sqrt(5f), ip = 1f / phi, ip2 = ip * ip, p2 = phi * phi;
                    Orbit(list, AllPerm, 0, 0, 2, 2);
                    Orbit(list, AllPerm, 1, 1, 1, r5);
                    Orbit(list, AllPerm, ip2, phi, phi, phi);
                    Orbit(list, AllPerm, ip, ip, ip, p2);
                    Orbit(list, EvenPerm, 0, ip2, 1, p2);
                    Orbit(list, EvenPerm, 0, ip, phi, r5);
                    Orbit(list, EvenPerm, ip, 1, phi, 2);
                    break;
                }
            }
            var result = list.ToArray();
            for (int i = 0; i < result.Length; i++) result[i] = result[i].normalized;
            return result;
        }

        // ---- edges and faces ---------------------------------------------------------

        /// <summary>Pairs at the shortest nonzero distance (within 1%) are edges.</summary>
        public static Vector2Int[] Edges(Vector4[] v, out float edgeLength)
        {
            float shortest = float.MaxValue;
            for (int i = 0; i < v.Length; i++)
                for (int j = i + 1; j < v.Length; j++)
                {
                    float d = (v[i] - v[j]).sqrMagnitude;
                    if (d > 1e-8f && d < shortest) shortest = d;
                }
            var edges = new List<Vector2Int>();
            if (shortest == float.MaxValue) { edgeLength = 0f; return edges.ToArray(); }
            edgeLength = Mathf.Sqrt(shortest);
            float limit = shortest * 1.01f * 1.01f;
            for (int i = 0; i < v.Length; i++)
                for (int j = i + 1; j < v.Length; j++)
                    if ((v[i] - v[j]).sqrMagnitude <= limit) edges.Add(new Vector2Int(i, j));
            return edges.ToArray();
        }

        /// <summary>
        /// Chordless planar cycles of length <paramref name="size"/>. Each cycle is found once:
        /// it starts at its smallest vertex and its second vertex is smaller than its last.
        /// </summary>
        public static int[][] Faces(Vector4[] v, Vector2Int[] edges, int size)
        {
            var adj = new List<int>[v.Length];
            var isEdge = new HashSet<long>();
            for (int i = 0; i < v.Length; i++) adj[i] = new List<int>();
            foreach (var e in edges)
            {
                adj[e.x].Add(e.y); adj[e.y].Add(e.x);
                isEdge.Add(Key(e.x, e.y));
            }

            var faces = new List<int[]>();
            var path = new int[size];
            for (int s = 0; s < v.Length; s++)
            {
                path[0] = s;
                Extend(v, adj, isEdge, path, 1, size, faces);
            }
            return faces.ToArray();
        }

        static void Extend(Vector4[] v, List<int>[] adj, HashSet<long> isEdge, int[] path, int depth, int size,
                           List<int[]> faces)
        {
            int last = path[depth - 1];
            if (depth == size)
            {
                if (!isEdge.Contains(Key(last, path[0])) || path[1] > last) return;
                // Chordless: no edge between non-consecutive vertices of the loop.
                for (int a = 0; a < size; a++)
                    for (int b = a + 2; b < size; b++)
                        if (!(a == 0 && b == size - 1) && isEdge.Contains(Key(path[a], path[b]))) return;
                if (!Planar(v, path)) return;
                faces.Add((int[])path.Clone());
                return;
            }
            foreach (int n in adj[last])
            {
                if (n <= path[0]) continue;
                bool used = false;
                for (int k = 1; k < depth; k++) if (path[k] == n) { used = true; break; }
                if (used) continue;
                path[depth] = n;
                Extend(v, adj, isEdge, path, depth + 1, size, faces);
            }
        }

        /// <summary>True when every vertex of the loop lies in the affine plane of its first three.</summary>
        static bool Planar(Vector4[] v, int[] loop)
        {
            if (loop.Length <= 3) return true;
            Vector4 o = v[loop[0]];
            Vector4 a = v[loop[1]] - o, b = v[loop[2]] - o;
            // Gram–Schmidt basis of the plane.
            Vector4 e1 = a.normalized;
            Vector4 e2 = (b - e1 * Vector4.Dot(b, e1)).normalized;
            for (int i = 3; i < loop.Length; i++)
            {
                Vector4 d = v[loop[i]] - o;
                Vector4 r = d - e1 * Vector4.Dot(d, e1) - e2 * Vector4.Dot(d, e2);
                if (r.sqrMagnitude > 1e-6f) return false;
            }
            return true;
        }

        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // ---- projection --------------------------------------------------------------

        /// <summary>
        /// Stereographic projection from the pole w = 1. Circles on S³ stay circles. Guarded: points
        /// within <paramref name="epsilon"/> of the pole are pushed out to a large finite radius
        /// instead of infinity.
        /// </summary>
        public static Vector3 Stereographic(Vector4 p, float epsilon = 1e-3f)
        {
            float d = 1f - p.w;
            if (d < epsilon) d = epsilon;
            return new Vector3(p.x, p.y, p.z) / d;
        }

        /// <summary>Perspective projection from a viewer at w = <paramref name="distance"/> (> 1).</summary>
        public static Vector3 Perspective(Vector4 p, float distance)
        {
            float d = Mathf.Max(distance - p.w, 1e-3f);
            return new Vector3(p.x, p.y, p.z) * (distance / d);
        }

        // ---- orbits ------------------------------------------------------------------

        static readonly int[][] AllPerm = BuildPermutations(false);
        static readonly int[][] EvenPerm = BuildPermutations(true);

        /// <summary>
        /// Every arrangement of (a, b, c, d) under the given permutations with every sign change on
        /// every coordinate, deduplicated (a sign flip on 0 is a duplicate).
        /// </summary>
        static void Orbit(List<Vector4> into, int[][] perms, float a, float b, float c, float d)
        {
            float[] m = { a, b, c, d };
            foreach (var perm in perms)
                for (int s = 0; s < 16; s++)
                {
                    var v = new Vector4(
                        m[perm[0]] * ((s & 1) == 0 ? -1 : 1),
                        m[perm[1]] * ((s & 2) == 0 ? -1 : 1),
                        m[perm[2]] * ((s & 4) == 0 ? -1 : 1),
                        m[perm[3]] * ((s & 8) == 0 ? -1 : 1));
                    bool dup = false;
                    for (int i = 0; i < into.Count; i++)
                        if ((into[i] - v).sqrMagnitude < 1e-8f) { dup = true; break; }
                    if (!dup) into.Add(v);
                }
        }

        static int[][] BuildPermutations(bool evenOnly)
        {
            var result = new List<int[]>();
            Permute(new[] { 0, 1, 2, 3 }, 0, result, evenOnly);
            return result.ToArray();
        }

        static void Permute(int[] p, int k, List<int[]> into, bool evenOnly)
        {
            if (k == p.Length)
            {
                if (!evenOnly || Parity(p) == 0) into.Add((int[])p.Clone());
                return;
            }
            for (int i = k; i < p.Length; i++)
            {
                int t = p[k]; p[k] = p[i]; p[i] = t;
                Permute(p, k + 1, into, evenOnly);
                t = p[k]; p[k] = p[i]; p[i] = t;
            }
        }

        static int Parity(int[] p)
        {
            int swaps = 0;
            var q = (int[])p.Clone();
            for (int i = 0; i < q.Length; i++)
                while (q[i] != i) { int j = q[i]; int t = q[i]; q[i] = q[j]; q[j] = t; swaps++; }
            return swaps & 1;
        }
    }
}
