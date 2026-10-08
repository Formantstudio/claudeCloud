using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Base for the engine's mesh-building components: owns one hidden dynamic <see cref="Mesh"/>,
    /// hands it to the <see cref="MeshFilter"/> on this object, and rebuilds through a reused
    /// <see cref="WireMeshBuilder"/> when a parameter changes or every frame while animating.
    ///
    /// Deliberately free of studio dependencies (no Curved World, no control rig): assign any wire
    /// material to the <see cref="MeshRenderer"/> — <c>GeometryEngine/Wire</c> is the standalone one,
    /// and the chamber materials work too. Rebuilds at a fixed resolution do not allocate.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public abstract class WireMeshComponent : MonoBehaviour
    {
        [Header("Wire")]
        [Tooltip("Hides the diagonal of every quad so the wire reads as a lattice instead of triangles.")]
        public bool hideQuadDiagonals = true;
        [Tooltip("Rebuild every frame in play mode. Off = rebuild only when a parameter changes.")]
        public bool animate = true;
        [Tooltip("Also animate in the editor when not playing.")]
        public bool animateInEditor;
        [Range(0f, 4f)] public float timeScale = 1f;

        public string Status { get; protected set; } = "Not built";

        protected readonly WireMeshBuilder builder = new WireMeshBuilder();
        protected float ElapsedTime => (float)elapsed;

        Mesh mesh;
        bool dirty = true;
        double elapsed;
        float sinceRebuild;

        /// <summary>Fill <paramref name="into"/> (already cleared) for time <paramref name="time"/>.</summary>
        protected abstract void Build(WireMeshBuilder into, float time);

        /// <summary>One line for the inspector readout.</summary>
        protected virtual string Describe() => "";

        protected virtual void OnEnable() { dirty = true; Rebuild(); }

        protected virtual void OnDisable()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh == mesh) filter.sharedMesh = null;
            if (mesh)
            {
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
            mesh = null;
        }

        protected virtual void OnValidate() { dirty = true; }

        /// <summary>Forces a rebuild next frame.</summary>
        public void MarkDirty() { dirty = true; }

        /// <summary>The last build's welded topology: Euler characteristic, boundary, orientability, NaNs.</summary>
        public TopologyReport MeasureTopology(float weld = 1e-4f) => TopologyReport.Measure(builder, weld);

        [ContextMenu("Log topology")]
        void LogTopology() => Debug.Log(name + ": " + MeasureTopology());

        /// <summary>
        /// Minimum seconds between animated rebuilds. 0 = every frame. Extractors whose cost is
        /// resolution³ override this so an animated field does not rebuild at frame rate.
        /// </summary>
        protected virtual float RebuildInterval => 0f;

        protected virtual void Update()
        {
            bool running = animate && (Application.isPlaying || animateInEditor);
            if (running) elapsed += Time.deltaTime * timeScale;
            if (dirty) { Rebuild(); return; }
            if (!running) return;
            if (RebuildInterval > 0f)
            {
                sinceRebuild += Time.deltaTime;
                if (sinceRebuild < RebuildInterval) return;
            }
            Rebuild();
        }

        public void Rebuild()
        {
            dirty = false;
            sinceRebuild = 0f;
            if (!mesh)
            {
                mesh = new Mesh { name = GetType().Name + " wire", hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
            }
            var filter = GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh != mesh) filter.sharedMesh = mesh;

            builder.hideQuadDiagonals = hideQuadDiagonals;
            builder.Clear();
            Build(builder, (float)elapsed);
            builder.Apply(mesh);
            Status = Describe() + " · " + (builder.VertexCount / 6).ToString("N0") + " quads, "
                   + builder.VertexCount.ToString("N0") + " vertices";
        }
    }
}
