using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>Named links, each given as a closed braid.</summary>
    public enum SeifertPreset
    {
        Trefoil,        // T(2,3)
        FigureEight,    // 4_1
        Cinquefoil,     // T(2,5)
        TorusKnot,      // T(p,q), from the p and q fields
        HopfLink,
        Borromean,
        Custom          // from the braid word string
    }

    /// <summary>
    /// Seifert surfaces by Seifert's algorithm, on closed-braid diagrams.
    ///
    /// Every link is a closed braid (Alexander's theorem), and on a closed-braid diagram Seifert's
    /// algorithm is exact and regular: smoothing every crossing in the orientation-preserving way
    /// leaves one Seifert circle per strand, nested around the braid axis. Each circle spans a disk;
    /// the disks are stacked at staggered heights, all oriented the same way; and each crossing
    /// σᵢ^±1 becomes a half-twisted band joining disk i to disk i+1, twisted one way or the other by
    /// the crossing's sign. The result is a connected (when the braid is) orientable surface whose
    /// boundary is the link, with
    ///   χ = (strands) − (crossings),   genus = (2 − χ − components) / 2.
    ///
    /// Geometry is exact for welding: every disk shares one rim polygon whose crossing slots are
    /// straight chords, and a band's two ends are the slot chord on each disk with the second end
    /// reversed by the half twist, so bands and disks meet vertex for vertex.
    /// </summary>
    public static class SeifertSurface
    {
        /// <summary>Braid word for a preset: signed generator indices, 1-based (σ₁ = 1, σ₁⁻¹ = −1).</summary>
        public static int[] Word(SeifertPreset preset, int p, int q, string custom, out int strands)
        {
            switch (preset)
            {
                case SeifertPreset.Trefoil: strands = 2; return new[] { 1, 1, 1 };
                case SeifertPreset.FigureEight: strands = 3; return new[] { 1, -2, 1, -2 };
                case SeifertPreset.Cinquefoil: strands = 2; return new[] { 1, 1, 1, 1, 1 };
                case SeifertPreset.HopfLink: strands = 2; return new[] { 1, 1 };
                case SeifertPreset.Borromean: strands = 3; return new[] { 1, -2, 1, -2, 1, -2 };
                case SeifertPreset.TorusKnot:
                {
                    // T(p,q) = (σ₁ σ₂ … σ_{p−1})^q on p strands.
                    p = Mathf.Clamp(p, 2, 12); q = Mathf.Clamp(Mathf.Abs(q), 1, 24) * (q < 0 ? -1 : 1);
                    strands = p;
                    var w = new List<int>();
                    for (int r = 0; r < Mathf.Abs(q); r++)
                        for (int g = 1; g < p; g++) w.Add(q < 0 ? -g : g);
                    return w.ToArray();
                }
                default:
                    return Parse(custom, out strands);
            }
        }

        /// <summary>
        /// Parses a braid word: signed integers ("1 -2 1 -2") or letters ("aBaB": a = σ₁, A = σ₁⁻¹).
        /// Strand count is one more than the largest generator used.
        /// </summary>
        public static int[] Parse(string word, out int strands)
        {
            var result = new List<int>();
            if (!string.IsNullOrEmpty(word))
            {
                int i = 0;
                while (i < word.Length)
                {
                    char ch = word[i];
                    if (char.IsLetter(ch))
                    {
                        int g = char.ToLowerInvariant(ch) - 'a' + 1;
                        result.Add(char.IsUpper(ch) ? -g : g);
                        i++;
                    }
                    else if (ch == '-' || char.IsDigit(ch))
                    {
                        int start = i++;
                        while (i < word.Length && char.IsDigit(word[i])) i++;
                        if (int.TryParse(word.Substring(start, i - start), out int g) && g != 0) result.Add(g);
                    }
                    else i++;
                }
            }
            int max = 1;
            foreach (int g in result) max = Mathf.Max(max, Mathf.Abs(g));
            strands = max + 1;
            return result.ToArray();
        }

        /// <summary>Number of link components: cycles of the braid's strand permutation.</summary>
        public static int Components(int[] word, int strands)
        {
            var perm = new int[strands];
            for (int i = 0; i < strands; i++) perm[i] = i;
            foreach (int g in word)
            {
                int a = Mathf.Abs(g) - 1;
                if (a < 0 || a + 1 >= strands) continue;
                int t = perm[a]; perm[a] = perm[a + 1]; perm[a + 1] = t;
            }
            var seen = new bool[strands];
            int cycles = 0;
            for (int i = 0; i < strands; i++)
            {
                if (seen[i]) continue;
                cycles++;
                for (int j = i; !seen[j]; j = perm[j]) seen[j] = true;
            }
            return cycles;
        }

        public static int EulerCharacteristic(int[] word, int strands) => strands - word.Length;

        /// <summary>Genus of the Seifert surface (connected braids): (2 − χ − components) / 2.</summary>
        public static int Genus(int[] word, int strands) =>
            (2 - EulerCharacteristic(word, strands) - Components(word, strands)) / 2;

        /// <summary>Settings for the geometry.</summary>
        [Serializable]
        public struct Shape
        {
            [Tooltip("Radius of every Seifert disk.")]
            public float radius;
            [Tooltip("Vertical spacing between stacked disks.")]
            public float spacing;
            [Tooltip("Fraction of each crossing's angular slot the band occupies.")]
            [Range(.1f, .9f)] public float bandWidth;
            [Tooltip("Rings from the disk centre to its rim.")]
            public int diskRings;
            [Tooltip("Segments across a band.")]
            public int bandColumns;
            [Tooltip("Segments along a band, through its half twist.")]
            public int bandRows;
            [Tooltip("Arc segments on the rim between neighbouring crossing slots.")]
            public int arcSegments;

            public static Shape Default => new Shape
            {
                radius = 1f, spacing = .35f, bandWidth = .45f, diskRings = 6,
                bandColumns = 4, bandRows = 12, arcSegments = 6
            };
        }

        /// <summary>Builds the surface of the closed braid <paramref name="word"/> into <paramref name="into"/>.</summary>
        public static void Build(WireMeshBuilder into, int[] word, int strands, Shape shape, List<Vector3> scratch)
        {
            int c = word.Length;
            strands = Mathf.Max(strands, 1);
            int m = Mathf.Max(shape.bandColumns, 1);
            int arc = Mathf.Max(shape.arcSegments, 1);
            int rings = Mathf.Max(shape.diskRings, 1);
            int rows = Mathf.Max(shape.bandRows, 2);
            float R = Mathf.Max(shape.radius, 1e-3f);
            int slots = Mathf.Max(c, 1);
            float delta = Mathf.Clamp(shape.bandWidth, .05f, .95f) * Mathf.PI / slots;

            // The shared rim polygon, counter-clockwise from above. For each slot: m chord segments,
            // then the arc to the next slot. Chord points are linear between the arc endpoints so
            // disks and bands compute them identically.
            var rim = scratch;
            rim.Clear();
            for (int k = 0; k < slots; k++)
            {
                float theta = Mathf.PI * 2f * k / slots;
                Vector3 a = Polar(R, theta - delta), b = Polar(R, theta + delta);
                if (c > 0)
                    for (int j = 0; j < m; j++) rim.Add(Vector3.LerpUnclamped(a, b, (float)j / m));
                float nextStart = Mathf.PI * 2f * (k + 1) / slots - delta;
                float from = c > 0 ? theta + delta : theta - delta;
                int segs = c > 0 ? arc : arc + m;
                for (int j = 0; j < segs; j++) rim.Add(Polar(R, Mathf.LerpUnclamped(from, nextStart, (float)j / segs)));
            }
            int n = rim.Count;

            // Disks: concentric copies of the rim polygon, a triangle fan at the centre.
            for (int d = 0; d < strands; d++)
            {
                Vector3 lift = new Vector3(0f, 0f, d * shape.spacing);
                for (int j = 0; j < n; j++)
                {
                    Vector3 p0 = rim[j], p1 = rim[(j + 1) % n];
                    into.Triangle(lift, lift + p0 / rings, lift + p1 / rings,
                                  new Vector2(.5f, .5f), Uv(p0 / rings, R), Uv(p1 / rings, R));
                    for (int r = 1; r < rings; r++)
                    {
                        float s0 = (float)r / rings, s1 = (float)(r + 1) / rings;
                        into.Quad(lift + p0 * s0, lift + p0 * s1, lift + p1 * s1, lift + p1 * s0,
                                  Uv(p0 * s0, R), Uv(p0 * s1, R), Uv(p1 * s1, R), Uv(p1 * s0, R));
                    }
                }
            }

            // Bands: one per crossing, from disk i up to disk i+1, half-twisted by the sign.
            for (int k = 0; k < c; k++)
            {
                int gen = word[k];
                int i = Mathf.Abs(gen) - 1;
                if (i < 0 || i + 1 >= strands) continue;
                float sign = gen > 0 ? 1f : -1f;
                float theta = Mathf.PI * 2f * k / slots;
                Vector3 a = Polar(R, theta - delta), b = Polar(R, theta + delta);
                Vector3 mid = (a + b) * .5f, half = (b - a) * .5f;

                for (int r = 0; r < rows; r++)
                for (int col = 0; col < m; col++)
                {
                    // Columns run from the chord's far end back to its near end, so the band
                    // traverses the shared chord opposite to the disk: the surface stays oriented.
                    float t0 = 1f - 2f * col / m, t1 = 1f - 2f * (col + 1) / m;
                    float s0 = (float)r / rows, s1 = (float)(r + 1) / rows;
                    into.Quad(Band(mid, half, t0, s0, i, shape.spacing, sign),
                              Band(mid, half, t1, s0, i, shape.spacing, sign),
                              Band(mid, half, t1, s1, i, shape.spacing, sign),
                              Band(mid, half, t0, s1, i, shape.spacing, sign),
                              new Vector2((float)col / m, s0), new Vector2((float)(col + 1) / m, s0),
                              new Vector2((float)(col + 1) / m, s1), new Vector2((float)col / m, s1));
                }
            }
        }

        /// <summary>
        /// A point on a band: the chord offset t·half, turned about the vertical through the chord
        /// midpoint by π·s (the half twist), lifted from disk i's height to disk i+1's.
        /// At s = 0 it is the chord on disk i; at s = 1 the chord on disk i+1, reversed.
        /// </summary>
        static Vector3 Band(Vector3 mid, Vector3 half, float t, float s, int disk, float spacing, float sign)
        {
            float a = Mathf.PI * s * sign;
            float ca = s >= 1f ? -1f : Mathf.Cos(a), sa = s >= 1f ? 0f : Mathf.Sin(a);
            Vector3 o = half * t;
            Vector3 turned = new Vector3(o.x * ca - o.y * sa, o.x * sa + o.y * ca, 0f);
            return mid + turned + new Vector3(0f, 0f, (disk + s) * spacing);
        }

        static Vector3 Polar(float r, float a) => new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), 0f);

        static Vector2 Uv(Vector3 p, float r) => new Vector2(p.x / (2f * r) + .5f, p.y / (2f * r) + .5f);
    }
}
