using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class WireMeshTests
    {
        static List<Vector3> SampleGrid(int cols, int rows, System.Func<float, float, Vector3> f)
        {
            var pts = new List<Vector3>();
            for (int r = 0; r <= rows; r++)
                for (int c = 0; c <= cols; c++)
                    pts.Add(f((float)c / cols, (float)r / rows));
            return pts;
        }

        [Test]
        public void TorusGridIsClosedOrientableGenusOne()
        {
            var b = new WireMeshBuilder();
            b.Grid(SampleGrid(24, 16, (u, v) =>
            {
                float a = u * 2 * Mathf.PI, t = v * 2 * Mathf.PI, r = 2 + .6f * Mathf.Cos(t);
                return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), .6f * Mathf.Sin(t));
            }), 24, 16);
            var t = TopologyReport.Measure(b);
            Assert.AreEqual(0, t.EulerCharacteristic, t.ToString());
            Assert.IsTrue(t.IsClosed && t.IsManifold && t.IsOrientable, t.ToString());
            Assert.AreEqual(1, t.components);
        }

        [Test]
        public void SphereGridWithPolesHasChiTwo()
        {
            var b = new WireMeshBuilder();
            b.Grid(SampleGrid(20, 12, (u, v) =>
            {
                float a = u * 2 * Mathf.PI, th = v * Mathf.PI;
                return new Vector3(Mathf.Sin(th) * Mathf.Cos(a), Mathf.Sin(th) * Mathf.Sin(a), Mathf.Cos(th));
            }), 20, 12);
            var t = TopologyReport.Measure(b);
            Assert.AreEqual(2, t.EulerCharacteristic, t.ToString());
            Assert.IsTrue(t.IsClosed && t.IsOrientable, t.ToString());
        }

        [Test]
        public void FlatPatchIsADiskWithOneBoundaryLoop()
        {
            var b = new WireMeshBuilder();
            b.Grid(SampleGrid(5, 4, (u, v) => new Vector3(u, v, 0)), 5, 4);
            var t = TopologyReport.Measure(b);
            Assert.AreEqual(1, t.EulerCharacteristic);
            Assert.AreEqual(1, t.boundaryLoops);
            Assert.AreEqual(18, t.boundaryEdges);
        }

        [Test]
        public void MobiusStripIsCaughtAsNonOrientable()
        {
            var b = new WireMeshBuilder();
            b.Grid(SampleGrid(40, 4, (u, v) =>
            {
                float a = u * 2 * Mathf.PI, h = v - .5f, r = 2 + h * Mathf.Cos(a * .5f);
                return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), h * Mathf.Sin(a * .5f));
            }), 40, 4);
            var t = TopologyReport.Measure(b);
            Assert.AreEqual(0, t.EulerCharacteristic, t.ToString());
            Assert.IsFalse(t.IsOrientable, t.ToString());
            Assert.AreEqual(1, t.boundaryLoops, "a Möbius strip has one boundary circle");
        }

        [Test]
        public void HiddenDiagonalsLiftTheVanishingComponent()
        {
            var b = new WireMeshBuilder { hideQuadDiagonals = true };
            b.Quad(Vector3.zero, Vector3.right, new Vector3(1, 1, 0), Vector3.up);
            // Along a-c the second component of triangle 1 must never reach 0.
            Assert.GreaterOrEqual(b.bary[0].y, 1f);
            Assert.GreaterOrEqual(b.bary[2].y, 1f);
            Assert.GreaterOrEqual(b.bary[3].z, 1f);
            Assert.GreaterOrEqual(b.bary[4].z, 1f);
            var mesh = new Mesh();
            b.Apply(mesh);
            Assert.AreEqual(UnityEngine.Rendering.IndexFormat.UInt32, mesh.indexFormat);
            Assert.GreaterOrEqual(mesh.bounds.size.x, 200f);
        }
    }
}
