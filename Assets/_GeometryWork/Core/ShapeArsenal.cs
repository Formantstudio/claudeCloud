using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ShapeArsenal : MonoBehaviour
    {
        public GameObject[] shapes = new GameObject[0];
        [Min(0)] public int selectedShape;
        public bool showSelectedShape;
        int appliedSelection = -1;
        bool appliedVisible;
        public string CurrentShape => shapes.Length == 0 ? "Empty" : shapes[Mathf.Clamp(selectedShape, 0, shapes.Length - 1)]?.name;
        void OnEnable() => Apply();
        void Update() => Apply();
        public void Apply()
        {
            if (shapes.Length == 0) return;
            selectedShape = Mathf.Clamp(selectedShape, 0, shapes.Length - 1);
            if (appliedSelection == selectedShape && appliedVisible == showSelectedShape) return;
            appliedSelection = selectedShape; appliedVisible = showSelectedShape;
            for (int i = 0; i < shapes.Length; i++)
                if (shapes[i]) shapes[i].SetActive(showSelectedShape && i == selectedShape);
        }
        [ContextMenu("Turn all catalog shapes off")] public void HideAll() { showSelectedShape = false; appliedSelection = -1; Apply(); }
        [ContextMenu("Next shape")] public void Next() { if (shapes.Length > 0) selectedShape = (selectedShape + 1) % shapes.Length; Apply(); }
        [ContextMenu("Previous shape")] public void Previous() { if (shapes.Length > 0) selectedShape = (selectedShape + shapes.Length - 1) % shapes.Length; Apply(); }
    }
}
