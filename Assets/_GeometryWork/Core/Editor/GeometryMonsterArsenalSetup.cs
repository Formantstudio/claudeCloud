using System;
using System.Collections.Generic;
using System.IO;
using NoiseBall;
using polyhedronGenerator.scripts;
using PsychedelicLab.GeometryFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PsychedelicLab.GeometryFX.Editor
{
    /// <summary>Creates a small set of layered geometric specimens in the existing working scene.</summary>
    public static class GeometryMonsterArsenalSetup
    {
        const string ScenePath = "Assets/Scenes/ProceduralTunnelTester-2.unity";
        const string RootName = "GEOMETRIC MONSTERS";
        const string AssetFolder = "Assets/_GeometryWork/MonsterMeshes";
        const string MaterialFolder = "Assets/_GeometryWork/Materials";
        const string ParticlePrefabPath = "Assets/polyhedronGenerator/prefabs/urp/wireframeParticle.prefab";
        const string ParticleMaterialPath = "Assets/_GeometryWork/Core/Dekeract_Particles.mat";
        const string NoiseComputePath = "Packages/jp.keijiro.noiseball6/NoiseBall.compute";
        const string MarchingPrefabPath = "Assets/_TheCrazyShapes/Keijiro_ComputeMarchingCubes_Sample.prefab";
        const string MarchingFieldPath = "Assets/_GeometryWork/Core/MonsterIsoField.compute";

        sealed class Recipe
        {
            public string name;
            public Vector3 position;
            public Color color;
            public PolyhedronBase outerBase, innerBase;
            public PolyhedronSwarm.Solid swarmBase;
            public Operation[] outerOps, innerOps;
            public Vector3 spin;
            public float noiseAmplitude, noiseFrequency;
            public int seed;
        }

        [MenuItem("Tools/Geometry FX/Build Monster Arsenal")]
        public static void BuildFromMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning("Open Assets/Scenes/ProceduralTunnelTester-2.unity, then run Build Monster Arsenal.");
                return;
            }
            BuildInScene(scene);
        }

        // Dedicated entry point for Unity batch mode; opens only the requested working scene.
        public static void BuildForBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildInScene(scene);
            EditorApplication.Exit(0);
        }

        static void BuildInScene(Scene scene)
        {
            var existing = FindRoot(scene);
            if (existing)
            {
                Selection.activeGameObject = existing;
                Debug.Log("GEOMETRIC MONSTERS already exists. Existing scene setup and edits were preserved.", existing);
                return;
            }

            EnsureFolder(AssetFolder);
            EnsureFolder(MaterialFolder);
            var noiseCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(NoiseComputePath);
            var particlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParticlePrefabPath);
            var particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            var marchingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarchingPrefabPath);
            var marchingField = AssetDatabase.LoadAssetAtPath<ComputeShader>(MarchingFieldPath);
            if (!noiseCompute || !particlePrefab || !particleMaterial || !marchingPrefab || !marchingField)
            {
                Debug.LogError("Monster setup needs NoiseBall, ComputeMarchingCubes, wireframeParticle and Dekeract particle assets. No scene objects were created.");
                return;
            }

            var sceneParent = GameObject.Find("Shape Swarms") ?? GameObject.Find("Geometry Particle Combo");
            if (!sceneParent)
            {
                Debug.LogError("Could not find Shape Swarms or Geometry Particle Combo in the working scene. No objects were created.");
                return;
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build geometric monster arsenal");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(sceneParent.transform, false);

            var recipes = new[]
            {
                new Recipe {
                    name = "01 — RADIANT KIS CROWN", position = new Vector3(-5.2f, -1.5f, -1f),
                    color = new Color(.08f, .82f, 1f), outerBase = PolyhedronBase.Icosahedron,
                    innerBase = PolyhedronBase.Octahedron, swarmBase = PolyhedronSwarm.Solid.Dodecahedron,
                    outerOps = new[] { new Operation { op = Operations.Kis, amount = .65f } },
                    innerOps = new[] { new Operation { op = Operations.Dual } },
                    spin = new Vector3(.05f, .12f, .025f), noiseAmplitude = 1.05f, noiseFrequency = 2.6f, seed = 17
                },
                new Recipe {
                    name = "02 — GYRO FRACTAL REACTOR", position = new Vector3(0, 1.3f, 1f),
                    color = new Color(1f, .22f, .58f), outerBase = PolyhedronBase.Dodecahedron,
                    innerBase = PolyhedronBase.Icosahedron, swarmBase = PolyhedronSwarm.Solid.Icosahedron,
                    outerOps = new[] { new Operation { op = Operations.Gyro, amount = .32f } },
                    innerOps = new[] { new Operation { op = Operations.Truncate, amount = .28f }, new Operation { op = Operations.Kis, amount = .4f } },
                    spin = new Vector3(.11f, -.045f, .08f), noiseAmplitude = 1.3f, noiseFrequency = 3.1f, seed = 41
                },
                new Recipe {
                    name = "03 — WHIRL POLYCRYSTAL", position = new Vector3(5.2f, -1.5f, -1f),
                    color = new Color(1f, .57f, .12f), outerBase = PolyhedronBase.Cube,
                    innerBase = PolyhedronBase.Dodecahedron, swarmBase = PolyhedronSwarm.Solid.Johnson,
                    outerOps = new[] { new Operation { op = Operations.Whirl, amount = .5f } },
                    innerOps = new[] { new Operation { op = Operations.Quinto, amount = .3f } },
                    spin = new Vector3(-.04f, .075f, .13f), noiseAmplitude = .9f, noiseFrequency = 4.0f, seed = 73
                }
            };

            for (int i = 0; i < recipes.Length; i++)
                BuildRecipe(root.transform, recipes[i], i, noiseCompute, particlePrefab, particleMaterial);

            BuildMarchingMonster(root.transform, marchingPrefab, marchingField, particlePrefab, particleMaterial);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("Built three animated NoiseBall/polyhedron/GeometryFX combinations and one ComputeMarchingCubes fractal reactor in ProceduralTunnelTester-2.", root);
        }

        static void BuildMarchingMonster(Transform parent, GameObject samplePrefab, ComputeShader field,
                                         GameObject particlePrefab, Material particleMaterial)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(samplePrefab, parent.gameObject.scene);
            go.name = "04 — MORPHOSPHERE / GPU ISOSURFACE";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, -4.2f, -2f);
            go.transform.localScale = Vector3.one * .86f;
            var motion = go.GetComponent<GeometricMonsterMotion>() ?? go.AddComponent<GeometricMonsterMotion>();
            motion.revolutionsPerSecond = new Vector3(.045f, -.08f, .035f);

            var visualizer = go.GetComponent<MarchingCubes.NoiseFieldVisualizer>();
            if (!visualizer)
            {
                Debug.LogError("ComputeMarchingCubes sample prefab is missing NoiseFieldVisualizer.", go);
                return;
            }
            var settings = new SerializedObject(visualizer);
            settings.FindProperty("_dimensions").vector3IntValue = new Vector3Int(56, 56, 56);
            settings.FindProperty("_gridScale").floatValue = .06f;
            settings.FindProperty("_triangleBudget").intValue = 48000;
            settings.FindProperty("_targetValue").floatValue = 0f;
            settings.FindProperty("_volumeCompute").objectReferenceValue = field;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer) renderer.sharedMaterial = CreateMonsterMaterial("Monster_4_Isosurface", new Color(.47f, .27f, 1f));

            CreatePolyhedronLayer(go.transform, "Tetrahedral spike cage", PolyhedronBase.Icosahedron,
                new[] { new Operation { op = Operations.Kis, amount = .48f }, new Operation { op = Operations.Dual } },
                CreateMonsterMaterial("Monster_4_Cage", new Color(.16f, .92f, .79f)), Vector3.one * 1.12f,
                AssetFolder + "/Monster_4_Cage.asset");

            var swarm = go.AddComponent<PolyhedronSwarm>();
            swarm.particlePrefab = particlePrefab;
            swarm.particleMaterial = particleMaterial;
            swarm.solid = PolyhedronSwarm.Solid.Icosahedron;
            swarm.radius = 1.5f;
            swarm.targetParticles = 10500;
            swarm.maxParticles = 16000;
            swarm.fractalLevels = 1;
            swarm.clusterSpread = .1f;
            swarm.twist = 1.1f;
            swarm.vortex = .8f;
            swarm.vertexParticleSize = .04f;
            swarm.edgeParticleSize = .009f;
            swarm.previewInEditor = true;
        }

        static void BuildRecipe(Transform parent, Recipe recipe, int index, ComputeShader compute,
                                GameObject particlePrefab, Material particleMaterial)
        {
            var specimen = new GameObject(recipe.name);
            Undo.RegisterCreatedObjectUndo(specimen, "Create " + recipe.name);
            specimen.transform.SetParent(parent, false);
            specimen.transform.localPosition = recipe.position;
            specimen.transform.localScale = Vector3.one * 1.15f;
            specimen.AddComponent<GeometricMonsterMotion>().revolutionsPerSecond = recipe.spin;

            var material = CreateMonsterMaterial("Monster_" + (index + 1), recipe.color);
            var noise = new GameObject("GPU NoiseBall — living core");
            noise.transform.SetParent(specimen.transform, false);
            noise.transform.localScale = Vector3.one * .82f;
            var filter = noise.AddComponent<MeshFilter>();
            var renderer = noise.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var noiseBall = noise.AddComponent<NoiseBallController>();
            noiseBall.parameters = new NoiseBallParameters {
                TriangleCount = 24000,
                TriangleExtent = .29f,
                NoiseFrequency = recipe.noiseFrequency,
                NoiseAmplitude = recipe.noiseAmplitude,
                NoiseAnimation = new Vector3(.07f, .13f, .23f)
            };
            var noiseSerialized = new SerializedObject(noiseBall);
            noiseSerialized.FindProperty("_compute").objectReferenceValue = compute;
            noiseSerialized.ApplyModifiedPropertiesWithoutUndo();

            CreatePolyhedronLayer(specimen.transform, "Kinetic outer lattice", recipe.outerBase,
                recipe.outerOps, material, Vector3.one * 1.85f, AssetFolder + "/Monster_" + (index + 1) + "_Outer.asset");
            CreatePolyhedronLayer(specimen.transform, "Nested razor shell", recipe.innerBase,
                recipe.innerOps, material, Vector3.one * 1.28f, AssetFolder + "/Monster_" + (index + 1) + "_Inner.asset");

            var swarm = specimen.AddComponent<PolyhedronSwarm>();
            swarm.particlePrefab = particlePrefab;
            swarm.particleMaterial = particleMaterial;
            swarm.solid = recipe.swarmBase;
            swarm.johnsonIndex = index == 2 ? 22 : 0;
            swarm.radius = 1.72f;
            swarm.targetParticles = 9000;
            swarm.maxParticles = 14000;
            swarm.fractalLevels = 1;
            swarm.clusterSpread = .11f;
            swarm.clusterRatio = .38f;
            swarm.twist = index == 1 ? 1.4f : .8f;
            swarm.vortex = index == 2 ? 1.15f : .5f;
            swarm.inversion = index == 1 ? .22f : .08f;
            swarm.swirlSpin = recipe.spin.y;
            swarm.cyan = recipe.color;
            swarm.gold = Color.Lerp(recipe.color, Color.white, .55f);
            swarm.previewInEditor = true;
            EditorUtility.SetDirty(specimen);
        }

        static void CreatePolyhedronLayer(Transform parent, string objectName, PolyhedronBase baseShape,
                                          Operation[] operations, Material material, Vector3 scale, string assetPath)
        {
            var shape = new GameObject(objectName);
            shape.transform.SetParent(parent, false);
            shape.transform.localScale = scale;
            shape.transform.localRotation = Quaternion.Euler(17f, 29f, 11f);
            var filter = shape.AddComponent<MeshFilter>();
            var renderer = shape.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var generator = shape.AddComponent<PolyhedronGenerator>();
            generator.liveUpdate = false;
            generator.polyhedronBaseForm = baseShape;
            generator.radius = 1;
            generator.operations = new List<Operation>(operations);
            generator.doubleSided = true;
            try
            {
                generator.generate();
                var generated = filter.sharedMesh;
                if (!generated) throw new InvalidOperationException("Generator returned no mesh.");
                generated.name = Path.GetFileNameWithoutExtension(assetPath);
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (existing)
                {
                    EditorUtility.CopySerialized(generated, existing);
                    existing.name = generated.name;
                    filter.sharedMesh = existing;
                    UnityEngine.Object.DestroyImmediate(generated);
                }
                else AssetDatabase.CreateAsset(generated, assetPath);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not generate " + objectName + ": " + e.Message, shape);
            }
            UnityEngine.Object.DestroyImmediate(generator);
        }

        static Material CreateMonsterMaterial(string name, Color color)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                material = new Material(shader) { name = name, enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.5f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject FindRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindChild(root.transform, RootName);
                if (found) return found.gameObject;
            }
            return null;
        }

        static Transform FindChild(Transform current, string name)
        {
            if (current.name == name) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                var found = FindChild(current.GetChild(i), name);
                if (found) return found;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
