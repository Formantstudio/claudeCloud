using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class AttractorTests
    {
        static readonly Attractor[] Flows =
            { Attractor.Lorenz, Attractor.Rossler, Attractor.Thomas, Attractor.Halvorsen, Attractor.Aizawa };

        static Vector3 Run(Attractor s, Vector3 seed, double h, double time)
        {
            var p = AttractorParameters.Defaults(s);
            double x = seed.x, y = seed.y, z = seed.z;
            int n = (int)System.Math.Round(time / h);
            for (int i = 0; i < n; i++) StrangeAttractors.Rk4Step(s, p, ref x, ref y, ref z, h);
            return new Vector3((float)x, (float)y, (float)z);
        }

        [Test]
        public void Rk4IsFourthOrder()
        {
            // Lorenz from a point on the attractor; error against a very fine reference must drop
            // by ~2^4 = 16 each time h halves.
            var seed = new Vector3(-5.76f, -7.6f, 20.4f);
            var reference = Run(Attractor.Lorenz, seed, .02 / 64, .5);
            float e1 = (Run(Attractor.Lorenz, seed, .02, .5) - reference).magnitude;
            float e2 = (Run(Attractor.Lorenz, seed, .01, .5) - reference).magnitude;
            float e3 = (Run(Attractor.Lorenz, seed, .005, .5) - reference).magnitude;
            Assert.That(e1 / e2, Is.InRange(11f, 22f), "e1/e2 = " + e1 / e2);
            Assert.That(e2 / e3, Is.InRange(11f, 22f), "e2/e3 = " + e2 / e3);
        }

        [Test]
        public void AdaptiveAgreesWithFineFixedStep()
        {
            var seed = new Vector3(-5.76f, -7.6f, 20.4f);
            var pts = new List<Vector3>();
            StrangeAttractors.IntegrateAdaptive(Attractor.Lorenz, AttractorParameters.Defaults(Attractor.Lorenz), seed,
                                                0, 1.0, .05, 1e-9, pts);
            Assert.AreEqual(21, pts.Count, "samples at t = 0, 0.05, ..., 1.0");
            Assert.Less((pts[0] - seed).magnitude, 1e-6f);
            var reference = Run(Attractor.Lorenz, seed, 1e-4, 1.0);
            Assert.Less((pts[20] - reference).magnitude, 2e-3f, pts[20] + " vs " + reference);
        }

        [Test]
        public void AdaptiveSpendsFewerStepsAtLooserTolerance()
        {
            var p = AttractorParameters.Defaults(Attractor.Rossler);
            var pts = new List<Vector3>();
            int tight = StrangeAttractors.IntegrateAdaptive(Attractor.Rossler, p, new Vector3(1, 1, 0), 0, 50, .5, 1e-10, pts);
            int loose = StrangeAttractors.IntegrateAdaptive(Attractor.Rossler, p, new Vector3(1, 1, 0), 0, 50, .5, 1e-5, pts);
            Assert.Less(loose, tight);
        }

        [Test]
        public void EveryFlowStaysFiniteAndBoundedOnItsAttractor()
        {
            var pts = new List<Vector3>();
            foreach (var s in Flows)
            {
                StrangeAttractors.Integrate(s, AttractorParameters.Defaults(s), StrangeAttractors.Seed(s),
                                            .005f, 4, 4000, 4000, pts);
                Assert.AreEqual(4000, pts.Count, s + " left the finite range");
                StrangeAttractors.Frame(s, out var centre, out float half);
                foreach (var q in pts)
                    Assert.Less((q - centre).magnitude, 3f * half, s + " escaped: " + q);
            }
        }

        [Test]
        public void LorenzSpendsItsTimeAroundZ23()
        {
            // The long-run mean of z on the classic Lorenz attractor is about 23.5.
            var pts = new List<Vector3>();
            StrangeAttractors.Integrate(Attractor.Lorenz, AttractorParameters.Defaults(Attractor.Lorenz),
                                        StrangeAttractors.Seed(Attractor.Lorenz), .005f, 2, 5000, 40000, pts);
            float sum = 0; foreach (var q in pts) sum += q.z;
            Assert.That(sum / pts.Count, Is.InRange(22f, 25f));
        }

        [Test]
        public void MapsStayInTheirTrappingSquare()
        {
            var pts = new List<Vector3>();
            foreach (var s in new[] { Attractor.Clifford, Attractor.DeJong })
            {
                var p = AttractorParameters.Defaults(s);
                StrangeAttractors.Iterate(s, p, StrangeAttractors.Seed(s), 100, 20000, pts);
                // Clifford: |x| <= 1 + |c|, |y| <= 1 + |d|. De Jong: both <= 2.
                float bx = s == Attractor.Clifford ? 1 + Mathf.Abs(p.c) : 2f, by = s == Attractor.Clifford ? 1 + Mathf.Abs(p.d) : 2f;
                foreach (var q in pts)
                {
                    Assert.LessOrEqual(Mathf.Abs(q.x), bx + 1e-5f);
                    Assert.LessOrEqual(Mathf.Abs(q.y), by + 1e-5f);
                }
            }
        }

        [Test]
        public void TrailTubeIsAnOpenOrientableCylinder()
        {
            var raw = new List<Vector3>();
            StrangeAttractors.Integrate(Attractor.Thomas, AttractorParameters.Defaults(Attractor.Thomas),
                                        StrangeAttractors.Seed(Attractor.Thomas), .02f, 4, 2000, 1500, raw);
            var even = new List<Vector3>();
            CurveSweep.ResampleByArcLength(raw, 600, even);
            Assert.AreEqual(600, even.Count);
            var b = new WireMeshBuilder();
            CurveSweep.Tube(b, even, false, 6, .01f, null, new List<Vector3>(), new List<Vector3>(), new List<Vector3>());
            var t = TopologyReport.Measure(b, 1e-6f);
            Assert.AreEqual(0, t.EulerCharacteristic, t.ToString());
            Assert.AreEqual(2, t.boundaryLoops, t.ToString());
            Assert.IsTrue(t.IsOrientable && t.nonFinite == 0, t.ToString());
        }

        [Test]
        public void ParallelTransportFramesStayOrthonormal()
        {
            var raw = new List<Vector3>();
            StrangeAttractors.Integrate(Attractor.Aizawa, AttractorParameters.Defaults(Attractor.Aizawa),
                                        StrangeAttractors.Seed(Attractor.Aizawa), .01f, 2, 3000, 2000, raw);
            var tangents = new List<Vector3>(); var normals = new List<Vector3>();
            CurveSweep.Frames(raw, false, tangents, normals);
            for (int i = 0; i < raw.Count; i++)
            {
                Assert.AreEqual(1f, normals[i].magnitude, 1e-4f);
                Assert.AreEqual(0f, Vector3.Dot(normals[i], tangents[i]), 1e-4f);
            }
        }
    }
}
