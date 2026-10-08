using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Topological facts about an unwelded triangle soup, measured after welding coincident
    /// positions. This is how the engine checks its own output: an unwelded wire mesh has no shared
    /// vertices, so V - E + F only means something once equal positions are merged.
    ///
    /// Welding is positional, so a self-intersecting immersion is only measured correctly when no
    /// two distinct parameter points land on exactly the same position. Grids that sample a
    /// self-intersection curve at the same point from both sheets will over-weld; the tests choose
    /// resolutions that avoid that.
    /// </summary>
    public struct TopologyReport
    {
        public int vertices, edges, faces;
        /// <summary>Edges used by exactly one triangle.</summary>
        public int boundaryEdges;
        /// <summary>Edges used by more than two triangles.</summary>
        public int nonManifoldEdges;
        /// <summary>Interior edges whose two triangles traverse them in the same direction.</summary>
        public int orientationConflicts;
        /// <summary>Closed loops formed by the boundary edges.</summary>
        public int boundaryLoops;
        /// <summary>Connected components (by shared welded vertices).</summary>
        public int components;
        /// <summary>Positions that are NaN or infinite.</summary>
        public int nonFinite;
        /// <summary>Triangles whose area is zero after welding (collapsed at poles, for instance).</summary>
        public int degenerateFaces;

        public int EulerCharacteristic => vertices - edges + faces;
        public bool IsClosed => boundaryEdges == 0;
        public bool IsManifold => nonManifoldEdges == 0;
        public bool IsOrientable => orientationConflicts == 0;

        public override string ToString() =>
            "V " + vertices + "  E " + edges + "  F " + faces + "  chi " + EulerCharacteristic +
            "  boundary " + boundaryEdges + " (" + boundaryLoops + " loops)  non-manifold " + nonManifoldEdges +
            "  orientation conflicts " + orientationConflicts + "  components " + components +
            "  degenerate " + degenerateFaces + "  non-finite " + nonFinite;

        /// <summary>
        /// Measures the triangles in <paramref name="positions"/> / <paramref name="indices"/>.
        /// Positions closer than <paramref name="weld"/> are merged. Degenerate triangles (two
        /// corners welded together) are dropped from the count, which is what makes a pole fan
        /// measure as a disk rather than as a ring of slivers.
        /// </summary>
        public static TopologyReport Measure(IList<Vector3> positions, IList<int> indices, float weld = 1e-4f)
        {
            var t = new TopologyReport();
            int n = positions.Count;
            var map = new int[n];
            var cells = new Dictionary<long, List<int>>(LongMix.Instance);
            var welded = new List<Vector3>();
            float inv = 1f / Mathf.Max(weld, 1e-9f);

            for (int i = 0; i < n; i++)
            {
                Vector3 p = positions[i];
                if (!IsFinite(p)) { t.nonFinite++; map[i] = -1; continue; }
                int cx = Mathf.FloorToInt(p.x * inv), cy = Mathf.FloorToInt(p.y * inv), cz = Mathf.FloorToInt(p.z * inv);
                int found = -1;
                for (int dz = -1; dz <= 1 && found < 0; dz++)
                for (int dy = -1; dy <= 1 && found < 0; dy++)
                for (int dx = -1; dx <= 1 && found < 0; dx++)
                {
                    if (!cells.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out var list)) continue;
                    foreach (int w in list)
                        if ((welded[w] - p).sqrMagnitude <= weld * weld) { found = w; break; }
                }
                if (found < 0)
                {
                    found = welded.Count;
                    welded.Add(p);
                    long k = Key(cx, cy, cz);
                    if (!cells.TryGetValue(k, out var list)) cells[k] = list = new List<int>();
                    list.Add(found);
                }
                map[i] = found;
            }
            t.vertices = welded.Count;

            // Directed edge counts: key (a,b) with a<b, value = (uses, sum of directions).
            var edgeUse = new Dictionary<long, Vector2Int>(LongMix.Instance);
            var used = new bool[welded.Count];
            var parent = new int[welded.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            for (int f = 0; f + 2 < indices.Count; f += 3)
            {
                int a = map[indices[f]], b = map[indices[f + 1]], c = map[indices[f + 2]];
                if (a < 0 || b < 0 || c < 0) continue;
                if (a == b || b == c || c == a) { t.degenerateFaces++; continue; }
                t.faces++;
                used[a] = used[b] = used[c] = true;
                Union(parent, a, b); Union(parent, b, c);
                CountEdge(edgeUse, a, b); CountEdge(edgeUse, b, c); CountEdge(edgeUse, c, a);
            }

            // A welded vertex used by no surviving face (only by degenerate slivers) is not part
            // of the surface.
            int unused = 0;
            for (int i = 0; i < used.Length; i++) if (!used[i]) unused++;
            t.vertices -= unused;

            var boundaryNext = new Dictionary<int, List<int>>();
            foreach (var kv in edgeUse)
            {
                t.edges++;
                int uses = kv.Value.x, direction = kv.Value.y;
                if (uses == 1)
                {
                    t.boundaryEdges++;
                    int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                    Link(boundaryNext, a, b); Link(boundaryNext, b, a);
                }
                else if (uses > 2) t.nonManifoldEdges++;
                else if (direction != 0) t.orientationConflicts++;
            }

            // Boundary loops: connected components of the boundary graph.
            var seen = new HashSet<int>();
            foreach (var start in boundaryNext.Keys)
            {
                if (seen.Contains(start)) continue;
                t.boundaryLoops++;
                var stack = new Stack<int>();
                stack.Push(start); seen.Add(start);
                while (stack.Count > 0)
                    foreach (int m in boundaryNext[stack.Pop()])
                        if (seen.Add(m)) stack.Push(m);
            }

            var roots = new HashSet<int>();
            for (int i = 0; i < used.Length; i++) if (used[i]) roots.Add(Find(parent, i));
            t.components = roots.Count;
            return t;
        }

        /// <summary>Measures a builder's current contents.</summary>
        public static TopologyReport Measure(WireMeshBuilder b, float weld = 1e-4f) =>
            Measure(b.vertices, b.triangles, weld);

        public static bool IsFinite(Vector3 p) =>
            !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
              float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));

        static long Key(int x, int y, int z) =>
            ((long)(x & 0x1fffff) << 42) | ((long)(y & 0x1fffff) << 21) | (long)(z & 0x1fffff);

        static void CountEdge(Dictionary<long, Vector2Int> edges, int a, int b)
        {
            int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            long k = ((long)lo << 32) | (uint)hi;
            edges.TryGetValue(k, out var v);
            edges[k] = new Vector2Int(v.x + 1, v.y + (a < b ? 1 : -1));
        }

        static void Link(Dictionary<int, List<int>> g, int a, int b)
        {
            if (!g.TryGetValue(a, out var l)) g[a] = l = new List<int>();
            l.Add(b);
        }

        /// <summary>
        /// long's own hash is lo ^ hi, which collides badly for edge keys (lo, lo + 1) and turned a
        /// 300k-vertex measurement from milliseconds into seconds. SplitMix64 finaliser instead.
        /// </summary>
        sealed class LongMix : IEqualityComparer<long>
        {
            public static readonly LongMix Instance = new LongMix();
            public bool Equals(long a, long b) => a == b;
            public int GetHashCode(long k)
            {
                ulong z = (ulong)k + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return (int)(z ^ (z >> 31));
            }
        }

        static int Find(int[] p, int i) { while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; } return i; }
        static void Union(int[] p, int a, int b) { a = Find(p, a); b = Find(p, b); if (a != b) p[a] = b; }
    }
}
