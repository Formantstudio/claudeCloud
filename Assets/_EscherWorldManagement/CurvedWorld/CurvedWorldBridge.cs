using System.Collections.Generic;
using AmazingAssets.CurvedWorld;
using UnityEngine;
using UnityEngine.Rendering;
using PsychedelicLab.MetavidoScene;
using PsychedelicLab.Staging;

namespace PsychedelicLab.Control
{
    // Curved World in the layer system. Curved World bends the VERTICES of every material that uses its shaders, so it bends what a stage layer's capture
    // camera sees, and the Metavido particles / voxels reconstructed from that capture come out already warped: the whole stage bends, layer by layer.
    //   * One Curved World controller (bend ID 1, manual update). A capture is its own camera, so just before each camera renders the bridge publishes THAT
    //     layer's bend (master amount x the layer's own amount, 0 when the layer is switched off): every stage layer can bend by a different amount
    //     at the same time although the shaders were generated with a single bend ID.
    //   * The materials of a layer's source objects are swapped for runtime COPIES that use the Curved World shaders (URP Lit -> "Amazing Assets/Curved World/Lit",
    //     Unlit -> "Curved World/Unlit"; anything else is left alone) only while the layer is bending, and put back a second after it stops. No asset is changed.
    //   * Presets are the good settings from the Curved World example scenes (only bend types the installed shaders were generated for). "Amount" scales the
    //     preset from nothing to its full strength, so 0 -> 1 is an intro or an outro. The bend pivot follows each camera, so what is near stays put.
    //   * Every value is a Macro Mixer target ("Curved World"): bend.amount, bend.preset, bend.<layer>.on, bend.<layer>.amount.
    public sealed class CurvedWorldBridge : MonoBehaviour
    {
        public static CurvedWorldBridge Instance { get; private set; }

        public struct Preset { public string name; public BendType type; public float curvature, vertical, horizontal, offsetCurvature; public Vector3 axis; }

        public static readonly Preset[] Presets =
        {
            new Preset { name = "Off", type = BendType.LittlePlanet_Y },
            new Preset { name = "Little Planet", type = BendType.LittlePlanet_Y, curvature = 35f },
            new Preset { name = "Cylinder Tower", type = BendType.CylindricalTower_X, curvature = 50f },
            new Preset { name = "Cylinder Rolloff", type = BendType.CylindricalRolloff_Z, curvature = 35f },
            new Preset { name = "Classic Runner", type = BendType.ClassicRunner_X_Positive, horizontal = -3f },
            new Preset { name = "Twisted Corridor", type = BendType.TwistedSpiral_X_Positive, curvature = 2f, vertical = 1.2f, horizontal = -1.2f, axis = new Vector3(1f, 0f, 0f) },
            new Preset { name = "Twisted Highway", type = BendType.TwistedSpiral_X_Positive, curvature = 1.2f, horizontal = -0.1f, offsetCurvature = -15f, axis = new Vector3(1f, 0.08f, 0f) },
            new Preset { name = "Ball", type = BendType.LittlePlanet_Y, curvature = 120f },
        };

        [Range(0f, 1f)] public float amount = 0f;
        [Tooltip("Strength multiplier on the preset's bend (1 = the preset as tuned, 3 = triple). Amount is how much of it is used; intensity is how hard it is.")] [Range(0f, 3f)] public float intensity = 1f;
        [Tooltip("0 = off, then the presets in the order of the list (1 Little Planet ... 7 Ball).")] public int preset = 1;
        [Tooltip("Rolls the final picture around the screen centre, in degrees (720 = two full turns). With the Ball preset it is the spin of the world compressing into a ball.")] [Range(-1440f, 1440f)] public float spin = 0f;
        [Tooltip("The bend pivot follows each camera (recommended); off = the world origin.")] public bool pivotFollowsCamera = true;

        sealed class LayerState
        {
            public string name; public bool on = true; public float amount = 1f;
            public MetavidoStageChannel channel; public Camera camera;
            public bool converted; public float lastWanted;
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<Material[]> originals = new List<Material[]>();
            public readonly List<Material> made = new List<Material>();
            public CurvedWorldCamera cullCam;
        }

        readonly Dictionary<string, LayerState> layers = new Dictionary<string, LayerState>();
        readonly Dictionary<Camera, LayerState> byCamera = new Dictionary<Camera, LayerState>();
        CurvedWorldController controller;
        // URP shader name -> the Curved World shader that replaces it (looked up once)
        readonly Dictionary<string, Shader> map = new Dictionary<string, Shader>();
        Shader slice4D;
        float nextSync;
        BendType keywordType = (BendType)(-1);

