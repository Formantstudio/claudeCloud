using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class DualContouringTests
    {
        struct Sphere : IScalarField
        {
            public float r;
            public float Sample(Vector3 p) => p.magnitude - r;
        }

        /// <summary>A box, rotated so its faces and corners never line up with the grid.</summary>
        struct RotatedBox : IScalarField
        {
            public Vector3 half;
            public Quaternion inverse;
            public float Sample(Vector3 p)
            {
                Vector3 q = inverse * p;
                Vector3 d = new Vector3(Mathf.Abs(q.x) - half.x, Mathf.Abs(q.y) - half.y, Mathf.Abs(q.z) - half.z);
                Vector3 m = Vector3.Max(d, Vector3.zero);
                return m.magnitude + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0f);
            }
        }

        struct Gyroid : IScalarField
        {
            public ImplicitSettings s;
            public float Sample(Vector3 p) => Implicits.Field(s, p, 0f);
        }

        static Quaternion BoxRotation => Quaternion.AngleAxis(27f, new Vector3(.3f, 1f, .2f));

        [Test]
        public void SphereIsClosedOrientableAndOutward()
        {
            var dc = new DualContouring { resolution = 24, extent = 1f };
            var b = new WireMeshBuilder();
            dc.Extract(new Sphere { r = .7f }, b);
            var t = TopologyReport.Measure(b, 1e-6f);
            Assert.AreEqual(2, t.EulerCharacteristic, t.ToString());
            Assert.IsTrue(t.IsClosed && t.IsOrientable && t.IsManifold, t.ToString());

            int outward = 0, faces = b.TriangleCount;
            for (int i = 0; i < b.vertices.Count; i += 3)
            {
                Vector3 c = (b.vertices[i] + b.vertices[i + 1] + b.vertices[i + 2]) / 3f;
                if (Vector3.Dot(b.normals[i], c) > 0f) outward++;
            }
            Assert.Greater(outward, faces * 99 / 100, "faces must point outward");
            foreach (var v in b.vertices) Assert.AreEqual(.7f, v.magnitude, .01f);
        }

        [Test]
        public void SharpCornersSurviveWhereSurfaceNetsRoundsThem()
        {
            var box = new RotatedBox { half = new Vector3(.5f, .38f, .44f), inverse = Quaternion.AngleAxis(-27f, new Vector3(.3f, 1f, .2f)) };
            float Worst(bool qef)
            {
                var dc = new DualContouring { resolution = 20, extent = 1f, solveQef = qef };
                var b = new WireMeshBuilder();
                dc.Extract(box, b);
                // For each of the 8 true corners, the nearest output vertex.
                float worst = 0f;
                for (int k = 0; k < 8; k++)
                {
                    var corner = BoxRotation * new Vector3((k & 1) == 0 ? -.5f : .5f, (k & 2) == 0 ? -.38f : .38f, (k & 4) == 0 ? -.44f : .44f);
                    float best = float.MaxValue;
                    foreach (var v in b.vertices) best = Mathf.Min(best, (v - corner).magnitude);
                    worst = Mathf.Max(worst, best);
                }
                return worst;
            }
            float step = 2f / 20f;
            float dcError = Worst(true), netsError = Worst(false);
            Assert.Less(dcError, .02f * step * 10f, "dual contouring corner error " + dcError);
            Assert.Greater(netsError, 3f * dcError, "surface nets should round corners: " + netsError + " vs " + dcError);
        }

        [Test]
        public void BoxVerticesLieOnTheSurface()
        {
            var box = new RotatedBox { half = new Vector3(.5f, .38f, .44f), inverse = Quaternion.AngleAxis(-27f, new Vector3(.3f, 1f, .2f)) };
            var dc = new DualContouring { resolution = 20, extent = 1f };
            var b = new WireMeshBuilder();
            dc.Extract(box, b);
            // Every vertex within 3% of a cell of the true surface. (Before the Newton guard,
            // cells built on grid corners lying exactly on a face put vertices 14% of a cell off.)
            float worst = 0f;
            foreach (var v in b.vertices) worst = Mathf.Max(worst, Mathf.Abs(box.Sample(v)));
            Assert.Less(worst, .03f * dc.Step);
            var t = TopologyReport.Measure(b, 1e-6f);
            Assert.AreEqual(2, t.EulerCharacteristic, t.ToString());
        }

        [Test]
        public void PseudoInverseSolvesFullRankAndIgnoresNullDirections()
        {
            var a = new double[3, 3]; var v = new double[3, 3];
            // Full rank: M = [[4,1,0],[1,3,1],[0,1,2]], x = (1, -2, 3) -> r = M x.
            DualContouring.PseudoInverseSolve(4, 1, 0, 3, 1, 2, 4 * 1 + 1 * -2, 1 * 1 + 3 * -2 + 1 * 3, -2 + 2 * 3, 1e-6, a, v,
                                              out double x0, out double x1, out double x2);
            Assert.AreEqual(1, x0, 1e-9); Assert.AreEqual(-2, x1, 1e-9); Assert.AreEqual(3, x2, 1e-9);
            // Rank 1 (one plane, normal z): only z is constrained.
            DualContouring.PseudoInverseSolve(0, 0, 0, 0, 0, 1, 0, 0, .5, .05, a, v, out x0, out x1, out x2);
            Assert.AreEqual(0, x0, 1e-12); Assert.AreEqual(0, x1, 1e-12); Assert.AreEqual(.5, x2, 1e-12);
        }

        [Test]
        public void GyroidExtractsFiniteAndOnTheLevelSet()
        {
            var g = new Gyroid { s = new ImplicitSettings { shape = ImplicitShape.Gyroid, frequency = 1.5f } };
            var dc = new DualContouring { resolution = 24, extent = 1f };
            var b = new WireMeshBuilder();
            dc.Extract(g, b);
            var t = TopologyReport.Measure(b, 1e-6f);
            Assert.Greater(t.faces, 1000);
            Assert.AreEqual(0, t.nonFinite);
            Assert.IsTrue(t.IsOrientable, t.ToString());
            float worst = 0f;
            foreach (var v in b.vertices) worst = Mathf.Max(worst, Mathf.Abs(g.Sample(v)));
            Assert.Less(worst, .05f);
        }
    }
}
