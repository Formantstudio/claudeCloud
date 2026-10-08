using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Strange attractors as wire geometry: a tube or ribbon swept along the trajectory with
    /// parallel-transport frames, or, for the iterated maps, a dust of tiny crosses.
    ///
    /// The trajectory is integrated once (fixed-step or adaptive RK4, in double precision) whenever
    /// a parameter changes, and cached. Animation slides a visible window along the cached curve, so
    /// a playing attractor costs one resample and one sweep per frame and never allocates.
    ///
    /// <see cref="IWireGeometry"/>: u runs along the visible trail, v around the tube.
    /// </summary>
    [AddComponentMenu("Geometry Engine/Strange Attractor")]
    public sealed class AttractorEngine : WireMeshComponent, IWireGeometry
    {
        public enum Shape { Tube, Ribbon, Dust }

        [Header("System")]
        public Attractor system = Attractor.Lorenz;
        [Tooltip("Reset the parameters to the system's classic chaotic values whenever the system changes.")]
        public bool useDefaultParameters = true;
        public AttractorParameters parameters = AttractorParameters.Defaults(Attractor.Lorenz);

        [Header("Integration")]
        [Tooltip("Step-doubling RK4: small steps where the flow is fast, large where it is slow.")]
        public bool adaptive = true;
        [Tooltip("Adaptive: allowed local error per step.")]
        [Range(1e-10f, 1e-3f)] public float tolerance = 1e-6f;
        [Tooltip("Fixed step: RK4 step size in flow time.")]
        [Range(.0005f, .05f)] public float dt = .005f;
        [Tooltip("Flow time (or, for maps, iterations x 0.01) recorded between stored points.")]
        [Range(.001f, .2f)] public float sampleInterval = .01f;
        [Tooltip("Flow time discarded first so the curve starts on the attractor.")]
        [Range(0f, 200f)] public float transientTime = 20f;
        [Tooltip("Points stored in the cached trajectory. Maps use this as the orbit length.")]
        [Range(500, 200000)] public int trajectoryPoints = 20000;

        [Header("Trail")]
        [Tooltip("Stored points visible at once.")]
        [Range(50, 200000)] public int visiblePoints = 6000;
        [Tooltip("Points the visible window advances per second.")]
        [Range(0f, 5000f)] public float flowSpeed = 400f;
        [Tooltip("The window is resampled to this many evenly spaced rings.")]
        [Range(16, 8192)] public int rings = 1200;

        [Header("Shape")]
        public Shape shape = Shape.Tube;
        [Range(3, 16)] public int tubeSides = 5;
        [Tooltip("Tube radius or ribbon half-width, in output units.")]
        [Min(.0001f)] public float thickness = .01f;
        [Tooltip("The attractor is framed to this radius around the object's origin.")]
        [Min(.01f)] public float size = 1f;

        readonly List<Vector3> trajectory = new List<Vector3>();
        readonly List<Vector3> window = new List<Vector3>();
        readonly List<Vector3> trail = new List<Vector3>();
        readonly List<Vector3> scratchT = new List<Vector3>(), scratchN = new List<Vector3>(), grid = new List<Vector3>();
        bool trajectoryDirty = true;
        Attractor builtSystem = (Attractor)(-1);
        int lastSteps;

        protected override void OnValidate()
        {
            if (useDefaultParameters && builtSystem != system && builtSystem != (Attractor)(-1))
                parameters = AttractorParameters.Defaults(system);
            trajectoryDirty = true;
            base.OnValidate();
        }

        [ContextMenu("Reset parameters to defaults")]
        public void ResetParameters() { parameters = AttractorParameters.Defaults(system); trajectoryDirty = true; MarkDirty(); }

        void Recompute()
        {
            trajectoryDirty = false;
            builtSystem = system;
            var seed = StrangeAttractors.Seed(system);
            if (StrangeAttractors.IsMap(system))
            {
                StrangeAttractors.Iterate(system, parameters, seed, 200, trajectoryPoints, trajectory);
                lastSteps = trajectoryPoints;
            }
            else if (adaptive)
                lastSteps = StrangeAttractors.IntegrateAdaptive(system, parameters, seed, transientTime,
                    (double)sampleInterval * (trajectoryPoints - 1), sampleInterval, tolerance, trajectory);
            else
            {
                int sub = Mathf.Max(1, Mathf.RoundToInt(sampleInterval / Mathf.Max(dt, 1e-5f)));
                StrangeAttractors.Integrate(system, parameters, seed, sampleInterval / sub, sub,
                    Mathf.RoundToInt(transientTime / Mathf.Max(dt, 1e-5f)), trajectoryPoints, trajectory);
                lastSteps = (trajectoryPoints * sub);
            }

            // Frame to unit size about the origin.
            StrangeAttractors.Frame(system, out var centre, out float half);
            float k = 1f / Mathf.Max(half, 1e-4f);
            for (int i = 0; i < trajectory.Count; i++) trajectory[i] = (trajectory[i] - centre) * k;
        }

        protected override void Build(WireMeshBuilder into, float time)
        {
            if (trajectoryDirty || builtSystem != system) Recompute();
            trail.Clear();
            int n = trajectory.Count;
            if (n < 2) return;

            int visible = Mathf.Clamp(visiblePoints, 2, n);
            int start = n == visible ? 0 : (int)((long)(time * flowSpeed) % (n - visible + 1));
            window.Clear();
            for (int i = 0; i < visible; i++) window.Add(trajectory[start + i] * size);

            if (shape == Shape.Dust || StrangeAttractors.IsMap(system))
            {
                trail.AddRange(window);
                float h = thickness;
                foreach (var p in window)
                {
                    into.Quad(p + new Vector3(-h, -h, 0), p + new Vector3(h, -h, 0), p + new Vector3(h, h, 0), p + new Vector3(-h, h, 0));
                    into.Quad(p + new Vector3(0, -h, -h), p + new Vector3(0, h, -h), p + new Vector3(0, h, h), p + new Vector3(0, -h, h));
                    into.Quad(p + new Vector3(-h, 0, -h), p + new Vector3(-h, 0, h), p + new Vector3(h, 0, h), p + new Vector3(h, 0, -h));
                }
                return;
            }

            CurveSweep.ResampleByArcLength(window, Mathf.Max(rings, 2), trail);
            if (shape == Shape.Ribbon) CurveSweep.Ribbon(into, trail, false, thickness, scratchT, scratchN, grid);
            else CurveSweep.Tube(into, trail, false, tubeSides, thickness, null, scratchT, scratchN, grid);
        }

        protected override string Describe() =>
            system + (StrangeAttractors.IsMap(system) ? " map" : adaptive ? " adaptive RK4" : " RK4") +
            " · " + trajectory.Count.ToString("N0") + " points, " + lastSteps.ToString("N0") + " evaluations";

        // ---- IWireGeometry ------------------------------------------------------

        public bool IsBuilt => trail.Count > 1;
        public int GridU => Mathf.Max(trail.Count, 2);
        public int GridV => Mathf.Max(tubeSides, 1);

        public Vector3 SampleGrid(float u, float v)
        {
            if (trail.Count < 2) return Vector3.zero;
            float f = Mathf.Repeat(u, 1f) * (trail.Count - 1);
            int i = Mathf.Min((int)f, trail.Count - 2);
            Vector3 p = Vector3.LerpUnclamped(trail[i], trail[i + 1], f - i);
            if (shape != Shape.Tube || scratchN.Count != trail.Count) return p;
            Vector3 t = scratchT[i], nrm = scratchN[i], b = Vector3.Cross(t, nrm);
            float a = v * Mathf.PI * 2f;
            return p + (nrm * Mathf.Cos(a) + b * Mathf.Sin(a)) * thickness;
        }
    }
}
