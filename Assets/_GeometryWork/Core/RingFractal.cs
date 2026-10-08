using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Actual recursive constructions for the ring tunnel, replacing the earlier sum-of-cosines.
    ///
    /// A cosine series with gain*lacunarity > 1 is fractal in the limit (Weierstrass-Mandelbrot),
    /// but at low depth and low amplitude it just reads as a wobble. These are built from real
    /// recursions instead, so the self-similarity is visible at the depths we can afford:
    ///
    /// - <see cref="CantorPosition"/>  : ring spacing from an IFS, so rings cluster into groups
    ///                                  of groups. This is the accordion that genuinely subdivides.
    /// - <see cref="KochRadius"/>      : piecewise-linear recursive teeth. Sharp, so the recursion
    ///                                  is legible, unlike smooth cosines.
    /// - <see cref="ApollonianScale"/> : nested child rings in the gaps, Descartes-style.
    /// </summary>
    public static class RingFractal
    {
        /// <summary>
        /// Self-similar ring spacing. Maps a uniform t in [0,1] through an iterated function
        /// system with maps f0(x) = s*x and f1(x) = s*x + (1-s). With s &lt; 0.5 that leaves a gap at
        /// every level, so rings bunch into clusters, clusters into super-clusters, and so on —
        /// a Cantor measure rather than a sine wave.
        ///
        /// s = 1/3 is the classic middle-thirds set. Larger s closes the gaps toward uniform.
        /// </summary>
        public static float CantorPosition(float t, int depth, float s)
        {
            depth = Mathf.Clamp(depth, 0, 16);
            if (depth == 0) return t;
            s = Mathf.Clamp(s, .05f, .5f);

            // Read t as a `depth`-bit address, most significant bit first, then walk the IFS.
            // Fractional part of the address is carried so the result stays continuous in t.
            float scaled = Mathf.Clamp01(t) * (1 << depth);
            int address = Mathf.Min((int)scaled, (1 << depth) - 1);
            float frac = scaled - address;

            float x = 0f, weight = 1f - s, step = 1f;
            for (int k = depth - 1; k >= 0; k--)
            {
                if ((address & (1 << k)) != 0) x += weight * step;
                step *= s;
            }
            // Spread the leftover fraction across the smallest surviving interval.
            x += frac * step;

            // Normalise so the full range still reaches 1.
            float span = 0f, w = 1f - s, st = 1f;
            for (int k = 0; k < depth; k++) { span += w * st; st *= s; }
            span += st;
            return span > 0f ? x / span : t;
        }

        /// <summary>
        /// Recursive teeth on teeth. A triangle-wave series rather than a cosine one: each octave
        /// adds sharp corners at a scaled frequency, so order-3 already looks like a Koch edge
        /// instead of a ripple. gain*lacunarity &gt; 1 keeps it a genuine fractal curve.
        ///
        /// Returns a signed displacement around 0, normalised to roughly +/-1.
        /// </summary>
        public static float KochRadius(float theta, int depth, float baseFrequency, float lacunarity, float gain)
        {
            depth = Mathf.Clamp(depth, 0, 10);
            if (depth == 0) return 0f;

            float sum = 0f, norm = 0f, amp = 1f, freq = Mathf.Max(baseFrequency, 1f);
            for (int k = 0; k < depth; k++)
            {
                sum += amp * Triangle(freq * theta);
                norm += amp;
                amp *= Mathf.Clamp(gain, .05f, .95f);
                freq *= Mathf.Max(lacunarity, 1.05f);
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>Triangle wave on [-1,1] with period 2*pi. Sharp corners are the point.</summary>
        static float Triangle(float x)
        {
            float p = Mathf.Repeat(x / (Mathf.PI * 2f), 1f);
            return 4f * Mathf.Abs(p - .5f) - 1f;
        }

        /// <summary>
        /// How much to shrink a child ring nested in a gap, `level` deep. Apollonian packings
        /// shrink roughly geometrically, so this is a geometric ratio with a floor to keep child
        /// rings from collapsing below a visible size.
        /// </summary>
        public static float ApollonianScale(int level, float ratio) =>
            Mathf.Max(Mathf.Pow(Mathf.Clamp(ratio, .1f, .95f), Mathf.Max(level, 0)), .02f);

        /// <summary>
        /// Positions of recursively inserted child rings between two parents, as fractions of the
        /// gap. Depth 1 gives the midpoint; depth 2 adds quarter points; and so on. Returns how
        /// many were written.
        /// </summary>
        public static int SubdivideGap(float[] into, int depth)
        {
            depth = Mathf.Clamp(depth, 0, 4);
            if (depth == 0 || into == null) return 0;
            int written = 0;
            for (int level = 1; level <= depth; level++)
            {
                int divisions = 1 << level;
                for (int i = 1; i < divisions; i += 2)
                {
                    if (written >= into.Length) return written;
                    into[written++] = (float)i / divisions;
                }
            }
            return written;
        }

        /// <summary>Total child rings produced by <see cref="SubdivideGap"/> at a given depth.</summary>
        public static int SubdivisionCount(int depth)
        {
            depth = Mathf.Clamp(depth, 0, 4);
            int total = 0;
            for (int level = 1; level <= depth; level++) total += 1 << (level - 1);
            return total;
        }

        /// <summary>Which recursion level a child ring from <see cref="SubdivideGap"/> belongs to.</summary>
        public static int SubdivisionLevel(int index, int depth)
        {
            depth = Mathf.Clamp(depth, 0, 4);
            int seen = 0;
            for (int level = 1; level <= depth; level++)
            {
                int here = 1 << (level - 1);
                if (index < seen + here) return level;
                seen += here;
            }
            return depth;
        }
    }
}
