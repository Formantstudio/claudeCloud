using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class EnneperPillarTests
    {
        static EnneperPillar.Shape Make(PillarFootprint footprint, Vector2 spacing) => new EnneperPillar.Shape
        {
            size = 3.8f, waist = .16f, halfHeight = .9f, endFlare = 2.1f, fluting = .1f,
            twist = 25f * Mathf.Deg2Rad, footprint = footprint,
            cellHalf = EnneperPillar.CellHalf(footprint, spacing)
        };

        [Test]
        public void RoundReproducesTheOriginalPillar()
        {
            var s = Make(PillarFootprint.Round, Vector2.one);
            float phase = .7f;
            for (int i = 0; i <= 10; i++)
            for (int j = 0; j <= 10; j++)
            {
                float u = i / 10f, v = j / 10f;
                // The formula EnneperFoldReality.ClosePillar used before this pass.
                float t = v * 2f - 1f, theta = u * Mathf.PI * 2f, twist = s.twist * t;
                float radial = s.size * (s.waist + s.endFlare * Mathf.Pow(Mathf.Abs(t), 4f));
                radial *= 1f + s.fluting * Mathf.Cos(4f * theta + phase + twist);
                var old = new Vector3(radial * Mathf.Cos(theta + twist), s.size * s.halfHeight * Mathf.Sin(t * Mathf.PI * .5f),
                                      radial * Mathf.Sin(theta + twist));
                Assert.Less((old - EnneperPillar.Point(s, u, v, phase)).magnitude, 1e-4f);
            }
        }

        [TestCase(PillarFootprint.Round)]
        [TestCase(PillarFootprint.Square)]
        [TestCase(PillarFootprint.Hexagonal)]
        public void SeamClosesAndEndsAreFlatAndHorizontal(PillarFootprint footprint)
        {
            var s = Make(footprint, new Vector2(5f, 4f));
            float h = s.size * s.halfHeight;
            for (int j = 0; j <= 20; j++)
            {
                float v = j / 20f;
                Assert.Less((EnneperPillar.Point(s, 0f, v, .3f) - EnneperPillar.Point(s, 1f, v, .3f)).magnitude, 1e-4f, "seam at v " + v);
            }
            for (int i = 0; i <= 48; i++)
            {
                float u = i / 48f;
                Assert.AreEqual(-h, EnneperPillar.Point(s, u, 0f, .3f).y, 1e-4f);
                Assert.AreEqual(h, EnneperPillar.Point(s, u, 1f, .3f).y, 1e-4f);
                // Arrives horizontally: the height stops changing before the ring does.
                Vector3 a = EnneperPillar.Point(s, u, 1f, .3f), b = EnneperPillar.Point(s, u, 1f - 1e-3f, .3f);
                Assert.Less(Mathf.Abs(a.y - b.y), 1e-3f * Mathf.Max((a - b).magnitude, 1e-6f) * 50f);
            }
        }

        [Test]
        public void SquareEndsLieOnTheCellAndHitItsCorners()
        {
            var spacing = new Vector2(5f, 4f);
            var s = Make(PillarFootprint.Square, spacing);
            var half = s.cellHalf;
            for (int i = 0; i <= 96; i++)
            {
                foreach (float v in new[] { 0f, 1f })
                {
                    var p = EnneperPillar.Point(s, i / 96f, v, .9f);
                    Assert.AreEqual(1f, Mathf.Max(Mathf.Abs(p.x) / half.x, Mathf.Abs(p.z) / half.y), 1e-4f, "on the cell boundary");
                }
            }
            int columns = EnneperPillar.SnapColumns(180);
            Assert.AreEqual(0, columns % 24);
            for (int k = 0; k < 4; k++)
            {
                int c = (2 * k + 1) * columns / 8;
                var p = EnneperPillar.Point(s, (float)c / columns, 1f, 0f);
                Assert.AreEqual(half.x, Mathf.Abs(p.x), 1e-4f); Assert.AreEqual(half.y, Mathf.Abs(p.z), 1e-4f);
            }
        }

        [Test]
        public void NeighbouringSquarePillarsShareTheirEdge()
        {
            var spacing = new Vector2(5f, 4f);
            var s = Make(PillarFootprint.Square, spacing);
            Vector3 b = EnneperPillar.LatticeCentre(PillarFootprint.Square, 1, 0, spacing);
            int n = 0;
            for (int i = 0; i <= 240; i++)
            {
                // A's ceiling points on its +x edge lie on B's ceiling ring (B's −x edge).
                var a = EnneperPillar.Point(s, i / 240f, 1f, 0f);
                if (Mathf.Abs(a.x - s.cellHalf.x) > 1e-4f) continue;
                n++;
                Vector3 local = a - b;
                Assert.AreEqual(-s.cellHalf.x, local.x, 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(local.z), s.cellHalf.y + 1e-4f);
            }
            Assert.Greater(n, 20);
        }

        [Test]
        public void HexEndsLieOnTheHexagonAndTileTheLattice()
        {
            var spacing = new Vector2(6f, 6f);
            var s = Make(PillarFootprint.Hexagonal, spacing);
            float apothem = s.cellHalf.x;
            for (int i = 0; i <= 120; i++)
            {
                var p = EnneperPillar.Point(s, i / 120f, 0f, .2f);
                // Regular hexagon with edge normals at 0°, 60°, 120°: max over normals of p·n = apothem.
                float m = 0f;
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    m = Mathf.Max(m, p.x * Mathf.Cos(a) + p.z * Mathf.Sin(a));
                }
                Assert.AreEqual(apothem, m, 1e-4f);
            }
            // Six neighbours, all at distance 2·apothem.
            var c = EnneperPillar.LatticeCentre(PillarFootprint.Hexagonal, 0, 0, spacing);
            foreach (var (col, row) in new[] { (1, 0), (-1, 0), (0, 1), (-1, 1), (0, -1), (-1, -1) })
                Assert.AreEqual(2f * apothem, (EnneperPillar.LatticeCentre(PillarFootprint.Hexagonal, col, row, spacing) - c).magnitude, 1e-4f);
        }

        [Test]
        public void WaistIsUntouchedByTheFootprint()
        {
            // At the waist (v = ½) the lattice footprints leave the round waist exactly as Round has it.
            var round = Make(PillarFootprint.Round, Vector2.one * 5f);
            var square = Make(PillarFootprint.Square, Vector2.one * 5f);
            for (int i = 0; i <= 24; i++)
                Assert.Less((EnneperPillar.Point(round, i / 24f, .5f, .4f) - EnneperPillar.Point(square, i / 24f, .5f, .4f)).magnitude, 1e-4f);
        }
    }
}
