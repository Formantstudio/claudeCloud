using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>Which points of the base 2-sphere get a fiber.</summary>
    public enum HopfBaseSet
    {
        /// <summary>Several circles of latitude: each becomes a nested torus of linked fibers.</summary>
        LatitudeRings,
        /// <summary>One circle of latitude: a single torus of Villarceau circles.</summary>
        SingleLatitude,
        /// <summary>Evenly spread over the whole sphere (Fibonacci lattice).</summary>
        FibonacciSphere,
        /// <summary>A meridian, pole to pole: fibers sweeping from the axis line out to the core circle.</summary>
        Meridian
    }

    /// <summary>
    /// The Hopf fibration S¹ → S³ → S².
    ///
    /// With S³ ⊂ C² as unit pairs (z₀, z₁), the Hopf map is h(z₀, z₁) = (2 z₀ z̄₁, |z₀|² − |z₁|²) ∈ S².
    /// The preimage of a base point with polar angle θ and azimuth φ is the great circle
    ///   z₀ = cos(θ/2) e^{i(t + φ)},  z₁ = sin(θ/2) e^{i t},  t ∈ [0, 2π).
    /// Points of S³ are stored as (Re z₀, Im z₀, Re z₁, Im z₁) = (x, y, z, w).
    ///
    /// Stereographic projection from (0, 0, 0, 1), X = (x, y, z) / (1 − w), maps each fiber to a
    /// circle in R³ (or the z-axis, for the fiber through the pole), and any two fibers link exactly
    /// once. Fibers over one circle of latitude sweep out a torus as Villarceau circles.
    /// A <see cref="Rotor4"/> applied before projection turns the whole fibration in 4-D.
    /// </summary>
    public static class HopfFibration
    {
        /// <summary>The point of the fiber over unit <paramref name="basePoint"/> at fiber angle <paramref name="t"/>.</summary>
        public static Vector4 FiberPoint(Vector3 basePoint, float t)
        {
            Vector3 b = basePoint.normalized;
            float c = Mathf.Sqrt(Mathf.Max(0f, (1f + b.z) * .5f));     // cos(θ/2)
            float s = Mathf.Sqrt(Mathf.Max(0f, (1f - b.z) * .5f));     // sin(θ/2)
            float phi = Mathf.Atan2(b.y, b.x);
            return new Vector4(c * Mathf.Cos(t + phi), c * Mathf.Sin(t + phi), s * Mathf.Cos(t), s * Mathf.Sin(t));
        }

        /// <summary>The Hopf map h: S³ → S².</summary>
        public static Vector3 Map(Vector4 p) => new Vector3(
            2f * (p.x * p.z + p.y * p.w),
            2f * (p.y * p.z - p.x * p.w),
            p.x * p.x + p.y * p.y - p.z * p.z - p.w * p.w);

        /// <summary>
        /// Stereographic projection from w = 1. Within <paramref name="poleGuard"/> of the pole the
        /// denominator is held at the guard, so the result stays finite.
        /// </summary>
        public static Vector3 Project(Vector4 p, float poleGuard = 1e-3f)
        {
            float d = Mathf.Max(1f - p.w, poleGuard);
            return new Vector3(p.x, p.y, p.z) / d;
        }

        /// <summary>The stereographic conformal factor 1 / (1 − w): how much a small length near p is magnified.</summary>
        public static float ConformalScale(Vector4 p, float poleGuard = 1e-3f) => 1f / Mathf.Max(1f - p.w, poleGuard);

        /// <summary>Base points for a fiber set. Latitude is in degrees from the north pole (0..180).</summary>
        public static void BasePoints(HopfBaseSet set, int count, int rings, float latitudeDegrees, List<Vector3> into)
        {
            into.Clear();
            count = Mathf.Max(count, 1);
            switch (set)
            {
                case HopfBaseSet.SingleLatitude:
                {
                    float th = Mathf.Clamp(latitudeDegrees, 1f, 179f) * Mathf.Deg2Rad;
                    for (int i = 0; i < count; i++) into.Add(Spherical(th, Mathf.PI * 2f * i / count));
                    break;
                }
                case HopfBaseSet.FibonacciSphere:
                {
                    float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
                    for (int i = 0; i < count; i++)
                    {
                        // Offset by half a step so neither exact pole is used: the fiber over the
                        // south pole runs through the projection pole.
                        float z = 1f - 2f * (i + .5f) / count;
                        into.Add(Spherical(Mathf.Acos(z), golden * i));
                    }
                    break;
                }
                case HopfBaseSet.Meridian:
                {
                    for (int i = 0; i < count; i++) into.Add(Spherical(Mathf.PI * (i + .5f) / count, 0f));
                    break;
                }
                default:
                {
                    rings = Mathf.Clamp(rings, 1, count);
                    int perRing = Mathf.Max(count / rings, 1);
                    // Rings spread around the requested latitude, never touching a pole.
                    float centre = Mathf.Clamp(latitudeDegrees, 10f, 170f) * Mathf.Deg2Rad;
                    float spread = Mathf.Min(centre, Mathf.PI - centre) * .8f;
                    for (int r = 0; r < rings; r++)
                    {
                        float th = rings == 1 ? centre : centre - spread + 2f * spread * r / (rings - 1);
                        float twist = r * Mathf.PI / perRing;            // stagger rings so fibers interleave
                        for (int i = 0; i < perRing; i++) into.Add(Spherical(th, twist + Mathf.PI * 2f * i / perRing));
                    }
                    break;
                }
            }
        }

        public static Vector3 Spherical(float polar, float azimuth) =>
            new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Sin(polar) * Mathf.Sin(azimuth), Mathf.Cos(polar));

        /// <summary>
        /// One projected fiber as <paramref name="samples"/> points (a closed loop; the first point is
        /// not repeated). Also writes each point's conformal scale when <paramref name="scales"/> is
        /// not null, so tubes can keep a constant thickness on S³.
        /// </summary>
        public static void Fiber(Vector3 basePoint, int samples, Rotor4 rotation, float phase, float poleGuard,
                                 List<Vector3> into, List<float> scales)
        {
            into.Clear();
            scales?.Clear();
            samples = Mathf.Max(samples, 3);
            for (int i = 0; i < samples; i++)
            {
                Vector4 p = rotation.Rotate(FiberPoint(basePoint, phase + Mathf.PI * 2f * i / samples));
                into.Add(Project(p, poleGuard));
                scales?.Add(ConformalScale(p, poleGuard));
            }
        }

        /// <summary>
        /// Gauss linking integral of two closed polylines, (1/4π) ∮∮ (a − b)·(da × db) / |a − b|³,
        /// by the midpoint rule. Any two Hopf fibers give ±1.
        /// </summary>
        public static float LinkingNumber(IList<Vector3> a, IList<Vector3> b)
        {
            double sum = 0;
            int n = a.Count, m = b.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a0 = a[i], a1 = a[(i + 1) % n];
                Vector3 pa = (a0 + a1) * .5f, da = a1 - a0;
                for (int j = 0; j < m; j++)
                {
                    Vector3 b0 = b[j], b1 = b[(j + 1) % m];
                    Vector3 r = pa - (b0 + b1) * .5f;
                    double len = r.magnitude;
                    if (len < 1e-9) continue;
                    sum += Vector3.Dot(r, Vector3.Cross(da, b1 - b0)) / (len * len * len);
                }
            }
            return (float)(sum / (4.0 * System.Math.PI));
        }
    }
}
