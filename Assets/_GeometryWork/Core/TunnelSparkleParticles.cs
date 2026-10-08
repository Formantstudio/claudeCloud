using System.Collections.Generic;
using UnityEngine;
using AmazingAssets.CurvedWorld;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Gives a Shapes FlowPath tunnel the geometry combo's look: a lattice wireframe skin
    /// plus the same SDF sparkle cloud clinging to its wall.
    ///
    /// Nothing the tunnel already had is edited. The wire material is cloned per instance,
    /// swapped onto the renderers while this component is enabled, and put back on disable,
    /// so Flowpath_Cyan and the shared tunnel mesh stay exactly as they were.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TunnelSparkleParticles : MonoBehaviour
    {
        public enum Detail { Preview = 64, Standard = 96, High = 128 }
        public enum Axis { X, Y, Z }

        [Header("Targets")]
        [Tooltip("Renderers to skin. Empty = every MeshRenderer on this object and its children.")]
        public Renderer[] targets;

        [Header("Wire skin")]
        [Tooltip("Material using PsychedelicLab/Tunnel Sparkle Wire. Cloned, never written to.")]
        public Material wireMaterial;
        public bool skinRenderers = true;
        [Range(0, 2)] public float wireOpacity = .5f;
        [Range(0, 4)] public float sparkle = 1f;
        [Tooltip("Keeps the wire in step with the scan system's guide lines.")]
        public WorldGridScan worldGridScan;
        [Tooltip("Shared Curved World bend, so the tunnel bends with the rest of the stage.")]
        public CurvedWorldBridge bridge;

        [Header("Sparkle cloud")]
        public bool emitParticles = true;
        public ComputeShader tunnelField;
        public Detail detail = Detail.Standard;
        public SdfParticleSettings particles = new SdfParticleSettings();
        [Tooltip("Applies a named starting point to the settings above, then returns to Custom.")]
        public SdfParticleSettings.Preset applyPreset = SdfParticleSettings.Preset.Custom;

        [Header("Tube fit (local units)")]
        [Tooltip("Reads radius and length off the renderer bounds on every rebuild.")]
        public bool autoFit = true;
        public Axis tubeAxis = Axis.Z;
        [Min(.01f)] public float radius = .5f;
        [Min(.001f)] public float wallThickness = .01f;
        [Min(.01f)] public float halfLength = .5f;
        [Range(0, .5f)] public float ringDepth = .02f;
        [Min(0)] public float ringFrequency = 18f;
        [Range(0, 32)] public int flutes = 0;
        [Range(0, .4f)] public float fluteDepth = 0f;
        [Range(-2, 2)] public float twist = 0f;
        [Tooltip("Spare room around the tube inside the field cube.")]
        [Range(1f, 2f)] public float volumePadding = 1.1f;

        [Header("Panel")]
        public bool showControls = false;

        public string Status { get; private set; } = "Play to preview the sparkle cloud";

        readonly SdfParticleRig rig = new SdfParticleRig();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<(Renderer renderer, Material[] originals, Material[] clones)> swapped =
            new List<(Renderer, Material[], Material[])>();

        ComputeShader compute;
        RenderTexture volume;
        int kernel, resolution;
        float side;
        double elapsed;
        Material boundWire;
        CurvedWorldBridge registeredBridge;
        bool skinned;

        void OnEnable()
        {
            if (particles == null) particles = new SdfParticleSettings();
            BuildSkin();
            if (Application.isPlaying) BuildCloud();
        }

        void OnDisable()
        {
            ReleaseCloud();
            ReleaseSkin();
        }

        void Update()
        {
            if (particles == null) particles = new SdfParticleSettings();
            if (applyPreset != SdfParticleSettings.Preset.Custom)
            {
                particles.Apply(applyPreset);
                applyPreset = SdfParticleSettings.Preset.Custom;
            }

            if (boundWire != wireMaterial || registeredBridge != bridge || skinned != skinRenderers) BuildSkin();

            float opacity = worldGridScan && !worldGridScan.guideLinesOn ? 0f : wireOpacity;
            foreach (var material in ownedMaterials)
            {
                if (!material) continue;
                material.SetFloat("_WireOpacity", opacity);
                material.SetFloat("_Sparkle", sparkle);
            }

            if (!Application.isPlaying) return;

            if (!emitParticles || !tunnelField || !particles.graph) { ReleaseCloud(); Status = StatusForMissingPieces(); return; }
            if (compute == null || resolution != (int)detail || rig.NeedsRebuild(particles.graph)) BuildCloud();
            if (compute == null || rig.Effect == null) return;

            elapsed += Time.deltaTime;
            Bake((float)elapsed);
            rig.Push(particles);

            Status = rig.HasSizeControl
                ? string.Format("Sparkles {0:0.####} world units · {1:0} / s", rig.WorldParticleSize(particles), particles.spawnRate)
                : "Graph has no ParticleSize parameter — size is stuck at the graph's own curve";
        }

        string StatusForMissingPieces()
        {
            if (!emitParticles) return "Sparkle cloud switched off";
            if (!tunnelField) return "Assign TunnelSdfField.compute";
            return "Assign a particle graph (duplicate SdfSparkleParticles.vfx to branch one)";
        }

        // ---- wire skin -------------------------------------------------------

        void BuildSkin()
        {
            ReleaseSkin();
            boundWire = wireMaterial;
            registeredBridge = bridge;
            skinned = skinRenderers;
            if (!wireMaterial || !skinRenderers) return;

            foreach (var renderer in Resolved())
            {
                if (!renderer) continue;
                var originals = renderer.sharedMaterials;
                var clones = new Material[Mathf.Max(originals.Length, 1)];
                for (int i = 0; i < clones.Length; i++)
                {
                    clones[i] = new Material(wireMaterial) { hideFlags = HideFlags.HideAndDontSave };
                    ownedMaterials.Add(clones[i]);
                }
                renderer.sharedMaterials = clones;
                swapped.Add((renderer, originals, clones));
            }

            // Curved World moves vertices on the GPU, past the straight mesh bounds.
            foreach (var renderer in Resolved())
                if (renderer) renderer.allowOcclusionWhenDynamic = false;

            if (bridge && ownedMaterials.Count > 0) bridge.AddSharedBend(ownedMaterials);
        }

        void ReleaseSkin()
        {
            if (registeredBridge && ownedMaterials.Count > 0) registeredBridge.RemoveSharedBend(ownedMaterials);
            foreach (var entry in swapped)
                if (entry.renderer) entry.renderer.sharedMaterials = entry.originals;
            swapped.Clear();
            foreach (var material in ownedMaterials) Dispose(material);
            ownedMaterials.Clear();
            boundWire = null; registeredBridge = null;
        }

        IEnumerable<Renderer> Resolved()
        {
            if (targets != null && targets.Length > 0) return targets;
            return GetComponentsInChildren<MeshRenderer>(true);
        }

        // ---- sparkle cloud ---------------------------------------------------

        void BuildCloud()
        {
            ReleaseCloud();
            if (!emitParticles || !tunnelField || !particles.graph) return;
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supports3DRenderTextures ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
            {
                Status = "This GPU cannot render the 3D particle field."; Debug.LogWarning(Status, this); return;
            }

            if (autoFit) Fit();

            resolution = (int)detail;
            side = Mathf.Max(2f * radius, 2f * halfLength) * volumePadding;

            compute = Instantiate(tunnelField);
            kernel = compute.FindKernel("Bake");

            volume = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RHalf)
            {
                name = "Tunnel sparkle field",
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = resolution,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            volume.Create();

            rig.Build(transform, "Tunnel sparkle particles", particles.graph, Centre(), side, 4211);
            rig.SetField(volume);
            Bake(0f);
            rig.Reinit();
            elapsed = 0;
        }

        /// <summary>Reads the tube off the renderer bounds so it lines up with the mesh you placed.</summary>
        void Fit()
        {
            Bounds local = default;
            bool any = false;
            foreach (var renderer in Resolved())
            {
                if (!renderer) continue;
                var b = renderer.localBounds;
                // localBounds is in the renderer's own space; bring it into this component's space.
                var centre = transform.InverseTransformPoint(renderer.transform.TransformPoint(b.center));
                var corner = transform.InverseTransformVector(renderer.transform.TransformVector(b.extents));
                var here = new Bounds(centre, new Vector3(Mathf.Abs(corner.x), Mathf.Abs(corner.y), Mathf.Abs(corner.z)) * 2f);
                if (!any) { local = here; any = true; } else local.Encapsulate(here);
            }
            if (!any) return;

            var size = local.size;
            tubeAxis = size.z >= size.x && size.z >= size.y ? Axis.Z : (size.y >= size.x ? Axis.Y : Axis.X);
            float along = tubeAxis == Axis.X ? size.x : tubeAxis == Axis.Y ? size.y : size.z;
            float acrossA = tubeAxis == Axis.X ? size.y : tubeAxis == Axis.Y ? size.z : size.x;
            float acrossB = tubeAxis == Axis.X ? size.z : tubeAxis == Axis.Y ? size.x : size.y;

            radius = Mathf.Max((acrossA + acrossB) * .25f, .01f);
            halfLength = Mathf.Max(along * .5f, .01f);
            wallThickness = Mathf.Max(radius * .02f, .001f);
        }

        Vector3 Centre()
        {
            Bounds local = default;
            bool any = false;
            foreach (var renderer in Resolved())
            {
                if (!renderer) continue;
                var b = renderer.localBounds;
                var centre = transform.InverseTransformPoint(renderer.transform.TransformPoint(b.center));
                if (!any) { local = new Bounds(centre, Vector3.zero); any = true; } else local.Encapsulate(centre);
            }
            return any ? local.center : Vector3.zero;
        }

        void Bake(float time)
        {
            if (compute == null || volume == null) return;
            compute.SetTexture(kernel, "_Out", volume);
            compute.SetVector("_Config", new Vector4(resolution, side, time, twist));
            compute.SetVector("_Tube", new Vector4(radius, wallThickness, halfLength, ringDepth));
            compute.SetVector("_Rib", new Vector4(ringFrequency, flutes, fluteDepth, (int)tubeAxis));
            int groups = (resolution + 7) / 8;
            compute.Dispatch(kernel, groups, groups, groups);
        }

        void ReleaseCloud()
        {
            rig.Release();
            if (volume) { volume.Release(); Dispose(volume); }
            if (compute) Dispose(compute);
            volume = null; compute = null; resolution = 0;
        }

        static void Dispose(Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }

        void OnGUI()
        {
            if (!showControls) return;
            GUILayout.BeginArea(new Rect(Screen.width - 316, 16, 300, 300), GUI.skin.box);
            GUILayout.Label("TUNNEL SPARKLE");
            GUILayout.Label(Status);
            GUILayout.Label("Particle size " + particles.sizeScale.ToString("0.00"));
            particles.sizeScale = GUILayout.HorizontalSlider(particles.sizeScale, .02f, 8f);
            GUILayout.Label("Density " + particles.spawnRate.ToString("0"));
            particles.spawnRate = GUILayout.HorizontalSlider(particles.spawnRate, 100f, 200000f);
            GUILayout.Label("Stick distance " + particles.stickDistance.ToString("0.0000"));
            particles.stickDistance = GUILayout.HorizontalSlider(particles.stickDistance, .0002f, .08f);
            GUILayout.Label("Wire opacity " + wireOpacity.ToString("0.00"));
            wireOpacity = GUILayout.HorizontalSlider(wireOpacity, 0f, 2f);
            GUILayout.Label("Wire sparkle " + sparkle.ToString("0.00"));
            sparkle = GUILayout.HorizontalSlider(sparkle, 0f, 4f);
            emitParticles = GUILayout.Toggle(emitParticles, "Sparkle cloud");
            GUILayout.EndArea();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.18f, .85f, .9f, .6f);
            Gizmos.DrawWireCube(Centre(), Vector3.one * Mathf.Max(2f * radius, 2f * halfLength) * volumePadding);
        }
    }
}
