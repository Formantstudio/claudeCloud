using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class SeifertSurfaceTests
    {
        static TopologyReport Build(int[] word, int strands)
        {
            var b = new WireMeshBuilder();
            SeifertSurface.Build(b, word, strands, SeifertSurface.Shape.Default, new List<Vector3>());
            return TopologyReport.Measure(b, 1e-4f);
        }

        // preset, expected chi, components, genus
        [TestCase(SeifertPreset.Trefoil, -1, 1, 1)]
        [TestCase(SeifertPreset.FigureEight, -1, 1, 1)]
        [TestCase(SeifertPreset.Cinquefoil, -3, 1, 2)]
        [TestCase(SeifertPreset.HopfLink, 0, 2, 0)]
        [TestCase(SeifertPreset.Borromean, -3, 3, 1)]
        public void PresetsHaveTheRightTopology(SeifertPreset preset, int chi, int components, int genus)
        {
            var word = SeifertSurface.Word(preset, 0, 0, null, out int strands);
            Assert.AreEqual(chi, SeifertSurface.EulerCharacteristic(word, strands));
            Assert.AreEqual(components, SeifertSurface.Components(word, strands));
            Assert.AreEqual(genus, SeifertSurface.Genus(word, strands));

            var t = Build(word, strands);
            Assert.AreEqual(chi, t.EulerCharacteristic, t.ToString());
            Assert.AreEqual(components, t.boundaryLoops, "boundary is the link: " + t);
            Assert.IsTrue(t.IsOrientable, "a Seifert surface is orientable: " + t);
            Assert.IsTrue(t.IsManifold, t.ToString());
            Assert.AreEqual(1, t.components, "connected: " + t);
            Assert.AreEqual(0, t.nonFinite);
        }

        [TestCase(2, 3)]
        [TestCase(3, 4)]
        [TestCase(3, 5)]
        [TestCase(4, 5)]
        [TestCase(2, 7)]
        public void TorusKnotGenusIsHalfOfPMinusOneQMinusOne(int p, int q)
        {
            var word = SeifertSurface.Word(SeifertPreset.TorusKnot, p, q, null, out int strands);
            Assert.AreEqual(1, SeifertSurface.Components(word, strands));
            // The braid Seifert surface of T(p,q) is minimal genus: (p − 1)(q − 1) / 2.
            Assert.AreEqual((p - 1) * (q - 1) / 2, SeifertSurface.Genus(word, strands));
            var t = Build(word, strands);
            Assert.AreEqual(1 - 2 * ((p - 1) * (q - 1) / 2), t.EulerCharacteristic, t.ToString());
            Assert.IsTrue(t.IsOrientable && t.IsManifold, t.ToString());
            Assert.AreEqual(1, t.boundaryLoops);
        }

        [Test]
        public void ParsesNumbersAndLetters()
        {
            var a = SeifertSurface.Parse("1 -2 1 -2", out int sa);
            var b = SeifertSurface.Parse("aBaB", out int sb);
            CollectionAssert.AreEqual(new[] { 1, -2, 1, -2 }, a);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(3, sa); Assert.AreEqual(3, sb);
        }

        [Test]
        public void UnlinkedStrandsGiveSeparateDisks()
        {
            // The empty braid on 2 strands is the 2-component unlink: two disks.
            var t = Build(new int[0], 2);
            Assert.AreEqual(2, t.EulerCharacteristic);
            Assert.AreEqual(2, t.components);
            Assert.AreEqual(2, t.boundaryLoops);
        }
    }
}
