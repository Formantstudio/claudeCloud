using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>Deterministic multi-axis motion shared by each generated monster root.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GeometricMonsterMotion : MonoBehaviour
    {
        [Tooltip("Local revolutions per second around each axis.")]
        public Vector3 revolutionsPerSecond = new Vector3(.07f, .11f, .05f);
        public bool animate = true;
        Quaternion initialRotation;

        void OnEnable() => initialRotation = transform.localRotation;

        void Update()
        {
            if (!animate) return;
#if UNITY_EDITOR
            double clock = Application.isPlaying ? Time.timeAsDouble : UnityEditor.EditorApplication.timeSinceStartup;
#else
            double clock = Time.timeAsDouble;
#endif
            var degrees = revolutionsPerSecond * (float)(clock * 360.0);
            transform.localRotation = initialRotation * Quaternion.Euler(degrees);
        }
    }
}
