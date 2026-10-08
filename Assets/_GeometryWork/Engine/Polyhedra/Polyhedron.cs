using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// A polygon mesh: vertex positions and faces as vertex loops, each counter-clockwise seen from
    /// outside. This is what the Conway operators read and write (<see cref="Conway"/>), so it keeps
    /// whole polygons rather than triangles: a hexagon of a truncated icosahedron stays one face.
    ///
    /// Adjacency is derived on demand from the directed edges: in an oriented closed manifold every
    /// directed edge a→b belongs to exactly one face and its reverse b→a to the neighbour, which is
    /// what <see cref="Validate"/> checks.
    /// </summary>
    public sealed class Polyhedron
    {
        public string name = "";
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int[]> faces = new List<int[]>();

        public int VertexCount => vertices.Count;
        public int FaceCount => faces.Count;

        /// <summary>Each edge is in two faces, so half the total face size.</summary>
        public int EdgeCount
        {
            get { int n = 0; foreach (var f in faces) n += f.Length; return n / 2; }
        }

        public int EulerCharacteristic => VertexCount - EdgeCount + FaceCount;

        public static long Key(int a, int b) => ((long)a << 32) | (uint)b;

        public Polyhedron Clone()
        {
            var p = new Polyhedron { name = name };
            p.vertices.AddRange(vertices);
            foreach (var f in faces) p.faces.Add((int[])f.Clone());
            return p;
        }

        // ---- geometry ----------------------------------------------------------

        public Vector3 FaceCentroid(int f)
        {
            var face = faces[f];
            Vector3 c = Vector3.zero;
            foreach (int v in face) c += vertices[v];
            return c / face.Length;
        }

        /// <summary>Newell's normal: well defined for non-planar and non-convex loops. Unit length, or zero.</summary>
        public Vector3 FaceNormal(int f)
        {
            var face = faces[f];
            Vector3 n = Vector3.zero;
            for (int i = 0; i < face.Length; i++)
            {
                Vector3 a = vertices[face[i]], b = vertices[face[(i + 1) % face.Length]];
                n.x += (a.y - b.y) * (a.z + b.z);
                n.y += (a.z - b.z) * (a.x + b.x);
                n.z += (a.x - b.x) * (a.y + b.y);
            }
            float m = n.magnitude;
            return m > 1e-12f ? n / m : Vector3.zero;
        }

        /// <summary>Largest distance of a vertex from its face's best plane, relative to the mean edge length.</summary>
        public float Planarity()
        {
            float worst = 0f;
            for (int f = 0; f < faces.Count; f++)
            {
                Vector3 n = FaceNormal(f), c = FaceCentroid(f);
                foreach (int v in faces[f]) worst = Mathf.Max(worst, Mathf.Abs(Vector3.Dot(vertices[v] - c, n)));
            }
            return worst / Mathf.Max(MeanEdgeLength(), 1e-12f);
        }

        public float MeanEdgeLength()
        {
            double sum = 0; int n = 0;
            foreach (var f in faces)
                for (int i = 0; i < f.Length; i++)
                {
                    sum += (vertices[f[i]] - vertices[f[(i + 1) % f.Length]]).magnitude;
                    n++;
                }
            return n > 0 ? (float)(sum / n) : 0f;
        }

        /// <summary>Six times the enclosed volume, positive when the faces wind outwards.</summary>
        public float SignedVolume()
        {
            double vol = 0;
            foreach (var f in faces)
            {
                Vector3 a = vertices[f[0]];
                for (int i = 1; i + 1 < f.Length; i++)
                    vol += Vector3.Dot(a, Vector3.Cross(vertices[f[i]], vertices[f[i + 1]]));
            }
            return (float)vol;
        }

        /// <summary>Moves the vertex centroid to the origin and scales the mean vertex radius to <paramref name="radius"/>.</summary>
        public void Normalize(float radius = 1f)
        {
            if (vertices.Count == 0) return;
            Vector3 c = Vector3.zero;
            foreach (var v in vertices) c += v;
            c /= vertices.Count;
            double r = 0;
            for (int i = 0; i < vertices.Count; i++) { vertices[i] -= c; r += vertices[i].magnitude; }
            float s = r > 1e-12 ? radius / (float)(r / vertices.Count) : 1f;
            for (int i = 0; i < vertices.Count; i++) vertices[i] *= s;
        }

        /// <summary>
        /// Winds every face outwards as seen from the vertex centroid. For seeds, which are convex;
        /// the operators preserve orientation themselves.
        /// </summary>
        public void OrientOutward()
        {
            Vector3 c = Vector3.zero;
            foreach (var v in vertices) c += v;
            c /= Mathf.Max(vertices.Count, 1);
            for (int f = 0; f < faces.Count; f++)
                if (Vector3.Dot(FaceNormal(f), FaceCentroid(f) - c) < 0f) System.Array.Reverse(faces[f]);
        }

        // ---- adjacency ---------------------------------------------------------

        /// <summary>Directed edge a→b to the face it bounds.</summary>
        public Dictionary<long, int> EdgeFaces()
        {
            var map = new Dictionary<long, int>(EdgeCount * 2);
            for (int f = 0; f < faces.Count; f++)
            {
                var face = faces[f];
                for (int i = 0; i < face.Length; i++) map[Key(face[i], face[(i + 1) % face.Length])] = f;
            }
            return map;
        }

        /// <summary>
        /// Walks the faces around each vertex. For a vertex v, start from an outgoing edge v→q in face
        /// f; the next face counter-clockwise (seen from outside) is the one holding v→prev_f(v). The
        /// result lists, per vertex, the neighbours q_k in that order: face k is the one holding v→q_k,
        /// and the edge between faces k and k + 1 is v–q_(k+1).
        /// </summary>
        public List<int>[] NeighbourRings()
        {
            var faceOf = new Dictionary<long, int>(EdgeCount * 2);
            var prevOf = new Dictionary<long, int>(EdgeCount * 2);
            var firstOut = new int[vertices.Count];
            for (int i = 0; i < firstOut.Length; i++) firstOut[i] = -1;
            for (int f = 0; f < faces.Count; f++)
            {
                var face = faces[f];
                int n = face.Length;
                for (int i = 0; i < n; i++)
                {
                    int v = face[i], next = face[(i + 1) % n], prev = face[(i + n - 1) % n];
                    faceOf[Key(v, next)] = f;
                    prevOf[Key(v, next)] = prev;
                    if (firstOut[v] < 0) firstOut[v] = next;
                }
            }
            var rings = new List<int>[vertices.Count];
            for (int v = 0; v < vertices.Count; v++)
            {
                var ring = rings[v] = new List<int>(6);
                int q = firstOut[v];
                if (q < 0) continue;
                int guard = 0;
                do
                {
                    ring.Add(q);
                    if (!prevOf.TryGetValue(Key(v, q), out q)) break;
                } while (q != ring[0] && ++guard < 1000);
            }
            return rings;
        }

        /// <summary>Undirected edges, a &lt; b.</summary>
        public List<Vector2Int> Edges()
        {
            var list = new List<Vector2Int>(EdgeCount);
            foreach (var f in faces)
                for (int i = 0; i < f.Length; i++)
                {
                    int a = f[i], b = f[(i + 1) % f.Length];
                    if (a < b) list.Add(new Vector2Int(a, b));
                }
            return list;
        }

        /// <summary>
        /// Null when this is a closed, consistently oriented 2-manifold with sane faces; otherwise the
        /// first problem found.
        /// </summary>
        public string Validate()
        {
            int nv = vertices.Count;
            foreach (var v in vertices)
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                    return "a vertex is not finite";
            var seen = new HashSet<long>();
            var faceCount = new int[nv];
            for (int f = 0; f < faces.Count; f++)
            {
                var face = faces[f];
                if (face == null || face.Length < 3) return "face " + f + " has fewer than three vertices";
                var inFace = new HashSet<int>();
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i], b = face[(i + 1) % face.Length];
                    if (a < 0 || a >= nv) return "face " + f + " indexes vertex " + a + " of " + nv;
                    if (!inFace.Add(a)) return "face " + f + " repeats vertex " + a;
                    if (!seen.Add(Key(a, b))) return "directed edge " + a + "→" + b + " is in two faces (non-manifold or mis-oriented)";
                    faceCount[a]++;
                }
            }
            foreach (long k in seen)
            {
                int a = (int)(k >> 32), b = (int)(uint)k;
                if (!seen.Contains(Key(b, a))) return "edge " + a + "→" + b + " has no reverse (open boundary)";
            }
            var rings = NeighbourRings();
            for (int v = 0; v < nv; v++)
            {
                if (faceCount[v] == 0) return "vertex " + v + " is in no face";
                // One fan around the vertex: the walk visits every face that holds it.
                if (rings[v].Count != faceCount[v]) return "vertex " + v + " is pinched (" + rings[v].Count + " of " + faceCount[v] + " faces in one fan)";
            }
            return null;
        }

        /// <summary>Counts of faces by side number, e.g. "8×3 6×4".</summary>
        public string FaceSignature()
        {
            var counts = new SortedDictionary<int, int>();
            foreach (var f in faces) counts[f.Length] = counts.TryGetValue(f.Length, out int n) ? n + 1 : 1;
            var sb = new StringBuilder();
            foreach (var kv in counts) { if (sb.Length > 0) sb.Append(' '); sb.Append(kv.Value).Append('×').Append(kv.Key); }
            return sb.ToString();
        }

        public override string ToString() =>
            (string.IsNullOrEmpty(name) ? "polyhedron" : name) + ": V " + VertexCount + "  E " + EdgeCount + "  F " + FaceCount +
            "  (" + FaceSignature() + ")";

        // ---- wire output -------------------------------------------------------

        /// <summary>
        /// Emits every face as a polygon whose interior fan edges never draw
        /// (<see cref="WireMeshBuilder.Polygon"/>), so the wire shows exactly the polyhedron's edges.
        /// </summary>
        public void Emit(WireMeshBuilder into) => Emit(into, Quaternion.identity, 1f);

        /// <summary>As <see cref="Emit(WireMeshBuilder)"/>, rotated and scaled about the origin.</summary>
        public void Emit(WireMeshBuilder into, Quaternion rotation, float scale)
        {
            var scratch = new List<Vector3>(8);
            foreach (var f in faces)
            {
                scratch.Clear();
                foreach (int v in f) scratch.Add(rotation * (vertices[v] * scale));
                into.Polygon(scratch);
            }
        }
    }
}
