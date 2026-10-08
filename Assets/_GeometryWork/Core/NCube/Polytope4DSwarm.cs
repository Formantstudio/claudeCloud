using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The six regular convex 4-polytopes, rotated in 4-D and projected to 3-D.
    ///
    /// | Polytope        | Vertices | Edges | Cells              |
    /// |-----------------|----------|-------|--------------------|
    /// | 5-cell          | 5        | 10    | 5 tetrahedra       |
    /// | 8-cell/tesseract| 16       | 32    | 8 cubes            |
    /// | 16-cell         | 8        | 24    | 16 tetrahedra      |
    /// | 24-cell         | 24       | 96    | 24 octahedra       |
    /// | 120-cell        | 600      | 1200  | 120 dodecahedra    |
    /// | 600-cell        | 120      | 720   | 600 tetrahedra     |
    ///
    /// The 120-cell is the headline and it is *cheaper* than the Dekeract: 600 nodes plus 1,200
    /// edges is 3,000 particles at two per edge, against the Dekeract's 11,264.
    ///
    /// Edges are never hand-written. Vertices come from coordinate orbits, then every pair is
    /// measured once at build time, the shortest distance is taken as the edge length, and any
    /// pair within tolerance of it becomes an edge. 600 vertices is 180,000 pairs — trivial, once.
    /// That one routine gives the whole table for free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Polytope4DSwarm : NodeEdgeSwarmBase
    {
        public enum Polytope { Cell5, Cell8, Cell16, Cell24, Cell120, Cell600 }
        public enum Projection { Stereographic, Perspective, Orthographic }

        [Header("Polytope")]
        public Polytope polytope = Polytope.Cell120;
        [Tooltip("How close a pair must be to the shortest distance to count as an edge.")]
        [Range(.002f, .2f)] public float edgeTolerance = .04f;

        [Header("4-D rotation")]
        [Tooltip("Use the scene's Hyperspace4DAxis instead of the rates below, so this figure shares the 4-D camera with everything else.")]
        public bool useSharedAxis = true;
        [Tooltip("Rotation rate in the xy and zw planes. Equal rates give the isoclinic turn.")]
        public Vector2 simpleRates = new Vector2(.09f, .09f);
        [Tooltip("Rotation rate in the mixed xz and yw planes — reads as tumbling through itself.")]
        public Vector2 mixedRates = new Vector2(.03f, 0f);

        [Header("Projection")]
        public Projection projection = Projection.Perspective;
        [Tooltip("Viewer distance along w. Near 1 the stereographic inversion gets violent.")]
        [Range(1.05f, 6f)] public float wDistance = 2.4f;

        protected override string SwarmName => Label(polytope) + " swarm";
        protected override int NodeCount => Mathf.Max(vertices4D == null ? 0 : vertices4D.Length, 1);
        protected override bool TopologyDirty =>
            vertices4D == null || builtPolytope != polytope ||
            !Mathf.Approximately(builtTolerance, edgeTolerance);

        Vector4[] vertices4D;
        Polytope builtPolytope;
        float builtTolerance;

        static string Label(Polytope p)
        {
            switch (p)
            {
                case Polytope.Cell5: return "5-cell";
                case Polytope.Cell8: return "8-cell";
                case Polytope.Cell16: return "16-cell";
                case Polytope.Cell24: return "24-cell";
                case Polytope.Cell600: return "600-cell";
                default: return "120-cell";
            }
        }

        protected override void BuildEdges(List<int> a, List<int> b)
        {
            builtPolytope = polytope;
            builtTolerance = edgeTolerance;
            vertices4D = Vertices(polytope);

            if (vertices4D == null || vertices4D.Length < 2) { vertices4D = new Vector4[1]; return; }

            // Normalise to the unit 3-sphere so `radius` means the same for every polytope.
            for (int i = 0; i < vertices4D.Length; i++)
            {
                float m = vertices4D[i].magnitude;
                if (m > 1e-6f) vertices4D[i] /= m;
            }

            // Shortest pairwise distance is the edge length for any regular polytope.
            float shortest = float.MaxValue;
            for (int i = 0; i < vertices4D.Length; i++)
                for (int j = i + 1; j < vertices4D.Length; j++)
                {
                    float d = (vertices4D[i] - vertices4D[j]).sqrMagnitude;
                    if (d > 1e-8f && d < shortest) shortest = d;
                }
            if (shortest == float.MaxValue) return;

            float limit = Mathf.Sqrt(shortest) * (1f + edgeTolerance);
            float limitSq = limit * limit;
            for (int i = 0; i < vertices4D.Length; i++)
                for (int j = i + 1; j < vertices4D.Length; j++)
                    if ((vertices4D[i] - vertices4D[j]).sqrMagnitude <= limitSq) { a.Add(i); b.Add(j); }
        }

        // ---- vertex sets -----------------------------------------------------

        /// <summary>The vertex set for a polytope, so other systems can reuse one definition.</summary>
        public static Vector4[] VerticesFor(Polytope p) => Vertices(p);

        static Vector4[] Vertices(Polytope p)
        {
            switch (p)
            {
                case Polytope.Cell5: return Cell5();
                case Polytope.Cell8: return Cell8();
                case Polytope.Cell16: return Cell16();
                case Polytope.Cell24: return Cell24();
                case Polytope.Cell600: return Cell600();
                default: return Cell120();
            }
        }

        /// <summary>5 vertices: the 4-simplex, as 5 of the 5-cube's corners in a symmetric frame.</summary>
        static Vector4[] Cell5() => new[]
        {
            new Vector4( 1,  1,  1, -1 / Mathf.Sqrt(5)),
            new Vector4( 1, -1, -1, -1 / Mathf.Sqrt(5)),
            new Vector4(-1,  1, -1, -1 / Mathf.Sqrt(5)),
            new Vector4(-1, -1,  1, -1 / Mathf.Sqrt(5)),
            new Vector4( 0,  0,  0,  4 / Mathf.Sqrt(5)),
        };

        /// <summary>16 vertices: every sign combination of (+/-1, +/-1, +/-1, +/-1).</summary>
        static Vector4[] Cell8()
        {
            var list = new List<Vector4>(16);
            for (int m = 0; m < 16; m++)
                list.Add(new Vector4(
                    (m & 1) == 0 ? -1 : 1, (m & 2) == 0 ? -1 : 1,
                    (m & 4) == 0 ? -1 : 1, (m & 8) == 0 ? -1 : 1));
            return list.ToArray();
        }

        /// <summary>8 vertices: the four axes, both signs.</summary>
        static Vector4[] Cell16() => new[]
        {
            new Vector4( 1, 0, 0, 0), new Vector4(-1, 0, 0, 0),
            new Vector4(0,  1, 0, 0), new Vector4(0, -1, 0, 0),
            new Vector4(0, 0,  1, 0), new Vector4(0, 0, -1, 0),
            new Vector4(0, 0, 0,  1), new Vector4(0, 0, 0, -1),
        };

        /// <summary>24 vertices: all permutations of (+/-1, +/-1, 0, 0).</summary>
        static Vector4[] Cell24()
        {
            var list = new List<Vector4>(24);
            int[,] pairs = { { 0, 1 }, { 0, 2 }, { 0, 3 }, { 1, 2 }, { 1, 3 }, { 2, 3 } };
            for (int k = 0; k < 6; k++)
                for (int s = 0; s < 4; s++)
                {
                    var v = Vector4.zero;
                    v[pairs[k, 0]] = (s & 1) == 0 ? -1 : 1;
                    v[pairs[k, 1]] = (s & 2) == 0 ? -1 : 1;
                    list.Add(v);
                }
            return list.ToArray();
        }

        /// <summary>
        /// 120 vertices of the 600-cell: the 24-cell's 24, plus 96 from even permutations of
        /// (+/-phi, +/-1, +/-1/phi, 0) / 2.
        /// </summary>
        static Vector4[] Cell600()
        {
            float phi = (1f + Mathf.Sqrt(5f)) * .5f;
            var list = new List<Vector4>(120);
            foreach (var v in Cell24()) list.Add(v);

            float[] magnitudes = { phi, 1f, 1f / phi, 0f };
            foreach (var perm in EvenPermutations())
                for (int s = 0; s < 8; s++)
                {
                    var v = new Vector4(
                        magnitudes[perm[0]] * ((s & 1) == 0 ? -1 : 1),
                        magnitudes[perm[1]] * ((s & 2) == 0 ? -1 : 1),
                        magnitudes[perm[2]] * ((s & 4) == 0 ? -1 : 1),
                        magnitudes[perm[3]]);
                    AddUnique(list, v * .5f);
                }
            return list.ToArray();
        }

        /// <summary>
        /// 600 vertices of the 120-cell. Built from its standard coordinate orbits: sign changes
        /// of golden-ratio tuples, plus even permutations of the longer orbits.
        /// </summary>
        static Vector4[] Cell120()
        {
            float phi = (1f + Mathf.Sqrt(5f)) * .5f;
            float r5 = Mathf.Sqrt(5f);
            float ip = 1f / phi;      // phi - 1
            float ip2 = ip * ip;      // 2 - phi
            float p2 = phi * phi;     // phi + 1

            var list = new List<Vector4>(600);

            // Orbits that take all permutations.
            AllPermutations(list, 0f, 0f, 2f, 2f);
            AllPermutations(list, 1f, 1f, 1f, r5);
            AllPermutations(list, ip2, phi, phi, phi);
            AllPermutations(list, ip, ip, ip, p2);

            // Orbits that take only even permutations.
            EvenPermutations(list, 0f, ip2, 1f, p2);
            EvenPermutations(list, 0f, ip, phi, r5);
            EvenPermutations(list, ip, 1f, phi, 2f);

            return list.ToArray();
        }

        // ---- permutation helpers --------------------------------------------

        static readonly int[][] AllPerm = BuildPermutations(false);
        static readonly int[][] EvenPerm = BuildPermutations(true);

        static int[][] EvenPermutations() => EvenPerm;

        static int[][] BuildPermutations(bool evenOnly)
        {
            var result = new List<int[]>();
            int[] p = { 0, 1, 2, 3 };
            Permute(p, 0, result, evenOnly);
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
                (p[k], p[i]) = (p[i], p[k]);
                Permute(p, k + 1, into, evenOnly);
                (p[k], p[i]) = (p[i], p[k]);
            }
        }

        /// <summary>0 for an even permutation, 1 for odd.</summary>
        static int Parity(int[] p)
        {
            int swaps = 0;
            var q = (int[])p.Clone();
            for (int i = 0; i < q.Length; i++)
                while (q[i] != i)
                {
                    int j = q[i];
                    (q[i], q[j]) = (q[j], q[i]);
                    swaps++;
                }
            return swaps & 1;
        }

        static void AllPermutations(List<Vector4> into, float a, float b, float c, float d) =>
            Orbit(into, AllPerm, a, b, c, d);

        static void EvenPermutations(List<Vector4> into, float a, float b, float c, float d) =>
            Orbit(into, EvenPerm, a, b, c, d);

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
                    AddUnique(into, v);
                }
        }

        /// <summary>Sign flips on a zero component duplicate, so the orbits need deduplicating.</summary>
        static void AddUnique(List<Vector4> into, Vector4 v)
        {
            for (int i = 0; i < into.Count; i++)
                if ((into[i] - v).sqrMagnitude < 1e-6f) return;
            into.Add(v);
        }

        // ---- per-frame -------------------------------------------------------

        protected override void UpdateNodes(Vector3[] into, float time)
        {
            if (vertices4D == null) return;
            float tau = Mathf.PI * 2f;
            int count = Mathf.Min(into.Length, vertices4D.Length);

            var shared = useSharedAxis ? Hyperspace4DAxis.Current : null;

            for (int i = 0; i < count; i++)
            {
                Vector4 v = vertices4D[i];
                if (shared) { into[i] = shared.RotateAndProject(v) * radius; continue; }
                float x = v.x, y = v.y, z = v.z, w = v.w;

                Rotate(ref x, ref y, simpleRates.x * time * tau);
                Rotate(ref z, ref w, simpleRates.y * time * tau);
                Rotate(ref x, ref z, mixedRates.x * time * tau);
                Rotate(ref y, ref w, mixedRates.y * time * tau);

                into[i] = Project(x, y, z, w) * radius;
            }
        }

        static void Rotate(ref float p, ref float q, float angle)
        {
            if (angle == 0f) return;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float p0 = p, q0 = q;
            p = p0 * c - q0 * s;
            q = p0 * s + q0 * c;
        }

        Vector3 Project(float x, float y, float z, float w)
        {
            switch (projection)
            {
                case Projection.Stereographic:
                {
                    // Guarded: w -> 1 sends the point to infinity.
                    float d = 1f - w;
                    if (Mathf.Abs(d) < .08f) d = Mathf.Sign(d == 0f ? 1f : d) * .08f;
                    return new Vector3(x, y, z) / d * .5f;
                }
                case Projection.Perspective:
                {
                    float d = Mathf.Max(wDistance, 1.05f) - w;
                    return new Vector3(x, y, z) * (Mathf.Max(wDistance, 1.05f) / Mathf.Max(d, .08f));
                }
                default:
                    return new Vector3(x, y, z);
            }
        }

        protected override string Describe() =>
            Label(polytope) + " · " + NodeTotal + " vertices, " + EdgeTotal + " edges";

        [ContextMenu("120-cell")] public void Use120() { polytope = Polytope.Cell120; }
        [ContextMenu("600-cell")] public void Use600() { polytope = Polytope.Cell600; }
        [ContextMenu("24-cell")] public void Use24() { polytope = Polytope.Cell24; }
    }
}
