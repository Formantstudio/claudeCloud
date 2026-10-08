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
    /// Edges are never hand-written. Vertices come from <see cref="Polytope4DLibrary"/>, then every pair is
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

        /// <summary>
        /// The vertex set for a polytope, so other systems can reuse one definition. Delegates to
        /// <see cref="Polytope4DLibrary"/>, whose orbits flip the sign of every coordinate — the
        /// version that used to live here flipped only three, which left the 600-cell with 84 of
        /// its 120 vertices and 60 of its 720 edges.
        /// </summary>
        public static Vector4[] VerticesFor(Polytope p) => Vertices(p);

        static Vector4[] Vertices(Polytope p) => Polytope4DLibrary.Vertices((RegularPolytope4D)(int)p);

        // ---- per-frame -------------------------------------------------------

        protected override void UpdateNodes(Vector3[] into, float time)
        {
            if (vertices4D == null) return;
            float tau = Mathf.PI * 2f;
            int count = Mathf.Min(into.Length, vertices4D.Length);

            var shared = useSharedAxis ? Hyperspace4DAxis.Current : null;

            // One SO(4) rotor per frame: the four plane rates are one bivector, exponentiated
            // together rather than applied as a chain of plane turns, so the motion is a true
            // geodesic and does not depend on an arbitrary plane order.
            float t = time * tau;
            var rotor = Rotor4.FromBivector(simpleRates.x * t, mixedRates.x * t, 0f,
                                            0f, mixedRates.y * t, simpleRates.y * t);

            for (int i = 0; i < count; i++)
            {
                Vector4 v = vertices4D[i];
                if (shared) { into[i] = shared.RotateAndProject(v) * radius; continue; }
                Vector4 r = rotor.Rotate(v);
                into[i] = Project(r.x, r.y, r.z, r.w) * radius;
            }
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
