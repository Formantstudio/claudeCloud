using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.Control
{
    [DisallowMultipleComponent, RequireComponent(typeof(CurvedWorldBridge))]
    [AddComponentMenu("Psychedelic Lab/Curved World Automation")]
    public sealed class CurvedWorldAutomation : MonoBehaviour
    {
        public enum Wave { Sine, Saw, Square }
        public enum Period { FourBars, TwoBars, OneBar, HalfBar, QuarterBar }
        public enum Parameter { Curvature, Horizontal, Vertical, Amount, Intensity }
        [Serializable]
        public sealed class Motion
        {
            public bool enabled = true;
            public Parameter parameter = Parameter.Curvature;
            [Tooltip("Off: continuous wave. On: runs one cycle when Trigger Moment is called.")]
            public bool momentOnly = true;
            public Wave wave = Wave.Sine;
            public Period period = Period.OneBar;
            [Tooltip("Wave endpoints, constrained to the target's supported range when applied.")]
            public float low = 2, high = 12;
            [Range(0, 1)] public float strength = 1;
            [Tooltip("Square wave's fraction spent at High.")]
            [Range(0.05f, 0.95f)] public float duty = 0.5f;
        }

        [Tooltip("Uses musical song position, including seek/pause and offline export. Empty auto-finds a scene clock.")]
        public MasterClock clock;
        [Min(1)] public float fallbackBpm = 120;
        [Min(1)] public int fallbackBeatsPerBar = 4;
        [Range(0, 1)] public float amount = 1;
        [Tooltip("Start moments on the next bar boundary. Off triggers immediately.")]
        public bool quantizeMomentToBar = true;
        public List<Motion> motions = new List<Motion> { new Motion() };

        double fallbackBeat, lastTime, momentBeat;
        bool momentTriggered;
        int transportRevision;
        float nextClockSearch;

        void OnEnable()
        {
            lastTime = Time.unscaledTimeAsDouble;
            fallbackBeat = 0;
            momentTriggered = false;
            FindClock();
        }
        void FindClock()
        {
            if (clock == null) clock = FindFirstObjectByType<MasterClock>();
            if (clock != null) transportRevision = clock.TransportRevision;
            nextClockSearch = Time.unscaledTime + 1;
        }
        void Update()
        {
            if (clock == null && Time.unscaledTime >= nextClockSearch) FindClock();
            double now = Time.unscaledTimeAsDouble;
            fallbackBeat += (now - lastTime) * Math.Max(1, fallbackBpm) / 60;
            lastTime = now;
            if (clock != null && transportRevision != clock.TransportRevision)
            { momentTriggered = false; transportRevision = clock.TransportRevision; }
        }
        double Beat => clock != null && clock.isActiveAndEnabled ? clock.Beat : fallbackBeat;
        int BeatsPerBar => Mathf.Max(1, clock != null && clock.isActiveAndEnabled ? clock.beatsPerBar : fallbackBeatsPerBar);

        public void TriggerMoment()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            double beat = Beat;
            momentBeat = quantizeMomentToBar ? Math.Ceiling(beat / BeatsPerBar - 0.000001) * BeatsPerBar : beat;
            momentTriggered = true;
        }
        public void StopMoment() => momentTriggered = false;
        void OnDisable() => momentTriggered = false;

        public static float Bars(Period period) => period switch
        { Period.FourBars => 4, Period.TwoBars => 2, Period.HalfBar => 0.5f, Period.QuarterBar => 0.25f, _ => 1 };

        public static float Sample(Wave wave, float phase, float duty) => wave switch
        {
            Wave.Saw => 1 - phase,
            Wave.Square => phase < duty ? 1 : 0,
            _ => 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2)
        };

        // Applied only to the render-time bend values. Authored Bridge values and camera transforms are untouched.
        public void Apply(ref CurvedWorldBridge.Preset preset, ref float bendAmount, ref float intensity)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || amount <= 0) return;
            double beat = Beat;
            foreach (var motion in motions)
            {
                if (motion == null || !motion.enabled) continue;
                double duration = Bars(motion.period) * BeatsPerBar;
                double cycles = (motion.momentOnly ? beat - momentBeat : beat) / duration;
                if (motion.momentOnly && (!momentTriggered || cycles < 0 || cycles >= 1)) continue;
                float phase = (float)(cycles - Math.Floor(cycles));
                float value = Mathf.Lerp(motion.low, motion.high, Sample(motion.wave, phase, motion.duty));
                float weight = Mathf.Clamp01(amount) * Mathf.Clamp01(motion.strength);
                switch (motion.parameter)
                {
                    case Parameter.Curvature: preset.curvature = Mathf.Lerp(preset.curvature, Mathf.Clamp(value, -30, 30), weight); break;
                    case Parameter.Horizontal: preset.horizontal = Mathf.Lerp(preset.horizontal, Mathf.Clamp(value, -15, 15), weight); break;
                    case Parameter.Vertical: preset.vertical = Mathf.Lerp(preset.vertical, Mathf.Clamp(value, -10, 10), weight); break;
                    case Parameter.Amount: bendAmount *= Mathf.Lerp(1, Mathf.Clamp01(value), weight); break;
                    case Parameter.Intensity: intensity = Mathf.Lerp(intensity, Mathf.Clamp(value, 0, 3), weight); break;
                }
            }
        }
    }
}
