using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AmazingAssets.CurvedWorld;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>One layer of a combo: a surface, where it sits, how it moves, how it is tinted.</summary>
    [Serializable]
    public sealed class ComboLayer
    {
        /// <summary>A parametric surface, or the fractal accordion ring tunnel.</summary>
        public enum Kind { Surface, AccordionRings }

        public bool on = true;
        public string label = "";
        public Kind kind = Kind.Surface;

        [Header("Shape")]
        public ManifoldSurface surface = ManifoldSurface.Torus;
        [Tooltip("Second shape for this layer's own morph. Leave equal to `surface` to hold still.")]
        public ManifoldSurface morphTo = ManifoldSurface.Torus;
        [Range(0, 1)] public float morph;
        [Tooltip("Drives this layer's morph back and forth on its own clock.")]
        public bool autoMorph;
        [Range(.1f, 4f)] public float morphSpeed = .25f;

        [Header("Resolution")]
        [Range(8, 256)] public int uResolution = 72;
        [Range(4, 256)] public int vResolution = 36;
        [Range(1, 8)] public int latticeDivisor = 3;

        [Header("Placement")]
        public Vector3 localPosition;
        public Vector3 localEuler;
        public Vector3 localScale = Vector3.one;
        [Tooltip("Degrees per second about each local axis.")]
        public Vector3 spin;

        [Header("Look")]
        [Tooltip("Per-layer material. Leave empty to clone the combo's shared one.")]
        public Material materialOverride;
        [Range(0, 2)] public float wireOpacity = .5f;
        public Color wireColor = new Color(.18f, .85f, .9f);
        public Color sparkColor = new Color(.8f, .65f, .3f);
        [Range(0, 4)] public float sparkle = 1f;

        [Header("Kaleidoscope (shape space)")]
        public KaleidoSettings kaleido = new KaleidoSettings();

        [Header("Shape parameters")]
        public ManifoldSettings shape = new ManifoldSettings();

        [Header("Accordion parameters (Kind = AccordionRings)")]
        public AccordionSettings accordion = new AccordionSettings();
        [Tooltip("Axis length of the ring tunnel.")]
        [Min(1f)] public float accordionLength = 18f;
        [Tooltip("Seconds for one open-and-close of this layer's accordion.")]
        [Min(1f)] public float accordionCycle = 14f;

        [NonSerialized] public RingState ringState;
    }

    /// <summary>
    /// A combo system: several manifolds running at once in one rig, nested and counter-rotating,
    /// each with its own resolution, placement, spin, tint and morph. This is the geometry combo
    /// idea applied to the manifold set rather than to one SDF sculpture.
    ///
    /// Rendering per layer is the chamber's: an unwelded triangle mesh with barycentric wire
    /// coordinates in UV1, normalised UVs, 32-bit indices, Curved World bend ID 1, WorldGridScan
    /// tint, and a cloned material registered with the shared bridge. Implements
    /// <see cref="IWireGeometry"/> across all layers, so one <see cref="WireParticleSwarm"/>
    /// covers the whole combo.
    ///
    /// CurvedGeometryChamber is untouched; this sits alongside it for the multi-surface case.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ManifoldComboChamber : MonoBehaviour, IWireGeometry
    {
        public enum Preset
        {
            Custom,
            KleinHelicoidAccordion,
            HelicoidKleinClifford,
            AccordionNest,
            NestedTori,
            RomanBoysPair,
            KnotCage,
            MinimalSurfaces,
            HyperbolicStack,
            SupershapeBloom
        }

        [Header("Links")]
        [Tooltip("Material using CurvedChamber or TunnelSparkleWire. Cloned per layer, never written to.")]
        public Material wireMaterial;
        public WorldGridScan worldGridScan;
        public CurvedWorldBridge bridge;

        [Header("Combo")]
        [Tooltip("Applies a named layer set, then returns to Custom.")]
        public Preset applyPreset = Preset.KleinHelicoidAccordion;
        public bool hideQuadDiagonals = true;
        public bool animate = true;
        [Tooltip("Counter-rotates alternate layers, which is what makes a nest read as separate shells.")]
        public bool counterRotate = true;

        public List<ComboLayer> layers = new List<ComboLayer>();

        public string Status { get; private set; } = "No layers";

        sealed class Built
        {
            public GameObject go;
            public Mesh mesh;
            public Material material;
            public Vector3[] vertices;
            public Vector3[] bary;
            public Vector2[] uv;
            public int u, v;
            public bool diagonals;
            public Material sourceMaterial;
            public float morphPhase;
        }

        readonly List<Built> built = new List<Built>();
        readonly List<Material> ownedMaterials = new List<Material>();
        GameObject root;
        CurvedWorldController bend;
        CurvedWorldBridge registeredBridge;
        Material boundMaterial;
        BendType previewType = (BendType)(-1);
        double elapsed;

        void OnEnable()
        {
            if (applyPreset != Preset.Custom && layers.Count == 0) ApplyPreset(applyPreset);
            Build();
        }

        void OnDisable() { Release(); }

        void Update()
        {
            if (applyPreset != Preset.Custom)
            {
                ApplyPreset(applyPreset);
                applyPreset = Preset.Custom;
                Build();
            }

            if (!wireMaterial) { Release(); Status = "No material"; return; }
            if (root == null || boundMaterial != wireMaterial || registeredBridge != bridge || Stale())
                Build();
            if (root == null) return;

            if (Application.isPlaying && animate) elapsed += Time.deltaTime;

            float time = (float)elapsed;
            int live = 0, verts = 0;
            for (int i = 0; i < built.Count && i < layers.Count; i++)
            {
                var layer = layers[i];
                var b = built[i];
                if (b.go == null) continue;
                b.go.SetActive(layer.on);
                if (!layer.on) continue;

                FillLayer(layer, b, time, i);
                PlaceLayer(layer, b, time, i);
                DriveLayerMaterial(layer, b);
                live++; verts += b.vertices.Length;
            }

            DriveBend();
            Status = live + " of " + layers.Count + " layers · " + verts.ToString("N0") + " vertices";
        }

        bool Stale()
        {
            if (built.Count != layers.Count) return true;
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                var b = built[i];
                if (b == null || b.go == null) return true;
                if (b.u != Mathf.Clamp(l.uResolution, 8, 256) ||
                    b.v != Mathf.Clamp(l.vResolution, 4, 256) ||
                    b.diagonals != hideQuadDiagonals ||
                    b.sourceMaterial != (l.materialOverride ? l.materialOverride : wireMaterial)) return true;
            }
            return false;
        }

        // ---- build -----------------------------------------------------------

        void Build()
        {
            Release();
            if (!wireMaterial) return;
            boundMaterial = wireMaterial;

            root = new GameObject("Generated manifold combo") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.SetParent(transform, false);

            if (!Application.isPlaying || !bridge)
            {
                bend = root.AddComponent<CurvedWorldController>();
                bend.bendID = 1; bend.manualUpdate = true;
                bend.bendType = BendType.TwistedSpiral_Z_Positive;
                bend.bendRotationAxisType = CurvedWorldController.AxisType.Custom;
                bend.bendRotationAxis = Vector3.forward;
            }

            foreach (var layer in layers) built.Add(BuildLayer(layer));

            registeredBridge = bridge;
            if (bridge && ownedMaterials.Count > 0) bridge.AddSharedBend(ownedMaterials);
        }

        Built BuildLayer(ComboLayer layer)
        {
            var b = new Built
            {
                u = Mathf.Clamp(layer.uResolution, 8, 256),
                v = Mathf.Clamp(layer.vResolution, 4, 256),
                diagonals = hideQuadDiagonals
            };

            string name = string.IsNullOrEmpty(layer.label) ? layer.surface.ToString() : layer.label;
            b.go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            b.go.transform.SetParent(root.transform, false);

            int count = layer.kind == ComboLayer.Kind.AccordionRings
                ? RingGeometry.QuadCount(b.u, b.v, Mathf.Clamp(layer.accordion.connectors, 0, 32)) * 6
                : b.u * b.v * 6;
            b.vertices = new Vector3[count];
            b.bary = new Vector3[count];
            b.uv = new Vector2[count];
            var indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;
            if (layer.kind == ComboLayer.Kind.AccordionRings)
            {
                layer.ringState = new RingState();
                layer.ringState.Allocate(b.u, b.v, Mathf.Clamp(layer.accordion.connectors, 0, 32));
                WriteRingBaryAndUV(b, layer.ringState);
            }
            else WriteBaryAndUV(b);

            b.mesh = new Mesh { name = name + " wire", hideFlags = HideFlags.HideAndDontSave };
            // Unwelding costs 6 vertices per quad, so even a modest layer passes 65535.
            b.mesh.indexFormat = IndexFormat.UInt32;
            b.mesh.MarkDynamic();
            b.mesh.vertices = b.vertices;
            b.mesh.SetUVs(1, b.bary);
            b.mesh.uv = b.uv;
            b.mesh.triangles = indices;
            b.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f);

            b.go.AddComponent<MeshFilter>().sharedMesh = b.mesh;
            var source = layer.materialOverride ? layer.materialOverride : wireMaterial;
            b.material = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            ownedMaterials.Add(b.material);
            b.sourceMaterial = source;
            var renderer = b.go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = b.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            return b;
        }

        /// <summary>
        /// Diagonal hiding: the wire draws where the smallest bary component nears zero, which
        /// includes the diagonal splitting each quad. Adding 1 to the component that vanishes
        /// along that diagonal keeps it in [1,2] so it never draws.
        /// </summary>
        static void WriteBaryAndUV(Built b)
        {
            float diag = b.diagonals ? 1f : 0f;
            int k = 0;
            for (int j = 0; j < b.v; j++)
            for (int i = 0; i < b.u; i++)
            {
                float u0 = (float)i / b.u, u1 = (float)(i + 1) / b.u;
                float v0 = (float)j / b.v, v1 = (float)(j + 1) / b.v;
                b.uv[k] = new Vector2(u0, v0); b.bary[k++] = new Vector3(1, diag, 0);
                b.uv[k] = new Vector2(u1, v0); b.bary[k++] = new Vector3(0, 1 + diag, 0);
                b.uv[k] = new Vector2(u1, v1); b.bary[k++] = new Vector3(0, diag, 1);
                b.uv[k] = new Vector2(u0, v0); b.bary[k++] = new Vector3(1, 0, diag);
                b.uv[k] = new Vector2(u1, v1); b.bary[k++] = new Vector3(0, 1, diag);
                b.uv[k] = new Vector2(u0, v1); b.bary[k++] = new Vector3(0, 0, 1 + diag);
            }
        }

        /// <summary>
        /// Bary and UV for the ring tunnel: u around a ring, v along the tunnel, with struts
        /// showing all four edges because they are thin enough to read as lines.
        /// </summary>
        static void WriteRingBaryAndUV(Built b, RingState st)
        {
            float diag = b.diagonals ? 1f : 0f;
            int k = 0;
            for (int r = 0; r < st.rings; r++)
            for (int i = 0; i < st.sides; i++)
            {
                float u0 = (float)i / st.sides, u1 = (float)(i + 1) / st.sides;
                float v0 = (float)r / st.rings, v1 = (float)(r + 1) / st.rings;
                b.uv[k] = new Vector2(u0, v0); b.bary[k++] = new Vector3(1, diag, 0);
                b.uv[k] = new Vector2(u1, v0); b.bary[k++] = new Vector3(0, 1 + diag, 0);
                b.uv[k] = new Vector2(u1, v1); b.bary[k++] = new Vector3(0, diag, 1);
                b.uv[k] = new Vector2(u0, v0); b.bary[k++] = new Vector3(1, 0, diag);
                b.uv[k] = new Vector2(u1, v1); b.bary[k++] = new Vector3(0, 1, diag);
                b.uv[k] = new Vector2(u0, v1); b.bary[k++] = new Vector3(0, 0, 1 + diag);
            }
            for (int r = 0; r < Mathf.Max(st.rings - 1, 0); r++)
            for (int c = 0; c < st.connectors; c++)
            {
                if (k + 6 > b.bary.Length) return;
                float u = (float)c / Mathf.Max(st.connectors, 1);
                float v0 = (float)r / st.rings, v1 = (float)(r + 1) / st.rings;
                b.uv[k] = new Vector2(u, v0); b.bary[k++] = new Vector3(1, 0, 0);
                b.uv[k] = new Vector2(u, v0); b.bary[k++] = new Vector3(0, 1, 0);
                b.uv[k] = new Vector2(u, v1); b.bary[k++] = new Vector3(0, 0, 1);
                b.uv[k] = new Vector2(u, v0); b.bary[k++] = new Vector3(1, 0, 0);
                b.uv[k] = new Vector2(u, v1); b.bary[k++] = new Vector3(0, 1, 0);
                b.uv[k] = new Vector2(u, v1); b.bary[k++] = new Vector3(0, 0, 1);
            }
        }

        // ---- per frame -------------------------------------------------------

        void FillLayer(ComboLayer layer, Built b, float time, int index)
        {
            if (layer.kind == ComboLayer.Kind.AccordionRings)
            {
                if (layer.ringState == null ||
                    !layer.ringState.Matches(b.u, b.v, Mathf.Clamp(layer.accordion.connectors, 0, 32)))
                {
                    layer.ringState = new RingState();
                    layer.ringState.Allocate(b.u, b.v, Mathf.Clamp(layer.accordion.connectors, 0, 32));
                }
                RingGeometry.Compute(layer.ringState, layer.accordion, layer.shape.radius,
                                     layer.accordionLength, time, animate, layer.accordionCycle);
                RingGeometry.Fill(layer.ringState, layer.accordion, b.vertices, layer.accordionLength);
                b.mesh.SetVertices(b.vertices);
                b.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f);
                return;
            }

            float m = layer.morph;
            if (layer.autoMorph && animate)
                m = .5f + .5f * Mathf.Sin(time * layer.morphSpeed * Mathf.PI * 2f + index);

            var from = layer.surface;
            var to = layer.morphTo;
            bool wrapU = Manifolds.WrapsU(from) && Manifolds.WrapsU(to);
            bool wrapV = Manifolds.WrapsV(from) && Manifolds.WrapsV(to);
            bool poles = Manifolds.HasPoles(from) || Manifolds.HasPoles(to);
            float inset = poles ? .5f / b.v : 0f;

            int k = 0;
            for (int j = 0; j < b.v; j++)
            for (int i = 0; i < b.u; i++)
            {
                float u0 = UParam(i, b.u, wrapU), u1 = UParam(i + 1, b.u, wrapU);
                float v0 = VParam(j, b.v, wrapV, inset), v1 = VParam(j + 1, b.v, wrapV, inset);
                Vector3 p0 = Bubble(b, Sample(layer, u0, v0, time, m));
                Vector3 p1 = Bubble(b, Sample(layer, u1, v0, time, m));
                Vector3 p2 = Bubble(b, Sample(layer, u1, v1, time, m));
                Vector3 p3 = Bubble(b, Sample(layer, u0, v1, time, m));
                b.vertices[k++] = p0; b.vertices[k++] = p1; b.vertices[k++] = p2;
                b.vertices[k++] = p0; b.vertices[k++] = p2; b.vertices[k++] = p3;
            }
            b.morphPhase = m;
            b.mesh.SetVertices(b.vertices);
            b.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f);
        }

        /// <summary>Adds the open bubbles' displacement, in this layer's own space.</summary>
        static Vector3 Bubble(Built b, Vector3 p) =>
            b != null && b.go ? Hyper4DField.ApplyLocal(b.go.transform, p) : p;

        static float UParam(int i, int n, bool wrap) => wrap ? (float)i / n : (float)i / Mathf.Max(n - 1, 1);
        static float VParam(int j, int n, bool wrap, float inset)
        {
            float t = wrap ? (float)j / n : (float)j / Mathf.Max(n - 1, 1);
            return Mathf.Lerp(inset, 1f - inset, t);
        }

        static Vector3 Sample(ComboLayer layer, float u, float v, float time, float morph)
        {
            Vector3 p = Manifolds.Evaluate(layer.surface, layer.shape, u, v, time);
            if (morph > 0f && layer.morphTo != layer.surface)
                p = Vector3.Lerp(p, Manifolds.Evaluate(layer.morphTo, layer.shape, u, v, time), morph);
            return KaleidoFold.Apply(layer.kaleido, p, time);
        }

        void PlaceLayer(ComboLayer layer, Built b, float time, int index)
        {
            float dir = counterRotate && (index & 1) == 1 ? -1f : 1f;
            Vector3 spin = animate ? layer.spin * time * dir : Vector3.zero;
            b.go.transform.localPosition = layer.localPosition;
            b.go.transform.localRotation = Quaternion.Euler(layer.localEuler + spin);
            b.go.transform.localScale = layer.localScale == Vector3.zero ? Vector3.one : layer.localScale;
        }

        void DriveLayerMaterial(ComboLayer layer, Built b)
        {
            var m = b.material;
            if (!m) return;
            float opacity = worldGridScan && !worldGridScan.guideLinesOn ? 0f : layer.wireOpacity;
            m.SetFloat("_WireOpacity", opacity);
            if (m.HasProperty("_Sparkle")) m.SetFloat("_Sparkle", layer.sparkle);
            if (m.HasProperty("_WireColor")) m.SetColor("_WireColor", layer.wireColor);
            if (m.HasProperty("_SparkColor")) m.SetColor("_SparkColor", layer.sparkColor);
            // UVs are normalised here, so no index-space correction is needed.
            if (m.HasProperty("_UVScale")) m.SetVector("_UVScale", new Vector4(1, 1, 0, 0));
            int div = Mathf.Max(layer.latticeDivisor, 1);
            if (m.HasProperty("_LatticeU")) m.SetFloat("_LatticeU", Mathf.Max(b.u / div, 2));
            if (m.HasProperty("_LatticeV")) m.SetFloat("_LatticeV", Mathf.Max(b.v / div, 2));
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
                foreach (var material in ownedMaterials)
                {
                    foreach (BendType type in Enum.GetValues(typeof(BendType)))
                        material.DisableKeyword("CURVEDWORLD_BEND_TYPE_" + type.ToString().ToUpperInvariant());
                    material.EnableKeyword("CURVEDWORLD_BEND_TYPE_" + p.type.ToString().ToUpperInvariant());
                }
            }
        }

        // ---- IWireGeometry: u is split across the layers ---------------------

        public bool IsBuilt => root != null && built.Count > 0;
        public int GridU
        {
            get { int max = 0; foreach (var b in built) if (b != null) max = Mathf.Max(max, b.u); return max; }
        }
        public int GridV
        {
            get { int max = 0; foreach (var b in built) if (b != null) max = Mathf.Max(max, b.v); return max; }
        }

        /// <summary>
        /// Samples the combo as one surface by dividing u between the active layers, so a single
        /// particle swarm covers every shell. Each layer's own transform is applied, so particles
        /// follow its placement and counter-rotation.
        /// </summary>
        public Vector3 SampleGrid(float u, float v)
        {
            if (!IsBuilt) return Vector3.zero;

            int live = 0;
            for (int i = 0; i < layers.Count; i++) if (layers[i].on) live++;
            if (live == 0) return Vector3.zero;

            u = Mathf.Repeat(u, 1f);
            float scaled = u * live;
            int pick = Mathf.Min((int)scaled, live - 1);
            float localU = scaled - pick;

            int seen = -1;
            for (int i = 0; i < layers.Count && i < built.Count; i++)
            {
                if (!layers[i].on) continue;
                if (++seen != pick) continue;

                var layer = layers[i];
                var b = built[i];

                if (layer.kind == ComboLayer.Kind.AccordionRings)
                {
                    Vector3 rp = layer.ringState != null
                        ? RingGeometry.Sample(layer.ringState, localU, v) : Vector3.zero;
                    return b != null && b.go != null
                        ? root.transform.InverseTransformPoint(b.go.transform.TransformPoint(rp))
                        : rp;
                }

                bool poles = Manifolds.HasPoles(layer.surface) || Manifolds.HasPoles(layer.morphTo);
                float inset = poles && b != null ? .5f / b.v : 0f;
                Vector3 p = Sample(layer, localU, Mathf.Lerp(inset, 1f - inset, Mathf.Clamp01(v)),
                                   (float)elapsed, b != null ? b.morphPhase : layer.morph);
                return b != null && b.go != null
                    ? root.transform.InverseTransformPoint(b.go.transform.TransformPoint(p)) + root.transform.localPosition
                    : p;
            }
            return Vector3.zero;
        }

        // ---- presets ---------------------------------------------------------

        /// <summary>Looks up one of the per-shape materials in _GeometryWork/Materials.</summary>
        static Material Mat(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_GeometryWork/Materials/" + name + ".mat");
#else
            return null;
#endif
        }

        void ApplyPreset(Preset preset)
        {
            if (preset == Preset.Custom) return;
            layers.Clear();
            switch (preset)
            {
                case Preset.KleinHelicoidAccordion:
                {
                    // A helicoid screwing down the axis of a fractal accordion tunnel, with a
                    // Klein bottle sitting inside it.
                    var rings = Layer("Fractal accordion", ManifoldSurface.Torus, 4.2f, 44, 56,
                        new Color(.18f, .85f, .9f), new Vector3(0, 0, 4));
                    rings.kind = ComboLayer.Kind.AccordionRings;
                    rings.accordionLength = 24f;
                    rings.accordion.bandFraction = .4f;
                    rings.accordion.connectors = 8;
                    rings.accordion.cantorSpacing = .7f;
                    rings.accordion.cantorDepth = 4;
                    rings.accordion.fractalOut = .55f;
                    rings.accordion.profileB = RingProfile.Star;
                    rings.wireOpacity = .46f;
                    rings.materialOverride = Mat("Wire_Accordion_Rings");
                    layers.Add(rings);

                    var helix = Layer("Helicoid spine", ManifoldSurface.Helicoid, 2.4f, 96, 72,
                        new Color(.95f, .72f, .3f), new Vector3(0, 0, 26));
                    helix.shape.pitch = 1.1f;
                    helix.shape.turns = 5f;
                    helix.wireOpacity = .6f;
                    helix.materialOverride = Mat("Wire_Helicoid_Gold");
                    layers.Add(helix);

                    var klein = Layer("Klein bottle", ManifoldSurface.KleinBottle, 1.9f, 88, 48,
                        new Color(.62f, .42f, .98f), new Vector3(11, 7, 0));
                    klein.shape.minorRadius = .85f;
                    klein.wireOpacity = .72f;
                    klein.materialOverride = Mat("Wire_Klein_Violet");
                    layers.Add(klein);
                    break;
                }

                case Preset.AccordionNest:
                {
                    // Three accordions at different fold scales, nested and counter-rotating.
                    for (int i = 0; i < 3; i++)
                    {
                        var l = Layer("Accordion " + (i + 1), ManifoldSurface.Torus,
                            2.2f + i * 1.7f, 36 - i * 6, 48 - i * 8,
                            Color.Lerp(new Color(.18f, .85f, .9f), new Color(.95f, .5f, .3f), i / 2f),
                            new Vector3(0, 0, 6 + i * 5));
                        l.kind = ComboLayer.Kind.AccordionRings;
                        l.accordionLength = 20f + i * 6f;
                        l.accordion.bellows = 3f + i * 3f;
                        l.accordion.cantorDepth = 3 + i;
                        l.accordion.cantorSpacing = .5f + i * .18f;
                        l.accordion.connectors = 6 + i * 3;
                        l.accordion.bandFraction = .5f - i * .12f;
                        l.accordion.fractalOut = .35f + i * .22f;
                        l.wireOpacity = .5f - i * .1f;
                        layers.Add(l);
                    }
                    break;
                }

                case Preset.HyperbolicStack:
                    // Four constant-negative-curvature surfaces together.
                    layers.Add(Layer("Dini", ManifoldSurface.DinisSurface, 2.6f, 120, 36,
                        new Color(.25f, .88f, .85f), new Vector3(0, 0, 10)));
                    layers.Add(Layer("Kuen", ManifoldSurface.KuenSurface, 2.4f, 88, 44,
                        new Color(.95f, .66f, .32f), new Vector3(0, 12, 0)));
                    layers.Add(Layer("Pseudosphere", ManifoldSurface.Pseudosphere, 3.2f, 72, 40,
                        new Color(.6f, .45f, .95f), new Vector3(0, 0, -14)));
                    layers.Add(Layer("Breather", ManifoldSurface.BreatherSurface, 2.2f, 96, 64,
                        new Color(.9f, .4f, .5f), new Vector3(6, 0, 0)));
                    layers[3].wireOpacity = .3f;
                    break;

                case Preset.SupershapeBloom:
                    // The same superformula at rising lobe counts, nested.
                    for (int i = 0; i < 4; i++)
                    {
                        var l = Layer("Supershape m=" + (3 + i * 2), ManifoldSurface.Supershape,
                            1.6f + i * 1.2f, 80, 44,
                            Color.Lerp(new Color(.2f, .9f, .8f), new Color(.98f, .55f, .2f), i / 3f),
                            new Vector3(0, 0, 8 + i * 6));
                        l.shape.superM = new Vector2(3 + i * 2, 2 + i);
                        l.shape.superN = new Vector3(.3f + i * .15f, 1.7f, 1.7f);
                        l.wireOpacity = .55f - i * .09f;
                        layers.Add(l);
                    }
                    break;

                case Preset.HelicoidKleinClifford:
                    // A helicoid screwing through a Klein bottle, wrapped by a Clifford torus.
                    layers.Add(Layer("Helicoid core", ManifoldSurface.Helicoid, 1.6f, 96, 44,
                        new Color(.9f, .75f, .3f), new Vector3(0, 0, 36)));
                    layers.Add(Layer("Klein shell", ManifoldSurface.KleinBottle, 3.1f, 88, 48,
                        new Color(.18f, .85f, .9f), new Vector3(0, 14, 0)));
                    layers.Add(Layer("Clifford cage", ManifoldSurface.CliffordTorus, 4.4f, 72, 40,
                        new Color(.55f, .4f, .95f), new Vector3(9, 0, 0)));
                    layers[2].wireOpacity = .32f;
                    break;

                case Preset.NestedTori:
                    for (int i = 0; i < 4; i++)
                    {
                        var l = Layer("Torus " + (i + 1), ManifoldSurface.Torus, 1.4f + i * 1.1f, 64, 28,
                            Color.Lerp(new Color(.18f, .85f, .9f), new Color(.9f, .55f, .25f), i / 3f),
                            new Vector3(0, 0, 10 + i * 7));
                        l.shape.minorRadius = .35f + i * .18f;
                        l.localEuler = new Vector3(i * 24f, 0, 0);
                        l.wireOpacity = .5f - i * .08f;
                        layers.Add(l);
                    }
                    break;

                case Preset.RomanBoysPair:
                    // The same topological object twice: one singular, one smooth, crossfading.
                    var roman = Layer("Roman to Boy's", ManifoldSurface.RomanSurface, 2.6f, 96, 52,
                        new Color(.2f, .9f, .8f), new Vector3(0, 8, 0));
                    roman.morphTo = ManifoldSurface.BoysSurface;
                    roman.autoMorph = true; roman.morphSpeed = .12f;
                    layers.Add(roman);
                    layers.Add(Layer("Cross-cap ghost", ManifoldSurface.CrossCap, 3.6f, 64, 34,
                        new Color(.85f, .45f, .55f), new Vector3(0, -6, 0)));
                    layers[1].wireOpacity = .22f;
                    break;

                case Preset.KnotCage:
                    layers.Add(Layer("Trefoil tube", ManifoldSurface.TorusKnotTube, 2.4f, 140, 14,
                        new Color(.95f, .7f, .3f), new Vector3(0, 0, 14)));
                    layers.Add(Layer("Trefoil ribbon", ManifoldSurface.TrefoilRibbon, 2.4f, 140, 10,
                        new Color(.3f, .85f, .95f), new Vector3(0, 0, -14)));
                    layers[1].shape.halfTwists = 3;
                    layers.Add(Layer("Twisted torus", ManifoldSurface.TwistedTorus, 3.4f, 96, 30,
                        new Color(.6f, .45f, .95f), new Vector3(0, 10, 0)));
                    layers[2].wireOpacity = .28f;
                    break;

                case Preset.MinimalSurfaces:
                    layers.Add(Layer("Catenoid", ManifoldSurface.Catenoid, 2.2f, 72, 30,
                        new Color(.25f, .85f, .9f), new Vector3(0, 0, 12)));
                    layers.Add(Layer("Enneper", ManifoldSurface.Enneper, 2.6f, 64, 64,
                        new Color(.95f, .6f, .35f), new Vector3(0, 16, 0)));
                    layers.Add(Layer("Henneberg", ManifoldSurface.Henneberg, 2.4f, 72, 36,
                        new Color(.7f, .5f, .95f), new Vector3(0, 0, -20)));
                    break;
            }
        }

        static ComboLayer Layer(string label, ManifoldSurface surface, float radius,
                                int u, int v, Color wire, Vector3 spin)
        {
            var l = new ComboLayer
            {
                label = label, surface = surface, morphTo = surface,
                uResolution = u, vResolution = v,
                wireColor = wire, spin = spin
            };
            l.shape.radius = radius;
            l.shape.minorRadius = radius * .3f;
            l.shape.extent = radius * 6f;
            return l;
        }

        // ---- teardown --------------------------------------------------------

        void Release()
        {
            if (registeredBridge && ownedMaterials.Count > 0) registeredBridge.RemoveSharedBend(ownedMaterials);
            foreach (var b in built)
            {
                if (b == null) continue;
                Dispose(b.mesh);
                Dispose(b.go);
            }
            built.Clear();
            foreach (var m in ownedMaterials) Dispose(m);
            ownedMaterials.Clear();
            Dispose(root);
            root = null; bend = null; registeredBridge = null; boundMaterial = null;
            previewType = (BendType)(-1);
        }

        static void Dispose(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        [ContextMenu("Preset: Klein + Helicoid + Fractal accordion")]
        public void PresetKHA() { applyPreset = Preset.KleinHelicoidAccordion; }
        [ContextMenu("Preset: Accordion nest")]
        public void PresetAccNest() { applyPreset = Preset.AccordionNest; }
        [ContextMenu("Preset: Hyperbolic stack")]
        public void PresetHyper() { applyPreset = Preset.HyperbolicStack; }
        [ContextMenu("Preset: Supershape bloom")]
        public void PresetBloom() { applyPreset = Preset.SupershapeBloom; }
        [ContextMenu("Preset: Helicoid + Klein + Clifford")]
        public void PresetHKC() { applyPreset = Preset.HelicoidKleinClifford; }
        [ContextMenu("Preset: Nested tori")]
        public void PresetTori() { applyPreset = Preset.NestedTori; }
        [ContextMenu("Preset: Roman to Boy's")]
        public void PresetRB() { applyPreset = Preset.RomanBoysPair; }
        [ContextMenu("Preset: Knot cage")]
        public void PresetKnot() { applyPreset = Preset.KnotCage; }
        [ContextMenu("Preset: Minimal surfaces")]
        public void PresetMinimal() { applyPreset = Preset.MinimalSurfaces; }
    }
}
