using UnityEditor;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Editor
{
    [CustomEditor(typeof(ShapeArsenal))]
    public sealed class ShapeArsenalEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var catalog = (ShapeArsenal)target;
            EditorGUILayout.LabelField("GEOMETRY ARSENAL", EditorStyles.boldLabel);
            var labels = new string[catalog.shapes.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = catalog.shapes[i] ? catalog.shapes[i].name : "Missing";
            if (labels.Length == 0) { DrawDefaultInspector(); return; }
            EditorGUI.BeginChangeCheck();
            bool visible = EditorGUILayout.Toggle("Show selected shape", catalog.showSelectedShape);
            int next = EditorGUILayout.Popup("Shape", catalog.selectedShape, labels);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous")) next = (catalog.selectedShape + labels.Length - 1) % labels.Length;
                if (GUILayout.Button("Next")) next = (catalog.selectedShape + 1) % labels.Length;
            }
            if (EditorGUI.EndChangeCheck() || next != catalog.selectedShape)
            {
                Undo.RecordObject(catalog, "Select geometric shape");
                foreach (var go in catalog.shapes) if (go) Undo.RecordObject(go, "Show geometric shape");
                catalog.selectedShape = next; catalog.showSelectedShape = visible; catalog.Apply(); EditorUtility.SetDirty(catalog);
            }
            if (GUILayout.Button("Select active shape controls")) Selection.activeGameObject = catalog.shapes[catalog.selectedShape];
            if (GUILayout.Button("Turn ALL catalog shapes off")) { catalog.HideAll(); EditorUtility.SetDirty(catalog); }
            EditorGUILayout.HelpBox(labels.Length + " combinations. All start disabled. Enable the selected shape here, or enable individual hierarchy objects manually. Each also has a reusable prefab.", MessageType.Info);
        }
    }
}
