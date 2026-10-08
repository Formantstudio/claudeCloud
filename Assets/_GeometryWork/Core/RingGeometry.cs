using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The fractal accordion ring tunnel as a reusable builder, so the chamber and the combo
    /// system share one implementation instead of two copies.
    ///
    /// Ring positions come from two stacked effects: a bellows weight per interval, cumulatively
    /// summed so rings bunch where the weight is small, then that parameter pushed through a
    /// Cantor IFS so the rings group into clusters of clusters. The second part is the one that
    /// genuinely subdivides.
    /// </summary>
    public sealed class RingState
    {
        public float[] z;            // ring centre positions along the axis
        public float[] profiles;     // rings * (sides + 1) radii for this frame
        public int sides, rings, stride, connectors;

        public bool Matches(int s, int r, int c) =>
            z != null && profiles != null && sides == s && rings == r && connectors == c;

        public void Allocate(int s, int r, int c)
        {
            sides = Mathf.Max(s, 3);
            rings = Mathf.Max(r, 2);
            connectors = Mathf.Max(c, 0);
            stride = sides + 1;
            z = new float[rings];
            profiles = new float[rings * stride];
        }
    }

    public static class RingGeometry
    {
        /// <summary>Unwelded quads: one band per ring plus one strut per gap per connector.</summary>
        public static int QuadCount(int sides, int rings, int connectors) =>
            sides * rings + connectors * Mathf.Max(rings - 1, 0);

        /// <summary>Recomputes ring positions and radii for this frame.</summary>
        public static void Compute(RingState st, AccordionSettings s, float radius, float length,
                                   float time, bool animate, float cycleSeconds)
        {
            float phase = time * (Mathf.PI * 2f / Mathf.Max(cycleSeconds, 1f));
            float openness = animate
                ? Mathf.Lerp(s.extendMin, s.extend, .5f + .5f * Mathf.Sin(phase))
                : s.extend;
            float bellowsPhase = animate ? phase * 1.7f : 0f;
            float breath = animate ? 1f + .04f * s.breathe * Mathf.Sin(phase * 2.3f) : 1f;
            float spinPhase = animate ? time * s.spin : 0f;

            Positions(st, s, length, openness, bellowsPhase);
            Profiles(st, s, radius, breath, spinPhase);
        }

        static void Positions(RingState st, AccordionSettings s, float length,
                              float openness, float bellowsPhase)
        {
            int octaves = Mathf.Clamp(s.octaves, 1, 4);
            float total = 0f;

            for (int r = 0; r < st.rings; r++)
            {
                float t = st.rings > 1 ? (float)r / (st.rings - 1) : 0f;
                float amp = 1f, freq = s.bellows, sum = 0f, norm = 0f;
                for (int o = 0; o < octaves; o++)
                {
                    sum += amp * Mathf.Cos(Mathf.PI * 2f * freq * t + bellowsPhase * (o + 1));
                    norm += amp;
                    amp *= Mathf.Clamp01(s.gain);
                    freq *= 2f;
                }
                float weight = norm > 0f ? 1f + s.accordion * (sum / norm) : 1f;
                st.z[r] = total;
                total += Mathf.Max(weight, .05f);
            }

            if (total <= 0f) total = 1f;
            for (int r = 0; r < st.rings; r++)
            {
                float t = st.z[r] / total;
                if (s.cantorSpacing > 0f && s.cantorDepth > 0)
                    t = Mathf.Lerp(t, RingFractal.CantorPosition(t, s.cantorDepth, s.cantorRatio),
                                   s.cantorSpacing);
                st.z[r] = t;
            }

            float span = length * openness;
            for (int r = 0; r < st.rings; r++) st.z[r] = st.z[r] * span - span * .5f;
        }

        static void Profiles(RingState st, AccordionSettings s, float radius, float breath, float spinPhase)
        {
            for (int r = 0; r < st.rings; r++)
            {
                float t = st.rings > 1 ? (float)r / (st.rings - 1) : 0f;
                float m = Mathf.Clamp01(s.profileMorph + s.profileMorphAlong * t);
                float twist = s.fractalTwistAlong * t * Mathf.PI * 2f + spinPhase;
                int baseIndex = r * st.stride;

                for (int i = 0; i <= st.sides; i++)
                {
                    float theta = (float)i / st.sides * Mathf.PI * 2f + spinPhase;
                    float a = RingProfiles.Radius(s.profileA, s, theta);
                    float b = RingProfiles.Radius(s.profileB, s, theta);
                    float rr = Mathf.Lerp(a, b, m);
                    rr *= 1f + s.fractalOut * s.fractalAmplitude *
                          RingFractal.KochRadius(theta + twist, s.fractalDepth, s.fractalBaseFrequency,
                                                 s.fractalLacunarity, s.fractalGain);
                    st.profiles[baseIndex + i] = radius * breath * Mathf.Max(rr, .02f);
                }
            }
        }

        public static Vector3 Point(RingState st, int ring, int side, float z)
        {
            float theta = (float)side / st.sides * Mathf.PI * 2f;
            float r = st.profiles[Mathf.Clamp(ring, 0, st.rings - 1) * st.stride + Mathf.Clamp(side, 0, st.sides)];
            return new Vector3(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r, z);
        }

        /// <summary>Fractional side index, for struts that sit between profile samples.</summary>
        public static Vector3 PointF(RingState st, int ring, float side, float z)
        {
            float wrapped = Mathf.Repeat(side, st.sides);
            int i = (int)wrapped;
            float f = wrapped - i;
            int baseIndex = Mathf.Clamp(ring, 0, st.rings - 1) * st.stride;
            float r = Mathf.Lerp(st.profiles[baseIndex + i],
                                 st.profiles[baseIndex + Mathf.Min(i + 1, st.sides)], f);
            float theta = wrapped / st.sides * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r, z);
        }

        /// <summary>Writes the band and strut vertices. Returns how many were written.</summary>
        public static int Fill(RingState st, AccordionSettings s, Vector3[] into, float length)
        {
            int k = 0;
            for (int r = 0; r < st.rings; r++)
            {
                float prev = r > 0 ? st.z[r] - st.z[r - 1] : (st.rings > 1 ? st.z[1] - st.z[0] : length);
                float next = r < st.rings - 1 ? st.z[r + 1] - st.z[r] : prev;
                float back = st.z[r] - prev * s.bandFraction * .5f;
                float front = st.z[r] + next * s.bandFraction * .5f;

                for (int i = 0; i < st.sides; i++)
                {
                    if (k + 6 > into.Length) return k;
                    Vector3 a = Point(st, r, i, back), b = Point(st, r, i + 1, back);
                    Vector3 c = Point(st, r, i + 1, front), d = Point(st, r, i, front);
                    into[k++] = a; into[k++] = b; into[k++] = c;
                    into[k++] = a; into[k++] = c; into[k++] = d;
                }
            }

            for (int r = 0; r < st.rings - 1; r++)
            {
                float span = st.z[r + 1] - st.z[r];
                float from = st.z[r] + span * s.bandFraction * .5f;
                float to = st.z[r + 1] - span * s.bandFraction * .5f;
                for (int c = 0; c < st.connectors; c++)
                {
                    if (k + 6 > into.Length) return k;
                    float centre = (float)c * st.sides / Mathf.Max(st.connectors, 1);
                    float half = s.connectorWidth * .5f;
                    Vector3 a = PointF(st, r, centre - half, from);
                    Vector3 b = PointF(st, r, centre + half, from);
                    Vector3 c2 = PointF(st, r + 1, centre + half, to);
                    Vector3 d = PointF(st, r + 1, centre - half, to);
                    into[k++] = a; into[k++] = b; into[k++] = c2;
                    into[k++] = a; into[k++] = c2; into[k++] = d;
                }
            }
            while (k < into.Length) into[k++] = Vector3.zero;
            return k;
        }

        /// <summary>A point on the ring tunnel at normalised (u, v), for particle sampling.</summary>
        public static Vector3 Sample(RingState st, float u, float v)
        {
            if (st.z == null || st.profiles == null || st.rings < 1) return Vector3.zero;
            float pos = Mathf.Clamp01(v) * (st.rings - 1);
            int r0 = Mathf.Clamp((int)pos, 0, st.rings - 1);
            int r1 = Mathf.Min(r0 + 1, st.rings - 1);
            float f = pos - r0;
            float side = Mathf.Repeat(u, 1f) * st.sides;
            return Vector3.Lerp(PointF(st, r0, side, st.z[r0]), PointF(st, r1, side, st.z[r1]), f);
        }
    }
}
