using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>Continuous chaotic flows (integrated) and iterated maps (stepped).</summary>
    public enum Attractor
    {
        Lorenz,
        Rossler,
        Thomas,
        Halvorsen,
        Aizawa,
        // Iterated maps: orbits are point clouds, not curves.
        Clifford,
        DeJong
    }

    /// <summary>
    /// Parameters for every attractor in one block. <see cref="Defaults"/> gives the classic chaotic
    /// values for each system. Only the fields a system reads matter to it.
    /// </summary>
    [Serializable]
    public struct AttractorParameters
    {
        public float a, b, c, d, e, f;

        public static AttractorParameters Defaults(Attractor system)
        {
            switch (system)
            {
                // σ, ρ, β
                case Attractor.Lorenz: return new AttractorParameters { a = 10f, b = 28f, c = 8f / 3f };
                case Attractor.Rossler: return new AttractorParameters { a = .2f, b = .2f, c = 5.7f };
                case Attractor.Thomas: return new AttractorParameters { b = .208186f };
                case Attractor.Halvorsen: return new AttractorParameters { a = 1.89f };
                case Attractor.Aizawa: return new AttractorParameters { a = .95f, b = .7f, c = .6f, d = 3.5f, e = .25f, f = .1f };
                case Attractor.Clifford: return new AttractorParameters { a = -1.4f, b = 1.6f, c = 1f, d = .7f };
                default: return new AttractorParameters { a = 1.4f, b = -2.3f, c = 2.4f, d = -2.1f };
            }
        }
    }

    /// <summary>
    /// Strange attractors, integrated in double precision with classical Runge–Kutta 4.
    ///
    /// Flows:
    ///   Lorenz     ẋ = σ(y − x),  ẏ = x(ρ − z) − y,  ż = xy − βz
    ///   Rössler    ẋ = −y − z,    ẏ = x + ay,       ż = b + z(x − c)
    ///   Thomas     ẋ = sin y − bx, cyclic in (x, y, z)
    ///   Halvorsen  ẋ = −ax − 4y − 4z − y², cyclic in (x, y, z)
    ///   Aizawa     ẋ = (z − b)x − dy,  ẏ = dx + (z − b)y,
    ///              ż = c + az − z³/3 − (x² + y²)(1 + ez) + fzx³
    /// Maps (3-D lift of the 2-D maps, z = the previous x, so the orbit has depth):
    ///   Clifford   x' = sin(ay) + c cos(ax),  y' = sin(bx) + d cos(by)
    ///   De Jong    x' = sin(ay) − cos(bx),    y' = sin(cx) − cos(dy)
    ///
    /// <see cref="IntegrateAdaptive"/> controls the step by step doubling (Richardson): one step of
    /// h against two of h/2, whose difference estimates the local error of a fifth-order-corrected
    /// result. Chaotic flows speed up and slow down by orders of magnitude around the attractor,
    /// so a fixed step either wastes work in the slow lobes or loses accuracy in the fast ones.
    /// </summary>
    public static class StrangeAttractors
    {
        public static bool IsMap(Attractor s) => s == Attractor.Clifford || s == Attractor.DeJong;

        /// <summary>A starting point inside each flow's basin.</summary>
        public static Vector3 Seed(Attractor s)
        {
            switch (s)
            {
                case Attractor.Lorenz: return new Vector3(.1f, 0f, 0f);
                case Attractor.Rossler: return new Vector3(.1f, 0f, 0f);
                case Attractor.Thomas: return new Vector3(.1f, 0f, 0f);
                case Attractor.Halvorsen: return new Vector3(-1.48f, -1.51f, 2.04f);
                case Attractor.Aizawa: return new Vector3(.1f, 0f, 0f);
                default: return new Vector3(.1f, .1f, 0f);
            }
        }

        /// <summary>Roughly where each attractor lives: centre and half-extent, for framing it at unit size.</summary>
        public static void Frame(Attractor s, out Vector3 centre, out float halfExtent)
        {
            switch (s)
            {
                case Attractor.Lorenz: centre = new Vector3(0, 0, 25); halfExtent = 28f; return;
                case Attractor.Rossler: centre = new Vector3(0, 0, 8); halfExtent = 14f; return;
                case Attractor.Thomas: centre = Vector3.zero; halfExtent = 4.5f; return;
                case Attractor.Halvorsen: centre = new Vector3(-2, -2, -2); halfExtent = 9f; return;
                case Attractor.Aizawa: centre = new Vector3(0, 0, .5f); halfExtent = 1.6f; return;
                default: centre = Vector3.zero; halfExtent = 2.6f; return;
            }
        }

        // ---- flows -----------------------------------------------------------------

        /// <summary>The vector field of a flow at (x, y, z).</summary>
        public static void Derivative(Attractor s, in AttractorParameters p, double x, double y, double z,
                                      out double dx, out double dy, out double dz)
        {
            switch (s)
            {
                case Attractor.Lorenz:
                    dx = p.a * (y - x); dy = x * (p.b - z) - y; dz = x * y - p.c * z; return;
                case Attractor.Rossler:
                    dx = -y - z; dy = x + p.a * y; dz = p.b + z * (x - p.c); return;
                case Attractor.Thomas:
                    dx = Math.Sin(y) - p.b * x; dy = Math.Sin(z) - p.b * y; dz = Math.Sin(x) - p.b * z; return;
                case Attractor.Halvorsen:
                    dx = -p.a * x - 4 * y - 4 * z - y * y;
                    dy = -p.a * y - 4 * z - 4 * x - z * z;
                    dz = -p.a * z - 4 * x - 4 * y - x * x;
                    return;
                case Attractor.Aizawa:
                    dx = (z - p.b) * x - p.d * y;
                    dy = p.d * x + (z - p.b) * y;
                    dz = p.c + p.a * z - z * z * z / 3.0 - (x * x + y * y) * (1 + p.e * z) + p.f * z * x * x * x;
                    return;
                default:
                    dx = dy = dz = 0; return;
            }
        }

        /// <summary>One classical RK4 step of size h, in place.</summary>
        public static void Rk4Step(Attractor s, in AttractorParameters p, ref double x, ref double y, ref double z, double h)
        {
            Derivative(s, p, x, y, z, out double k1x, out double k1y, out double k1z);
            Derivative(s, p, x + .5 * h * k1x, y + .5 * h * k1y, z + .5 * h * k1z, out double k2x, out double k2y, out double k2z);
            Derivative(s, p, x + .5 * h * k2x, y + .5 * h * k2y, z + .5 * h * k2z, out double k3x, out double k3y, out double k3z);
            Derivative(s, p, x + h * k3x, y + h * k3y, z + h * k3z, out double k4x, out double k4y, out double k4z);
            x += h / 6.0 * (k1x + 2 * k2x + 2 * k3x + k4x);
            y += h / 6.0 * (k1y + 2 * k2y + 2 * k3y + k4y);
            z += h / 6.0 * (k1z + 2 * k2z + 2 * k3z + k4z);
        }

        /// <summary>
        /// Fixed-step trajectory: discards <paramref name="transientSteps"/> to fall onto the attractor,
        /// then records <paramref name="count"/> points, <paramref name="substeps"/> RK4 steps apart.
        /// </summary>
        public static void Integrate(Attractor s, AttractorParameters p, Vector3 seed, float dt, int substeps,
                                     int transientSteps, int count, List<Vector3> into)
        {
            into.Clear();
            if (IsMap(s)) { Iterate(s, p, seed, transientSteps, count, into); return; }
            double x = seed.x, y = seed.y, z = seed.z;
            substeps = Mathf.Max(substeps, 1);
            for (int i = 0; i < transientSteps; i++) Rk4Step(s, p, ref x, ref y, ref z, dt);
            for (int k = 0; k < count; k++)
            {
                for (int i = 0; i < substeps; i++) Rk4Step(s, p, ref x, ref y, ref z, dt);
                if (!Finite(x, y, z)) break;
                into.Add(new Vector3((float)x, (float)y, (float)z));
            }
        }

        /// <summary>
        /// Adaptive trajectory over flow time <paramref name="duration"/> (after
        /// <paramref name="transientTime"/>), recording a point every <paramref name="sampleInterval"/>
        /// of flow time. The step size h adapts by step doubling to keep the per-step error under
        /// <paramref name="tolerance"/>; recorded points land exactly on the sample times.
        /// </summary>
        public static int IntegrateAdaptive(Attractor s, AttractorParameters p, Vector3 seed, double transientTime,
                                            double duration, double sampleInterval, double tolerance, List<Vector3> into)
        {
            into.Clear();
            int steps = 0;
            if (IsMap(s)) return 0;
            double x = seed.x, y = seed.y, z = seed.z, h = Math.Min(.01, sampleInterval);
            const double hMin = 1e-7;
            // Event times: first the end of the transient, then every sample time after it.
            double t = 0, target = transientTime, end = transientTime + duration;

            while (steps < 10_000_000)
            {
                if (t >= target)
                {
                    if (target > end + 1e-12) break;
                    into.Add(new Vector3((float)x, (float)y, (float)z));
                    target += sampleInterval;
                    continue;
                }

                bool landsOnTarget = h >= target - t;
                double step = landsOnTarget ? target - t : h;

                double x1 = x, y1 = y, z1 = z;
                Rk4Step(s, p, ref x1, ref y1, ref z1, step);
                double x2 = x, y2 = y, z2 = z;
                Rk4Step(s, p, ref x2, ref y2, ref z2, step * .5);
                Rk4Step(s, p, ref x2, ref y2, ref z2, step * .5);
                steps += 3;

                // Step doubling: the two results differ by ~15/16 of the coarse step's error.
                double err = Math.Max(Math.Abs(x2 - x1), Math.Max(Math.Abs(y2 - y1), Math.Abs(z2 - z1))) / 15.0;
                double grow = err > 0 ? .9 * Math.Pow(tolerance / err, .2) : 4.0;
                if (err > tolerance && step > hMin)
                {
                    h = Math.Max(step * Math.Max(.2, grow), hMin);
                    continue;
                }

                // Accept, with the Richardson correction (locally fifth order).
                x = x2 + (x2 - x1) / 15.0; y = y2 + (y2 - y1) / 15.0; z = z2 + (z2 - z1) / 15.0;
                t = landsOnTarget ? target : t + step;
                if (!Finite(x, y, z)) break;
                // Only grow h from a full step; a step clipped to land on a sample says nothing about h.
                if (!landsOnTarget || step >= h) h = Math.Max(Math.Min(step * Math.Min(4.0, grow), sampleInterval), hMin);
            }
            return steps;
        }

        // ---- maps ------------------------------------------------------------------

        /// <summary>Orbit of an iterated map, lifted to 3-D with z = the previous x.</summary>
        public static void Iterate(Attractor s, AttractorParameters p, Vector3 seed, int transient, int count, List<Vector3> into)
        {
            into.Clear();
            double x = seed.x, y = seed.y, prev = seed.x;
            for (int i = 0; i < transient + count; i++)
            {
                double nx, ny;
                if (s == Attractor.Clifford)
                {
                    nx = Math.Sin(p.a * y) + p.c * Math.Cos(p.a * x);
                    ny = Math.Sin(p.b * x) + p.d * Math.Cos(p.b * y);
                }
                else
                {
                    nx = Math.Sin(p.a * y) - Math.Cos(p.b * x);
                    ny = Math.Sin(p.c * x) - Math.Cos(p.d * y);
                }
                prev = x; x = nx; y = ny;
                if (i >= transient) into.Add(new Vector3((float)x, (float)y, (float)prev));
            }
        }

        static bool Finite(double x, double y, double z) =>
            !(double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) ||
              double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z));
    }
}
