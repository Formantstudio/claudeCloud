using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class Rotor4Tests
    {
        static readonly Vector4[] Probes =
        {
            new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 0, 0, 1),
            new Vector4(.3f, -.7f, .2f, .5f), new Vector4(-1.2f, .4f, .9f, -.3f),
        };

        /// <summary>The Givens matrix Hyperspace4DAxis composes: positive angle turns axis a toward b.</summary>
        static Matrix4x4 Givens(int a, int b, float radians)
        {
            var g = Matrix4x4.identity;
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            g[a, a] = c; g[a, b] = -s; g[b, a] = s; g[b, b] = c;
            return g;
        }

        static readonly int[,] Axes = { { 0, 1 }, { 0, 2 }, { 0, 3 }, { 1, 2 }, { 1, 3 }, { 2, 3 } };

        static void Near(Vector4 expected, Vector4 actual, float tol = 1e-5f) =>
            Assert.Less((expected - actual).magnitude, tol, "expected " + expected + " got " + actual);

        [Test]
        public void EachPlaneMatchesGivensConvention()
        {
            for (int p = 0; p < 6; p++)
            foreach (float angle in new[] { .37f, -1.1f, 2.9f })
            {
                var rotor = Rotor4.Plane((Plane4)p, angle);
                var g = Givens(Axes[p, 0], Axes[p, 1], angle);
                foreach (var v in Probes) Near(g * v, rotor.Rotate(v));
            }
        }

        [Test]
        public void PreservesLengthAndInnerProduct()
        {
            var r = Rotor4.FromBivector(.4f, -1.3f, .9f, 2.2f, -.6f, 1.7f);
            for (int i = 0; i < Probes.Length; i++)
            for (int j = 0; j < Probes.Length; j++)
                Assert.AreEqual(Vector4.Dot(Probes[i], Probes[j]),
                                Vector4.Dot(r.Rotate(Probes[i]), r.Rotate(Probes[j])), 1e-5f);
        }

        [Test]
        public void DeterminantIsPlusOne()
        {
            var m = Rotor4.FromBivector(.4f, -1.3f, .9f, 2.2f, -.6f, 1.7f).ToMatrix();
            Assert.AreEqual(1.0, Det4(m), 1e-5);
        }

        [Test]
        public void CompositionMatchesMatrixProduct()
        {
            var a = Rotor4.FromBivector(.2f, .5f, -.3f, .1f, .8f, -.4f);
            var b = Rotor4.FromBivector(-.6f, .1f, 1.2f, -.9f, .3f, .7f);
            var ab = a * b;
            foreach (var v in Probes) Near(a.Rotate(b.Rotate(v)), ab.Rotate(v));
            foreach (var v in Probes) Near(a.ToMatrix() * (b.ToMatrix() * v), ab.Rotate(v));
        }

        [Test]
        public void InverseUndoes()
        {
            var r = Rotor4.FromBivector(.9f, -.2f, .4f, 1.6f, -2.1f, .3f);
            foreach (var v in Probes) Near(v, r.Inverse.Rotate(r.Rotate(v)));
        }

        [Test]
        public void CommutingPlanesComposeToBivector()
        {
            // XY and ZW commute, so the sequential product equals the simultaneous exponential.
            var seq = Rotor4.Plane(Plane4.XY, .7f) * Rotor4.Plane(Plane4.ZW, -1.3f);
            var sim = Rotor4.FromBivector(.7f, 0, 0, 0, 0, -1.3f);
            foreach (var v in Probes) Near(seq.Rotate(v), sim.Rotate(v));
        }

        [Test]
        public void EqualRatesAreIsoclinic()
        {
            // Equal angles in XY and ZW: every unit vector turns through exactly the same angle.
            var r = Rotor4.FromBivector(.6f, 0, 0, 0, 0, .6f);
            foreach (var v in Probes)
            {
                var u = v.normalized;
                Assert.AreEqual(Mathf.Cos(.6f), Vector4.Dot(u, r.Rotate(u)), 1e-5f);
            }
            var angles = r.InvariantAngles;
            Assert.AreEqual(angles.x, angles.y, 1e-4f);
        }

        [Test]
        public void DoubleRotationReportsItsAngles()
        {
            var r = Rotor4.Double(Plane4.XZ, 1.1f, .3f);
            var a = r.InvariantAngles;
            Assert.AreEqual(1.1f, a.x, 1e-4f);
            Assert.AreEqual(.3f, a.y, 1e-4f);
        }

        [Test]
        public void SlerpHitsEndpointsAndStaysRigid()
        {
            var a = Rotor4.FromBivector(.2f, .1f, 0, .4f, 0, -.3f);
            var b = Rotor4.FromBivector(-1.4f, .9f, 2.1f, 0, .5f, .2f);
            foreach (var v in Probes)
            {
                Near(a.Rotate(v), Rotor4.Slerp(a, b, 0).Rotate(v));
                Near(b.Rotate(v), Rotor4.Slerp(a, b, 1).Rotate(v));
                Assert.AreEqual(v.magnitude, Rotor4.Slerp(a, b, .37f).Rotate(v).magnitude, 1e-5f);
            }
        }

        [Test]
        public void SlerpTakesTheShortWayForNegatedPair()
        {
            var a = Rotor4.FromBivector(.3f, 0, 0, 0, 0, 0);
            var neg = new Rotor4(-a.left, -a.right);
            var mid = Rotor4.Slerp(a, neg, .5f);
            foreach (var v in Probes) Near(a.Rotate(v), mid.Rotate(v));
        }

        [Test]
        public void ConstantRateIsOneParameterSubgroup()
        {
            // exp((s + t) B) = exp(sB) exp(tB): animating a bivector at constant rate is a geodesic.
            float[] b = { .3f, -.8f, .5f, 1.1f, -.2f, .6f };
            Rotor4 At(float t) => Rotor4.FromBivector(b[0] * t, b[1] * t, b[2] * t, b[3] * t, b[4] * t, b[5] * t);
            var lhs = At(1.7f);
            var rhs = At(.5f) * At(1.2f);
            foreach (var v in Probes) Near(lhs.Rotate(v), rhs.Rotate(v));
        }

        static double Det4(Matrix4x4 m)
        {
            double[,] a = new double[4, 4];
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) a[i, j] = m[i, j];
            double det = 1;
            for (int c = 0; c < 4; c++)
            {
                int p = c;
                for (int r = c + 1; r < 4; r++) if (System.Math.Abs(a[r, c]) > System.Math.Abs(a[p, c])) p = r;
                if (p != c) { for (int k = 0; k < 4; k++) { var t = a[c, k]; a[c, k] = a[p, k]; a[p, k] = t; } det = -det; }
                det *= a[c, c];
                for (int r = c + 1; r < 4; r++)
                {
                    double f = a[r, c] / a[c, c];
                    for (int k = c; k < 4; k++) a[r, k] -= f * a[c, k];
                }
            }
            return det;
        }
    }
}
