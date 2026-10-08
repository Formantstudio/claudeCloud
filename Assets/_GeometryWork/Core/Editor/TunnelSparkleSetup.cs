using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;
using PsychedelicLab.Control;
using PsychedelicLab.GeometryFX;
using AmazingAssets.CurvedWorld;

namespace PsychedelicLab.GeometryFX.EditorTools
{
    /// <summary>
    /// Click-to-apply hookups. Nothing runs on import; these are menu items so the scene only
    /// changes when you ask it to.
    /// </summary>
    static class TunnelSparkleSetup
    {
        const string Folder = "Assets/_GeometryWork/Core/";
        const string WireMaterial = Folder + "TunnelSparkleWire.mat";
        const string FieldCompute = Folder + "TunnelSdfField.compute";
        const string SparkleGraph = Folder + "SdfSparkleParticles.vfx";
        const string SwarmMaterial = Folder + "Dekeract_Particles.mat";
        const string SwarmPrefab = "Assets/polyhedronGenerator/prefabs/urp/wireframeParticle.prefab";

        // ---- tunnel ----------------------------------------------------------

        [MenuItem("Tools/Geometry FX/Sparkle the tunnel", false, 20)]
        static void SparkleTunnel()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WireMaterial);
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(FieldCompute);
            var graph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(SparkleGraph);
            if (!material || !compute || !graph)
            {
                Debug.LogError("Tunnel sparkle: missing one of " + WireMaterial + ", " + FieldCompute + ", " + SparkleGraph);
                return;
            }

            var targets = Targets();
            if (targets.Count == 0)
            {
                Debug.LogWarning("Tunnel sparkle: select the tunnel object(s), or open a scene that has a Tunnel_001.");
                return;
            }

            var scan = Object.FindFirstObjectByType<WorldGridScan>();
            var bridge = Object.FindFirstObjectByType<CurvedWorldBridge>();
            int added = 0, reused = 0;

            foreach (var go in targets)
            {
                var sparkle = go.GetComponent<TunnelSparkleParticles>();
                if (sparkle) reused++;
                else { sparkle = Undo.AddComponent<TunnelSparkleParticles>(go); added++; }

                Undo.RecordObject(sparkle, "Sparkle the tunnel");
                sparkle.wireMaterial = material;
                sparkle.tunnelField = compute;
                if (sparkle.particles == null) sparkle.particles = new SdfParticleSettings();
                sparkle.particles.graph = graph;
                if (!sparkle.worldGridScan) sparkle.worldGridScan = scan;
                if (!sparkle.bridge) sparkle.bridge = bridge;
                EditorUtility.SetDirty(sparkle);
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Tunnel sparkle: " + added + " added, " + reused + " already present. Save the scene to keep it.");
        }

        // ---- combo -----------------------------------------------------------

