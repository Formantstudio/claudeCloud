using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Shared topology and projection for the n-cube swarms. Rotates in n dimensions as a chain of
    /// 2-plane rotations, then collapses to 3-D one dimension at a time.
    ///
    /// The chain stays deliberately: the left/right quaternion form <see cref="Rotor4"/> uses is
    /// special to SO(4) (SO(4) ≅ SU(2)×SU(2)/Z₂ has no analogue for n ≥ 5), and these swarms run
    /// from 4 to 14 dimensions. The 4-D figures (<see cref="Polytope4DSwarm"/>,
    /// <see cref="Hyperspace4DAxis"/>) use the rotor.
    ///
    /// DekeractTetraSwarm is deliberately left alone. This is a separate parameterised family, one
    /// component per dimension:
    ///
    /// | n  | Component      | Vertices | Edges | Particles at 2/edge |
    /// |----|----------------|----------|-------|---------------------|
    /// | 4  | TesseractSwarm | 16       | 32    | 80                  |
    /// | 5  | PenteractSwarm | 32       | 80    | 192                 |
    /// | 6  | HexeractSwarm  | 64       | 192   | 448                 |
    /// | 7  | HepteractSwarm | 128      | 448   | 1,024               |
    /// | 8  | OcteractSwarm  | 256      | 1,024 | 2,304               |
    /// | 9  | EnneractSwarm  | 512      | 2,304 | 5,120               |
    /// | 10 | DekeractSwarm  | 1,024    | 5,120 | 11,264              |
    ///
    /// Vertices are 2^n and edges are n * 2^(n-1). n = 10 at two particles per edge is 11,264,
    /// which is the budget the original Dekeract swarm already runs at.
    /// </summary>
    public abstract class NCubeSwarmBase : NodeEdgeSwarmBase
    {
        public enum ProjectionMode { Perspective, Orthographic, Mixed }

        [Header("n-cube shape and projection")]
        [Tooltip("0 = a readable 3-cube arrangement, 1 = the full n-D projection.")]
        [Range(0, 1)] public float projectionMorph = .65f;
        public bool animateProjection = true;
        public ProjectionMode projection = ProjectionMode.Perspective;
        [Tooltip("Viewer distance per collapsed dimension. Lower is a more violent projection.")]
        [Range(1.2f, 8f)] public float viewerDistance = 2.6f;
        [Tooltip("How many 2-plane rotations to compose. More planes tumble through more dimensions at once.")]
        [Range(1, 20)] public int rotationPlanes = 20;
        [Range(0, 2)] public float rotationSpeed = .25f;

        /// <summary>Dimension count. Each subclass pins this.</summary>
        public abstract int Dimensions { get; }

        public int VertexCount => 1 << Clamp(Dimensions);
        public int EdgeCount => Clamp(Dimensions) * (1 << (Clamp(Dimensions) - 1));

        protected override int NodeCount => VertexCount;
        protected override bool TopologyDirty => builtDimensions != Clamp(Dimensions);

        int builtDimensions;
        float[] coordinate;
        readonly float[] cosine = new float[20];
        readonly float[] sine = new float[20];

        static int Clamp(int n) => Mathf.Clamp(n, 2, 14);

        protected override void BuildEdges(List<int> a, List<int> b)
        {
            int n = Clamp(Dimensions);
            builtDimensions = n;
            coordinate = new float[n];

            // One edge per vertex per axis where that vertex has a 0 bit: n * 2^(n-1) total.
            int vertices = 1 << n;
            for (int v = 0; v < vertices; v++)
                for (int d = 0; d < n; d++)
                    if ((v & (1 << d)) == 0) { a.Add(v); b.Add(v | (1 << d)); }
        }

        protected override void UpdateNodes(Vector3[] into, float time)
        {
            int n = builtDimensions;
            if (coordinate == null || coordinate.Length != n) coordinate = new float[n];

            float morph = animateProjection && Application.isPlaying
                ? Mathf.Lerp(.15f, .95f, .5f + .5f * Mathf.Sin(time * .16f))
                : projectionMorph;

            int planes = Mathf.Clamp(rotationPlanes, 1, 20);
            for (int r = 0; r < planes; r++)
            {
                float angle = .27f * (r + 1) + time * rotationSpeed * (.3f + .07f * r);
                cosine[r] = Mathf.Cos(angle);
                sine[r] = Mathf.Sin(angle);
            }

            // Corner coordinate so every vertex sits the same distance from the origin whatever
            // the dimension: 1/sqrt(n).
            float corner = 1f / Mathf.Sqrt(n);
            int vertices = Mathf.Min(into.Length, 1 << n);

            for (int v = 0; v < vertices; v++)
            {
                for (int d = 0; d < n; d++) coordinate[d] = (v & (1 << d)) == 0 ? -corner : corner;

                for (int r = 0; r < planes; r++)
                {
                    int a = r % n, b = (a + 3) % n;
                    if (a == b) continue;
                    float x = coordinate[a], y = coordinate[b];
                    coordinate[a] = x * cosine[r] - y * sine[r];
                    coordinate[b] = x * sine[r] + y * cosine[r];
                }

                Vector3 full = Collapse(n);
                // The readable endpoint: a plain 3-cube from the lowest three bits, nudged by the
                // projection so the morph has somewhere to travel from.
                Vector3 cube = new Vector3(
                    (v & 1) == 0 ? -.65f : .65f,
                    (v & 2) == 0 ? -.65f : .65f,
                    (v & 4) == 0 ? -.65f : .65f) + full * .18f;

                into[v] = Vector3.Lerp(cube, full, morph) * radius;
            }
        }

        /// <summary>
        /// n-D to 3-D, collapsing one dimension at a time from the highest down to 3, so the
        /// projection is a chain of perspective divisions rather than a single drop.
        /// </summary>
        Vector3 Collapse(int n)
        {
            float x = coordinate[0], y = coordinate[1], z = coordinate[2];
            if (projection == ProjectionMode.Orthographic) return new Vector3(x, y, z) * 1.55f;

            float d = Mathf.Max(viewerDistance, 1.2f);
            for (int k = n - 1; k >= 3; k--)
            {
                float w = coordinate[k];
                float denominator = projection == ProjectionMode.Mixed && (k & 1) == 0 ? d : d - w;
                float scale = 1f / Mathf.Max(denominator, .25f);
                x *= scale; y *= scale; z *= scale;
            }
            // Renormalise, or each extra dimension shrinks the whole figure away.
            return new Vector3(x, y, z) * 1.55f * Mathf.Pow(Mathf.Max(d - 1f, .3f), Mathf.Max(n - 3, 0) * .5f);
        }

        protected override string Describe() =>
            Dimensions + "-cube · " + VertexCount + " vertices, " + EdgeCount + " edges";
    }
}
