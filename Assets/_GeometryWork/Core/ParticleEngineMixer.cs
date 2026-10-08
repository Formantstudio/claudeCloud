using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.VFX;
using PsychedelicLab.Control;
using PsychedelicLab.Staging;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>PerformanceMixer bridge for one GeometryParticleCombo routed through a named stage channel.</summary>
    [ExecuteAlways, DefaultExecutionOrder(10000), DisallowMultipleComponent]
    public sealed class ParticleEngineMixer : MonoBehaviour
    {
        public static ParticleEngineMixer Instance { get; private set; }

        [Header("Scene route")]
        public string channelName = "BG3";
        public MetavidoStageChannel channel;
        public GeometryParticleCombo geometryCombo;
        public CurvedGeometryChamber tunnel;
        public WorldGridScan grid;
        public WireParticleSwarm wireSwarm;
        public DekeractTetraSwarm tetraSwarm;
        public CurvedWorldBridge curvedWorld;

        [Header("Mixer state")]
        [Range(1000f, 80000f)] public float density = 10000f;
        [Range(0.02f, 8f)] public float particleSize = 0.35f;
        [Range(0f, 1f)] public float morph;
        public bool autoCycle;
        public bool tunnelOn = true;
        [Range(0f, 1f)] public float tunnelLevel = 0.089f;
        public bool gridOn = true;
        public bool wireSwarmOn = true;
        public bool shapesOn = true;
        [Range(0f, 1f)] public float scatter;

        float nextRouteCheck;

        public int CatalogRevision => GetInstanceID();

        void OnEnable()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("More than one ParticleEngineMixer is active; PerformanceMixer uses the first one.", this);
            else Instance = this;
            Bind();
            if (Application.isPlaying) Apply();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void OnValidate()
        {
            Bind();
            if (Application.isPlaying) Apply();
        }

        void Update()
        {
            // In edit mode the HUD writes values explicitly through SetControllerValue.
            // Avoid continuous scene mutation and renderer-layer routing from an editor callback.
            if (!Application.isPlaying) return;
            if (Time.unscaledTime >= nextRouteCheck)
            {
                nextRouteCheck = Time.unscaledTime + 0.25f;
                Bind();
                RouteGeneratedRenderers();
            }
            Apply();
        }

        public bool SetControllerValue(string id, float value)
        {
            if (string.IsNullOrEmpty(id) || float.IsNaN(value) || float.IsInfinity(value)) return false;
            foreach (var target in MixerTargets())
            {
                if (target == null || target.id != id || target.set == null) continue;
                target.set(Mathf.Clamp(value, target.min, target.max));
                Bind();
                Apply();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                    if (geometryCombo) UnityEditor.EditorUtility.SetDirty(geometryCombo);
                    if (tunnel) UnityEditor.EditorUtility.SetDirty(tunnel);
                    if (grid) UnityEditor.EditorUtility.SetDirty(grid);
                    if (wireSwarm) UnityEditor.EditorUtility.SetDirty(wireSwarm);
                    if (tetraSwarm) UnityEditor.EditorUtility.SetDirty(tetraSwarm);
                }
#endif
                return true;
            }
            return false;
        }

        void Bind()
        {
            if (!channel || channel.channelName != channelName)
            {
                foreach (var candidate in FindObjectsByType<MetavidoStageChannel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (candidate != null && string.Equals(candidate.channelName, channelName, StringComparison.OrdinalIgnoreCase))
                    { channel = candidate; break; }
            }
            if (!geometryCombo) geometryCombo = FindFirstObjectByType<GeometryParticleCombo>(FindObjectsInactive.Include);
            if (!geometryCombo) return;
            if (!tunnel) tunnel = geometryCombo.GetComponent<CurvedGeometryChamber>();
            if (!grid) grid = geometryCombo.GetComponent<WorldGridScan>();
            if (!wireSwarm) wireSwarm = geometryCombo.GetComponent<WireParticleSwarm>();
            if (!tetraSwarm) tetraSwarm = geometryCombo.GetComponentInChildren<DekeractTetraSwarm>(true);
            if (!curvedWorld) curvedWorld = FindFirstObjectByType<CurvedWorldBridge>(FindObjectsInactive.Include);
            if (tunnel && curvedWorld && tunnel.bridge != curvedWorld) tunnel.bridge = curvedWorld;
        }

        void Apply()
        {
            if (geometryCombo)
            {
                geometryCombo.particlesPerSecond = density;
                if (geometryCombo.particleSettings == null) geometryCombo.particleSettings = new SdfParticleSettings();
                geometryCombo.particleSettings.sizeScale = particleSize;
                geometryCombo.autoCycle = autoCycle;
                if (!autoCycle) geometryCombo.SetMorph(morph);
                geometryCombo.showFlowpath = shapesOn;
                geometryCombo.showSymbols = shapesOn;
                SetActive(geometryCombo.flowpathLayer, shapesOn);
                SetActive(geometryCombo.symbolLayer, shapesOn);
            }
            if (tunnel)
            {
                tunnel.enabled = tunnelOn;
                tunnel.wireOpacity = tunnelLevel;
            }
            if (grid)
            {
                grid.guideLinesOn = gridOn;
                var gridVfx = grid.GetComponent<VisualEffect>();
                if (gridVfx && gridVfx.enabled != gridOn) gridVfx.enabled = gridOn;
            }
            if (wireSwarm) wireSwarm.enabled = wireSwarmOn;
            if (tetraSwarm) tetraSwarm.scatter = scatter;
        }

        void RouteGeneratedRenderers()
        {
            if (!geometryCombo || !MetavidoLayerRoutes.TryGet(channelName, out var slot)) return;
            int rendererLayer = slot.SourceLayer;
            var renderers = geometryCombo.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                // WorldGridScan intentionally lives on the shared Guides output. All other
                // generated geometry and particle renderers belong to this named capture route.
                if (grid && renderer.gameObject == grid.gameObject) continue;
                if (renderer.gameObject.layer != rendererLayer) renderer.gameObject.layer = rendererLayer;
            }
        }

        public IEnumerable<PerformanceMixer.Target> MixerTargets()
        {
            const string prefix = "particleEngine.geometryCombo.";
            const string group = "Particle Engine: Geometry Combo (";
            string namedGroup = group + channelName + ")";
            yield return Toggle(prefix + "on", namedGroup, "Geometry layer on", "Enable the Geometry Particle Combo's Metavido channel", () => channel && channel.channelEnabled, v => { if (channel) channel.channelEnabled = v; });
            yield return Scalar(prefix + "mix", namedGroup, "Geometry layer mix", "Whole captured particle layer opacity", 0f, 1f, () => channel ? channel.opacity : 0f, v => { if (channel) channel.opacity = v; });
            yield return Scalar(prefix + "density", namedGroup, "SDF particle density", "Particle spawn rate in particles per second", 1000f, 80000f, () => density, v => density = v);
            yield return Scalar(prefix + "size", namedGroup, "SDF particle size", "Scale the GeometryFX particle size", 0.02f, 8f, () => particleSize, v => particleSize = v);
            yield return Scalar(prefix + "morph", namedGroup, "Shape morph", "Morph between the selected particle shapes", 0f, 1f, () => morph, v => { morph = v; autoCycle = false; });
            yield return Toggle(prefix + "autoCycle", namedGroup, "Automatic shape cycle", "Cycle through the GeometryFX particle shapes", () => autoCycle, v => autoCycle = v);
            yield return Toggle(prefix + "tunnel.on", namedGroup, "Procedural tunnel", "Show or hide the curved procedural tunnel", () => tunnelOn, v => tunnelOn = v);
            yield return Scalar(prefix + "tunnel.level", namedGroup, "Tunnel wire level", "Opacity of the tunnel lattice", 0f, 1f, () => tunnelLevel, v => tunnelLevel = v);
            yield return Toggle(prefix + "grid.on", namedGroup, "Particle grid overlay", "Show or hide this combo's global guide-grid overlay", () => gridOn, v => gridOn = v);
            yield return Toggle(prefix + "swarm.on", namedGroup, "Tunnel particle swarm", "Show or hide the tunnel-riding wire particle swarm", () => wireSwarmOn, v => wireSwarmOn = v);
            yield return Toggle(prefix + "shapes.on", namedGroup, "Geometry accents", "Show or hide the flowpath and symbol accents", () => shapesOn, v => shapesOn = v);
            yield return Scalar(prefix + "scatter", namedGroup, "Dekeract scatter", "Scatter or reassemble the tetrahedron swarm", 0f, 1f, () => scatter, v => scatter = v);
        }

        // Compact single-call feed for the Stage Manager Geometry tab.
        public string ControllerJson()
        {
            var sb = new StringBuilder(1024);
            sb.Append('[');
            bool first = true;
            foreach (var target in MixerTargets())
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":\"").Append(Json(target.id)).Append("\",\"l\":\"").Append(Json(target.label))
                    .Append("\",\"h\":\"").Append(Json(target.hint)).Append("\",\"t\":").Append(target.toggle ? '1' : '0')
                    .Append(",\"a\":").Append(target.min.ToString("0.#####", CultureInfo.InvariantCulture))
                    .Append(",\"b\":").Append(target.max.ToString("0.#####", CultureInfo.InvariantCulture))
                    .Append(",\"v\":").Append((target.get != null ? target.get() : 0f).ToString("0.#####", CultureInfo.InvariantCulture)).Append('}');
            }
            return sb.Append(']').ToString();
        }

        static string Json(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        static void SetActive(GameObject target, bool active)
        {
            if (target && target.activeSelf != active) target.SetActive(active);
        }

        static PerformanceMixer.Target Scalar(string id, string group, string label, string hint, float min, float max, Func<float> read, Action<float> write)
            => new PerformanceMixer.Target { id = id, group = group, label = label, hint = hint, min = min, max = max, get = read, set = write };

        static PerformanceMixer.Target Toggle(string id, string group, string label, string hint, Func<bool> read, Action<bool> write)
            => new PerformanceMixer.Target { id = id, group = group, label = label, hint = hint, toggle = true, min = 0f, max = 1f,
                get = () => read() ? 1f : 0f, set = value => write(value >= 0.5f) };
    }
}
