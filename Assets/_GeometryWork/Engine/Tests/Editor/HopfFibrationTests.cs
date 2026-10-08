using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class HopfFibrationTests
    {
        [Test]
        public void FiberPointsLieOnS3AndMapToTheirBasePoint()
        {
            var bases = new List<Vector3>();
            HopfFibration.BasePoints(HopfBaseSet.FibonacciSphere, 40, 1, 90, bases);
            foreach (var b in bases)
                for (int k = 0; k < 16; k++)
                {
                    var p = HopfFibration.FiberPoint(b, k * .41f);
                    Assert.AreEqual(1f, p.magnitude, 1e-5f);
                    var h = HopfFibration.Map(p);
                    Assert.Less((h - b).magnitude, 1e-4f, "h(fiber) must be the base point");
                }
        }

        [Test]
        public void FibersAreGreatCirclesOnS3()
        {
            // A great circle is the unit circle of a 2-plane through the origin: p(t) = cos t·a + sin t·b.
            var b = HopfFibration.Spherical(1.1f, .7f);
            Vector4 a0 = HopfFibration.FiberPoint(b, 0), a1 = HopfFibration.FiberPoint(b, Mathf.PI / 2);
            Assert.AreEqual(0f, Vector4.Dot(a0, a1), 1e-5f);
            for (float t = 0; t < 6.2f; t += .3f)
            {
                var expected = a0 * Mathf.Cos(t) + a1 * Mathf.Sin(t);
                Assert.Less((expected - HopfFibration.FiberPoint(b, t)).magnitude, 1e-5f);
            }
        }

        [Test]
        public void ProjectedFibersArePlanarCircles()
        {
            var pts = new List<Vector3>();
            HopfFibration.Fiber(HopfFibration.Spherical(2.0f, 1.3f), 64, Rotor4.identity, 0, 1e-4f, pts, null);
            // Circumcircle of three points, then every other point must lie on it.
            Vector3 a = pts[0], b = pts[21], c = pts[43];
            Vector3 ab = b - a, ac = c - a, n = Vector3.Cross(ab, ac);
            Vector3 centre = a + (Vector3.Cross(n, ab) * ac.sqrMagnitude + Vector3.Cross(ac, n) * ab.sqrMagnitude) / (2f * n.sqrMagnitude);
            float r = (a - centre).magnitude;
            foreach (var p in pts)
            {
                Assert.AreEqual(r, (p - centre).magnitude, 1e-3f * Mathf.Max(1f, r));
                Assert.AreEqual(0f, Vector3.Dot(p - a, n.normalized), 1e-3f * Mathf.Max(1f, r));
            }
        }

        [Test]
        public void AnyTwoFibersLinkOnce()
        {
            var bases = new List<Vector3>();
            HopfFibration.BasePoints(HopfBaseSet.LatitudeRings, 12, 3, 80, bases);
            var rot = Rotor4.FromBivector(.3f, .2f, .5f, -.4f, .1f, .25f);
            var fibers = new List<List<Vector3>>();
            foreach (var b in bases)
            {
                var f = new List<Vector3>();
                HopfFibration.Fiber(b, 256, rot, 0, 1e-4f, f, null);
                fibers.Add(f);
            }
            for (int i = 0; i < fibers.Count; i++)
                for (int j = i + 1; j < fibers.Count; j++)
                    Assert.AreEqual(1f, Mathf.Abs(HopfFibration.LinkingNumber(fibers[i], fibers[j])), .02f,
                                    "fibers " + i + "," + j);
        }

        [Test]
        public void TubeAroundAFiberIsATorus()
        {
            var pts = new List<Vector3>();
            HopfFibration.Fiber(HopfFibration.Spherical(1.0f, .4f), 96, Rotor4.identity, 0, 1e-3f, pts, null);
            var b = new WireMeshBuilder();
            CurveSweep.Tube(b, pts, true, 8, .05f, null, new List<Vector3>(), new List<Vector3>(), new List<Vector3>());
            var t = TopologyReport.Measure(b, 1e-5f);
            Assert.AreEqual(0, t.EulerCharacteristic, t.ToString());
            Assert.IsTrue(t.IsClosed && t.IsOrientable && t.IsManifold, t.ToString());
        }

        [Test]
        public void ProjectionNeverEmitsNaNEvenThroughThePole()
        {
            // The fiber over the south pole passes exactly through the projection pole.
            var pts = new List<Vector3>();
            HopfFibration.Fiber(new Vector3(0, 0, -1), 128, Rotor4.identity, 0, 1e-3f, pts, null);
            foreach (var p in pts) Assert.IsTrue(TopologyReport.IsFinite(p), p.ToString());
        }
    }
}
