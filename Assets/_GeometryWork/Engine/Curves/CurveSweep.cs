using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Rotation-minimising frames and the tubes and ribbons swept with them.
    ///
    /// Frenet frames flip wherever curvature vanishes and spin wildly where torsion is high, which
    /// turns a swept tube inside out (GEOMETRY-MAP §3). These are Bishop / parallel-transport frames
    /// computed by the double-reflection method (Wang, Jüttler, Zheng, Liu 2008): each frame is the
    /// previous one reflected twice, which is fourth-order accurate and never divides by curvature.
    ///
    /// On a closed curve parallel transport does not come back to where it started — the mismatch
    /// is the curve's holonomy (total twist). It is measured and spread evenly along the curve so the
    /// seam closes exactly; a tube on a closed curve is then a true torus.
    /// </summary>
    public static class CurveSweep
    {
        /// <summary>
        /// Writes a unit normal per point into <paramref name="normals"/> (the binormal is
        /// tangent × normal). Tangents are central differences, wrapping when <paramref name="closed"/>.
        /// </summary>
        public static void Frames(IList<Vector3> points, bool closed, List<Vector3> tangents, List<Vector3> normals)
        {
            int n = points.Count;
            tangents.Clear(); normals.Clear();
            if (n < 2) return;

            for (int i = 0; i < n; i++)
            {
                Vector3 prev = closed ? points[(i - 1 + n) % n] : points[Mathf.Max(i - 1, 0)];
                Vector3 next = closed ? points[(i + 1) % n] : points[Mathf.Min(i + 1, n - 1)];
                Vector3 t = next - prev;
                tangents.Add(t.sqrMagnitude > 1e-20f ? t.normalized : (i > 0 ? tangents[i - 1] : Vector3.forward));
            }

            normals.Add(AnyPerpendicular(tangents[0]));
            for (int i = 0; i + 1 < n; i++) normals.Add(Transport(points[i], points[i + 1], tangents[i], tangents[i + 1], normals[i]));

            if (!closed) return;

            // Holonomy: transport the last frame back to the first and measure the angle it is off
            // by, then unwind it linearly so frame n coincides with frame 0.
            Vector3 back = Transport(points[n - 1], points[0], tangents[n - 1], tangents[0], normals[n - 1]);
            float twist = SignedAngle(normals[0], back, tangents[0]);
            for (int i = 1; i < n; i++)
            {
                float a = -twist * i / n;
                normals[i] = Rotate(normals[i], tangents[i], a);
            }
        }

        /// <summary>Double-reflection transport of normal <paramref name="r0"/> from (x0, t0) to (x1, t1).</summary>
        public static Vector3 Transport(Vector3 x0, Vector3 x1, Vector3 t0, Vector3 t1, Vector3 r0)
        {
            Vector3 v1 = x1 - x0;
            float c1 = Vector3.Dot(v1, v1);
            if (c1 < 1e-20f) return r0;
            Vector3 rL = r0 - (2f / c1) * Vector3.Dot(v1, r0) * v1;
            Vector3 tL = t0 - (2f / c1) * Vector3.Dot(v1, t0) * v1;
            Vector3 v2 = t1 - tL;
            float c2 = Vector3.Dot(v2, v2);
            Vector3 r1 = c2 < 1e-20f ? rL : rL - (2f / c2) * Vector3.Dot(v2, rL) * v2;
            // Re-orthonormalise against float drift.
            r1 = Vector3.ProjectOnPlane(r1, t1);
            return r1.sqrMagnitude > 1e-20f ? r1.normalized : AnyPerpendicular(t1);
        }

        public static Vector3 AnyPerpendicular(Vector3 t)
        {
            Vector3 a = Mathf.Abs(t.x) < .9f ? Vector3.right : Vector3.up;
            return Vector3.Cross(t, a).normalized;
        }

        /// <summary>Angle from a to b about axis (all roughly perpendicular to axis), in (−π, π].</summary>
        public static float SignedAngle(Vector3 a, Vector3 b, Vector3 axis) =>
            Mathf.Atan2(Vector3.Dot(Vector3.Cross(a, b), axis), Vector3.Dot(a, b));

        /// <summary>Rodrigues rotation of v about unit axis k.</summary>
        public static Vector3 Rotate(Vector3 v, Vector3 k, float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return v * c + Vector3.Cross(k, v) * s + k * (Vector3.Dot(k, v) * (1f - c));
        }

        /// <summary>
        /// A tube of <paramref name="sides"/> around the curve. Closed curves give a torus (both
        /// directions wrap); open curves give an open cylinder. U runs around, V along.
        /// <paramref name="radius"/> may vary per point (null = constant <paramref name="baseRadius"/>).
        /// </summary>
        public static void Tube(WireMeshBuilder into, IList<Vector3> points, bool closed, int sides, float baseRadius,
                                IList<float> radius, List<Vector3> scratchT, List<Vector3> scratchN, List<Vector3> ring)
        {
            int n = points.Count;
            if (n < 2 || sides < 3) return;
            Frames(points, closed, scratchT, scratchN);

            int rows = closed ? n : n - 1;
            ring.Clear();
            // Ring vertices for every point, plus the first again when closed so the grid wraps.
            for (int r = 0; r <= rows; r++)
            {
                int i = r % n;
                Vector3 t = scratchT[i], nrm = scratchN[i], b = Vector3.Cross(t, nrm);
                float rad = radius != null ? radius[i] : baseRadius;
                for (int s = 0; s <= sides; s++)
                {
                    float a = (s % sides) * (Mathf.PI * 2f / sides);
                    ring.Add(points[i] + (nrm * Mathf.Cos(a) + b * Mathf.Sin(a)) * rad);
                }
            }
            into.Grid(ring, sides, rows);
        }

        /// <summary>
        /// A flat ribbon of half-width <paramref name="halfWidth"/> along the curve's transported
        /// normal. Two columns, so the wire shows its edges and rungs.
        /// </summary>
        public static void Ribbon(WireMeshBuilder into, IList<Vector3> points, bool closed, float halfWidth,
                                  List<Vector3> scratchT, List<Vector3> scratchN, List<Vector3> grid)
        {
            int n = points.Count;
            if (n < 2) return;
            Frames(points, closed, scratchT, scratchN);
            int rows = closed ? n : n - 1;
            grid.Clear();
            for (int r = 0; r <= rows; r++)
            {
                int i = r % n;
                Vector3 w = scratchN[i] * halfWidth;
                grid.Add(points[i] - w); grid.Add(points[i]); grid.Add(points[i] + w);
            }
            into.Grid(grid, 2, rows);
        }

        /// <summary>
        /// Resamples a polyline to <paramref name="count"/> points evenly spaced by arc length.
        /// Integrators produce points bunched where the flow is slow; a tube wants even spacing.
        /// </summary>
        public static void ResampleByArcLength(IList<Vector3> source, int count, List<Vector3> into)
        {
            into.Clear();
            int n = source.Count;
            if (n == 0 || count < 2) return;
            if (n == 1) { for (int i = 0; i < count; i++) into.Add(source[0]); return; }

            float total = 0f;
            for (int i = 1; i < n; i++) total += Vector3.Distance(source[i - 1], source[i]);
            if (total <= 0f) { for (int i = 0; i < count; i++) into.Add(source[0]); return; }

            float step = total / (count - 1);
            int seg = 1;
            float segStart = 0f, segLen = Vector3.Distance(source[0], source[1]);
            for (int k = 0; k < count; k++)
            {
                float target = Mathf.Min(k * step, total);
                while (seg < n - 1 && segStart + segLen < target)
                {
                    segStart += segLen;
                    seg++;
                    segLen = Vector3.Distance(source[seg - 1], source[seg]);
                }
                float t = segLen > 0f ? (target - segStart) / segLen : 0f;
                into.Add(Vector3.LerpUnclamped(source[seg - 1], source[seg], Mathf.Clamp01(t)));
            }
        }
    }
}
