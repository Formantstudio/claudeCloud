using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>What a pillar's flared floor and ceiling ends open out to.</summary>
    public enum PillarFootprint
    {
        /// <summary>Round in a single pillar; the lattice cell (square) when pillars stand in a hall.</summary>
        Auto,
        /// <summary>A circle of radius waist + end flare. Neighbouring flares overlap freely.</summary>
        Round,
        /// <summary>The rectangular lattice cell: neighbouring ends meet edge to edge.</summary>
        Square,
        /// <summary>The hexagonal lattice cell: neighbouring ends meet edge to edge.</summary>
        Hexagonal
    }

    /// <summary>
    /// The Enneper pillar: the closed form the Enneper fold is deformed into at full
    /// <c>pillarClosure</c>. A vertical waist whose two ends flare out horizontally into a floor and a
    /// ceiling. This is a designed deformation, not a minimal surface (ENNEPER-REALITY.md).
    ///
    /// u runs around the pillar (θ = 2πu, so u = 0 and u = 1 meet at the seam), v runs from floor to
    /// ceiling (t = 2v − 1 in [−1, 1]). Height is H·sin(πt/2), so dy/dt = 0 at both ends: the
    /// surface meets the floor and ceiling planes tangentially.
    ///
    /// **Lattice footprints.** With a Square or Hexagonal footprint the end rings are the lattice
    /// cell's boundary rather than a circle, and the twist and fluting are anchored to zero at the
    /// ends. Neighbouring pillars therefore share each cell edge exactly, at the same height, and both
    /// arrive there horizontally — the floors and ceilings merge into one continuous C¹ vault across
    /// the hall instead of interpenetrating. Cell corners sit at fixed u — (2k+1)/8 for a square cell,
    /// (2k+1)/12 for a hexagon — so a grid whose (columns − 1) is a multiple of 24 lands a vertex on
    /// every corner (<see cref="SnapColumns"/>). Between corners the end ring runs straight along the
    /// cell edge, so its chords lie exactly on the shared edge whatever the sampling.
    ///
    /// Round reproduces the original pillar exactly: radius waist + flare·t⁴, fluting and twist
    /// carried all the way to the ends.
    /// </summary>
    public static class EnneperPillar
    {
        /// <summary>Per-frame shape, computed once and shared by every vertex of every copy.</summary>
        public struct Shape
        {
            /// <summary>Fold size: the overall scale every length below is a fraction of.</summary>
            public float size;
            public float waist, halfHeight, endFlare, fluting;
            /// <summary>Twist from floor to waist (and waist to ceiling), radians.</summary>
            public float twist;
            /// <summary>Resolved footprint (never Auto).</summary>
            public PillarFootprint footprint;
            /// <summary>Square: half the cell's width (x) and depth (z). Hexagonal: x is the apothem.</summary>
            public Vector2 cellHalf;
        }

        const float Tau = Mathf.PI * 2f;
        static readonly float Cos30 = Mathf.Sqrt(3f) * .5f;

        /// <summary>
        /// Everything about one ring of the pillar (one v) that does not depend on u. The chamber
        /// fills its lattice a row at a time, so a caller can compute this once per row.
        /// </summary>
        public struct Row
        {
            public float v, t, w, keep, y;
            /// <summary>Round: twist angle. Lattice: fluting phase twist.</summary>
            public float twist;
            /// <summary>Lattice: the anchored twist (zero at the ends), as cos/sin.</summary>
            public float anchoredCos, anchoredSin;
            /// <summary>Round: radius before fluting.</summary>
            public float radial;
        }

        public static Row MakeRow(in Shape s, float v)
        {
            var r = new Row { v = v, t = Mathf.Clamp01(v) * 2f - 1f };
            float t2 = r.t * r.t;
            r.w = t2 * t2;                                    // flare weight t⁴: 0 at the waist, 1 at the ends
            r.keep = 1f - r.w;
            r.y = s.size * s.halfHeight * Mathf.Sin(r.t * Mathf.PI * .5f);
            r.twist = s.twist * r.t;
            r.radial = s.size * (s.waist + s.endFlare * r.w);
            float anchored = r.twist * r.keep;
            r.anchoredCos = Mathf.Cos(anchored);
            r.anchoredSin = Mathf.Sin(anchored);
            return r;
        }

        /// <summary>
        /// The pillar point in arrangement space (y up, footprint in xz). <paramref name="flutePhase"/>
        /// is the copy's own fold phase, so the flutes turn with the Enneper surface they belong to.
        /// </summary>
        public static Vector3 Point(in Shape s, float u, float v, float flutePhase)
        {
            var row = MakeRow(s, v);
            return Point(s, row, u, flutePhase);
        }

        /// <summary><see cref="Point(in Shape, float, float, float)"/> with the row precomputed.</summary>
        public static Vector3 Point(in Shape s, in Row row, float u, float flutePhase)
        {
            float theta = Mathf.Clamp01(u) * Tau;

            if (s.footprint == PillarFootprint.Round || s.footprint == PillarFootprint.Auto)
            {
                float radial = row.radial * (1f + s.fluting * Mathf.Cos(4f * theta + flutePhase + row.twist));
                return new Vector3(radial * Mathf.Cos(theta + row.twist), row.y, radial * Mathf.Sin(theta + row.twist));
            }

            // Lattice footprints: blend the round waist into the cell boundary, with twist and
            // fluting faded out by the same weight so the ends sit exactly on the cell.
            float flute = 1f + s.fluting * row.keep * Mathf.Cos(4f * theta + flutePhase + row.twist);
            float waistR = s.size * s.waist * flute;
            float wx = waistR * Mathf.Cos(theta), wz = waistR * Mathf.Sin(theta);
            Vector2 end = s.footprint == PillarFootprint.Hexagonal ? HexBoundary(u, s.cellHalf.x) : RectBoundary(u, s.cellHalf);
            float hx = wx + (end.x - wx) * row.w, hz = wz + (end.y - wz) * row.w;
            return new Vector3(hx * row.anchoredCos - hz * row.anchoredSin, row.y, hx * row.anchoredSin + hz * row.anchoredCos);
        }

        /// <summary>
        /// Point on a rectangle's boundary at parameter u: corners at u = (2k+1)/8, straight between,
        /// u = 0 at the middle of the +x edge (matching θ = 0 on the waist).
        /// </summary>
        public static Vector2 RectBoundary(float u, Vector2 half)
        {
            float s = Mathf.Repeat(u, 1f) * 4f - .5f;
            int k = Mathf.FloorToInt(s);
            float f = s - k;
            Vector2 a = RectCorner(k, half), b = RectCorner(k + 1, half);
            return a + (b - a) * f;
        }

        static Vector2 RectCorner(int k, Vector2 h)
        {
            switch (((k % 4) + 4) % 4)
            {
                case 0: return new Vector2(h.x, h.y);
                case 1: return new Vector2(-h.x, h.y);
                case 2: return new Vector2(-h.x, -h.y);
                default: return new Vector2(h.x, -h.y);
            }
        }

        /// <summary>
        /// Point on a regular hexagon (apothem <paramref name="apothem"/>) with corners at
        /// 30° + 60°k, i.e. u = (2k+1)/12, and flat edges facing ±x.
        /// </summary>
        public static Vector2 HexBoundary(float u, float apothem)
        {
            float s = Mathf.Repeat(u, 1f) * 6f - .5f;
            int k = Mathf.FloorToInt(s);
            float f = s - k;
            float r = apothem / Cos30;
            float a0 = (k + .5f) * (Tau / 6f), a1 = a0 + Tau / 6f;
            var p = new Vector2(r * Mathf.Cos(a0), r * Mathf.Sin(a0));
            var q = new Vector2(r * Mathf.Cos(a1), r * Mathf.Sin(a1));
            return p + (q - p) * f;
        }

        /// <summary>
        /// Largest column count ≤ <paramref name="columns"/> with (count − 1) divisible by 24, so
        /// square (eighths) and hexagonal (twelfths) cell corners both land on grid vertices.
        /// </summary>
        public static int SnapColumns(int columns) => Mathf.Max(25, ((columns - 1) / 24) * 24 + 1);

        /// <summary>
        /// Centre of lattice slot (column, row) for a hall. Square: a rectangular grid of
        /// <paramref name="spacing"/>. Hexagonal: rows √3/2·d apart, odd rows offset by half a cell,
        /// so every pillar has six neighbours at distance d = <paramref name="spacing"/>.x.
        /// </summary>
        public static Vector3 LatticeCentre(PillarFootprint footprint, int column, int row, Vector2 spacing)
        {
            if (footprint == PillarFootprint.Hexagonal)
            {
                float d = spacing.x;
                return new Vector3((column + ((row & 1) != 0 ? .5f : 0f)) * d, 0f, row * d * Cos30);
            }
            return new Vector3(column * spacing.x, 0f, row * spacing.y);
        }

        /// <summary>Half-extent of the lattice cell for a spacing: what the end rings open out to.</summary>
        public static Vector2 CellHalf(PillarFootprint footprint, Vector2 spacing) =>
            footprint == PillarFootprint.Hexagonal ? new Vector2(spacing.x * .5f, spacing.x * .5f) : spacing * .5f;
    }
}
