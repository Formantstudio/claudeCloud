using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Extracts an <see cref="ImplicitShape"/> field by dual contouring (see <see cref="DualContouring"/>),
    /// so sharp geometry — Menger sponge, Mandelbox corridors, Sierpinski faces — keeps its edges,
    /// and smooth geometry — the TPMS family — stays smooth. Switch <see cref="method"/> to
    /// SurfaceNets to see the difference on the same field: that is what
    /// <c>ImplicitSurfaceChamber</c> uses.
    ///
    /// The field's screw dislocation (<see cref="ImplicitSettings.dislocation"/>) applies here too.
    /// Cost is resolution³ samples plus ~12 per surface crossing, so animation re-extracts on a
    /// timer rather than every frame.
    /// </summary>
    [AddComponentMenu("Geometry Engine/Dual Contour Surface")]
    public sealed class DualContourEngine : WireMeshComponent, IWireGeometry
    {
        public enum Method { DualContouring, SurfaceNets }

        [Header("Field")]
        public ImplicitSettings field = new ImplicitSettings();

        [Header("Extraction")]
        public Method method = Method.DualContouring;
        [Tooltip("Cells per axis. Cost is the cube of this.")]
        [Range(8, 128)] public int resolution = 48;
        [Tooltip("Half-size of the sampled box in local units. The field sees it as -1..1.")]
        [Min(.01f)] public float extent = 2f;
        [Tooltip("QEF eigenvalue cut-off, relative to the largest. Lower is sharper but noisier.")]
        [Range(.001f, .5f)] public float singularThreshold = .05f;
        [Tooltip("Stone ↔ flesh: 1 snaps every vertex to its cell centre — voxel masonry with the same topology, so it crossfades continuously.")]
        [Range(0, 1)] public float cubeness;
        [Tooltip("Sample and solve on worker threads. Output is identical; off only for profiling.")]
        public bool multithreaded = true;

        [Header("Motion")]
        [Tooltip("Seconds between re-extractions while animating.")]
        [Range(.02f, 4f)] public float rebuildInterval = .25f;
        [Tooltip("Drifts the level set while animating, sweeping a minimal surface between its labyrinths.")]
        [Range(0f, 1f)] public float levelDrift = .3f;

        readonly DualContouring extractor = new DualContouring();

        struct Adapter : IScalarField
        {
            public ImplicitSettings settings;
            public float inverseExtent, time;
            public float Sample(Vector3 p) => Implicits.Field(settings, p * inverseExtent, time);
        }

        protected override float RebuildInterval => rebuildInterval;

        protected override void Build(WireMeshBuilder into, float time)
        {
            extractor.resolution = Mathf.Clamp(resolution, 8, 128);
            extractor.extent = Mathf.Max(extent, .01f);
            extractor.singularThreshold = singularThreshold;
            extractor.solveQef = method == Method.DualContouring;
            extractor.cubeness = cubeness;
            extractor.parallel = multithreaded;

            float saved = field.level;
            if (animate && levelDrift > 0f && time > 0f) field.level = saved + Mathf.Sin(time * .35f) * levelDrift;
            try
            {
                extractor.Extract(new Adapter { settings = field, inverseExtent = 1f / extractor.extent, time = time }, into);
            }
            finally { field.level = saved; }
        }

        protected override string Describe() =>
            field.shape + " · " + method + (cubeness > 0f ? " · cubeness " + cubeness.ToString("0.##") : "") + " " + extractor.resolution + "³ · " + extractor.ActiveCells.ToString("N0") +
            " cells" + (extractor.solveQef ? ", " + extractor.ClampedVertices + " clamped, " + extractor.ProjectedVertices + " projected" : "") +
            (field.dislocation != 0f ? " · screw " + field.dislocation.ToString("0.##") : "");

        // ---- IWireGeometry: an implicit surface has no (u, v); walk the extracted vertices ----

        public bool IsBuilt => builder.VertexCount > 0;
        public int GridU => Mathf.Max(resolution, 2);
        public int GridV => Mathf.Max(resolution, 2);

        public Vector3 SampleGrid(float u, float v)
        {
            int count = builder.VertexCount;
            if (count == 0) return Vector3.zero;
            float t = Mathf.Repeat(u, 1f) * .61803399f + Mathf.Clamp01(v);
            return builder.vertices[Mathf.Clamp((int)(Mathf.Repeat(t, 1f) * count), 0, count - 1)];
        }

        [ContextMenu("Menger sponge (sharp)")]
        public void UseMenger() { field.shape = ImplicitShape.MengerSponge; method = Method.DualContouring; MarkDirty(); }
        [ContextMenu("Gyroid staircase")]
        public void UseGyroidStair() { field.shape = ImplicitShape.Gyroid; field.dislocation = 1f; field.dislocationAxis = Axis3.Z; MarkDirty(); }
        [ContextMenu("Mandelbox rooms")]
        public void UseMandelbox() { field.shape = ImplicitShape.Mandelbox; field.boxScale = -1.75f; MarkDirty(); }
    }
}
