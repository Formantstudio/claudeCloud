using System;
using UnityEngine;
using UnityEngine.Rendering;
using AmazingAssets.CurvedWorld;
using System.Collections.Generic;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The chamber. One component, three modes:
    ///
    /// - <see cref="Mode.Cylinder"/>     the original procedural tunnel, unchanged. Default, so
    ///                                   existing scenes render exactly as before.
    /// - <see cref="Mode.Manifold"/>     any shape in <see cref="ManifoldSurface"/>, with from/to
    ///                                   morphing across the whole set.
    /// - <see cref="Mode.FractalRings"/> the fractal accordion: Cantor-clustered ring spacing,
    ///                                   recursive ring edges, bands and connecting struts.
    ///
    /// Rendering is the same in all three: an unwelded triangle mesh with barycentric wire
    /// coordinates in UV1, Curved World bend ID 1, WorldGridScan tint, material cloned per
    /// instance and registered with the shared bridge. Implements <see cref="IWireGeometry"/>, so
    /// <see cref="WireParticleSwarm"/> can put the particle layer on any of it.
    ///
    /// Every field the old version serialized is still here under the same name, so scene values
    /// carry over.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CurvedGeometryChamber : MonoBehaviour, IWireGeometry
    {
        public enum Mode { Cylinder, Manifold, FractalRings, CalabiYau }
        [NonSerialized] public Func<float, float, Vector3, Vector3> surfaceDeformation;
        /// <summary>
        /// Set by a driver whose <see cref="surfaceDeformation"/> ignores its input this frame (a
        /// fully closed Enneper pillar, a full Scherk tower). The base surface is then not evaluated
        /// at all: the deformation receives Vector3.zero.
        /// </summary>
        [NonSerialized] public bool deformationReplacesSurface;

        [Header("Mode")]
        public Mode mode = Mode.Cylinder;

        [Header("Links")]
        public Material chamberMaterial;
        public PsychedelicLab.Control.WorldGridScan worldGridScan;
        public CurvedWorldBridge bridge;
        public GameObject flowpathRoot;

        [Header("Resolution")]
        [Range(6, 256)] public int sides = 12;
        [Range(4, 384)] public int rings = 48;
        [Tooltip("Wire lattice is this many times coarser than the mesh, so lines land on real grid vertices.")]
        [Range(1, 8)] public int latticeDivisor = 3;
        [Tooltip("Hides the diagonal of every quad so the wire reads as a clean lattice instead of triangles.")]
        public bool hideQuadDiagonals;

        [Header("Size")]
        [Min(2)] public float radius = 3.8f;
        [Min(4)] public float length = 28;
        [Range(0, 1)] public float wireOpacity = .5f;

        [HideInInspector, Range(-30, 30)] public float curvature = 3;
        [HideInInspector, Range(-15, 15)] public float horizontal = .12f;
        [HideInInspector, Range(-10, 10)] public float vertical = .08f;
        [HideInInspector] public bool animate = true;
        [HideInInspector, Range(0, 5)] public float breathing = 1.2f;
        [HideInInspector] public float cycleSeconds = 24;

        [Header("Manifold mode")]
        public ManifoldSurface from = ManifoldSurface.Torus;
        public ManifoldSurface to = ManifoldSurface.KleinBottle;
        [Range(0, 1)] public float morph;
        public bool autoCycle;
        [Min(1f)] public float secondsPerShape = 12f;
        [Range(.1f, .9f)] public float transitionFraction = .4f;
        public ManifoldSettings shape = new ManifoldSettings();
        [Range(-2, 2)] public float manifoldSpin = .08f;

        [Header("Calabi-Yau mode")]
        [Tooltip("Degree of the Fermat quintic. n^2 patches, so 5 gives the iconic 25-patch figure. 3 and 4 are much cheaper.")]
        [Range(2, 7)] public int quinticDegree = 5;
        [Tooltip("Projection angle from C^2 down to R^3. Sweeping this is the signature move.")]
        [Range(0, 6.2832f)] public float projectionAngle = .7854f;
        public bool animateProjectionAngle = true;
        [Range(0, 1)] public float projectionAngleSpeed = .08f;
        [Range(.1f, 8f)] public float calabiScale = 2.4f;

        [Header("Kaleidoscope (shape space)")]
        [Tooltip("Folds the geometry itself into mirrored sectors, so the wire and the particles agree. Identity when sectors is 0.")]
        public KaleidoSettings kaleido = new KaleidoSettings();

        [Header("Fractal rings mode")]
        public AccordionSettings accordion = new AccordionSettings();

        public string Status { get; private set; } = "Cylinder";

        GameObject generated;
        Mesh mesh;
        Material instance;
        CurvedWorldController bend;
        Material source;
        CurvedWorldBridge registeredBridge;
        GameObject boundFlowpath;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<(Renderer renderer, Material[] originals, Material[] clones)> swapped =
            new List<(Renderer, Material[], Material[])>();
        BendType previewType = (BendType)(-1);

        Vector3[] vertices;
        // One evaluated point per lattice node, (sides + 1) x (rings + 1). Every node is shared by
        // up to four quads (six unwelded vertices), so evaluating the surface per node instead of per
        // quad corner does a quarter of the work for the same mesh.
        Vector3[] lattice;
        Vector3[] bary;
        Vector2[] uv;
        int[] indices;
        int builtSides, builtRings, builtConnectors;
        Mode builtMode;
        bool builtDiagonals;

        // Fractal-rings scratch, reused each frame so an animated tunnel does not allocate.
        float[] ringZ;
        float[] profiles;
        int profileStride;
        double elapsed;

        void OnEnable() { Build(); }
        void OnDisable() { Release(); }

        void Update()
        {
            if (!chamberMaterial) { Release(); Status = "No material"; return; }
            if (generated == null || source != chamberMaterial || builtMode != mode ||
                builtSides != Clamped(sides, 6, 256) || builtRings != Clamped(rings, 4, 384) ||
                builtConnectors != ConnectorsForMode() || builtDiagonals != hideQuadDiagonals ||
                registeredBridge != bridge || boundFlowpath != flowpathRoot)
                Build();
            if (generated == null) return;

            if (Application.isPlaying && animate) elapsed += Time.deltaTime;
            if (mode == Mode.Manifold && autoCycle) Cycle();

            Fill((float)elapsed);
            DriveBend();
            DriveMaterial();
            Status = Describe();
        }

        static int Clamped(int v, int lo, int hi) => Mathf.Clamp(v, lo, hi);
        int ConnectorsForMode() => mode == Mode.FractalRings ? Mathf.Clamp(accordion.connectors, 0, 32) : 0;

        string Describe()
        {
            switch (mode)
            {
                case Mode.Manifold:
                    return Mathf.Approximately(morph, 0f) ? from.ToString()
                         : Mathf.Approximately(morph, 1f) ? to.ToString()
                         : from + " → " + to + "  " + (morph * 100f).ToString("0") + "%";
                case Mode.CalabiYau:
                    return "Calabi-Yau n=" + quinticDegree + " · " + (quinticDegree * quinticDegree) + " patches";
                case Mode.FractalRings:
                    return "Fractal rings · " + builtRings + " rings, " + builtConnectors + " struts";
                default:
                    return "Cylinder · " + builtSides + " x " + builtRings;
            }
        }

        void Cycle()
        {
            int count = Enum.GetValues(typeof(ManifoldSurface)).Length;
            double cycle = elapsed / Mathf.Max(secondsPerShape, 1f);
            int index = (int)(cycle % count);
            from = (ManifoldSurface)index;
            to = (ManifoldSurface)((index + 1) % count);
            float phase = (float)(cycle - Math.Floor(cycle));
            morph = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - transitionFraction, 1f, phase));
        }

        // ---- build -----------------------------------------------------------

        void Build()
        {
            Release();
            if (!chamberMaterial) return;

            builtMode = mode;
            builtSides = Clamped(sides, 6, 256);
            builtRings = Clamped(rings, 4, 384);
            builtConnectors = ConnectorsForMode();
            builtDiagonals = hideQuadDiagonals;
            source = chamberMaterial;

            generated = new GameObject("Generated curved chamber") { hideFlags = HideFlags.HideAndDontSave };
            generated.transform.SetParent(transform, false);

            if (!Application.isPlaying || !bridge)
            {
                bend = generated.AddComponent<CurvedWorldController>();
                bend.bendID = 1; bend.manualUpdate = true;
                bend.bendType = BendType.TwistedSpiral_Z_Positive;
                bend.bendRotationAxisType = CurvedWorldController.AxisType.Custom;
                bend.bendRotationAxis = Vector3.forward;
            }

            int quads = builtSides * builtRings + builtConnectors * Mathf.Max(builtRings - 1, 0);
            int count = quads * 6;
            vertices = new Vector3[count];
            lattice = new Vector3[(builtSides + 1) * (builtRings + 1)];
            bary = new Vector3[count];
            uv = new Vector2[count];
            indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;
            WriteBaryAndUV();

            profileStride = builtSides + 1;
            ringZ = new float[builtRings];
            profiles = new float[builtRings * profileStride];

            mesh = new Mesh { name = "Procedural chamber triangles", hideFlags = HideFlags.HideAndDontSave };
            // Unwelding costs 6 vertices per quad, so this runs past 65535 at useful resolutions.
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();
            Fill(0f);
            mesh.SetUVs(1, bary);
            mesh.uv = uv;
            mesh.triangles = indices;

            generated.AddComponent<MeshFilter>().sharedMesh = mesh;
            instance = new Material(chamberMaterial) { hideFlags = HideFlags.HideAndDontSave };
            ownedMaterials.Add(instance);
            var renderer = generated.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = instance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;

            boundFlowpath = flowpathRoot;
            registeredBridge = bridge;

            // Flowpath accents get owned copies so they bend with this chamber, exactly as before.
            if (flowpathRoot)
                foreach (var accent in flowpathRoot.GetComponentsInChildren<Renderer>(true))
                {
                    var originals = accent.sharedMaterials;
                    var clones = (Material[])originals.Clone();
                    bool changed = false;
                    for (int i = 0; i < clones.Length; i++)
                        if (clones[i] && clones[i].shader.name == "PsychedelicLab/Curved Geometry Accent")
                        {
                            clones[i] = new Material(clones[i]) { hideFlags = HideFlags.HideAndDontSave };
                            ownedMaterials.Add(clones[i]);
                            changed = true;
                        }
                    if (changed) { accent.sharedMaterials = clones; swapped.Add((accent, originals, clones)); }
                }

            if (bridge) bridge.AddSharedBend(ownedMaterials);
        }

        /// <summary>
        /// Barycentric wire coordinates and normalised UVs. Topology is fixed between rebuilds, so
        /// this runs once.
        ///
        /// UV0 is normalised 0..1. The old version wrote index-space UVs, which made any shader
        /// that multiplies UV by a density (TunnelSparkleWire) alias into grain.
        ///
        /// Diagonal hiding: the wire appears where the smallest bary component nears zero, which
        /// includes the diagonal splitting each quad. Adding 1 to the component that vanishes along
        /// that diagonal keeps it in [1,2] so it never draws. No extra vertex attribute needed.
        /// </summary>
        void WriteBaryAndUV()
        {
            float diag = builtDiagonals ? 1f : 0f;
            int k = 0;

            for (int r = 0; r < builtRings; r++)
            for (int s = 0; s < builtSides; s++)
            {
                float u0 = (float)s / builtSides, u1 = (float)(s + 1) / builtSides;
                float v0 = (float)r / builtRings, v1 = (float)(r + 1) / builtRings;
                Emit(ref k, new Vector2(u0, v0), new Vector3(1, diag, 0));
                Emit(ref k, new Vector2(u1, v0), new Vector3(0, 1 + diag, 0));
                Emit(ref k, new Vector2(u1, v1), new Vector3(0, diag, 1));
                Emit(ref k, new Vector2(u0, v0), new Vector3(1, 0, diag));
                Emit(ref k, new Vector2(u1, v1), new Vector3(0, 1, diag));
                Emit(ref k, new Vector2(u0, v1), new Vector3(0, 0, 1 + diag));
            }

            // Struts show all four edges; they are thin enough to read as lines.
            for (int r = 0; r < Mathf.Max(builtRings - 1, 0); r++)
            for (int c = 0; c < builtConnectors; c++)
            {
                float u = (float)c / Mathf.Max(builtConnectors, 1);
                float v0 = (float)r / builtRings, v1 = (float)(r + 1) / builtRings;
                Emit(ref k, new Vector2(u, v0), new Vector3(1, 0, 0));
                Emit(ref k, new Vector2(u, v0), new Vector3(0, 1, 0));
                Emit(ref k, new Vector2(u, v1), new Vector3(0, 0, 1));
                Emit(ref k, new Vector2(u, v0), new Vector3(1, 0, 0));
                Emit(ref k, new Vector2(u, v1), new Vector3(0, 1, 0));
                Emit(ref k, new Vector2(u, v1), new Vector3(0, 0, 1 + 0));
            }
        }

        void Emit(ref int k, Vector2 t, Vector3 b) { uv[k] = t; bary[k] = b; k++; }

        // ---- per-frame geometry ---------------------------------------------

        void Fill(float time)
        {
            if (mesh == null || vertices == null) return;
            if (builtMode == Mode.FractalRings) FillRings(time);
            else FillGrid(time);

            mesh.SetVertices(vertices);
            // Curved World displaces vertices on the GPU, outside the straight mesh bounds.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * Mathf.Max(200f, length * 2f));
        }

        /// <summary>
        /// Everything about this frame's evaluation that does not depend on (u, v), worked out once
        /// rather than once per vertex. <see cref="SampleGrid"/> builds the same struct the same way,
        /// so the particles and the wire cannot disagree.
        /// </summary>
        struct Frame
        {
            public float time, breath, spinCos, spinSin, calabiAngle;
            public bool spin;
            public bool wrapU, wrapV;
            public float inset;
            public int patches, perPatch;
        }

        Frame MakeFrame(float time)
        {
            var f = new Frame { time = time, wrapU = true, wrapV = false };
            float spin = builtMode == Mode.Manifold ? (animate ? time * manifoldSpin : 0f) : 0f;
            f.spin = spin != 0f;
            f.spinCos = Mathf.Cos(spin * Mathf.PI * 2f);
            f.spinSin = Mathf.Sin(spin * Mathf.PI * 2f);
            f.breath = animate ? 1f + .03f * breathing * Mathf.Sin(time * 1.7f) : 1f;
            if (builtMode == Mode.Manifold)
            {
                f.wrapU = Manifolds.WrapsU(from, shape) && Manifolds.WrapsU(to, shape);
                f.wrapV = Manifolds.WrapsV(from, shape) && Manifolds.WrapsV(to, shape);
                f.inset = Manifolds.HasPoles(from, shape) || Manifolds.HasPoles(to, shape) ? .5f / builtRings : 0f;
                shape.radius = radius;
                shape.extent = length;
            }
            // Calabi-Yau is drawn as n^2 separate patches sharing one grid.
            int n = Mathf.Clamp(quinticDegree, 2, 7);
            f.patches = builtMode == Mode.CalabiYau ? n * n : 1;
            f.perPatch = Mathf.Max(builtSides / f.patches, 2);
            f.calabiAngle = animateProjectionAngle
                ? projectionAngle + time * projectionAngleSpeed * Mathf.PI * 2f
                : projectionAngle;
            return f;
        }

        void FillGrid(float time)
        {
            var f = MakeFrame(time);
            int stride = builtSides + 1;

            // Pass 1: the surface, once per lattice node.
            for (int r = 0; r <= builtRings; r++)
            {
                float v = VParam(r, f.wrapV, f.inset);
                int row = r * stride;
                for (int c = 0; c <= builtSides; c++)
                {
                    int patch = 0;
                    float u;
                    if (f.patches > 1)
                    {
                        // Patch-local u. A column past the last patch only ever feeds skipped quads.
                        patch = c / f.perPatch;
                        if (patch >= f.patches) { lattice[row + c] = Vector3.zero; continue; }
                        u = (float)(c % f.perPatch) / (f.perPatch - 1);
                    }
                    else u = UParam(c, f.wrapU);
                    lattice[row + c] = Grid(u, v, ref f, patch);
                }
            }

            // Pass 2: scatter into the unwelded triangles.
            int k = 0;
            for (int r = 0; r < builtRings; r++)
            {
                int row0 = r * stride, row1 = row0 + stride;
                for (int s = 0; s < builtSides; s++)
                {
                    if (f.patches > 1 && (s % f.perPatch == f.perPatch - 1 || s / f.perPatch >= f.patches))
                    {
                        // Columns that would straddle a patch boundary collapse to a point instead of
                        // stretching across the gap. Six identical vertices draw nothing.
                        for (int n = 0; n < 6; n++) vertices[k++] = Vector3.zero;
                        continue;
                    }
                    Vector3 a = lattice[row0 + s], b = lattice[row0 + s + 1];
                    Vector3 c = lattice[row1 + s + 1], d = lattice[row1 + s];
                    vertices[k++] = a; vertices[k++] = b; vertices[k++] = c;
                    vertices[k++] = a; vertices[k++] = c; vertices[k++] = d;
                }
            }
            // Grid mode has no struts; any remaining slots stay at the origin.
            while (k < vertices.Length) vertices[k++] = Vector3.zero;
        }

        // `count` quads span [0, 1] in count + 1 lattice lines, so line i sits at i / count on either
        // kind of axis: a wrapping one lands its last line back on the first, an open one ends exactly
        // on the far edge. (Open axes used i / (count - 1), which put the last line past 1: an open
        // u-axis drew one column of extrapolated surface beyond its domain, and an open v-axis, whose
        // Lerp clamps, drew a final row of zero-area quads — the flicker the pole inset exists to avoid.)
        float UParam(int i, bool wrap) => (float)i / builtSides;
        float VParam(int j, bool wrap, float inset) => Mathf.Lerp(inset, 1f - inset, (float)j / builtRings);

        Vector3 Grid(float u, float v, ref Frame f, int patch)
        {
            Vector3 p;
            if (deformationReplacesSurface && surfaceDeformation != null && builtMode != Mode.CalabiYau)
                p = Vector3.zero;
            else if (builtMode == Mode.CalabiYau)
                p = Manifolds.CalabiYau(quinticDegree, patch, u, v, f.calabiAngle, calabiScale);
            else if (builtMode == Mode.Manifold)
            {
                p = Manifolds.Evaluate(from, shape, u, v, f.time);
                if (morph > 0f) p = Vector3.Lerp(p, Manifolds.Evaluate(to, shape, u, v, f.time), morph);
            }
            else
            {
                // The original chamber shape, unchanged.
                float r = radius * (1f + .055f * Mathf.Sin(v * Mathf.PI * 8f));
                float angle = u * Mathf.PI * 2f;
                p = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, -4f + v * length);
            }

            if (surfaceDeformation != null) p = surfaceDeformation(u, v, p);
            p *= f.breath;
            if (f.spin) p = new Vector3(p.x * f.spinCos - p.y * f.spinSin, p.x * f.spinSin + p.y * f.spinCos, p.z);
            p = KaleidoFold.Apply(kaleido, p, f.time);
            // Bubbles last, and only where one is open: the 3-D form is produced first and the
            // fourth dimension is added locally on top, so the grid and rails stay intact.
            return Hyper4DField.ApplyLocal(transform, p);
        }

        // ---- fractal rings ---------------------------------------------------

        void FillRings(float time)
        {
            float phase = time * (Mathf.PI * 2f / Mathf.Max(cycleSeconds, 1f));
            float openness = animate
                ? Mathf.Lerp(accordion.extendMin, accordion.extend, .5f + .5f * Mathf.Sin(phase))
                : accordion.extend;
            float bellowsPhase = animate ? phase * 1.7f : 0f;
            float breath = animate ? 1f + .04f * accordion.breathe * Mathf.Sin(phase * 2.3f) : 1f;
            float spinPhase = animate ? time * accordion.spin : 0f;

            BuildRingPositions(openness, bellowsPhase);
            BuildAllProfiles(spinPhase, breath);

            int k = 0;
            for (int r = 0; r < builtRings; r++)
            {
                float prev = r > 0 ? ringZ[r] - ringZ[r - 1] : (builtRings > 1 ? ringZ[1] - ringZ[0] : length);
                float next = r < builtRings - 1 ? ringZ[r + 1] - ringZ[r] : prev;
                float back = ringZ[r] - prev * accordion.bandFraction * .5f;
                float front = ringZ[r] + next * accordion.bandFraction * .5f;

                for (int s = 0; s < builtSides; s++)
                {
                    Vector3 a = RingPoint(r, s, back), b = RingPoint(r, s + 1, back);
                    Vector3 c = RingPoint(r, s + 1, front), d = RingPoint(r, s, front);
                    vertices[k++] = a; vertices[k++] = b; vertices[k++] = c;
                    vertices[k++] = a; vertices[k++] = c; vertices[k++] = d;
                }
            }

            for (int r = 0; r < builtRings - 1; r++)
            {
                float span = ringZ[r + 1] - ringZ[r];
                float fromZ = ringZ[r] + span * accordion.bandFraction * .5f;
                float toZ = ringZ[r + 1] - span * accordion.bandFraction * .5f;
                for (int c = 0; c < builtConnectors; c++)
                {
                    float centreSide = (float)c * builtSides / Mathf.Max(builtConnectors, 1);
                    float half = accordion.connectorWidth * .5f;
                    Vector3 a = RingPointF(r, centreSide - half, fromZ);
                    Vector3 b = RingPointF(r, centreSide + half, fromZ);
                    Vector3 c2 = RingPointF(r + 1, centreSide + half, toZ);
                    Vector3 d = RingPointF(r + 1, centreSide - half, toZ);
                    vertices[k++] = a; vertices[k++] = b; vertices[k++] = c2;
                    vertices[k++] = a; vertices[k++] = c2; vertices[k++] = d;
                }
            }
            while (k < vertices.Length) vertices[k++] = Vector3.zero;
        }

        /// <summary>
        /// Ring positions from two stacked effects: a bellows weight per interval, cumulatively
        /// summed so rings bunch where the weight is small, then the resulting parameter pushed
        /// through a Cantor IFS so rings group into clusters of clusters. The second part is the
        /// one that genuinely subdivides.
        /// </summary>
        void BuildRingPositions(float openness, float bellowsPhase)
        {
            int octaves = Mathf.Clamp(accordion.octaves, 1, 4);
            float total = 0f;

            for (int r = 0; r < builtRings; r++)
            {
                float t = builtRings > 1 ? (float)r / (builtRings - 1) : 0f;
                float amp = 1f, freq = accordion.bellows, sum = 0f, norm = 0f;
                for (int o = 0; o < octaves; o++)
                {
                    sum += amp * Mathf.Cos(Mathf.PI * 2f * freq * t + bellowsPhase * (o + 1));
                    norm += amp;
                    amp *= Mathf.Clamp01(accordion.gain);
                    freq *= 2f;
                }
                float weight = norm > 0f ? 1f + accordion.accordion * (sum / norm) : 1f;
                ringZ[r] = total;
                total += Mathf.Max(weight, .05f);
            }

            if (total <= 0f) total = 1f;
            for (int r = 0; r < builtRings; r++)
            {
                float t = ringZ[r] / total;
                if (accordion.cantorSpacing > 0f && accordion.cantorDepth > 0)
                    t = Mathf.Lerp(t, RingFractal.CantorPosition(t, accordion.cantorDepth, accordion.cantorRatio),
                                   accordion.cantorSpacing);
                ringZ[r] = t;
            }

            float span = length * openness;
            for (int r = 0; r < builtRings; r++) ringZ[r] = ringZ[r] * span - span * .5f;
        }

        /// <summary>
        /// Every ring's radii for this frame, in one pass. Computed up front because the struts
        /// need two rings' profiles at once, and recomputing per strut made this quadratic in the
        /// connector count.
        /// </summary>
        void BuildAllProfiles(float spinPhase, float breath)
        {
            for (int r = 0; r < builtRings; r++)
            {
                float t = builtRings > 1 ? (float)r / (builtRings - 1) : 0f;
                float m = Mathf.Clamp01(accordion.profileMorph + accordion.profileMorphAlong * t);
                float twist = accordion.fractalTwistAlong * t * Mathf.PI * 2f + spinPhase;
                int baseIndex = r * profileStride;

                for (int s = 0; s <= builtSides; s++)
                {
                    float theta = (float)s / builtSides * Mathf.PI * 2f + spinPhase;
                    float a = RingProfiles.Radius(accordion.profileA, accordion, theta);
                    float b = RingProfiles.Radius(accordion.profileB, accordion, theta);
                    float rr = Mathf.Lerp(a, b, m);
                    rr *= 1f + accordion.fractalOut * accordion.fractalAmplitude *
                          RingFractal.KochRadius(theta + twist, accordion.fractalDepth,
                                                 accordion.fractalBaseFrequency,
                                                 accordion.fractalLacunarity, accordion.fractalGain);
                    profiles[baseIndex + s] = radius * breath * Mathf.Max(rr, .02f);
                }
            }
        }

        Vector3 RingPoint(int ring, int side, float z)
        {
            float theta = (float)side / builtSides * Mathf.PI * 2f;
            float r = profiles[ring * profileStride + Mathf.Clamp(side, 0, builtSides)];
            return new Vector3(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r, z);
        }

        Vector3 RingPointF(int ring, float side, float z)
        {
            float wrapped = Mathf.Repeat(side, builtSides);
            int i = (int)wrapped;
            float f = wrapped - i;
            int baseIndex = Mathf.Clamp(ring, 0, builtRings - 1) * profileStride;
            float r = Mathf.Lerp(profiles[baseIndex + i], profiles[baseIndex + Mathf.Min(i + 1, builtSides)], f);
            float theta = wrapped / builtSides * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r, z);
        }

        // ---- IWireGeometry ---------------------------------------------------

        public bool IsBuilt => generated != null && builtSides > 0 && builtRings > 0;
        public int GridU => builtSides;
        public int GridV => builtRings;

        /// <summary>
        /// A point on the current surface at normalised (u, v). Reads the same per-frame data the
        /// mesh was built from, so the particle layer always sits exactly on the wire.
        /// </summary>
        public Vector3 SampleGrid(float u, float v)
        {
            if (!IsBuilt) return Vector3.zero;
            float time = (float)elapsed;
            u = Mathf.Repeat(u, 1f);
            v = Mathf.Clamp01(v);

            if (builtMode == Mode.FractalRings)
            {
                if (profiles == null || ringZ == null) return Vector3.zero;
                float ringPos = v * (builtRings - 1);
                int r0 = Mathf.Clamp((int)ringPos, 0, builtRings - 1);
                int r1 = Mathf.Min(r0 + 1, builtRings - 1);
                float f = ringPos - r0;
                float side = u * builtSides;
                return Vector3.Lerp(RingPointF(r0, side, ringZ[r0]), RingPointF(r1, side, ringZ[r1]), f);
            }

            var frame = MakeFrame(time);
            int samplePatch = builtMode == Mode.CalabiYau ? Mathf.Min((int)(u * frame.patches), frame.patches - 1) : 0;
            return Grid(u, Mathf.Lerp(frame.inset, 1f - frame.inset, v), ref frame, samplePatch);
        }

        // ---- material and bend ----------------------------------------------

        void DriveMaterial()
        {
            if (!instance) return;
            instance.SetFloat("_WireOpacity", worldGridScan && !worldGridScan.guideLinesOn ? 0f : wireOpacity);
            // UV0 is normalised here, so shaders that scale by a density need no correction.
            if (instance.HasProperty("_UVScale")) instance.SetVector("_UVScale", new Vector4(1, 1, 0, 0));
            int div = Mathf.Max(latticeDivisor, 1);
            if (instance.HasProperty("_LatticeU")) instance.SetFloat("_LatticeU", Mathf.Max(builtSides / div, 2));
            if (instance.HasProperty("_LatticeV")) instance.SetFloat("_LatticeV", Mathf.Max(builtRings / div, 2));
        }

        void DriveBend()
        {
            // Runtime rendering belongs to the same bridge as the nightclub. This controller is
            // edit preview only.
            if (!bend) return;
            var p = bridge
                ? (bridge.customBend
                    ? new CurvedWorldBridge.Preset { type = bridge.customShape, curvature = bridge.customCurvature, horizontal = bridge.customHorizontal, vertical = bridge.customVertical, axis = bridge.customAxis }
                    : CurvedWorldBridge.Presets[Mathf.Clamp(bridge.preset, 0, 7)])
                : new CurvedWorldBridge.Preset { type = BendType.TwistedSpiral_Z_Positive, curvature = curvature, horizontal = horizontal, vertical = vertical, axis = Vector3.forward };
            float amount = bridge
                ? (bridge.enabled && (bridge.customBend || bridge.preset > 0) ? bridge.amount * bridge.intensity : 0f)
                : 1f;

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

        // ---- teardown --------------------------------------------------------

        void Release()
        {
            if (registeredBridge) registeredBridge.RemoveSharedBend(ownedMaterials);
            foreach (var entry in swapped)
                if (entry.renderer)
                {
                    var current = entry.renderer.sharedMaterials;
                    for (int i = 0; i < current.Length && i < entry.clones.Length; i++)
                        if (current[i] == entry.clones[i]) current[i] = entry.originals[i];
                    entry.renderer.sharedMaterials = current;
                }
            swapped.Clear();
            foreach (var material in ownedMaterials) Dispose(material);
            ownedMaterials.Clear();
            Dispose(generated); Dispose(mesh);
            generated = null; mesh = null; instance = null; bend = null;
            registeredBridge = null; boundFlowpath = null; source = null;
            previewType = (BendType)(-1);
            vertices = null; lattice = null; bary = null; uv = null; indices = null; ringZ = null; profiles = null;
            builtSides = builtRings = builtConnectors = 0;
        }

        static void Dispose(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }

        [ContextMenu("Mode: Cylinder")] public void UseCylinder() { mode = Mode.Cylinder; }
        [ContextMenu("Mode: Manifold")] public void UseManifold() { mode = Mode.Manifold; }
        [ContextMenu("Mode: Fractal rings")] public void UseFractalRings() { mode = Mode.FractalRings; }
        [ContextMenu("Mode: Calabi-Yau")]
        public void UseCalabiYau()
        {
            mode = Mode.CalabiYau;
            // 25 patches need columns to spare; 5 per patch is the practical floor.
            sides = Mathf.Max(sides, Mathf.Clamp(quinticDegree, 2, 7) * Mathf.Clamp(quinticDegree, 2, 7) * 6);
            rings = Mathf.Max(rings, 16);
        }
        [ContextMenu("Next shape pair")]
        public void NextPair()
        {
            int count = Enum.GetValues(typeof(ManifoldSurface)).Length;
            mode = Mode.Manifold; autoCycle = false;
            from = to; to = (ManifoldSurface)(((int)to + 1) % count); morph = 0f;
        }
    }
}
