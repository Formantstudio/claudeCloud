using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The Hopf fibration as swept wire tubes, with a particle bundle through <see cref="IWireGeometry"/>.
    ///
    /// Each base point on S² becomes a great circle on S³, turned by a 4-D <see cref="Rotor4"/> and
    /// stereographically projected to a circle in R³. Every pair of circles links exactly once;
    /// fibers over one circle of latitude are the Villarceau circles of a torus.
    ///
    /// Particles: put a <c>WireParticleSwarm</c> on this object. <see cref="SampleGrid"/> reads u as
    /// the position along a fiber and v as which fiber, and the fibers flow at a rate that is phase
    /// shifted by latitude, so the bundle visibly circulates.
    /// </summary>
    [AddComponentMenu("Geometry Engine/Hopf Fibration")]
    public sealed class HopfFibrationEngine : WireMeshComponent, IWireGeometry
    {
        [Header("Fibers")]
        public HopfBaseSet baseSet = HopfBaseSet.LatitudeRings;
        [Range(1, 256)] public int fiberCount = 36;
        [Tooltip("Latitude rings, for LatitudeRings.")]
        [Range(1, 12)] public int rings = 3;
        [Tooltip("Polar angle of the base circle in degrees, 90 = equator (the Clifford torus).")]
        [Range(1f, 179f)] public float latitude = 90f;
        [Range(8, 512)] public int samplesPerFiber = 128;

        [Header("Tubes")]
        [Range(3, 24)] public int tubeSides = 6;
        [Min(.0001f)] public float tubeRadius = .03f;
        [Tooltip("Scale each tube by the stereographic magnification, so every fiber has the same thickness on S³. Off = constant thickness in R³.")]
        public bool conformalTubes = true;
        [Tooltip("Skip fibers that reach further than this from the centre. The fiber through the projection pole is a line to infinity.")]
        [Min(1f)] public float clipRadius = 40f;
        [Min(.01f)] public float scale = 1f;

        [Header("4-D rotation (radians per second, per plane)")]
        [Tooltip("XY, XZ, YZ: ordinary rotations of the picture.")]
        public Vector3 rate3D = new Vector3(0f, 0f, 0f);
        [Tooltip("XW, YW, ZW: tilt S³ through the projection pole, which reshapes every circle.")]
        public Vector3 rateW = new Vector3(.07f, 0f, .11f);
        [Tooltip("Static 4-D orientation added to the animated one, same plane order.")]
        public Vector3 angle3D, angleW;

        [Header("Particle bundle")]
        [Tooltip("Turns per second that particles travel along each fiber.")]
        [Range(-2f, 2f)] public float fiberFlow = .1f;
        [Tooltip("Extra phase per unit of base latitude, so neighbouring tori shear past each other.")]
        [Range(-4f, 4f)] public float flowShear = 1f;

        readonly List<Vector3> bases = new List<Vector3>();
        readonly List<Vector3> fiber = new List<Vector3>();
        readonly List<float> scales = new List<float>();
        readonly List<float> radii = new List<float>();
        readonly List<Vector3> scratchT = new List<Vector3>(), scratchN = new List<Vector3>(), ring = new List<Vector3>();
        Rotor4 rotation = Rotor4.identity;
        int drawn;
        float builtTime;

        public Rotor4 RotationAt(float time) => Rotor4.FromBivector(
            angle3D.x + rate3D.x * time, angle3D.y + rate3D.y * time, angleW.x + rateW.x * time,
            angle3D.z + rate3D.z * time, angleW.y + rateW.y * time, angleW.z + rateW.z * time);

        protected override void Build(WireMeshBuilder into, float time)
        {
            builtTime = time;
            rotation = RotationAt(time);
            HopfFibration.BasePoints(baseSet, fiberCount, rings, latitude, bases);
            drawn = 0;
            float clip = clipRadius / Mathf.Max(scale, 1e-4f);

            foreach (var b in bases)
            {
                HopfFibration.Fiber(b, samplesPerFiber, rotation, 0f, 1e-3f, fiber, scales);
                bool inside = true;
                for (int i = 0; i < fiber.Count && inside; i++) inside = fiber[i].sqrMagnitude <= clip * clip;
                if (!inside) continue;

                radii.Clear();
                for (int i = 0; i < fiber.Count; i++)
                {
                    fiber[i] *= scale;
                    radii.Add(tubeRadius * (conformalTubes ? Mathf.Min(scales[i] * .5f, 50f) : 1f));
                }
                CurveSweep.Tube(into, fiber, true, tubeSides, tubeRadius, radii, scratchT, scratchN, ring);
                drawn++;
            }
        }

        protected override string Describe() => "Hopf · " + drawn + "/" + bases.Count + " fibers";

        // ---- IWireGeometry ------------------------------------------------------

        public bool IsBuilt => bases.Count > 0;
        public int GridU => samplesPerFiber;
        public int GridV => Mathf.Max(bases.Count, 1);

        /// <summary>u: position along the fiber; v: which fiber. Read from the same rotation the mesh used.</summary>
        public Vector3 SampleGrid(float u, float v)
        {
            if (bases.Count == 0) return Vector3.zero;
            int f = Mathf.Clamp((int)(Mathf.Repeat(v, 1f) * bases.Count), 0, bases.Count - 1);
            Vector3 b = bases[f];
            float phase = Mathf.PI * 2f * (builtTime * fiberFlow + flowShear * Mathf.Acos(Mathf.Clamp(b.z, -1f, 1f)) / Mathf.PI);
            Vector4 p = rotation.Rotate(HopfFibration.FiberPoint(b, Mathf.PI * 2f * u + phase));
            return HopfFibration.Project(p, 1e-3f) * scale;
        }

        [ContextMenu("Clifford torus (equator)")]
        public void UseClifford() { baseSet = HopfBaseSet.SingleLatitude; latitude = 90f; fiberCount = 32; MarkDirty(); }
        [ContextMenu("Nested tori")]
        public void UseNested() { baseSet = HopfBaseSet.LatitudeRings; rings = 4; fiberCount = 48; latitude = 90f; MarkDirty(); }
        [ContextMenu("Whole sphere")]
        public void UseSphere() { baseSet = HopfBaseSet.FibonacciSphere; fiberCount = 64; MarkDirty(); }
    }
}
