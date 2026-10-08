using System;
using UnityEngine;
using UnityEngine.Rendering;
using AmazingAssets.CurvedWorld;
using PsychedelicLab.Control;

namespace PsychedelicLab.EscherWorld
{
    /// <summary>
    /// One Escher room: an implicit field, extracted as geometry, bent by Curved World, tinted by
    /// WorldGridScan.
    ///
    /// Because the architecture fields are triply periodic, a room is **not a mesh that has to be
    /// placed** — it is a window onto an infinite lattice. `latticeOffset` moves the window, which
    /// is how a corridor can keep going forever without new geometry, and `dislocation` on the
    /// field shears that lattice so one circuit of the axis lands a floor higher.
    ///
    /// Rebuilds cost O(resolution^3) field samples, so they are gated on change rather than run
    /// every frame. `levelDrift` and `offsetDrift` step on a timer; everything else rebuilds only
    /// when a value actually moves.
    ///
    /// Self-contained on purpose: no dependency on the geometry folder, so the two can be worked
    /// on at the same time.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class EscherRoom : MonoBehaviour
    {
        [Header("Links")]
        [Tooltip("Material for the room. Cloned per instance, never written to. A chamber wire material works here.")]
        public Material roomMaterial;
        [Tooltip("Optional second material used when extraction is Cubed, so masonry and flesh can look different.")]
        public Material cubedMaterial;
        public WorldGridScan worldGridScan;
        public CurvedWorldBridge bridge;

        [Header("Field")]
        public EscherFieldSettings field = new EscherFieldSettings();

        [Header("Extraction")]
        public RoomExtraction extraction = RoomExtraction.Rounded;
        [Tooltip("Cells per axis. Cost is the cube of this: 40 is a working value, 72 is a render setting.")]
        [Range(8, 96)] public int resolution = 40;
        [Tooltip("Half-size of the sampled box, in local units.")]
        [Min(.5f)] public float extent = 8f;
        [Tooltip("Suppresses the shared diagonal on cubed faces, so masonry reads as square panels.")]
        public bool hideQuadDiagonals = true;

        [Header("Lattice window")]
        [Tooltip("Moves the window over the infinite lattice. Scrolling this travels the building without new geometry.")]
        public Vector3 latticeOffset;
        [Tooltip("Units per second the window scrolls. The infinite corridor.")]
        public Vector3 offsetDrift;

        [Header("Look")]
        [Range(0f, 1f)] public float wireOpacity = .5f;
        [Range(1, 8)] public int latticeDivisor = 3;

        [Header("Transformation")]
        [Tooltip("Re-extracts on a timer so the room can change shape. Each rebuild is a full field sample, so keep it slow.")]
        public bool animate;
        [Range(.05f, 4f)] public float rebuildInterval = .3f;
        [Tooltip("Drifts the level set, which sweeps the walls into the openings and back. The cheapest transformation that reads.")]
        [Range(0f, 1f)] public float levelDrift;
        [Tooltip("Bend preset this room asks the shared bridge for while it is the current world. -1 = leave the bridge alone.")]
        [Range(-1, 7)] public int bendPreset = -1;

        [Header("W slice")]
        [Tooltip("Added to the field's own W. An outlet sets this per slice, so several rooms cut from one 4-D structure can share a position.")]
        public float wSlice;

        public string Status { get; private set; } = "Not built";
        public Mesh Mesh => mesh;

        GameObject generated;
        Mesh mesh;
        Material instance;
        CurvedWorldController bend;
        CurvedWorldBridge registeredBridge;
        readonly System.Collections.Generic.List<Material> owned =
            new System.Collections.Generic.List<Material>();
        BendType previewType = (BendType)(-1);
        EscherSurfaceBuilder.Scratch scratch;

        // Snapshot of everything that forces a rebuild.
        int builtResolution;
        RoomExtraction builtExtraction;
        bool builtDiagonals;
        Material builtMaterial;
        RoomField builtField;
        float builtLevel, builtThickness, builtFrequency, builtDislocation, builtCore;
        float builtPower, builtBoxScale, builtBarth, builtExtent;
        int builtIterations, builtFolds;
        RoomAxis builtAxis;
        Vector3 builtOffset;

        float builtW, builtWInfluence;
        Vector3 builtWRotation;
        float nextRebuild;
        double elapsed;

        void OnEnable() { Rebuild(); }
        void OnDisable() { Release(); }

        void Update()
        {
            var material = Active();
            if (!material) { Release(); Status = "No material"; return; }
            if (Application.isPlaying && animate) elapsed += Time.deltaTime;

            if (animate && Application.isPlaying)
            {
                if (offsetDrift != Vector3.zero)
                    latticeOffset += offsetDrift * Time.deltaTime;
                if (levelDrift > 0f)
                    field.level = Mathf.Sin((float)elapsed * .35f) * levelDrift;
            }

            if (generated == null || builtMaterial != material || registeredBridge != bridge || Dirty())
                Rebuild();
            else if (animate && Application.isPlaying && Time.time >= nextRebuild)
            {
                nextRebuild = Time.time + Mathf.Max(rebuildInterval, .05f);
                Extract();
            }

            DriveMaterial();
            DriveBend();
        }

        Material Active() =>
            extraction == RoomExtraction.Cubed && cubedMaterial ? cubedMaterial : roomMaterial;

        bool Dirty() =>
            builtResolution != Mathf.Clamp(resolution, 8, 96) ||
            builtExtraction != extraction ||
            builtDiagonals != hideQuadDiagonals ||
            builtField != field.field ||
            builtAxis != field.dislocationAxis ||
            builtIterations != field.iterations ||
            builtFolds != field.folds ||
            !Mathf.Approximately(builtExtent, extent) ||
            !Mathf.Approximately(builtLevel, field.level) ||
            !Mathf.Approximately(builtThickness, field.thickness) ||
            !Mathf.Approximately(builtFrequency, field.frequency) ||
            !Mathf.Approximately(builtDislocation, field.dislocation) ||
            !Mathf.Approximately(builtCore, field.dislocationCore) ||
            !Mathf.Approximately(builtPower, field.power) ||
            !Mathf.Approximately(builtBoxScale, field.boxScale) ||
            !Mathf.Approximately(builtBarth, field.barthW) ||
            (builtOffset - latticeOffset).sqrMagnitude > 1e-6f ||
            !Mathf.Approximately(builtW, wSlice + field.w) ||
            !Mathf.Approximately(builtWInfluence, field.wInfluence) ||
            (builtWRotation - field.wRotation).sqrMagnitude > 1e-6f;

        void Snapshot()
        {
            builtResolution = Mathf.Clamp(resolution, 8, 96);
            builtExtraction = extraction;
            builtDiagonals = hideQuadDiagonals;
            builtField = field.field;
            builtAxis = field.dislocationAxis;
            builtIterations = field.iterations;
            builtFolds = field.folds;
            builtExtent = extent;
            builtLevel = field.level;
            builtThickness = field.thickness;
            builtFrequency = field.frequency;
            builtDislocation = field.dislocation;
            builtCore = field.dislocationCore;
            builtPower = field.power;
            builtBoxScale = field.boxScale;
            builtBarth = field.barthW;
            builtOffset = latticeOffset;
            builtW = wSlice + field.w;
            builtWRotation = field.wRotation;
            builtWInfluence = field.wInfluence;
        }

        // ---- build -----------------------------------------------------------

        void Rebuild()
        {
            Release();
            var material = Active();
            if (!material) return;
            Snapshot();
            builtMaterial = material;

            generated = new GameObject("Generated Escher room") { hideFlags = HideFlags.HideAndDontSave };
            generated.transform.SetParent(transform, false);

            if (!Application.isPlaying || !bridge)
            {
                bend = generated.AddComponent<CurvedWorldController>();
                bend.bendID = 1; bend.manualUpdate = true;
                bend.bendType = BendType.TwistedSpiral_Z_Positive;
                bend.bendRotationAxisType = CurvedWorldController.AxisType.Custom;
                bend.bendRotationAxis = Vector3.forward;
            }

            mesh = new Mesh { name = field.field + " room", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();

            generated.AddComponent<MeshFilter>().sharedMesh = mesh;
            instance = new Material(material) { hideFlags = HideFlags.HideAndDontSave };
            owned.Add(instance);
            var renderer = generated.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = instance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;

            Extract();

            registeredBridge = bridge;
            if (bridge) bridge.AddSharedBend(owned);
        }

        /// <summary>
        /// Samples the field and rebuilds the mesh. The lattice offset is folded into the field by
        /// shifting the sample positions, which is what makes the window move rather than the room.
        /// </summary>
        void Extract()
        {
            if (mesh == null) return;
            scratch ??= new EscherSurfaceBuilder.Scratch();

            // The lattice offset only means anything for a surface that repeats; a bounded room
            // would just slide out of its own sample box.
            Vector3 window = EscherFields.IsPeriodic(field.field) ? builtOffset : Vector3.zero;

            EscherSurfaceBuilder.Build(mesh, scratch, field, builtExtraction,
                                       builtResolution, builtExtent, (float)elapsed,
                                       builtDiagonals, window, wSlice);

            int tris = scratch.triangles.Count / 3;
            Status = string.Format("{0} · {1} · {2} tris at {3}^3{4}{5}",
                field.field, builtExtraction, tris.ToString("N0"), builtResolution,
                EscherFields.IsFourDimensional(field.field)
                    ? " · w " + (wSlice + field.w).ToString("0.##") : " · 3D only",
                field.dislocation != 0f && EscherFields.SupportsDislocation(field.field)
                    ? " · " + field.dislocation.ToString("0.##") + " floors/turn"
                    : "");
        }

        // ---- material and bend ----------------------------------------------

        void DriveMaterial()
        {
            if (!instance) return;
            instance.SetFloat("_WireOpacity", worldGridScan && !worldGridScan.guideLinesOn ? 0f : wireOpacity);
            // UV0 from the builder is normalised triplanar, so no index-space correction is needed.
            if (instance.HasProperty("_UVScale")) instance.SetVector("_UVScale", new Vector4(1, 1, 0, 0));
            int div = Mathf.Max(latticeDivisor, 1);
            if (instance.HasProperty("_LatticeU")) instance.SetFloat("_LatticeU", Mathf.Max(builtResolution / div, 2));
            if (instance.HasProperty("_LatticeV")) instance.SetFloat("_LatticeV", Mathf.Max(builtResolution / div, 2));
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
                foreach (var m in owned)
                {
                    foreach (BendType t in Enum.GetValues(typeof(BendType)))
                        m.DisableKeyword("CURVEDWORLD_BEND_TYPE_" + t.ToString().ToUpperInvariant());
                    m.EnableKeyword("CURVEDWORLD_BEND_TYPE_" + p.type.ToString().ToUpperInvariant());
                }
            }
        }

        void Release()
        {
            if (registeredBridge && owned.Count > 0) registeredBridge.RemoveSharedBend(owned);
            foreach (var m in owned) Dispose(m);
            owned.Clear();
            Dispose(generated); Dispose(mesh);
            generated = null; mesh = null; instance = null; bend = null;
            registeredBridge = null; builtMaterial = null; previewType = (BendType)(-1);
            builtResolution = 0;
        }

        static void Dispose(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        // ---- presets ---------------------------------------------------------

        [ContextMenu("Escher staircase (gyroid, 1 floor per turn)")]
        public void PresetStaircase()
        {
            field.field = RoomField.Gyroid;
            field.frequency = 2f;
            field.thickness = .14f;
            field.level = 0f;
            field.dislocation = 1f;
            field.dislocationAxis = RoomAxis.Y;
            field.dislocationCore = .3f;
            extraction = RoomExtraction.Cubed;
            resolution = 48;
        }

        [ContextMenu("Cathedral (Barth sextic)")]
        public void PresetCathedral()
        {
            field.field = RoomField.BarthSextic;
            field.dislocation = 0f;
            field.thickness = .05f;
            field.barthW = 1f;
            extraction = RoomExtraction.Rounded;
            resolution = 56;
        }

        [ContextMenu("Galleries (Mandelbox)")]
        public void PresetGalleries()
        {
            field.field = RoomField.Mandelbox;
            field.boxScale = -1.75f;
            field.iterations = 8;
            field.thickness = 0f;
            field.dislocation = 0f;
            extraction = RoomExtraction.Rounded;
            resolution = 56;
        }

        [ContextMenu("Masonry (Menger)")]
        public void PresetMasonry()
        {
            field.field = RoomField.MengerSponge;
            field.folds = 3;
            field.thickness = 0f;
            field.dislocation = 0f;
            extraction = RoomExtraction.Cubed;
            resolution = 54;
        }
    }
}
