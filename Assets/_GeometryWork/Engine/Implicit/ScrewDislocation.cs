using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>A coordinate axis.</summary>
    public enum Axis3 { X, Y, Z }

    /// <summary>
    /// The Escher screw dislocation, as one shared transform of field space:
    ///
    ///   q = RotateToAxis(p, axis)
    ///   q.z += dislocation · period · atan2(q.y, q.x) / 2π · SmoothStep(0, 1, r / core)
    ///   p' = RotateFromAxis(q, axis)
    ///
    /// One circuit of the axis advances the lattice by <c>dislocation</c> periods, so walking round
    /// puts you a floor higher in identical geometry — a property of the field, so it holds from every
    /// angle. The atan2 branch cut is invisible exactly when <c>dislocation × period</c> is a whole
    /// number of the field's own repeats along the axis: <c>period</c> must be the field's true period
    /// in the coordinates <c>p</c> is measured in, and <c>dislocation</c> an integer. The smooth core
    /// fades the shear out on the axis itself, where every azimuth meets.
    ///
    /// Both <see cref="Implicits"/> and the Escher rooms call this, so the two pipelines step alike.
    /// </summary>
    public static class ScrewDislocation
    {
        public static Vector3 Apply(Vector3 p, Axis3 axis, float dislocation, float period, float core)
        {
            if (dislocation == 0f) return p;
            Vector3 q = ToAxis(p, axis);
            float r = Mathf.Sqrt(q.x * q.x + q.y * q.y);
            float ease = r <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Min(r / Mathf.Max(core, 1e-4f), 1f));
            if (ease > 0f)
                q.z += dislocation * period * (Mathf.Atan2(q.y, q.x) / (Mathf.PI * 2f)) * ease;
            return FromAxis(q, axis);
        }

        /// <summary>Cyclic permutation that brings <paramref name="axis"/> to Z.</summary>
        public static Vector3 ToAxis(Vector3 p, Axis3 axis) =>
            axis == Axis3.X ? new Vector3(p.y, p.z, p.x) : axis == Axis3.Y ? new Vector3(p.z, p.x, p.y) : p;

        /// <summary>Inverse of <see cref="ToAxis"/>.</summary>
        public static Vector3 FromAxis(Vector3 q, Axis3 axis) =>
            axis == Axis3.X ? new Vector3(q.z, q.x, q.y) : axis == Axis3.Y ? new Vector3(q.y, q.z, q.x) : q;

        /// <summary>
        /// Period along every axis of a TPMS evaluated as f(π · frequency · p): the trigonometric
        /// lattice repeats every 2π in its argument, so every 2 / frequency in p.
        /// </summary>
        public static float TpmsPeriod(float frequency) => 2f / Mathf.Max(frequency, 1e-3f);
    }
}