        // The universal controls. Off = the presets above drive the bend. On = these values drive it, for any shape, on every bent
        // layer and every object that was registered with AddSharedBend (for example the infinite club).
        [Header("Custom bend (the universal controls)")]
        [Tooltip("On = the controls below drive the bend instead of the preset.")] public bool customBend;
        [Tooltip("The Curved World shape used when Custom Bend is on.")] public BendType customShape = BendType.CylindricalRolloff_Z;
        [Range(-30f, 30f), Tooltip("Curvature of the shape.")] public float customCurvature = 30f;
        [Range(-15f, 15f), Tooltip("Bend left (negative) and right (positive).")] public float customHorizontal;
        [Range(-10f, 10f), Tooltip("Bend up (positive) and down (negative).")] public float customVertical;
        [Tooltip("Rotation axis for shapes that spin (Twisted Spiral uses X).")] public Vector3 customAxis = Vector3.right;

        // Materials outside the stage layers that bend with this controller (they get the shape keyword with the rest).
        readonly List<Material> sharedBend = new List<Material>();

        public void AddSharedBend(IEnumerable<Material> materials)
        {
            foreach (var m in materials)
                if (m != null && !sharedBend.Contains(m))
                {
                    sharedBend.Add(m);
                    SetKeyword(m, CurrentType());
                }
        }

        public void RemoveSharedBend(IEnumerable<Material> materials)
        {
            foreach (var m in materials) sharedBend.Remove(m);
        }

        BendType CurrentType() => customBend ? customShape : Presets[Mathf.Clamp(preset, 0, Presets.Length - 1)].type;

        Camera spinCam; Quaternion spinSaved;
        Camera outputCam;

        void OnEnable()
        {
            Instance = this;
            if (!Application.isPlaying) return;
            void Map(string from, string to) { var s = Shader.Find(to); if (s != null) map[from] = s; }
            Map("Universal Render Pipeline/Lit", "Amazing Assets/Curved World/Lit");
            Map("Universal Render Pipeline/Simple Lit", "Amazing Assets/Curved World/Simple Lit");
            Map("Universal Render Pipeline/Complex Lit", "Amazing Assets/Curved World/Complex Lit");
            Map("Universal Render Pipeline/Baked Lit", "Amazing Assets/Curved World/Baked Lit");
            Map("Universal Render Pipeline/Unlit", "Amazing Assets/Curved World/Unlit");
            Map("Universal Render Pipeline/Particles/Lit", "Amazing Assets/Curved World/Particles/Lit");
            Map("Universal Render Pipeline/Particles/Simple Lit", "Amazing Assets/Curved World/Particles/Simple Lit");
            Map("Universal Render Pipeline/Particles/Unlit", "Amazing Assets/Curved World/Particles/Unlit");
            Map("Sprites/Default", "Amazing Assets/Curved World/Sprites/Default");
            slice4D = Shader.Find("PsychedelicLab/Slice4D");
            var go = new GameObject("Curved World (bridge)");
            go.transform.SetParent(transform, false);
            controller = go.AddComponent<CurvedWorldController>();
            controller.manualUpdate = true;
            controller.bendID = 1;
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
            foreach (var l in layers.Values) Restore(l);
            if (controller != null) { controller.DisableBend(); Destroy(controller.gameObject); }
            controller = null;
        }

        // ---------------------------------------------------------------- per camera: publish this layer's bend
        void OnBegin(ScriptableRenderContext context, Camera camera)
        {
            if (controller == null) return;
            float a = amount;
            if (byCamera.TryGetValue(camera, out var layer)) a = layer.on ? amount * layer.amount : 0f;
            Publish((preset > 0 || customBend) ? a : 0f, camera);
            if (Mathf.Abs(spin * intensity) > 0.01f && camera == outputCam && camera != null)
            {
                spinCam = camera; spinSaved = camera.transform.rotation;
                camera.transform.rotation = spinSaved * Quaternion.AngleAxis(spin * intensity, Vector3.forward);
            }
        }

        void OnEnd(ScriptableRenderContext context, Camera camera)
        {
            if (spinCam != null && camera == spinCam) { camera.transform.rotation = spinSaved; spinCam = null; }
        }

