using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AmazingAssets.CurvedWorld;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Extracts the zero crossing of an <see cref="ImplicitShape"/> field as a wire mesh, so the
    /// gyroid family, the Barth sextic, the Mandelbulb, the Mandelbox and the Menger sponge render
    /// with the chamber's barycentric wire, Curved World bend and WorldGridScan tint.
    ///
    /// Extraction is **naive surface nets**: one vertex per cell, placed at the average of the
    /// zero crossings on that cell's edges, then a quad across every grid edge where the field
    /// changes sign. That is chosen over marching cubes because it needs no 256-case table and
    /// gives a surface rather than voxel faces — `FractalMeshFactory` in PsychedelicLab/Warp
    /// already covers the blocky voxel look, so this complements it instead of repeating it.
    ///
    /// Rebuilds are not per-frame: sampling is O(resolution^3), so it only re-extracts when a
    /// parameter changed, or on a timer when `animate` is on.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ImplicitSurfaceChamber : MonoBehaviour, IWireGeometry
    {
        [Header("Links")]
        [Tooltip("Material using CurvedChamber or TunnelSparkleWire. Cloned, never written to.")]
        public Material wireMaterial;
        public WorldGridScan worldGridScan;
        public CurvedWorldBridge bridge;

        [Header("Field")]
        public ImplicitSettings field = new ImplicitSettings();

        [Header("Extraction")]
        [Tooltip("Cells per axis. Cost is the cube of this, so 48 is a good working value and 96 is a render setting.")]
        [Range(12, 96)] public int resolution = 44;
        [Tooltip("Half-size of the sampled box in local units.")]
        [Min(.1f)] public float extent = 4f;
        [Tooltip("Surface nets rounds every edge (the original look). Dual contouring keeps sharp edges and corners square.")]
        public DualContourEngine.Method method = DualContourEngine.Method.SurfaceNets;
        [Tooltip("Stone ↔ flesh: 1 snaps every vertex to its cell centre, turning the surface into voxel masonry with the same topology, so this crossfades continuously.")]
        [Range(0, 1)] public float cubeness;
        [Tooltip("Sample the field on worker threads. Output is identical; turn off only to profile.")]
        public bool multithreaded = true;
        [Tooltip("Hides the diagonal of every quad so the wire reads as a clean lattice.")]
        public bool hideQuadDiagonals = true;
        [Range(0, 1)] public float wireOpacity = .5f;
        [Range(1, 8)] public int latticeDivisor = 3;

        [Header("Motion")]
        [Tooltip("Re-extracts on a timer. Each rebuild samples resolution^3, so keep this slow.")]
        public bool animate;
        [Tooltip("Seconds between re-extractions while animating.")]
        [Range(.05f, 4f)] public float rebuildInterval = .25f;
        [Tooltip("Drifts the level set, which sweeps a minimal surface from one labyrinth to the other.")]
        [Range(0, 1)] public float levelDrift = .35f;
        [Range(-2, 2)] public float spin = .05f;

        public string Status { get; private set; } = "Not built";

        GameObject generated;
        Mesh mesh;
        Material instance;
        CurvedWorldController bend;
        CurvedWorldBridge registeredBridge;
        readonly List<Material> ownedMaterials = new List<Material>();
        BendType previewType = (BendType)(-1);

        // Extraction runs through the engine's extractor (surface nets or dual contouring, threaded),
        // into a reused builder that carries the wire invariants.
        readonly DualContouring extractor = new DualContouring();
        readonly WireMeshBuilder builder = new WireMeshBuilder();
        List<Vector3> verts => builder.vertices;

        struct FieldAdapter : IScalarField
        {
            public ImplicitSettings settings;
            public float time;
            public float Sample(Vector3 p) => Implicits.Field(settings, p, time);
        }

        int builtRes;
        DualContourEngine.Method builtMethod;
        float builtCubeness;
        Material builtMaterial;
        bool builtDiagonals;
        ImplicitShape builtShape;
        float builtLevel, builtThickness, builtFrequency, builtPower, builtBoxScale, builtBarth;
        int builtIterations, builtFolds;
        float builtDislocation, builtDislocationCore;
        Axis3 builtDislocationAxis;
        int builtSpace;
        float nextRebuild;
        double elapsed;


        void OnEnable() { Rebuild(); }
        void OnDisable() { Release(); }

        void Update()
        {
            if (!wireMaterial) { Release(); Status = "No material"; return; }
            if (Application.isPlaying && animate) elapsed += Time.deltaTime;

            if (generated == null || builtMaterial != wireMaterial || registeredBridge != bridge || Dirty())
                Rebuild();
            else if (animate && Application.isPlaying && Time.time >= nextRebuild)
            {
                nextRebuild = Time.time + Mathf.Max(rebuildInterval, .05f);
                Extract();
            }

            DriveMaterial();
            DriveBend();
            if (generated) generated.transform.localRotation =
                Quaternion.Euler(0f, 0f, (float)elapsed * spin * 360f);
        }

        bool Dirty() =>
            builtRes != Mathf.Clamp(resolution, 12, 96) ||
            builtDiagonals != hideQuadDiagonals ||
            builtMethod != method ||
            !Mathf.Approximately(builtCubeness, cubeness) ||
            builtShape != field.shape ||
            !Mathf.Approximately(builtLevel, field.level) ||
            !Mathf.Approximately(builtThickness, field.thickness) ||
            !Mathf.Approximately(builtFrequency, field.frequency) ||
            !Mathf.Approximately(builtPower, field.power) ||
            !Mathf.Approximately(builtBoxScale, field.boxScale) ||
            !Mathf.Approximately(builtBarth, field.barthW) ||
            builtIterations != field.iterations ||
            builtFolds != field.folds ||
            !Mathf.Approximately(builtDislocation, field.dislocation) ||
            builtDislocationAxis != field.dislocationAxis ||
            !Mathf.Approximately(builtDislocationCore, field.dislocationCore) ||
            builtSpace != (field.space != null ? field.space.Signature() : 0);

        void Snapshot()
        {
            builtRes = Mathf.Clamp(resolution, 12, 96);
            builtDiagonals = hideQuadDiagonals;
            builtMethod = method; builtCubeness = cubeness;
            builtShape = field.shape;
            builtLevel = field.level; builtThickness = field.thickness;
            builtFrequency = field.frequency; builtPower = field.power;
            builtBoxScale = field.boxScale; builtBarth = field.barthW;
            builtIterations = field.iterations; builtFolds = field.folds;
            builtDislocation = field.dislocation; builtDislocationAxis = field.dislocationAxis;
            builtDislocationCore = field.dislocationCore;
            builtSpace = field.space != null ? field.space.Signature() : 0;
        }

        // ---- build -----------------------------------------------------------

        void Rebuild()
        {
            Release();
            if (!wireMaterial) return;
            Snapshot();
            builtMaterial = wireMaterial;

            generated = new GameObject("Generated implicit surface") { hideFlags = HideFlags.HideAndDontSave };
            generated.transform.SetParent(transform, false);

            if (!Application.isPlaying || !bridge)
            {
                bend = generated.AddComponent<CurvedWorldController>();
                bend.bendID = 1; bend.manualUpdate = true;
                bend.bendType = BendType.TwistedSpiral_Z_Positive;
                bend.bendRotationAxisType = CurvedWorldController.AxisType.Custom;
                bend.bendRotationAxis = Vector3.forward;
            }

            mesh = new Mesh { name = field.shape + " wire", hideFlags = HideFlags.HideAndDontSave };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();

            generated.AddComponent<MeshFilter>().sharedMesh = mesh;
            instance = new Material(wireMaterial) { hideFlags = HideFlags.HideAndDontSave };
            ownedMaterials.Add(instance);
            var renderer = generated.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = instance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;

            Extract();

            registeredBridge = bridge;
            if (bridge) bridge.AddSharedBend(ownedMaterials);
        }

        /// <summary>
        /// Surface nets (the original extractor: one vertex per cell at the mean of its edge
        /// crossings, a quad across every sign-changing grid edge) or dual contouring, both through
        /// <see cref="DualContouring"/>. Sampling and vertex placement run on worker threads, which
        /// is what the ESCHER-STEP-PLAN's "how much is per-frame?" question needed: a 64^3 field
        /// re-extracts about three times faster on four cores, with identical output.
        /// </summary>
        void Extract()
        {
            float time = (float)elapsed;
            var probe = field;
            float savedLevel = probe.level;
            if (animate && levelDrift > 0f)
                probe.level = savedLevel + Mathf.Sin(time * .35f) * levelDrift;

            // The field is defined on -1..1; sample it there and scale to the chamber afterwards.
            extractor.resolution = builtRes;
            extractor.extent = 1f;
            extractor.centre = Vector3.zero;
            extractor.solveQef = method == DualContourEngine.Method.DualContouring;
            // Surface nets as it always was: linear crossings, no refinement.
            extractor.crossingIterations = extractor.solveQef ? 6 : 0;
            extractor.cubeness = cubeness;
            extractor.parallel = multithreaded;

            builder.hideQuadDiagonals = builtDiagonals;
            builder.Clear();
            try { extractor.Extract(new FieldAdapter { settings = probe, time = time }, builder); }
            finally { probe.level = savedLevel; }

            // Into chamber units, then through any open 4-D bubbles (main thread: it reads transforms).
            var v = builder.vertices;
            for (int i = 0; i < v.Count; i++) v[i] = Hyper4DField.ApplyLocal(transform, v[i] * extent);

            builder.Apply(mesh);
            // Curved World displaces vertices on the GPU, outside the straight mesh bounds.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * Mathf.Max(200f, extent * 4f));

            Status = field.shape + " · " + method + (cubeness > 0f ? " · cubeness " + cubeness.ToString("0.##") : "")
                   + " · " + (verts.Count / 6).ToString("N0") + " quads, "
                   + verts.Count.ToString("N0") + " vertices, " + builtRes + "^3 field"
                   + (Implicits.IsPeriodic(field.shape) ? " · tiles" : "")
                   + (Implicits.IsSignedDistance(field.shape) ? " · signed distance" : " · level set");
        }

        // ---- IWireGeometry ---------------------------------------------------

        public bool IsBuilt => generated != null && verts.Count > 0;
        public int GridU => Mathf.Max(builtRes, 2);
        public int GridV => Mathf.Max(builtRes, 2);

        /// <summary>
        /// Walks the extracted vertices rather than a parameterisation: an implicit surface has no
        /// (u,v), so particles ride the triangles the extractor produced.
        /// </summary>
        public Vector3 SampleGrid(float u, float v)
        {
            if (!IsBuilt) return Vector3.zero;
            int count = verts.Count;
            float t = Mathf.Repeat(u, 1f) * .61803399f + Mathf.Clamp01(v);
            int i = Mathf.Clamp((int)(Mathf.Repeat(t, 1f) * count), 0, count - 1);
            return verts[i];
        }

        // ---- material and bend ----------------------------------------------

        void DriveMaterial()
        {
            if (!instance) return;
            instance.SetFloat("_WireOpacity", worldGridScan && !worldGridScan.guideLinesOn ? 0f : wireOpacity);
            if (instance.HasProperty("_UVScale")) instance.SetVector("_UVScale", new Vector4(1, 1, 0, 0));
            int div = Mathf.Max(latticeDivisor, 1);
            if (instance.HasProperty("_LatticeU")) instance.SetFloat("_LatticeU", Mathf.Max(builtRes / div, 2));
            if (instance.HasProperty("_LatticeV")) instance.SetFloat("_LatticeV", Mathf.Max(builtRes / div, 2));
        }

        void DriveBend()
        {
            if (!bend) return;
            var p = bridge
                ? (bridge.customBend
                    ? new CurvedWorldBridge.Preset { type = bridge.customShape, curvature = bridge.customCurvature, horizontal = bridge.customHorizontal, vertical = bridge.customVertical, axis = bridge.customAxis }
                    : CurvedWorldBridge.Presets[Mathf.Clamp(bridge.preset, 0, 7)])
                : new CurvedWorldBridge.Preset { type = BendType.TwistedSpiral_Z_Positive, curvature = 0f, horizontal = 0f, vertical = 0f, axis = Vector3.forward };
            float amount = bridge
                ? (bridge.enabled && (bridge.customBend || bridge.preset > 0) ? bridge.amount * bridge.intensity : 0f)
                : 0f;

            bend.bendType = p.type;
            bend.bendCurvatureSize = p.curvature * amount;
            bend.bendHorizontalSize = p.horizontal * amount;
            bend.bendVerticalSize = p.vertical * amount;
            bend.bendCurvatureOffset = p.offsetCurvature;
            bend.bendRotationAxis = p.axis;
            bend.bendPivotPointPosition = bridge && bridge.pivotFollowsCamera && Camera.main
                ? Camera.main.transform.position : Vector3.zero;
            bend.ManualUpdate();

            if (previewType != p.type)
            {
                previewType = p.type;
                foreach (var m in ownedMaterials)
                {
                    foreach (BendType type in Enum.GetValues(typeof(BendType)))
                        m.DisableKeyword("CURVEDWORLD_BEND_TYPE_" + type.ToString().ToUpperInvariant());
                    m.EnableKeyword("CURVEDWORLD_BEND_TYPE_" + p.type.ToString().ToUpperInvariant());
                }
            }
        }

        void Release()
        {
            if (registeredBridge && ownedMaterials.Count > 0) registeredBridge.RemoveSharedBend(ownedMaterials);
            foreach (var m in ownedMaterials) Dispose(m);
            ownedMaterials.Clear();
            Dispose(generated); Dispose(mesh);
            generated = null; mesh = null; instance = null; bend = null;
            registeredBridge = null; builtMaterial = null; previewType = (BendType)(-1);
            builtRes = 0;
            builder.Clear();
        }

        static void Dispose(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        [ContextMenu("Gyroid")] public void UseGyroid() { field.shape = ImplicitShape.Gyroid; }
        [ContextMenu("Barth sextic")] public void UseBarth() { field.shape = ImplicitShape.BarthSextic; }
        [ContextMenu("Mandelbox rooms")]
        public void UseMandelbox() { field.shape = ImplicitShape.Mandelbox; field.boxScale = -1.75f; }
        [ContextMenu("Mandelbulb")] public void UseBulb() { field.shape = ImplicitShape.Mandelbulb; }
        [ContextMenu("Menger sponge")] public void UseMenger() { field.shape = ImplicitShape.MengerSponge; }
    }
}