        [MenuItem("Tools/Geometry FX/Give the combo particle size control", false, 21)]
        static void UpgradeComboGraph()
        {
            var graph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(SparkleGraph);
            if (!graph) { Debug.LogError("Missing " + SparkleGraph); return; }

            var combos = Object.FindObjectsByType<GeometryParticleCombo>(FindObjectsSortMode.None);
            if (combos.Length == 0) { Debug.LogWarning("No GeometryParticleCombo in the open scene."); return; }

            foreach (var combo in combos)
            {
                Undo.RecordObject(combo, "Give the combo particle size control");
                if (combo.particleSettings == null) combo.particleSettings = new SdfParticleSettings();
                combo.particleSettings.graph = graph;
                combo.particleGraph = graph;
                EditorUtility.SetDirty(combo);
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Pointed " + combos.Length + " combo(s) at SdfSparkleParticles.vfx.");
        }

        // ---- chamber ---------------------------------------------------------

        [MenuItem("Tools/Geometry FX/Chamber: switch to fractal rings", false, 40)]
        static void ChamberFractalRings() => SetChamberMode(CurvedGeometryChamber.Mode.FractalRings);

        [MenuItem("Tools/Geometry FX/Chamber: switch to manifold shapes", false, 41)]
        static void ChamberManifold() => SetChamberMode(CurvedGeometryChamber.Mode.Manifold);

        [MenuItem("Tools/Geometry FX/Chamber: back to cylinder", false, 42)]
        static void ChamberCylinder() => SetChamberMode(CurvedGeometryChamber.Mode.Cylinder);

        static void SetChamberMode(CurvedGeometryChamber.Mode mode)
        {
            var chamber = Chamber();
            if (!chamber) { Debug.LogWarning("No CurvedGeometryChamber in the open scene."); return; }
            Undo.RecordObject(chamber, "Set chamber mode");
            chamber.mode = mode;
            EditorUtility.SetDirty(chamber);
            Selection.activeGameObject = chamber.gameObject;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Chamber mode: " + mode + ". Save the scene to keep it.");
        }

        [MenuItem("Tools/Geometry FX/Chamber: add the particle swarm layer", false, 43)]
        static void ChamberSwarm()
        {
            var chamber = Chamber();
            if (!chamber) { Debug.LogWarning("No CurvedGeometryChamber in the open scene."); return; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwarmPrefab);
            var material = AssetDatabase.LoadAssetAtPath<Material>(SwarmMaterial);
            if (!prefab || !material)
            {
                Debug.LogError("Swarm: missing " + SwarmPrefab + " or " + SwarmMaterial);
                return;
            }

            var host = chamber.gameObject;
            var swarm = host.GetComponent<WireParticleSwarm>();
            if (!swarm) swarm = Undo.AddComponent<WireParticleSwarm>(host);
            Undo.RecordObject(swarm, "Add wire particle swarm");
            swarm.particlePrefab = prefab;
            swarm.particleMaterial = material;
            swarm.chamber = chamber;
            EditorUtility.SetDirty(swarm);

            Selection.activeGameObject = host;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Particle swarm layer added to " + host.name + ". Save the scene to keep it.");
        }

        static CurvedGeometryChamber Chamber() => Object.FindFirstObjectByType<CurvedGeometryChamber>();

        const string Mats = "Assets/_GeometryWork/Materials/";

        // ---- 4-D activation layer -------------------------------------------

        [MenuItem("Tools/Geometry FX/4D/Add the 4D axis (activation layer)", false, 2)]
        static void Add4DAxis()
        {
            var existing = Object.FindFirstObjectByType<Hyperspace4DAxis>();
            if (existing)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("4D axis already in the scene; selected it.");
                return;
            }

            var host = new GameObject("Hyperspace 4D Axis");
            Undo.RegisterCreatedObjectUndo(host, "Add 4D axis");
            var axis = Undo.AddComponent<Hyperspace4DAxis>(host);
            Undo.RecordObject(axis, "Add 4D axis");
            axis.clock = Object.FindFirstObjectByType<MasterClock>();
            axis.kick = Object.FindFirstObjectByType<KickReactivity>();
            // Steady camera looking down the tunnel: spiral across the view, rock the two W
            // planes that actually show from there.
            var cam = Camera.main;
            axis.SetUpForView(cam ? cam.transform.forward : Vector3.forward);
            EditorUtility.SetDirty(axis);

            Selection.activeGameObject = host;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("4D axis added, set up for the camera's view direction. Activation starts at 0 — trigger it from a cue or the context menu.");
        }