        void Publish(float a, Camera camera)
        {
            // Custom Bend: the universal controls (any shape, left/right, up/down, curvature). Otherwise the preset.
            var p = customBend
                ? new Preset { name = "Custom", type = customShape, curvature = customCurvature, vertical = customVertical, horizontal = customHorizontal, offsetCurvature = 0f, axis = customAxis }
                : Presets[Mathf.Clamp(preset, 0, Presets.Length - 1)];
            float animatedIntensity = intensity;
            var automation = GetComponent<CurvedWorldAutomation>();
            if (automation != null) automation.Apply(ref p, ref a, ref animatedIntensity);
            RefreshShapeKeywords(p.type);
            controller.bendType = p.type;
            controller.bendID = 1;
            controller.manualUpdate = true;
            controller.bendCurvatureSize = p.curvature * a * animatedIntensity;
            controller.bendVerticalSize = p.vertical * a * animatedIntensity;
            controller.bendHorizontalSize = p.horizontal * a * animatedIntensity;
            controller.bendCurvatureOffset = p.offsetCurvature;
            controller.bendRotationAxis = p.axis;
            controller.bendRotationAxisType = CurvedWorldController.AxisType.Custom;
            controller.bendPivotPoint = null;
            controller.bendPivotPointPosition = pivotFollowsCamera && camera != null ? camera.transform.position : Vector3.zero;
            controller.ManualUpdate();
        }

        // ---------------------------------------------------------------- layers and their materials
        void Update()
        {
            if (controller == null || Time.unscaledTime < nextSync) return;
            nextSync = Time.unscaledTime + 0.3f;
            Discover();
            if (outputCam == null)
            {
                var o = FindFirstObjectByType<MetavidoStageOutput>();
                outputCam = o != null ? o.GetComponent<Camera>() : Camera.main;
            }
            var type = CurrentType();
            foreach (var l in layers.Values)
            {
                bool want = (preset > 0 || customBend) && l.on && amount * l.amount > 0.001f;
                if (want) { l.lastWanted = Time.unscaledTime; if (!l.converted) Convert(l); }
                else if (l.converted && Time.unscaledTime - l.lastWanted > 1f) Restore(l);
            }
            RefreshShapeKeywords(type);
        }

        void RefreshShapeKeywords(BendType type)
        {
            if (type != keywordType)
            {
                keywordType = type;
                foreach (var l in layers.Values) foreach (var m in l.made) SetKeyword(m, type);
                foreach (var m in sharedBend) if (m != null) SetKeyword(m, type);
            }
        }

        void Discover()
        {
            foreach (var ch in FindObjectsByType<MetavidoStageChannel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ch == null || string.IsNullOrEmpty(ch.channelName)) continue;
                if (!layers.TryGetValue(ch.channelName, out var l)) layers[ch.channelName] = l = new LayerState { name = ch.channelName };
                l.channel = ch;
                var cam = ch.capture != null ? ch.capture.GetComponent<Camera>() : null;
                if (cam != null && l.camera != cam) { if (l.camera != null) byCamera.Remove(l.camera); l.camera = cam; byCamera[cam] = l; }
            }
        }

        static string KeywordFor(BendType type) { return "CURVEDWORLD_BEND_TYPE_" + type.ToString().ToUpperInvariant(); }

        static void SetKeyword(Material m, BendType type)
        {
            foreach (BendType t in System.Enum.GetValues(typeof(BendType))) m.DisableKeyword(KeywordFor(t));
            m.EnableKeyword(KeywordFor(type));
        }

        Material MakeCurved(Material src, BendType type)
        {
            if (src == null) return null;
            string shader = src.shader != null ? src.shader.name : "";
            Shader target = null;
            if (src.HasProperty("_OverallAlpha") && src.HasProperty("_MainGridTexture")) target = Shader.Find("PsychedelicLab/Curved Neon Grid");
            else if (shader == "PsychedelicLab/Slice4D") target = src.shader;   // the 4D shapes bend in their own shader; only the keyword is needed
            else map.TryGetValue(shader, out target);
            if (target == null) return null;
            var m = new Material(src) { name = src.name + " (Curved)" };
            if (target != src.shader) m.shader = target;
            foreach (var property in src.GetTexturePropertyNames())
                if (m.HasProperty(property)) m.SetTexture(property, src.GetTexture(property));
            SetKeyword(m, type);
            return m;
        }

