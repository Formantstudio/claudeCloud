using System.IO;
using UnityEditor;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Editor
{
    /// <summary>
    /// GameObject ▸ Geometry Engine: one menu item per engine component. Each creates the object
    /// under the current selection, with the shared GeometryEngine/Wire material (created on first
    /// use next to this folder), registers Undo, and selects it.
    /// </summary>
    static class GeometryEngineMenu
    {
        const string MaterialPath = "Assets/_GeometryWork/Engine/Materials/GeometryEngineWire.mat";

        [MenuItem("GameObject/Geometry Engine/Hopf Fibration", false, 10)]
        static void Hopf(MenuCommand c) => Create<HopfFibrationEngine>("Hopf Fibration", c);

        [MenuItem("GameObject/Geometry Engine/Strange Attractor", false, 11)]
        static void Attractor(MenuCommand c) => Create<AttractorEngine>("Strange Attractor", c);

        [MenuItem("GameObject/Geometry Engine/Seifert Surface", false, 12)]
        static void Seifert(MenuCommand c) => Create<SeifertSurfaceBuilder>("Seifert Surface", c);

        [MenuItem("GameObject/Geometry Engine/Dual Contour Surface", false, 13)]
        static void DualContour(MenuCommand c) => Create<DualContourEngine>("Dual Contour Surface", c);

        static void Create<T>(string name, MenuCommand command) where T : WireMeshComponent
        {
            var go = new GameObject(name);
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = WireMaterial();
            go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeObject = go;
        }

        static Material WireMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing) return existing;
            var shader = Shader.Find("GeometryEngine/Wire");
            if (!shader)
            {
                Debug.LogWarning("GeometryEngine/Wire shader not found (is URP installed?). Assign a wire material by hand.");
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var material = new Material(shader) { name = "GeometryEngineWire" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
