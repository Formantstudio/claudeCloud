using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using PsychedelicLab.Control;
using PsychedelicLab.Shapes4D;

namespace PsychedelicLab.GeometryFX.Editor
{
    public static class ShapeArsenalSetup
    {
        const string ScenePath = "Assets/Scenes/ProceduralTunnelTester-2.unity";
        const string RootName = "SHAPE ARSENAL - 4D and 3D";
        const string Folder4D = "Assets/_4DManagementSystem/Shapes/Presets";
        const string Folder3D = "Assets/_GeometryWork/Shapes/Presets";
        static GameObject particles;
        static Material particleMat;
        static WorldGridScan scan;
        static CurvedWorldBridge bridge;

        [MenuItem("Tools/Geometry FX/Add All Shapes to Scene (Disabled)")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open ProceduralTunnelTester-2 outside Play mode.");
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == RootName) { Selection.activeGameObject = r; return; }
            particles = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/polyhedronGenerator/prefabs/urp/wireframeParticle.prefab");
            particleMat = Mat("Particles_Ice");
            if (!particles || !particleMat || !Mat("Wire_Manifold_Teal")) throw new InvalidOperationException("Missing geometry library materials or particle prefab.");
            foreach (var r in scene.GetRootGameObjects())
            {
                if (!scan) scan = r.GetComponentInChildren<WorldGridScan>(true);
                if (!bridge) bridge = r.GetComponentInChildren<CurvedWorldBridge>(true);
            }
            Folder(Folder4D); Folder(Folder3D);
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add shape arsenal");
            var root = new GameObject(RootName); root.SetActive(false);
            Undo.RegisterCreatedObjectUndo(root, "Add shape arsenal");
            var catalog = root.AddComponent<ShapeArsenal>();
            var entries = new List<GameObject>();
            GameObject Add(string name, bool fourD, Action<GameObject> configure)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform, false);
                configure(go);
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, (fourD ? Folder4D : Folder3D) + "/" + name + ".prefab");
                if (!prefab) throw new InvalidOperationException("Could not save shape prefab " + name);
                entries.Add(go); return go;
            }
            Add("4D - Duocylinder Crystal Boundary", true, go => {
                var s = go.AddComponent<Duocylinder4DSwarm>(); Configure(s, 0); s.particleSolid = NodeEdgeSwarmBase.ParticleSolid.Cube;
                s.circleSegments = 20; s.radialLayers = 3; s.radius = 2.5f;
            });
            Add("4D - Hopf Linked Orbit Swarm", true, go => {
                var s = go.AddComponent<HopfFiber4DSwarm>(); Configure(s, 1); s.particleSolid = NodeEdgeSwarmBase.ParticleSolid.Octahedron;
                s.fibers = 36; s.samplesPerFiber = 64; s.radius = 2.5f;
            });
            Add("4D - Clifford Mandelbulb Crust", true, go => {
                var s = go.AddComponent<CliffordMandelbulbSwarm>(); Configure(s, 2); s.radius = 2.2f;
                s.particleSolid = NodeEdgeSwarmBase.ParticleSolid.Icosahedron;
            });
            foreach (var cell in new[] { Polytope4DSwarm.Polytope.Cell120, Polytope4DSwarm.Polytope.Cell600 })
                Add("4D - " + cell + " Polycrystal", true, go => {
                    var s = go.AddComponent<Polytope4DSwarm>(); Configure(s, 3); s.polytope = cell; s.radius = 2.3f;
                    s.particleSolid = cell == Polytope4DSwarm.Polytope.Cell120 ? NodeEdgeSwarmBase.ParticleSolid.Dodecahedron : NodeEdgeSwarmBase.ParticleSolid.Tetrahedron;
                });
            var surfaces = new[] { ManifoldSurface.TorusKnotTube, ManifoldSurface.TorusKnotTube,
                ManifoldSurface.Supershape, ManifoldSurface.Supershape, ManifoldSurface.TrefoilRibbon,
                ManifoldSurface.FigureEightTorus, ManifoldSurface.Enneper, ManifoldSurface.DinisSurface,
                ManifoldSurface.BreatherSurface, ManifoldSurface.KleinBottle, ManifoldSurface.ConicalSpiral,
                ManifoldSurface.TwistedTorus };
            var names = new[] { "Trefoil Particle Reactor", "Five Seven Knot Engine", "Seven Lobe Star Crystal",
                "Nine Lobe Thorn Bloom", "Trefoil Ribbon Crown", "Figure Eight Nexus", "Enneper Fold Flower",
                "Dini Spiral Drill", "Breather Wave Cathedral", "Klein Bottle Inversion", "Conical Spiral Shell", "Twisted Torus Furnace" };
            for (int i = 0; i < surfaces.Length; i++)
            {
                int n = i;
                Add("3D - " + names[n], false, go => {
                    var c = go.AddComponent<CurvedGeometryChamber>(); c.mode = CurvedGeometryChamber.Mode.Manifold;
                    c.from = c.to = surfaces[n]; c.autoCycle = false; c.animate = false; c.sides = 64; c.rings = 48;
                    c.chamberMaterial = Mat(n % 2 == 0 ? "Wire_Knot_Lime" : "Wire_Manifold_Teal");
                    c.worldGridScan = scan; c.bridge = bridge; c.wireOpacity = .5f; c.manifoldSpin = .035f;
                    c.shape.radius = 1.4f; c.shape.minorRadius = .5f; c.shape.extent = 3;
                    c.shape.tubeRadius = .18f; c.shape.bandWidth = .6f; c.shape.pitch = .55f;
                    c.shape.turns = 3; c.shape.knotP = n == 1 ? 5 : 2; c.shape.knotQ = n == 1 ? 7 : 3;
                    c.shape.superM = n == 3 ? new Vector2(9, 6) : new Vector2(7, 5);
                    c.shape.superN = new Vector3(.55f, 1.7f, 1.7f);
                    var s = go.AddComponent<WireParticleSwarm>(); Wire(s, n); s.chamber = c;
                });
            }
            foreach (var field in new[] { ImplicitShape.Gyroid, ImplicitShape.SchwarzD, ImplicitShape.BarthSextic, ImplicitShape.Mandelbox })
                Add("3D - " + field + " Particle Vault", false, go => {
                    var c = go.AddComponent<ImplicitSurfaceChamber>(); c.field.shape = field; c.resolution = 32;
                    c.extent = field == ImplicitShape.Mandelbox ? 2.5f : 1.8f;
                    c.field.frequency = 2.3f; c.field.iterations = 7; c.animate = false;
                    if (field == ImplicitShape.Mandelbox) c.field.level = .03f;
                    c.wireMaterial = Mat("Wire_Gyroid_Teal"); c.worldGridScan = scan; c.bridge = bridge;
                    Wire(go.AddComponent<WireParticleSwarm>(), (int)field);
                });
            for (int i = 0; i < 4; i++)
            {
                int n = i;
                Add("3D - Polyhedral Cluster " + (n + 1), false, go => {
                    var s = go.AddComponent<PolyhedronSwarm>(); Configure(s, n);
                    s.solid = n == 0 ? PolyhedronSwarm.Solid.Icosahedron : n == 1 ? PolyhedronSwarm.Solid.Octahedron : PolyhedronSwarm.Solid.Johnson;
                    s.johnsonIndex = n == 2 ? 22 : 71; s.radius = 2;
                    s.particleSolid = (NodeEdgeSwarmBase.ParticleSolid)(n + 1);
                    s.twist = n % 2 == 0 ? .8f : -.7f; s.vortex = .35f; s.clusterSpread = .1f;
                });
            }
            var fieldShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/_4DManagementSystem/Shapes/ArsenalFields.compute");
            var marchingShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Scenes/Keijiro/ComputeMarchingCubes/MarchingCubes.compute");
            if (!fieldShader || !marchingShader) throw new InvalidOperationException("Missing marching-cubes field assets.");
            foreach (ArsenalFieldSurface.Form form in Enum.GetValues(typeof(ArsenalFieldSurface.Form)))
            {
                bool is4D = form == ArsenalFieldSurface.Form.CliffordTorusShell4D;
                Add((is4D ? "4D" : "3D") + " - Marching " + form, is4D, go => {
                    var surface = go.AddComponent<ArsenalFieldSurface>(); surface.form = form;
                    surface.fieldShader = fieldShader; surface.marchingShader = marchingShader;
                    go.GetComponent<MeshRenderer>().sharedMaterial = SurfaceMaterial((int)form, is4D);
                    var corona = new GameObject("Polyhedral particle corona"); corona.transform.SetParent(go.transform, false);
                    var s = corona.AddComponent<PolyhedronSwarm>(); Configure(s, (int)form);
                    s.solid = PolyhedronSwarm.Solid.Icosahedron; s.radius = 1.8f; s.targetParticles = 4500;
                    s.maxParticles = 6000; s.particleSolid = NodeEdgeSwarmBase.ParticleSolid.Cube;
                    s.edgeParticleSize = .01f; s.vertexParticleSize = .025f;
                });
            }
            catalog.shapes = entries.ToArray(); catalog.selectedShape = 0; catalog.Apply(); root.SetActive(true);
            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Shape arsenal installed: 6 four-dimensional and 27 three-dimensional combinations. All start disabled.", root);
        }
        static void Configure(NodeEdgeSwarmBase s, int palette)
        {
            s.particlePrefab = particles; s.particleMaterial = particleMat; s.targetParticles = 14000; s.maxParticles = 20000;
            s.vertexParticleSize = .04f; s.edgeParticleSize = .014f; s.assembleOnPlay = false;
            s.cyan = Color.HSVToRGB((palette * .19f + .48f) % 1, .65f, 1);
            s.gold = Color.Lerp(s.cyan, new Color(1, .65f, .2f), .65f);
        }
        static void Wire(WireParticleSwarm s, int palette)
        {
            s.particlePrefab = particles; s.particleMaterial = particleMat; s.nodesU = 40; s.nodesV = 32;
            s.maxParticles = 12000; s.fractalLevels = 1; s.nodeSize = .026f; s.travellerSize = .012f;
            s.assembleOnPlay = false; s.cyan = Color.HSVToRGB((.48f + palette * .13f) % 1, .7f, 1);
        }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/_GeometryWork/Materials/" + name + ".mat");
        static Material SurfaceMaterial(int index, bool fourD)
        {
            string folder = fourD ? "Assets/_4DManagementSystem/Shapes/Materials" : "Assets/_GeometryWork/Shapes/Materials";
            Folder(folder); string path = folder + "/MarchingSurface" + index + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is not available.");
            material = new Material(shader) { name = "Marching surface " + index, enableInstancing = true };
            Color color = Color.HSVToRGB((.48f + .11f * index) % 1, .55f, .72f);
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", .4f); material.SetFloat("_Smoothness", .58f);
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .2f);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