        void Convert(LayerState l)
        {
            if (l.channel == null || l.channel.sourceObjects == null) return;
            var type = Presets[Mathf.Clamp(preset, 0, Presets.Length - 1)].type;
            int swapped = 0;
            foreach (var r in l.channel.sourceObjects.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer) && !(r is ParticleSystemRenderer) && !(r is SpriteRenderer)) continue;
                var original = r.sharedMaterials;
                var copy = new Material[original.Length];
                bool any = false;
                for (int i = 0; i < original.Length; i++)
                {
                    var curved = MakeCurved(original[i], type);
                    if (curved != null) { copy[i] = curved; l.made.Add(curved); any = true; } else copy[i] = original[i];
                }
                if (!any) continue;
                l.renderers.Add(r); l.originals.Add(original);
                r.sharedMaterials = copy;
                swapped++;
            }
            l.converted = true;
            if (l.camera != null && l.cullCam == null)
            {
                // a wide culling frustum so bent geometry is not culled away at the edges
                l.cullCam = l.camera.GetComponent<CurvedWorldCamera>() ?? l.camera.gameObject.AddComponent<CurvedWorldCamera>();
                l.cullCam.matrixType = CurvedWorldCamera.MatrixType.Perspective;
                l.cullCam.fieldOfView = 110f;
            }
            Debug.Log("Curved World: layer " + l.name + " bends (" + swapped + " renderer(s) switched to the Curved World shaders, preset " + Presets[preset].name + ").");
        }

        void Restore(LayerState l)
        {
            if (!l.converted) return;
            for (int i = 0; i < l.renderers.Count; i++) if (l.renderers[i] != null) l.renderers[i].sharedMaterials = l.originals[i];
            foreach (var m in l.made) if (m != null) Destroy(m);
            l.renderers.Clear(); l.originals.Clear(); l.made.Clear();
            if (l.cullCam != null) { Destroy(l.cullCam); l.cullCam = null; }
            l.converted = false;
        }

        // ---------------------------------------------------------------- settings access
        public static bool TryGet(string key, out float value)
        {
            value = 0f;
            var s = Instance;
            if (s == null) return false;
            switch (key)
            {
                case "bend.amount": value = s.amount; return true;
                case "bend.intensity": value = s.intensity; return true;
                case "bend.preset": value = s.preset; return true;
                case "bend.spin": value = s.spin; return true;
            }
            return false;
        }

        public static bool TrySet(string key, float v)
        {
            var s = Instance;
            if (s == null) return false;
            switch (key)
            {
                case "bend.amount": s.amount = Mathf.Clamp01(v); return true;
                case "bend.intensity": s.intensity = Mathf.Clamp(v, 0f, 3f); return true;
                case "bend.preset": s.preset = Mathf.Clamp(Mathf.RoundToInt(v), 0, Presets.Length - 1); return true;
                case "bend.spin": s.spin = Mathf.Clamp(v, -1440f, 1440f); return true;
            }
            return false;
        }

        public IEnumerable<PerformanceMixer.Target> MixerTargets()
        {
            yield return new PerformanceMixer.Target { id = "bend.amount", group = "Curved World", label = "Bend amount (all layers)", hint = "0 = flat, 1 = the preset's full bend. Ramp it for an intro or an outro", min = 0f, max = 1f, get = () => amount, set = v => amount = Mathf.Clamp01(v) };
            yield return new PerformanceMixer.Target { id = "bend.intensity", group = "Curved World", label = "Bend intensity", hint = "How hard the selected morph bends (1 = as tuned, up to 3x)", min = 0f, max = 3f, get = () => intensity, set = v => intensity = Mathf.Clamp(v, 0f, 3f) };
            yield return new PerformanceMixer.Target
            {
                id = "bend.preset", group = "Curved World", label = "Bend preset (0 off, 1 Little Planet, 2 Cylinder Tower, 3 Cylinder Rolloff, 4 Classic Runner, 5 Twisted Corridor, 6 Twisted Highway, 7 Ball)",
                hint = "Which bend; the settings come from the Curved World example scenes", min = 0f, max = Presets.Length - 1, get = () => preset, set = v => preset = Mathf.Clamp(Mathf.RoundToInt(v), 0, Presets.Length - 1),
            };
            yield return new PerformanceMixer.Target { id = "bend.spin", group = "Curved World", label = "Spin the picture (degrees)", hint = "Rolls the whole picture around its centre; 720 = two turns. Wind it up with the Ball preset for the spin into a ball", min = -1440f, max = 1440f, get = () => spin, set = v => spin = Mathf.Clamp(v, -1440f, 1440f) };
            foreach (var slot in StageLayers.Channels)
            {
                string name = slot.Name;
                string display = StageLayers.Display(name);
                yield return new PerformanceMixer.Target
                {
                    id = "bend." + name + ".on", group = "Curved World layers", label = display + " layer bends", hint = "Switch this stage layer's bend on or off", toggle = true, min = 0f, max = 1f,
                    get = () => layers.TryGetValue(name, out var l) ? (l.on ? 1f : 0f) : 1f, set = v => { if (layers.TryGetValue(name, out var l)) l.on = v >= 0.5f; },
                };
                yield return new PerformanceMixer.Target
                {
                    id = "bend." + name + ".amount", group = "Curved World layers", label = display + " layer bend amount", hint = "This layer's share of the master bend", min = 0f, max = 1f,
                    get = () => layers.TryGetValue(name, out var l) ? l.amount : 1f, set = v => { if (layers.TryGetValue(name, out var l)) l.amount = Mathf.Clamp01(v); },
                };
            }
        }
    }
}
