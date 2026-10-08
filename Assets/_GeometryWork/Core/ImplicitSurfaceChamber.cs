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

        // Extraction buffers, reused between rebuilds.
        float[] samples;
        int[] cellVertex;
        Vector3[] cellPoint;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> bary = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        int builtRes;
        Material builtMaterial;
        bool builtDiagonals;
        ImplicitShape builtShape;
        float builtLevel, builtThickness, builtFrequency, builtPower, builtBoxScale, builtBarth;
        int builtIterations, builtFolds;
        float nextRebuild;
        double elapsed;

        static readonly Vector3Int[] EdgeAxes = { new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, 0, 1) };

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
            builtShape != field.shape ||
            !Mathf.Approximately(builtLevel, field.level) ||
            !Mathf.Approximately(builtThickness, field.thickness) ||
            !Mathf.Approximately(builtFrequency, field.frequency) ||
            !Mathf.Approximately(builtPower, field.power) ||
            !Mathf.Approximately(builtBoxScale, field.boxScale) ||
            !Mathf.Approximately(builtBarth, field.barthW) ||
            builtIterations != field.iterations ||
            builtFolds != field.folds;

        void Snapshot()
        {
            builtRes = Mathf.Clamp(resolution, 12, 96);
            builtDiagonals = hideQuadDiagonals;
            builtShape = field.shape;
            builtLevel = field.level; builtThickness = field.thickness;
            builtFrequency = field.frequency; builtPower = field.power;
            builtBoxScale = field.boxScale; builtBarth = field.barthW;
            builtIterations = field.iterations; builtFolds = field.folds;
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
        /// Naive surface nets. Samples the field at every grid corner, puts one vertex per cell at
        /// the mean of its edge zero-crossings, then emits a quad across each grid edge whose two
        /// corners disagree in sign.
        /// </summary>
        void Extract()
        {
            int n = builtRes;
            int corners = n + 1;
            int cornerCount = corners * corners * corners;
            if (samples == null || samples.Length != cornerCount) samples = new float[cornerCount];
            int cellCount = n * n * n;
            if (cellVertex == null || cellVertex.Length != cellCount)
            {
                cellVertex = new int[cellCount];
                cellPoint = new Vector3[cellCount];
            }

            float time = (float)elapsed;
            var probe = field;
            float savedLevel = probe.level;
            if (animate && levelDrift > 0f)
                probe.level = savedLevel + Mathf.Sin(time * .35f) * levelDrift;

            // Sample the field.
            float step = 2f / n;
            for (int z = 0; z < corners; z++)
            for (int y = 0; y < corners; y++)
            for (int x = 0; x < corners; x++)
            {
                var p = new Vector3(-1f + x * step, -1f + y * step, -1f + z * step);
                samples[(z * corners + y) * corners + x] = Implicits.Field(probe, p, time);
            }
            probe.level = savedLevel;

            verts.Clear(); bary.Clear(); uvs.Clear(); tris.Clear();
            for (int i = 0; i < cellCount; i++) cellVertex[i] = -1;

            float S(int x, int y, int z) => samples[(z * corners + y) * corners + x];

            // One vertex per cell that straddles the surface.
            int placed = 0;
            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector3 sum = Vector3.zero;
                int hits = 0;
                for (int c = 0; c < 12; c++)
                {
                    int a0 = EdgeA[c], a1 = EdgeB[c];
                    int ax = x + (a0 & 1), ay = y + ((a0 >> 1) & 1), az = z + ((a0 >> 2) & 1);
                    int bx = x + (a1 & 1), by = y + ((a1 >> 1) & 1), bz = z + ((a1 >> 2) & 1);
                    float va = S(ax, ay, az), vb = S(bx, by, bz);
                    if ((va < 0f) == (vb < 0f)) continue;
                    float t = va / (va - vb);
                    sum += Vector3.Lerp(new Vector3(ax, ay, az), new Vector3(bx, by, bz), t);
                    hits++;
                }
                if (hits == 0) continue;
                Vector3 local = sum / hits;
                int cell = (z * n + y) * n + x;
                cellPoint[cell] = Hyper4DField.ApplyLocal(transform,
                    new Vector3(-1f + local.x * step, -1f + local.y * step, -1f + local.z * step) * extent);
                cellVertex[cell] = placed++;
            }

            // A quad across every sign-changing grid edge, joining the four cells around it.
            float diag = builtDiagonals ? 1f : 0f;
            for (int z = 1; z < n; z++)
            for (int y = 1; y < n; y++)
            for (int x = 1; x < n; x++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    var d = EdgeAxes[axis];
                    float va = S(x, y, z), vb = S(x + d.x, y + d.y, z + d.z);
                    if ((va < 0f) == (vb < 0f)) continue;

                    // The four cells sharing this edge, offset in the two other axes.
                    int o1 = (axis + 1) % 3, o2 = (axis + 2) % 3;
                    int c0 = Cell(n, x, y, z, 0, 0, o1, o2);
                    int c1 = Cell(n, x, y, z, -1, 0, o1, o2);
                    int c2 = Cell(n, x, y, z, -1, -1, o1, o2);
                    int c3 = Cell(n, x, y, z, 0, -1, o1, o2);
                    if (c0 < 0 || c1 < 0 || c2 < 0 || c3 < 0) continue;
                    if (cellVertex[c0] < 0 || cellVertex[c1] < 0 || cellVertex[c2] < 0 || cellVertex[c3] < 0) continue;

                    Vector3 p0 = cellPoint[c0], p1 = cellPoint[c1], p2 = cellPoint[c2], p3 = cellPoint[c3];
                    bool flip = va < 0f;
                    if (flip) { var t2 = p1; p1 = p3; p3 = t2; }

                    // Unwelded, so each triangle carries its own bary for the wire.
                    EmitQuad(p0, p1, p2, p3, diag);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(1, bary);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // Curved World displaces vertices on the GPU, outside the straight mesh bounds.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * Mathf.Max(200f, extent * 4f));

            Status = field.shape + " · " + (verts.Count / 6).ToString("N0") + " quads, "
                   + verts.Count.ToString("N0") + " vertices, " + builtRes + "^3 field"
                   + (Implicits.IsPeriodic(field.shape) ? " · tiles" : "")
                   + (Implicits.IsSignedDistance(field.shape) ? " · signed distance" : " · level set");
        }

        static int Cell(int n, int x, int y, int z, int d1, int d2, int o1, int o2)
        {
            int cx = x, cy = y, cz = z;
            Offset(ref cx, ref cy, ref cz, o1, d1);
            Offset(ref cx, ref cy, ref cz, o2, d2);
            if (cx < 0 || cy < 0 || cz < 0 || cx >= n || cy >= n || cz >= n) return -1;
            return (cz * n + cy) * n + cx;
        }

        static void Offset(ref int x, ref int y, ref int z, int axis, int d)
        {
            if (axis == 0) x += d; else if (axis == 1) y += d; else z += d;
        }

        void EmitQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float diag)
        {
            int i = verts.Count;
            // (a,b,c): the shared diagonal a-c vanishes in component 1.
            verts.Add(a); bary.Add(new Vector3(1, diag, 0)); uvs.Add(new Vector2(0, 0));
            verts.Add(b); bary.Add(new Vector3(0, 1 + diag, 0)); uvs.Add(new Vector2(1, 0));
            verts.Add(c); bary.Add(new Vector3(0, diag, 1)); uvs.Add(new Vector2(1, 1));
            // (a,c,d): the diagonal vanishes in component 2.
            verts.Add(a); bary.Add(new Vector3(1, 0, diag)); uvs.Add(new Vector2(0, 0));
            verts.Add(c); bary.Add(new Vector3(0, 1, diag)); uvs.Add(new Vector2(1, 1));
            verts.Add(d); bary.Add(new Vector3(0, 0, 1 + diag)); uvs.Add(new Vector2(0, 1));
            for (int k = 0; k < 6; k++) tris.Add(i + k);
        }

        // The 12 cube edges as corner-index pairs; bit 0 = x, bit 1 = y, bit 2 = z.
        static readonly int[] EdgeA = { 0, 1, 2, 0, 4, 5, 6, 4, 0, 1, 3, 2 };
        static readonly int[] EdgeB = { 1, 3, 3, 2, 5, 7, 7, 6, 4, 5, 7, 6 };

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
            verts.Clear(); bary.Clear(); uvs.Clear(); tris.Clear();
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
