using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class ConwayTests
    {
        // Notation, V, E, F: the Platonic seeds, every Archimedean solid, Catalan duals, and the
        // Goldberg-style operators on a few seeds.
        static readonly object[] Known =
        {
            new object[] { "T", 4, 6, 4 }, new object[] { "C", 8, 12, 6 }, new object[] { "O", 6, 12, 8 },
            new object[] { "D", 20, 30, 12 }, new object[] { "I", 12, 30, 20 },
            new object[] { "tT", 12, 18, 8 }, new object[] { "aC", 12, 24, 14 }, new object[] { "tC", 24, 36, 14 },
            new object[] { "tO", 24, 36, 14 }, new object[] { "eC", 24, 48, 26 }, new object[] { "bC", 48, 72, 26 },
            new object[] { "sC", 24, 60, 38 }, new object[] { "aD", 30, 60, 32 }, new object[] { "tD", 60, 90, 32 },
            new object[] { "tI", 60, 90, 32 }, new object[] { "eD", 60, 120, 62 }, new object[] { "bD", 120, 180, 62 },
            new object[] { "sD", 60, 150, 92 },
            new object[] { "jC", 14, 24, 12 }, new object[] { "kC", 14, 36, 24 }, new object[] { "kO", 14, 36, 24 },
            new object[] { "oC", 26, 48, 24 }, new object[] { "mC", 26, 72, 48 }, new object[] { "gC", 38, 60, 24 },
            new object[] { "nC", 14, 36, 24 }, new object[] { "dkdC", 24, 36, 14 },
            new object[] { "cC", 32, 48, 18 }, new object[] { "wC", 56, 84, 30 }, new object[] { "qC", 44, 72, 30 },
            new object[] { "cD", 80, 120, 42 }, new object[] { "gaD", 30 + 32 + 120, 300, 120 },
            new object[] { "P5", 10, 15, 7 }, new object[] { "A5", 10, 20, 12 }, new object[] { "Y5", 6, 10, 6 },
            new object[] { "k4P6", 12 + 6, 18 + 24, 2 + 24 }, new object[] { "t4kC", 32, 60, 30 },
            new object[] { "rgC", 38, 60, 24 },
        };

        [TestCaseSource(nameof(Known))]
        public void CountsMatchTheNamedSolid(string notation, int v, int e, int f)
        {
            var p = Conway.Parse(notation);
            Assert.AreEqual(v, p.VertexCount, p.ToString());
            Assert.AreEqual(e, p.EdgeCount, p.ToString());
            Assert.AreEqual(f, p.FaceCount, p.ToString());
            Assert.AreEqual(2, p.EulerCharacteristic);
            Assert.IsNull(p.Validate(), notation);
            Assert.Greater(p.SignedVolume(), 0f, "faces wind outwards");
        }

        [TestCase("tI", "12×5 20×6")]
        [TestCase("sC", "32×3 6×4")]
        [TestCase("bD", "30×4 20×6 12×10")]
        [TestCase("gC", "24×5")]
        [TestCase("cC", "12×6 6×4")]
        public void FacesHaveTheRightShapes(string notation, string signature)
        {
            var expected = new SortedSet<string>(signature.Split(' '));
            var actual = new SortedSet<string>(Conway.Parse(notation).FaceSignature().Split(' '));
            Assert.AreEqual(string.Join(" ", expected), string.Join(" ", actual));
        }

        [TestCase("tI")]
        [TestCase("gD")]
        [TestCase("wT")]
        [TestCase("qO")]
        [TestCase("A7")]
        public void WireOutputIsAClosedOrientableSphere(string notation)
        {
            var b = new WireMeshBuilder();
            Conway.Parse(notation).Emit(b);
            var t = TopologyReport.Measure(b, 1e-5f);
            Assert.AreEqual(2, t.EulerCharacteristic, t.ToString());
            Assert.IsTrue(t.IsClosed && t.IsManifold && t.IsOrientable, t.ToString());
            Assert.AreEqual(1, t.components);
        }

        [Test]
        public void PolygonDrawsOnlyItsOwnEdges()
        {
            // In each fan triangle the drawn edge is where a bary component reaches 0; that must be
            // exactly the polygon side p_i p_i+1, never a spoke to the centre.
            var b = new WireMeshBuilder();
            var hex = new List<Vector3>();
            for (int i = 0; i < 6; i++) hex.Add(new Vector3(Mathf.Cos(i * Mathf.PI / 3), Mathf.Sin(i * Mathf.PI / 3), 0));
            b.Polygon(hex);
            Assert.AreEqual(18, b.VertexCount);
            for (int t = 0; t < 6; t++)
            {
                Vector3 c = b.bary[t * 3], p0 = b.bary[t * 3 + 1], p1 = b.bary[t * 3 + 2];
                // Component 0 vanishes on p0 p1 (the side) and is 1 at the centre.
                Assert.AreEqual(0f, p0.x); Assert.AreEqual(0f, p1.x); Assert.AreEqual(1f, c.x);
                // Components 1 and 2 never fall below 1, so the spokes never draw.
                foreach (var v in new[] { c, p0, p1 }) { Assert.GreaterOrEqual(v.y, 1f); Assert.GreaterOrEqual(v.z, 1f); }
            }
        }

        [TestCase("tC")]
        [TestCase("tI")]
        [TestCase("eC")]
        [TestCase("bC")]
        [TestCase("sC")]
        [TestCase("aD")]
        [TestCase("sD")]
        [TestCase("bD")]
        public void CanonicalArchimedeanSolidsAreUniform(string notation)
        {
            var p = Conway.Parse(notation, out float error, 100000, 1000);
            Assert.Less(error, 1e-4f, "converged");
            // Uniform: every edge the same length.
            float min = float.MaxValue, max = 0f;
            foreach (var e in p.Edges())
            {
                float len = (p.vertices[e.x] - p.vertices[e.y]).magnitude;
                min = Mathf.Min(min, len); max = Mathf.Max(max, len);
            }
            Assert.Less((max - min) / max, 2e-3f, $"edge lengths {min}..{max}");
            Assert.Less(p.Planarity(), 1e-3f);
            Assert.IsNull(p.Validate());
            Assert.Greater(p.SignedVolume(), 0f);
        }

        [TestCase("cC")]
        [TestCase("wD")]
        [TestCase("gI")]
        [TestCase("qC")]
        public void CanonicalisationPlanarisesAndMakesEdgesTangent(string notation)
        {
            var p = Conway.Parse(notation);
            float before = p.Planarity();
            float error = Conway.Canonicalize(p, 2000, 1e-7f);
            Assert.Less(error, 1e-4f);
            Assert.Less(p.Planarity(), 1e-3f, "planar");
            if (before > 1e-3f) Assert.Less(p.Planarity(), before, "planarity improves");
            // Every edge's closest point to the origin is on the unit sphere.
            Vector3 centre = Vector3.zero;
            int n = 0;
            foreach (var e in p.Edges())
            {
                Vector3 a = p.vertices[e.x], d = p.vertices[e.y] - a;
                Vector3 t = a - d * (Vector3.Dot(a, d) / d.sqrMagnitude);
                Assert.AreEqual(1f, t.magnitude, 1e-3f);
                centre += t; n++;
            }
            Assert.Less((centre / n).magnitude, 1e-3f, "tangent points centred");
        }

        [Test]
        public void SnubsCanonicaliseThroughTheirDuals()
        {
            // Relaxing the finished snub cube from its operator positions wanders off; Canonicalize
            // falls back to relaxing the dual (a gyro, which relaxes well) and reciprocating back.
            var p = Conway.Parse("sC");
            Assert.Less(Conway.Canonicalize(p, 2000, 1e-7f), 1e-4f);
            float min = float.MaxValue, max = 0f;
            foreach (var e in p.Edges())
            {
                float len = (p.vertices[e.x] - p.vertices[e.y]).magnitude;
                min = Mathf.Min(min, len); max = Mathf.Max(max, len);
            }
            Assert.Less((max - min) / max, 2e-3f);
        }

        [Test]
        public void DualOfTheCanonicalFormIsItsReciprocal()
        {
            // Canonical dual pairs share the midsphere, and each edge crosses its dual edge at a right
            // angle at the tangent point. The dual of the canonical tC is the canonical triakis octahedron.
            var p = Conway.Parse("tC");
            Conway.Canonicalize(p, 2000, 1e-7f);
            var d = Conway.Dual(p);
            float error = Conway.Canonicalize(d, 2000, 1e-7f);
            Assert.Less(error, 1e-4f);
            Assert.AreEqual(24, d.FaceCount);
            Assert.IsNull(d.Validate());
        }

        [Test]
        public void NotationReadsRightToLeft()
        {
            // dkd applied to C, step by step, is t.
            var stepwise = Conway.Dual(Conway.Kis(Conway.Dual(Conway.Cube())));
            var parsed = Conway.Parse("tC");
            Assert.AreEqual(stepwise.VertexCount, parsed.VertexCount);
            Assert.AreEqual(stepwise.FaceSignature(), parsed.FaceSignature());
            // ka is not ak.
            Assert.AreNotEqual(Conway.Parse("kaC").VertexCount, Conway.Parse("akC").VertexCount);
        }

        [TestCase("")]
        [TestCase("tX")]
        [TestCase("Ct")]
        [TestCase("P")]
        [TestCase("P2")]
        [TestCase("d3C")]
        [TestCase("t-I")]
        public void BadNotationIsRejected(string notation)
        {
            Assert.Throws<FormatException>(() => Conway.Parse(notation));
        }

        [Test]
        public void RunawayChainsStopAtTheFaceBudget()
        {
            Assert.Throws<InvalidOperationException>(() => Conway.Parse("ggggggI", 50000));
        }
    }
}
