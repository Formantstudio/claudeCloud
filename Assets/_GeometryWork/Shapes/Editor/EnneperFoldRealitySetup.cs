using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX.Editor
{
    public static class EnneperFoldRealitySetup
    {
        [MenuItem("Tools/Geometry FX/Enneper/Add Escher Fold Variations (Disabled)")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            ShapeArsenal arsenal = null;
            WorldGridScan scan = null;
            CurvedWorldBridge bridge = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!arsenal) arsenal = root.GetComponentInChildren<ShapeArsenal>(true);
                if (!scan) scan = root.GetComponentInChildren<WorldGridScan>(true);
                if (!bridge) bridge = root.GetComponentInChildren<CurvedWorldBridge>(true);
            }
            if (!arsenal)
            {
                var root = new GameObject("ENNEPER FOLD REALITY");
                Undo.RegisterCreatedObjectUndo(root, "Add Enneper folds");
                arsenal = root.AddComponent<ShapeArsenal>();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_GeometryWork/Materials/Wire_Manifold_Teal.mat");
            if (!material) throw new System.InvalidOperationException("Missing Enneper wire material.");
            string folder = "Assets/_GeometryWork/Shapes/Presets";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_GeometryWork/Shapes", "Presets");
            var entries = new List<GameObject>(arsenal.shapes);
            foreach (EnneperFoldReality.Arrangement arrangement in System.Enum.GetValues(typeof(EnneperFoldReality.Arrangement)))
            {
                string name = "Enneper Reality - " + arrangement;
                if (entries.Exists(go => go && go.name == name)) continue;
                var go = new GameObject(name); go.SetActive(false); go.transform.SetParent(arsenal.transform, false);
                Undo.RegisterCreatedObjectUndo(go, "Add Enneper folds");
                var folds = go.AddComponent<EnneperFoldReality>();
                folds.arrangement = arrangement; folds.foldMaterial = material;
                folds.worldGridScan = scan; folds.bridge = bridge;
                folds.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_GeometryWork/Materials/Particles_Ice.mat");
                folds.particlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/polyhedronGenerator/prefabs/urp/wireframeParticle.prefab");
                folds.FrameSingleFold();
                // Prefab references only persistent assets; scene links belong to the scene instance.
                folds.worldGridScan = null; folds.bridge = null;
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + name + ".prefab"))
                    PrefabUtility.SaveAsPrefabAsset(go, folder + "/" + name + ".prefab");
                folds.worldGridScan = scan; folds.bridge = bridge;
                entries.Add(go);
            }
            Undo.RecordObject(arsenal, "Register Enneper variations");
            arsenal.shapes = entries.ToArray(); EditorUtility.SetDirty(arsenal);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = arsenal.gameObject;
            Debug.Log("Enneper Reality variations added disabled. Select them from the Shape Arsenal inspector.");
        }
    }
}
