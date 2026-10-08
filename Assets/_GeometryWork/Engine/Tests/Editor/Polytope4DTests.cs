using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class Polytope4DTests
    {
        [TestCase(RegularPolytope4D.Cell5, 5, 10, 10, 5, 4)]
        [TestCase(RegularPolytope4D.Cell8, 16, 32, 24, 8, 4)]
        [TestCase(RegularPolytope4D.Cell16, 8, 24, 32, 16, 6)]
        [TestCase(RegularPolytope4D.Cell24, 24, 96, 96, 24, 8)]
        [TestCase(RegularPolytope4D.Cell120, 600, 1200, 720, 120, 4)]
        [TestCase(RegularPolytope4D.Cell600, 120, 720, 1200, 600, 12)]
        public void CountsMatchTheRegularPolytope(RegularPolytope4D kind, int v, int e, int f, int c, int degree)
        {
            var p = Polytope4DLibrary.Get(kind);
            Assert.AreEqual(v, p.vertices.Length, "vertices");
            Assert.AreEqual(e, p.edges.Length, "edges");
            Assert.AreEqual(f, p.faces.Length, "2-faces");
            Assert.AreEqual(c, p.CellCount, "cells via chi(S^3) = 0");
            Assert.AreEqual(degree, p.Degree, "vertex degree");
        }

        [Test]
        public void EveryVertexIsOnTheUnitSphereAndEveryEdgeHasOneLength()
        {
            foreach (RegularPolytope4D kind in System.Enum.GetValues(typeof(RegularPolytope4D)))
            {
                var p = Polytope4DLibrary.Get(kind);
                foreach (var v in p.vertices) Assert.AreEqual(1f, v.magnitude, 1e-5f, kind.ToString());
                foreach (var e in p.edges)
                    Assert.AreEqual(p.edgeLength, (p.vertices[e.x] - p.vertices[e.y]).magnitude, 1e-4f, kind.ToString());
                // Regular: every vertex has the same degree.
                var deg = new int[p.vertices.Length];
                foreach (var e in p.edges) { deg[e.x]++; deg[e.y]++; }
                foreach (int d in deg) Assert.AreEqual(p.Degree, d, kind.ToString());
            }
        }

        [Test]
        public void KnownEdgeLengthsOnTheUnitSphere()
        {
            float phi = (1 + Mathf.Sqrt(5)) / 2;
            Assert.AreEqual(1f / phi, Polytope4DLibrary.Get(RegularPolytope4D.Cell600).edgeLength, 1e-5f);
            Assert.AreEqual(1f, Polytope4DLibrary.Get(RegularPolytope4D.Cell8).edgeLength, 1e-5f);
            Assert.AreEqual(Mathf.Sqrt(2), Polytope4DLibrary.Get(RegularPolytope4D.Cell16).edgeLength, 1e-5f);
            Assert.AreEqual(1f, Polytope4DLibrary.Get(RegularPolytope4D.Cell24).edgeLength, 1e-5f);
            // 120-cell edge on the unit sphere: 1 / (sqrt(2) phi^2).
            Assert.AreEqual(1f / (Mathf.Sqrt(2) * phi * phi), Polytope4DLibrary.Get(RegularPolytope4D.Cell120).edgeLength, 1e-5f);
        }

        [Test]
        public void SixHundredCellIsCentrallySymmetricAndRotationInvariant()
        {
            var p = Polytope4DLibrary.Get(RegularPolytope4D.Cell600);
            // Left multiplication by a vertex of the 600-cell (a unit icosian) permutes the
            // vertex set: the 120 vertices are the binary icosahedral group.
            var r = new Rotor4(p.vertices[37], new Vector4(0, 0, 0, 1));
            foreach (var v in p.vertices)
            {
                var w = r.Rotate(v);
                float best = float.MaxValue;
                foreach (var u in p.vertices) best = Mathf.Min(best, (u - w).sqrMagnitude);
                Assert.Less(best, 1e-8f);
            }
        }

        [Test]
        public void StereographicProjectionIsFiniteEverywhere()
        {
            foreach (RegularPolytope4D kind in System.Enum.GetValues(typeof(RegularPolytope4D)))
                foreach (var v in Polytope4DLibrary.Get(kind).vertices)
                    Assert.IsTrue(TopologyReport.IsFinite(Polytope4DLibrary.Stereographic(v)), kind + " " + v);
        }
    }
}
