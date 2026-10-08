using System.Collections.Generic;
using UnityEngine;
using polyhedronGenerator.scripts;
using polyhedronGenerator.scripts.solids;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Mixes the installed polyhedronGenerator library into the swarm family. Every solid it can
    /// build becomes a node-and-edge figure with the same particle layer, swirl and fractal
    /// clustering as the n-cubes.
    ///
    /// The integration point is `MeshBuilder.edges()`, which returns an explicit edge list — so
    /// the Platonics, prisms, antiprisms and all of the Johnson solids come through one code path
    /// without hand-writing topology for any of them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PolyhedronSwarm : NodeEdgeSwarmBase
    {
        public enum Solid
        {
            Tetrahedron, Cube, Octahedron, Dodecahedron, Icosahedron,
            Prism, Antiprism, Johnson
        }

        [Header("Polyhedron")]
        public Solid solid = Solid.Icosahedron;
        [Tooltip("Side count for the prism and antiprism.")]
        [Range(3, 32)] public int prismSides = 6;
        [Range(.1f, 4f)] public float prismHeight = 1.4f;
        [Tooltip("Johnson solid index. 0 is J1 (Square Pyramid); there are 92 in the set.")]
        [Range(0, 91)] public int johnsonIndex;
        [Tooltip("Merges coincident vertices before reading the edge list, so shared corners are one node.")]
        public bool weldVertices = true;
        [Range(.0001f, .05f)] public float weldDistance = .001f;
        [Tooltip("Pushes every vertex onto a sphere. Turns the solid into its spherical form.")]
        [Range(0, 1)] public float spherizeSolid;

        [Header("Motion")]
        [Range(-2, 2)] public float spinX = .06f;
        [Range(-2, 2)] public float spinY = .11f;
        [Range(-2, 2)] public float spinZ;

        protected override string SwarmName => solid + " swarm";
        protected override int NodeCount => Mathf.Max(baseVertices == null ? 0 : baseVertices.Length, 1);

        protected override bool TopologyDirty =>
            builtSolid != solid || builtPrismSides != prismSides || builtJohnson != johnsonIndex ||
            builtWeld != weldVertices || !Mathf.Approximately(builtHeight, prismHeight) ||
            baseVertices == null;

        Vector3[] baseVertices;
        Solid builtSolid;
        int builtPrismSides, builtJohnson;
        bool builtWeld;
        float builtHeight;
        string buildError;

        /// <summary>
        /// Vertices and edge pairs for a solid, without needing a component. `edgePairs` comes back
        /// as a flat list of index pairs. Returns null when the generator fails.
        /// </summary>
        public static Vector3[] VerticesFor(Solid solid, int prismSides, float prismHeight,
                                            int johnsonIndex, out List<int> edgePairs)
        {
            edgePairs = new List<int>();
            MeshBuilder shape;
            try { shape = GenerateStatic(solid, prismSides, prismHeight, johnsonIndex); }
            catch { return null; }
            if (shape == null || shape.vectorList == null || shape.vectorList.Count == 0) return null;

            shape.removeDuplicateVertices(.001f);
            var verts = shape.vectorList.ToArray();

            float longest = 0f;
            foreach (var v in verts) longest = Mathf.Max(longest, v.magnitude);
            if (longest > 1e-5f)
                for (int i = 0; i < verts.Length; i++) verts[i] /= longest;

            var edges = shape.edges();
            if (edges != null)
            {
                int count = verts.Length;
                var seen = new HashSet<int>();
                foreach (var e in edges)
                {
                    if (e == null || e.Count < 2) continue;
                    int i = e[0], j = e[1];
                    if (i < 0 || j < 0 || i >= count || j >= count || i == j) continue;
                    int key = i < j ? i * count + j : j * count + i;
                    if (!seen.Add(key)) continue;
                    edgePairs.Add(i); edgePairs.Add(j);
                }
            }
            return verts;
        }

        static MeshBuilder GenerateStatic(Solid solid, int prismSides, float prismHeight, int johnsonIndex)
        {
            switch (solid)
            {
                case Solid.Cube: return Cube.generate(1);
                case Solid.Octahedron: return Octahedron.generate(1);
                case Solid.Dodecahedron: return Dodecahedron.generate(1);
                case Solid.Icosahedron: return Icosahedron.generate(1);
                case Solid.Prism: return Prisma.generate(1f, Mathf.Max(prismSides, 3), prismHeight);
                case Solid.Antiprism: return AntiPrisma.generate(1f, Mathf.Max(prismSides, 3), prismHeight);
                case Solid.Johnson: return Johnson.generate(1, Mathf.Clamp(johnsonIndex, 0, 91));
                default: return Tetrahedon.generate(1);
            }
        }

        protected override void BuildEdges(List<int> a, List<int> b)
        {
            builtSolid = solid;
            builtPrismSides = prismSides;
            builtJohnson = johnsonIndex;
            builtWeld = weldVertices;
            builtHeight = prismHeight;
            buildError = null;

            MeshBuilder shape = null;
            try { shape = Generate(); }
            catch (System.Exception e) { buildError = e.Message; }

            if (shape == null || shape.vectorList == null || shape.vectorList.Count == 0)
            {
                buildError ??= "generator returned no vertices";
                baseVertices = new Vector3[1];
                return;
            }

            if (weldVertices) shape.removeDuplicateVertices(weldDistance);

            baseVertices = shape.vectorList.ToArray();

            // Normalise to a unit figure so `radius` means the same thing for every solid.
            float longest = 0f;
            foreach (var v in baseVertices) longest = Mathf.Max(longest, v.magnitude);
            if (longest > 1e-5f)
                for (int i = 0; i < baseVertices.Length; i++) baseVertices[i] /= longest;

            var edges = shape.edges();
            if (edges == null) { buildError = "no edge list"; return; }

            int count = baseVertices.Length;
            var seen = new HashSet<int>();
            foreach (var edge in edges)
            {
                if (edge == null || edge.Count < 2) continue;
                int i = edge[0], j = edge[1];
                if (i < 0 || j < 0 || i >= count || j >= count || i == j) continue;
                // Deduplicate: faces share edges, so the same pair comes back more than once.
                int key = i < j ? i * count + j : j * count + i;
                if (!seen.Add(key)) continue;
                a.Add(i); b.Add(j);
            }
        }

        MeshBuilder Generate()
        {
            switch (solid)
            {
                case Solid.Cube: return Cube.generate(1);
                case Solid.Octahedron: return Octahedron.generate(1);
                case Solid.Dodecahedron: return Dodecahedron.generate(1);
                case Solid.Icosahedron: return Icosahedron.generate(1);
                case Solid.Prism: return Prisma.generate(1f, Mathf.Max(prismSides, 3), prismHeight);
                case Solid.Antiprism: return AntiPrisma.generate(1f, Mathf.Max(prismSides, 3), prismHeight);
                case Solid.Johnson: return Johnson.generate(1, Mathf.Clamp(johnsonIndex, 0, 91));
                default: return Tetrahedon.generate(1);
            }
        }

        protected override void UpdateNodes(Vector3[] into, float time)
        {
            if (baseVertices == null) return;
            var turn = Quaternion.Euler(
                spinX * time * 360f, spinY * time * 360f, spinZ * time * 360f);

            int count = Mathf.Min(into.Length, baseVertices.Length);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = baseVertices[i];
                if (spherizeSolid > 0f && p.sqrMagnitude > 1e-6f)
                    p = Vector3.Lerp(p, p.normalized, spherizeSolid);
                into[i] = turn * p * radius;
            }
        }

        protected override string Describe() =>
            buildError != null
                ? solid + " failed: " + buildError
                : (solid == Solid.Johnson ? "Johnson J" + (johnsonIndex + 1) : solid.ToString())
                  + " · " + NodeTotal + " vertices, " + EdgeTotal + " edges";

        [ContextMenu("Next Johnson solid")]
        public void NextJohnson() { solid = Solid.Johnson; johnsonIndex = (johnsonIndex + 1) % 92; }
    }
}
