using System;
using System.Collections.Generic;
using UnityEngine;
using AmazingAssets.CurvedWorld;

namespace PsychedelicLab.Control
{
    // The Curved World counterpart of UserCameraPositionManager: a numbered lineup of bend states (slots), chosen one at a time.
    // Each slot is a complete bend setting: which Curved World preset, how much of it, how hard, and the picture spin.
    // Selecting a slot fades the current bend out, switches the preset while it is out of sight, then fades the new one in.
    // It only writes to CurvedWorldBridge (the stage's single Curved World controller); it never touches the cameras.
    // No keys are bound here: call Select(), Next(), Previous() or Step() from a button or a control scheme later.
    [DisallowMultipleComponent]
    [AddComponentMenu("Psychedelic Lab/User Curved Controller Manager")]
    public sealed class UserCurvedControllerManager : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            [Tooltip("Shown in the Inspector and to any future button label.")]
            public string label = "Slot";
            [Tooltip("Index into CurvedWorldBridge.Presets: 0 Off, 1 Little Planet, 2 Cylinder Tower, 3 Cylinder Rolloff, 4 Classic Runner, 5 Twisted Corridor, 6 Twisted Highway, 7 Ball.")]
            [Range(0, 7)] public int preset = 3;
            [Range(0f, 1f)] public float amount = 1f;
            [Range(0f, 3f)] public float intensity = 1f;
            [Range(-1440f, 1440f)] public float spin;
            public bool customBend;
            public BendType customShape = BendType.TwistedSpiral_Z_Positive;
            [Range(-30, 30)] public float customCurvature = 2;
            [Range(-15, 15)] public float customHorizontal = -1.2f;
            [Range(-10, 10)] public float customVertical = 1.2f;
            public Vector3 customAxis = Vector3.forward;
            public bool pivotFollowsCamera = true;

            public void Capture(CurvedWorldBridge bridge)
            {
                preset = bridge.preset; amount = bridge.amount; intensity = bridge.intensity; spin = bridge.spin;
                customBend = bridge.customBend; customShape = bridge.customShape;
                customCurvature = bridge.customCurvature; customHorizontal = bridge.customHorizontal;
                customVertical = bridge.customVertical; customAxis = bridge.customAxis;
                pivotFollowsCamera = bridge.pivotFollowsCamera;
            }
            public void ApplyShape(CurvedWorldBridge bridge)
            {
                bridge.customBend = customBend; bridge.customShape = customShape;
                bridge.customCurvature = customCurvature; bridge.customHorizontal = customHorizontal;
                bridge.customVertical = customVertical; bridge.customAxis = customAxis;
                bridge.pivotFollowsCamera = pivotFollowsCamera;
            }
        }

        [Tooltip("Bend states in order. Next/Previous walk this list and wrap around.")]
        public List<Slot> slots = new List<Slot>();
        [Tooltip("Index into slots to use at startup. Out-of-range values are clamped.")]
        public int startingSlot;
        [Tooltip("Seconds to fade out, switch and fade back in when the slot changes. 0 = cut.")]
        [Min(0f)] public float transitionSeconds = 1.5f;

        public static UserCurvedControllerManager Active { get; private set; }

        public int CurrentSlot { get; private set; } = -1;
        public bool HasSelection => CurrentSlot >= 0;

        // Transition state: the bend values we are moving from and to, and where we are in the fade.
        Slot from, to;
        float progress = 1f;     // 0..1 across the whole change
        bool started;
        Slot original;
        CurvedWorldBridge ownedBridge;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (Active != null && Active != this)
            {
                Debug.LogWarning("UserCurvedControllerManager: only one enabled manager at a time.", this);
                enabled = false;
                return;
            }
            Active = this;
        }

        void Start()
        {
            if (slots.Count == 0) return;
            started = Select(startingSlot, instant: true);
        }

        void Update()
        {
            if (!started && slots.Count > 0) started = Select(startingSlot, instant: true);
            if (!HasSelection || progress >= 1f || CurvedWorldBridge.Instance == null) return;
            progress = transitionSeconds <= 0f ? 1f : Mathf.Min(1f, progress + Time.deltaTime / transitionSeconds);
            Apply();
        }

        // Writes one frame of the transition into the bridge.
        void Apply()
        {
            var bridge = CurvedWorldBridge.Instance;
            if (bridge == null) return;
            float t = Mathf.SmoothStep(0f, 1f, progress);
            // First half: the old bend fades out. Second half: the new bend fades in. The preset switches at the midpoint, when nothing is bent.
            bool second = progress >= 0.5f;
            float fade = second ? (progress - 0.5f) * 2f : 1f - progress * 2f;
            fade = Mathf.SmoothStep(0, 1, fade);
            (second ? to : from).ApplyShape(bridge);
            bridge.preset = second ? to.preset : from.preset;
            bridge.amount = second ? to.amount * fade : from.amount * fade;
            bridge.intensity = Mathf.Lerp(from.intensity, to.intensity, t);
            bridge.spin = Mathf.Lerp(from.spin, to.spin, t);
        }

        // ---------------------------------------------------------------- selection
        public bool Select(int index, bool instant = false)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || Active != this || CurvedWorldBridge.Instance == null || slots.Count == 0) return false;
            index = Mathf.Clamp(index, 0, slots.Count - 1);
            if (slots[index] == null) return false;
            if (original == null)
            {
                ownedBridge = CurvedWorldBridge.Instance;
                original = new Slot(); original.Capture(ownedBridge);
            }
            if (!instant && HasSelection && index == CurrentSlot) return false;
            var next = slots[index];
            var current = HasSelection && CurrentSlot < slots.Count ? slots[CurrentSlot] : null;
            CurrentSlot = index;
            started = true;

            if (instant || transitionSeconds <= 0f || current == null || CurvedWorldBridge.Instance == null)
            {
                from = to = next;
                progress = 1f;
                Apply();
                return true;
            }

            // Start from whatever is on screen now, so an interrupted fade does not jump.
            from = new Slot(); from.Capture(CurvedWorldBridge.Instance);
            to = next;
            progress = 0f;
            return true;
        }

        public bool Next() => Step(1);
        public bool Previous() => Step(-1);

        public bool Step(int direction)
        {
            if (slots.Count == 0 || direction == 0) return false;
            int start = HasSelection ? CurrentSlot : (direction > 0 ? -1 : 0);
            int index = ((start + direction) % slots.Count + slots.Count) % slots.Count;
            return Select(index);
        }

        public Slot GetSlot(int index) => index >= 0 && index < slots.Count ? slots[index] : null;

        void OnDisable()
        {
            if (Active == this) Active = null;
            // Release the controller without losing the authored bend it took over.
            if (original != null && ownedBridge != null)
            {
                original.ApplyShape(ownedBridge);
                ownedBridge.preset = original.preset; ownedBridge.amount = original.amount;
                ownedBridge.intensity = original.intensity; ownedBridge.spin = original.spin;
            }
            original = null; ownedBridge = null; started = false;
            CurrentSlot = -1;
            progress = 1f;
        }
    }
}