        [MenuItem("Tools/Geometry FX/4D/Add a 4D manipulation ring", false, 3)]
        static void Add4DRing()
        {
            var axis = Object.FindFirstObjectByType<Hyperspace4DAxis>();
            if (!axis) { Add4DAxis(); axis = Object.FindFirstObjectByType<Hyperspace4DAxis>(); }

            var host = new GameObject("4D Manipulation Ring");
            Undo.RegisterCreatedObjectUndo(host, "Add 4D ring");
            var ring = Undo.AddComponent<Hyper4DRing>(host);
            Undo.RecordObject(ring, "Add 4D ring");
            ring.axis = axis;
            var chamber = Chamber();
            if (chamber)
            {
                host.transform.position = chamber.transform.position;
                ring.ringRadius = Mathf.Max(chamber.radius * 1.1f, 1f);
                ring.wrapLength = Mathf.Max(chamber.length, 10f);
            }
            EditorUtility.SetDirty(ring);

            Selection.activeGameObject = host;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("4D manipulation ring added. The bubbles open with the axis activation; everything outside them is untouched.");
        }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Mats + name + ".mat");

        // ---- implicit surfaces ----------------------------------------------

        [MenuItem("Tools/Geometry FX/Implicit/Gyroid", false, 80)]
        static void AddGyroid() => AddImplicit(ImplicitShape.Gyroid, "Wire_Gyroid_Teal", "Gyroid");

        [MenuItem("Tools/Geometry FX/Implicit/Schwarz P", false, 81)]
        static void AddSchwarz() => AddImplicit(ImplicitShape.SchwarzP, "Wire_Gyroid_Teal", "Schwarz P");

        [MenuItem("Tools/Geometry FX/Implicit/Neovius", false, 82)]
        static void AddNeovius() => AddImplicit(ImplicitShape.Neovius, "Wire_Gyroid_Teal", "Neovius");

        [MenuItem("Tools/Geometry FX/Implicit/Barth sextic", false, 83)]
        static void AddBarth() => AddImplicit(ImplicitShape.BarthSextic, "Wire_Barth_Rose", "Barth Sextic");

        [MenuItem("Tools/Geometry FX/Implicit/Mandelbulb", false, 84)]
        static void AddBulb() => AddImplicit(ImplicitShape.Mandelbulb, "Wire_Mandel_Ember", "Mandelbulb");

        [MenuItem("Tools/Geometry FX/Implicit/Mandelbox (rooms)", false, 85)]
        static void AddBox() => AddImplicit(ImplicitShape.Mandelbox, "Wire_Mandel_Ember", "Mandelbox Rooms");

        [MenuItem("Tools/Geometry FX/Implicit/Menger sponge", false, 86)]
        static void AddMenger() => AddImplicit(ImplicitShape.MengerSponge, "Wire_Menger_Ice", "Menger Sponge");

        [MenuItem("Tools/Geometry FX/Implicit/Sierpinski", false, 87)]
        static void AddSierp() => AddImplicit(ImplicitShape.Sierpinski, "Wire_Menger_Ice", "Sierpinski");

        static void AddImplicit(ImplicitShape shape, string material, string label)
        {
            var mat = Mat(material) ?? AssetDatabase.LoadAssetAtPath<Material>(WireMaterial);
            if (!mat) { Debug.LogError("Implicit: no wire material found under " + Mats); return; }

            var host = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(host, "Add " + label);
            var chamber = Undo.AddComponent<ImplicitSurfaceChamber>(host);
            Undo.RecordObject(chamber, "Add " + label);
            chamber.wireMaterial = mat;
            chamber.field.shape = shape;
            chamber.worldGridScan = Object.FindFirstObjectByType<WorldGridScan>();
            chamber.bridge = Object.FindFirstObjectByType<CurvedWorldBridge>();
            if (shape == ImplicitShape.Mandelbox) { chamber.field.boxScale = -1.75f; chamber.resolution = 56; }
            if (Implicits.IsPeriodic(shape)) chamber.field.thickness = .12f;
            EditorUtility.SetDirty(chamber);

            AttachSwarm(host, null);
            Selection.activeGameObject = host;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log(label + " added. Save the scene to keep it.");
        }

        // ---- combo ----------------------------------------------------------

        [MenuItem("Tools/Geometry FX/Combo/Klein + Helicoid + Fractal accordion", false, 100)]
        static void AddComboKHA() => AddCombo(ManifoldComboChamber.Preset.KleinHelicoidAccordion, "Klein Helicoid Accordion");

        [MenuItem("Tools/Geometry FX/Combo/Accordion nest", false, 101)]
        static void AddComboNest() => AddCombo(ManifoldComboChamber.Preset.AccordionNest, "Accordion Nest");

        [MenuItem("Tools/Geometry FX/Combo/Hyperbolic stack", false, 102)]
        static void AddComboHyper() => AddCombo(ManifoldComboChamber.Preset.HyperbolicStack, "Hyperbolic Stack");

        [MenuItem("Tools/Geometry FX/Combo/Supershape bloom", false, 103)]
        static void AddComboBloom() => AddCombo(ManifoldComboChamber.Preset.SupershapeBloom, "Supershape Bloom");

        [MenuItem("Tools/Geometry FX/Combo/Knot cage", false, 104)]
        static void AddComboKnot() => AddCombo(ManifoldComboChamber.Preset.KnotCage, "Knot Cage");

        [MenuItem("Tools/Geometry FX/Combo/Roman to Boy's", false, 105)]
        static void AddComboRB() => AddCombo(ManifoldComboChamber.Preset.RomanBoysPair, "Roman To Boys");

        [MenuItem("Tools/Geometry FX/Combo/Minimal surfaces", false, 106)]
        static void AddComboMin() => AddCombo(ManifoldComboChamber.Preset.MinimalSurfaces, "Minimal Surfaces");

        [MenuItem("Tools/Geometry FX/Combo/Nested tori", false, 107)]
        static void AddComboTori() => AddCombo(ManifoldComboChamber.Preset.NestedTori, "Nested Tori");

        static void AddCombo(ManifoldComboChamber.Preset preset, string label)
        {
            var mat = Mat("Wire_Chamber_Cyan") ?? AssetDatabase.LoadAssetAtPath<Material>(WireMaterial);
            if (!mat) { Debug.LogError("Combo: no wire material found under " + Mats); return; }

            var host = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(host, "Add " + label);
            var combo = Undo.AddComponent<ManifoldComboChamber>(host);
            Undo.RecordObject(combo, "Add " + label);
            combo.wireMaterial = mat;
            combo.applyPreset = preset;
            combo.worldGridScan = Object.FindFirstObjectByType<WorldGridScan>();
            combo.bridge = Object.FindFirstObjectByType<CurvedWorldBridge>();
            EditorUtility.SetDirty(combo);

            AttachSwarm(host, combo);
            Selection.activeGameObject = host;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log(label + " combo added. Save the scene to keep it.");
        }

        /// <summary>Adds the particle layer, picking a particle material to match.</summary>
        static void AttachSwarm(GameObject host, ManifoldComboChamber combo)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwarmPrefab);
            var material = Mat("Particles_Cyan") ?? AssetDatabase.LoadAssetAtPath<Material>(SwarmMaterial);
            if (!prefab || !material) return;
            if (host.GetComponent<WireParticleSwarm>()) return;

            var swarm = Undo.AddComponent<WireParticleSwarm>(host);
            Undo.RecordObject(swarm, "Attach swarm");
            swarm.particlePrefab = prefab;
            swarm.particleMaterial = material;
            swarm.comboChamber = combo;
            EditorUtility.SetDirty(swarm);
        }

        // ---- swarms ----------------------------------------------------------

        [MenuItem("Tools/Geometry FX/Swarm/Metatron's Cube", false, 60)]
        static void AddMetatron() => AddSwarm<MetatronCubeSwarm>("Metatron's Cube");

        [MenuItem("Tools/Geometry FX/Swarm/Tesseract (4D)", false, 61)]
        static void AddTesseract() => AddSwarm<TesseractSwarm>("Tesseract");

        [MenuItem("Tools/Geometry FX/Swarm/Penteract (5D)", false, 62)]
        static void AddPenteract() => AddSwarm<PenteractSwarm>("Penteract");

        [MenuItem("Tools/Geometry FX/Swarm/Hexeract (6D)", false, 63)]
        static void AddHexeract() => AddSwarm<HexeractSwarm>("Hexeract");

        [MenuItem("Tools/Geometry FX/Swarm/Hepteract (7D)", false, 64)]
        static void AddHepteract() => AddSwarm<HepteractSwarm>("Hepteract");

        [MenuItem("Tools/Geometry FX/Swarm/Octeract (8D)", false, 65)]
        static void AddOcteract() => AddSwarm<OcteractSwarm>("Octeract");

        [MenuItem("Tools/Geometry FX/Swarm/Enneract (9D)", false, 66)]
        static void AddEnneract() => AddSwarm<EnneractSwarm>("Enneract");

        [MenuItem("Tools/Geometry FX/Swarm/Dekeract (10D)", false, 67)]
        static void AddDekeract() => AddSwarm<DekeractSwarm>("Dekeract");

        [MenuItem("Tools/Geometry FX/Swarm/Polyhedron (Platonic, prism, Johnson)", false, 68)]
        static void AddPolyhedron() => AddSwarm<PolyhedronSwarm>("Polyhedron");

        /// <summary>Drops a swarm on a new object with the prefab and material already assigned.</summary>
        static void AddSwarm<T>(string label) where T : NodeEdgeSwarmBase
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwarmPrefab);
            var material = AssetDatabase.LoadAssetAtPath<Material>(SwarmMaterial);
            if (!prefab || !material)
            {
                Debug.LogError("Swarm: missing " + SwarmPrefab + " or " + SwarmMaterial);
                return;
            }

            var host = new GameObject(label + " Swarm");
            Undo.RegisterCreatedObjectUndo(host, "Add " + label + " swarm");
            var swarm = Undo.AddComponent<T>(host);
            Undo.RecordObject(swarm, "Add " + label + " swarm");
            swarm.particlePrefab = prefab;
            swarm.particleMaterial = material;
            EditorUtility.SetDirty(swarm);

            Selection.activeGameObject = host;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log(label + " swarm added. Save the scene to keep it.");
        }

        static List<GameObject> Targets()
        {
            var list = new List<GameObject>();
            foreach (var go in Selection.gameObjects)
                if (go.GetComponent<MeshRenderer>()) list.Add(go);
            if (list.Count > 0) return list;

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (renderer.gameObject.name.StartsWith("Tunnel_")) list.Add(renderer.gameObject);
            return list;
        }
    }
}
