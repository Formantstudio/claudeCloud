using System;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>The six coordinate planes of 4-space, in the order Hyperspace4DAxis uses.</summary>
    public enum Plane4 { XY, XZ, XW, YZ, YW, ZW }

    /// <summary>
    /// A rotation of 4-space as a pair of unit quaternions: SO(4) ≅ (SU(2) × SU(2)) / Z₂.
    ///
    /// A point p = (x, y, z, w) is read as the quaternion P = w + x i + y j + z k and rotated as
    /// <c>P' = L · P · R</c>. Left multiplication alone is a left-isoclinic rotation (every point turns
    /// by the same angle, in a pair of orthogonal planes, the same way); right multiplication alone
    /// is right-isoclinic. Every 4-D rotation is exactly one of each, which is why this
    /// representation has no gimbal lock and why <see cref="Slerp"/> follows a true geodesic.
    ///
    /// Angles follow Hyperspace4DAxis' Givens convention: a positive angle in plane (a, b) turns axis
    /// a toward axis b. <see cref="FromBivector"/> applies all six plane angles simultaneously — as
    /// the exponential of one bivector, not a chain of rotations — so the result does not depend on
    /// an arbitrary order, and animating it at constant rate is a one-parameter subgroup.
    /// (L, R) and (−L, −R) are the same rotation.
    /// </summary>
    [Serializable]
    public struct Rotor4
    {
        /// <summary>Left quaternion, as (i, j, k, real).</summary>
        public Vector4 left;
        /// <summary>Right quaternion, as (i, j, k, real).</summary>
        public Vector4 right;

        public Rotor4(Vector4 left, Vector4 right) { this.left = left; this.right = right; }

        public static Rotor4 identity => new Rotor4(new Vector4(0, 0, 0, 1), new Vector4(0, 0, 0, 1));

        // ---- construction -------------------------------------------------------

        /// <summary>
        /// The rotation exp(B) for the bivector B with these plane angles (radians), applied
        /// simultaneously. The bivector splits into its self-dual and anti-self-dual halves, which
        /// exponentiate independently into the left and right quaternions:
        ///   ℓ = (yz − xw, −xz − yw, xy − zw),  r = (−yz − xw, xz − yw, −xy − zw),
        ///   L = exp(ℓ/2), R = exp(r/2).
        /// Equal angles in XY and ZW therefore give a pure right-isoclinic turn.
        /// </summary>
        public static Rotor4 FromBivector(float xy, float xz, float xw, float yz, float yw, float zw)
        {
            var l = new Vector3(yz - xw, -xz - yw, xy - zw);
            var r = new Vector3(-yz - xw, xz - yw, -xy - zw);
            return new Rotor4(Exp(l * .5f), Exp(r * .5f));
        }

        /// <summary>Plane angles in <see cref="Plane4"/> order (XY, XZ, XW, YZ, YW, ZW), radians.</summary>
        public static Rotor4 FromBivector(float[] planes) =>
            FromBivector(planes[0], planes[1], planes[2], planes[3], planes[4], planes[5]);

        /// <summary>A simple rotation by <paramref name="radians"/> in one coordinate plane.</summary>
        public static Rotor4 Plane(Plane4 plane, float radians)
        {
            switch (plane)
            {
                case Plane4.XY: return FromBivector(radians, 0, 0, 0, 0, 0);
                case Plane4.XZ: return FromBivector(0, radians, 0, 0, 0, 0);
                case Plane4.XW: return FromBivector(0, 0, radians, 0, 0, 0);
                case Plane4.YZ: return FromBivector(0, 0, 0, radians, 0, 0);
                case Plane4.YW: return FromBivector(0, 0, 0, 0, radians, 0);
                default: return FromBivector(0, 0, 0, 0, 0, radians);
            }
        }

        /// <summary>
        /// Left and right isoclinic parts given directly: each vector's direction is the quaternion
        /// axis and its length is the angle (radians) every point is turned through by that half.
        /// </summary>
        public static Rotor4 Isoclinic(Vector3 leftAngleAxis, Vector3 rightAngleAxis) =>
            new Rotor4(Exp(leftAngleAxis), Exp(rightAngleAxis));

        /// <summary>
        /// A double rotation: <paramref name="alpha"/> in the plane spanned by axes (a, b) and
        /// <paramref name="beta"/> in its orthogonal complement. Equal angles are isoclinic.
        /// </summary>
        public static Rotor4 Double(Plane4 plane, float alpha, float beta) =>
            Plane(plane, alpha) * Plane(Complement(plane), beta);

        /// <summary>The plane orthogonal to <paramref name="p"/>: XY↔ZW, XZ↔YW, XW↔YZ.</summary>
        public static Plane4 Complement(Plane4 p) => (Plane4)(5 - (int)p);

        /// <summary>Unit quaternion exp(v) for a pure quaternion v; |v| is the half-angle of the 3-D analogue.</summary>
        public static Vector4 Exp(Vector3 v)
        {
            float a = v.magnitude;
            if (a < 1e-8f) return new Vector4(v.x, v.y, v.z, 1f).normalized;
            float s = Mathf.Sin(a) / a;
            return new Vector4(v.x * s, v.y * s, v.z * s, Mathf.Cos(a));
        }

        // ---- use ---------------------------------------------------------------

        /// <summary>Rotates a 4-D point.</summary>
        public Vector4 Rotate(Vector4 p)
        {
            // Quaternion layout: (i, j, k, real). The point's real part is w.
            Vector4 q = Mul(Mul(left, new Vector4(p.x, p.y, p.z, p.w)), right);
            return q;
        }

        /// <summary>Composition: <c>(a * b).Rotate(p) == a.Rotate(b.Rotate(p))</c>.</summary>
        public static Rotor4 operator *(Rotor4 a, Rotor4 b) =>
            new Rotor4(Mul(a.left, b.left), Mul(b.right, a.right));

        /// <summary>The inverse rotation.</summary>
        public Rotor4 Inverse => new Rotor4(Conj(left), Conj(right));

        /// <summary>Renormalises both halves, to stop drift after long chains of composition.</summary>
        public Rotor4 Normalized => new Rotor4(left.normalized, right.normalized);

        /// <summary>
        /// Geodesic interpolation on SO(4): slerp each half. The pair is sign-aligned first, since
        /// (L, R) and (−L, −R) are the same rotation and the short way round must be taken.
        /// </summary>
        public static Rotor4 Slerp(Rotor4 a, Rotor4 b, float t)
        {
            if (Vector4.Dot(a.left, b.left) + Vector4.Dot(a.right, b.right) < 0f)
                b = new Rotor4(-b.left, -b.right);
            return new Rotor4(SlerpUnit(a.left, b.left, t), SlerpUnit(a.right, b.right, t));
        }

        /// <summary>Column-vector matrix: <c>ToMatrix() * p == Rotate(p)</c>.</summary>
        public Matrix4x4 ToMatrix()
        {
            var m = Matrix4x4.identity;
            m.SetColumn(0, Rotate(new Vector4(1, 0, 0, 0)));
            m.SetColumn(1, Rotate(new Vector4(0, 1, 0, 0)));
            m.SetColumn(2, Rotate(new Vector4(0, 0, 1, 0)));
            m.SetColumn(3, Rotate(new Vector4(0, 0, 0, 1)));
            return m;
        }

        /// <summary>
        /// The two invariant angles of this rotation (each in [0, π]): a double rotation turns
        /// one plane by <c>a</c> and its orthogonal plane by <c>b</c>. Isoclinic when they are equal.
        /// θL and θR are the half-angles of L and R; the plane angles are θL ± θR.
        /// </summary>
        public Vector2 InvariantAngles
        {
            get
            {
                float tl = Mathf.Acos(Mathf.Clamp(Mathf.Abs(left.w), 0f, 1f));
                float tr = Mathf.Acos(Mathf.Clamp(Mathf.Abs(right.w), 0f, 1f));
                return new Vector2(tl + tr, Mathf.Abs(tl - tr));
            }
        }

        // ---- quaternion helpers (i, j, k, real) ---------------------------------

        public static Vector4 Mul(Vector4 a, Vector4 b) => new Vector4(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        static Vector4 Conj(Vector4 q) => new Vector4(-q.x, -q.y, -q.z, q.w);

        /// <summary>
        /// Plain slerp with no hemisphere flip. Flipping one half alone would turn (L, R) into
        /// (L, −R), which is the rotation composed with −I — a different rotation entirely. The
        /// sign is chosen once, for the pair, in <see cref="Slerp"/>.
        /// </summary>
        static Vector4 SlerpUnit(Vector4 a, Vector4 b, float t)
        {
            float d = Vector4.Dot(a, b);
            if (d > .9995f) return (a + (b - a) * t).normalized;
            float theta = Mathf.Acos(Mathf.Clamp(d, -1f, 1f));
            float s = Mathf.Sin(theta);
            return a * (Mathf.Sin((1f - t) * theta) / s) + b * (Mathf.Sin(t * theta) / s);
        }

        public override string ToString() => "L" + left + " R" + right;
    }
}
