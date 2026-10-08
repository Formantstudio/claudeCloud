using System;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>A scalar field: negative inside, positive outside, zero on the surface.</summary>
    public interface IScalarField
    {
        float Sample(Vector3 p);
    }

    /// <summary>
    /// Dual contouring (Ju, Losasso, Schaefer, Warren 2002): one vertex per cell that straddles the
    /// surface, placed by minimising the quadratic error function over the cell's Hermite data,
    ///
    ///   QEF(x) = Σᵢ ((x − pᵢ) · nᵢ)²
    ///
    /// where pᵢ are the exact zero crossings on the cell's edges and nᵢ the unit field gradients
    /// there. Where the normals agree the minimiser slides freely along the surface; where they
    /// disagree (an edge or a corner) it is pinned to the intersection of the tangent planes — which
    /// is why dual contouring keeps a Menger sponge's corners square while surface nets, which uses
    /// the plain mean of the crossings, rounds them off.
    ///
    /// The QEF is solved about the mass point c with a truncated pseudo-inverse: AᵀA is
    /// eigen-decomposed (Jacobi, 3×3, double precision), eigenvalues below
    /// <see cref="singularThreshold"/> × the largest are dropped, so directions the normals do not
    /// constrain stay at c instead of flying off. Results outside the cell are clamped into it.
    ///
    /// Faces are surface-nets style: a quad across every grid edge whose ends differ in sign,
    /// joining the four cells around it, oriented by which end is inside. Output goes through
    /// <see cref="WireMeshBuilder"/>, so it carries the wire pipeline's invariants.
    /// Buffers are kept between runs; extraction at a fixed resolution does not allocate.
    /// </summary>
    public sealed class DualContouring
    {
        /// <summary>Cells per axis.</summary>
        public int resolution = 48;
        /// <summary>Half-size of the sampled cube, centred on <see cref="centre"/>.</summary>
        public float extent = 1f;
        public Vector3 centre;
        /// <summary>Relative eigenvalue cut-off for the QEF pseudo-inverse. Higher = smoother, lower = sharper.</summary>
        public float singularThreshold = .05f;
        /// <summary>When false, vertices are the mass point: plain surface nets, for comparison.</summary>
        public bool solveQef = true;
        /// <summary>False-position iterations refining each edge crossing on the true field.</summary>
        public int crossingIterations = 6;
        /// <summary>
        /// Stone ↔ flesh. 0 places each vertex where the surface is (smooth or sharp); 1 snaps it to
        /// its cell centre, which turns every quad into an axis-aligned face of the dual grid — true
        /// voxel masonry. The topology is identical at every value, so the crossfade is continuous:
        /// the ESCHER-STEP-PLAN's cubed-to-rounded transition as a single dial.
        /// </summary>
        public float cubeness;
        /// <summary>
        /// Sample the field and solve the cells on worker threads. The field must then be safe to
        /// evaluate concurrently (pure: no Unity API, no shared mutable state) — every field in the
        /// engine is. Output is identical to the serial path.
        /// </summary>
        public bool parallel = true;

        /// <summary>Cells that produced a vertex, in the last run.</summary>
        public int ActiveCells { get; private set; }
        /// <summary>Vertices whose QEF solution left the cell and was clamped back.</summary>
        public int ClampedVertices { get; private set; }
        /// <summary>Vertices that were off the zero set and were Newton-projected back onto it.</summary>
        public int ProjectedVertices { get; private set; }

        float[] samples;
        int[] cellVertex;
        Vector3[] cellPoint;
        bool[] cellHas;
        int clampedCount, projectedCount;

        // Corner offsets: bit 0 = x, bit 1 = y, bit 2 = z; the 12 cube edges as corner pairs.
        static readonly int[] EdgeA = { 0, 1, 2, 0, 4, 5, 6, 4, 0, 1, 3, 2 };
        static readonly int[] EdgeB = { 1, 3, 3, 2, 5, 7, 7, 6, 4, 5, 7, 6 };

        public float Step => 2f * extent / Mathf.Max(resolution, 1);

        Vector3 Corner(int x, int y, int z) =>
            centre + new Vector3(-extent + x * Step, -extent + y * Step, -extent + z * Step);

        /// <summary>Extracts the zero set of <paramref name="field"/> into <paramref name="into"/> (appended).</summary>
        public void Extract<TField>(TField field, WireMeshBuilder into) where TField : IScalarField
        {
            int n = Mathf.Max(resolution, 2);
            int c = n + 1;
            if (samples == null || samples.Length != c * c * c) samples = new float[c * c * c];
            if (cellVertex == null || cellVertex.Length != n * n * n)
            {
                cellVertex = new int[n * n * n];
                cellPoint = new Vector3[n * n * n];
                cellHas = new bool[n * n * n];
            }
            clampedCount = 0; projectedCount = 0;
            bool threads = UseThreads(n);

            // Pass 1: the field at every corner. Independent per z-slab.
            if (threads) System.Threading.Tasks.Parallel.For(0, c, z => SampleSlab(field, z, c));
            else for (int z = 0; z < c; z++) SampleSlab(field, z, c);

            // Pass 2: one vertex per surface cell. Independent per cell; each slab gets its own
            // eigen scratch so threads never share it.
            if (threads) System.Threading.Tasks.Parallel.For(0, n, z => PlaceSlab(field, z, n, new double[3, 3], new double[3, 3]));
            else
            {
                double[,] a = new double[3, 3], v = new double[3, 3];
                for (int z = 0; z < n; z++) PlaceSlab(field, z, n, a, v);
            }

            // Pass 3: number the vertices in cell order, so the output is the same however the
            // work above was scheduled.
            ActiveCells = 0;
            for (int i = 0; i < n * n * n; i++) cellVertex[i] = cellHas[i] ? ActiveCells++ : -1;
            ClampedVertices = clampedCount; ProjectedVertices = projectedCount;

            // A quad across each sign-changing interior edge.
            for (int z = 1; z < n; z++)
            for (int y = 1; y < n; y++)
            for (int x = 1; x < n; x++)
            {
                float v0 = S(x, y, z);
                for (int axis = 0; axis < 3; axis++)
                {
                    float v1 = axis == 0 ? S(x + 1, y, z) : axis == 1 ? S(x, y + 1, z) : S(x, y, z + 1);
                    if ((v0 < 0f) == (v1 < 0f)) continue;
                    int o1 = (axis + 1) % 3, o2 = (axis + 2) % 3;
                    int c0 = CellAt(n, x, y, z, 0, 0, o1, o2), c1 = CellAt(n, x, y, z, -1, 0, o1, o2);
                    int c2 = CellAt(n, x, y, z, -1, -1, o1, o2), c3 = CellAt(n, x, y, z, 0, -1, o1, o2);
                    if (c0 < 0 || c1 < 0 || c2 < 0 || c3 < 0) continue;
                    if (cellVertex[c0] < 0 || cellVertex[c1] < 0 || cellVertex[c2] < 0 || cellVertex[c3] < 0) continue;

                    Vector3 p0 = cellPoint[c0], p1 = cellPoint[c1], p2 = cellPoint[c2], p3 = cellPoint[c3];
                    // Face the outside: winding follows the edge direction when its start is inside.
                    if (v0 < 0f) into.Quad(p0, p1, p2, p3);
                    else into.Quad(p0, p3, p2, p1);
                }
            }
        }

        bool UseThreads(int n)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;   // no threads on WebGL
#else
            return parallel && n >= 16 && System.Environment.ProcessorCount > 1;
#endif
        }

        void SampleSlab<TField>(TField field, int z, int c) where TField : IScalarField
        {
            for (int y = 0; y < c; y++)
            for (int x = 0; x < c; x++)
            {
                float v = field.Sample(Corner(x, y, z));
                // Exact zeros make the sign test ambiguous; nudge them outside.
                samples[(z * c + y) * c + x] = v == 0f || float.IsNaN(v) ? 1e-9f : v;
            }
        }

        void PlaceSlab<TField>(TField field, int z, int n, double[,] eigA, double[,] eigV) where TField : IScalarField
        {
            float step = Step;
            int clamped = 0, projected = 0;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int cell = (z * n + y) * n + x;
                Vector3 lo = Corner(x, y, z);
                cellHas[cell] = PlaceVertex(field, x, y, z, lo, step, eigA, eigV, ref clamped, ref projected, out Vector3 p);
                if (cellHas[cell]) cellPoint[cell] = p;
            }
            if (clamped != 0) System.Threading.Interlocked.Add(ref clampedCount, clamped);
            if (projected != 0) System.Threading.Interlocked.Add(ref projectedCount, projected);
        }

        float S(int x, int y, int z)
        {
            int c = resolution + 1;
            return samples[(z * c + y) * c + x];
        }

        static int CellAt(int n, int x, int y, int z, int d1, int d2, int o1, int o2)
        {
            int cx = x, cy = y, cz = z;
            if (o1 == 0) cx += d1; else if (o1 == 1) cy += d1; else cz += d1;
            if (o2 == 0) cx += d2; else if (o2 == 1) cy += d2; else cz += d2;
            if (cx < 0 || cy < 0 || cz < 0 || cx >= n || cy >= n || cz >= n) return -1;
            return (cz * n + cy) * n + cx;
        }

        /// <summary>Hermite data for one cell, then the QEF minimiser (or the mass point).</summary>
        bool PlaceVertex<TField>(TField field, int x, int y, int z, Vector3 lo, float step,
                                 double[,] eigA, double[,] eigV, ref int clamped, ref int projected, out Vector3 result)
            where TField : IScalarField
        {
            // AᵀA (symmetric, upper triangle), Aᵀb, mass point.
            double a00 = 0, a01 = 0, a02 = 0, a11 = 0, a12 = 0, a22 = 0, b0 = 0, b1 = 0, b2 = 0;
            Vector3 mass = Vector3.zero;
            int hits = 0;

            for (int e = 0; e < 12; e++)
            {
                int ca = EdgeA[e], cb = EdgeB[e];
                int ax = x + (ca & 1), ay = y + ((ca >> 1) & 1), az = z + ((ca >> 2) & 1);
                int bx = x + (cb & 1), by = y + ((cb >> 1) & 1), bz = z + ((cb >> 2) & 1);
                float va = S(ax, ay, az), vb = S(bx, by, bz);
                if ((va < 0f) == (vb < 0f)) continue;

                Vector3 pa = Corner(ax, ay, az), pb = Corner(bx, by, bz);
                Vector3 p = Crossing(field, pa, pb, va, vb);
                mass += p;
                hits++;
                if (!solveQef) continue;

                Vector3 nrm = Gradient(field, p, step * .0001f);
                float len = nrm.magnitude;
                if (len < 1e-12f || float.IsNaN(len)) continue;
                nrm /= len;
                double nx = nrm.x, ny = nrm.y, nz = nrm.z, d = nx * p.x + ny * p.y + nz * p.z;
                a00 += nx * nx; a01 += nx * ny; a02 += nx * nz;
                a11 += ny * ny; a12 += ny * nz; a22 += nz * nz;
                b0 += nx * d; b1 += ny * d; b2 += nz * d;
            }

            if (hits == 0) { result = default; return false; }
            mass /= hits;
            if (!solveQef) { result = Cube(mass, lo, step); return true; }

            // Solve about the mass point: x = c + A⁺ (Aᵀb − AᵀA c).
            double cx = mass.x, cy = mass.y, cz = mass.z;
            double r0 = b0 - (a00 * cx + a01 * cy + a02 * cz);
            double r1 = b1 - (a01 * cx + a11 * cy + a12 * cz);
            double r2 = b2 - (a02 * cx + a12 * cy + a22 * cz);
            PseudoInverseSolve(a00, a01, a02, a11, a12, a22, r0, r1, r2, singularThreshold, eigA, eigV,
                               out double dx, out double dy, out double dz);
            var solved = new Vector3((float)(cx + dx), (float)(cy + dy), (float)(cz + dz));

            Vector3 hi = lo + Vector3.one * step;
            var placed = Vector3.Min(Vector3.Max(solved, lo), hi);
            if (!TopologyReport.IsFinite(placed)) { result = Cube(mass, lo, step); return true; }
            if (placed != solved) clamped++;

            // The vertex must sit on the zero set. A QEF minimiser is on it wherever the Hermite
            // data is consistent — corners included — but a clamped solution, or one built from a
            // grid corner that sits exactly on the surface and flips sign with rounding, is not.
            // A few Newton steps along the gradient pull those back without leaving the cell;
            // vertices already on the surface are untouched, so sharp features stay sharp.
            float tolerance = step * 1e-4f;
            for (int i = 0; i < 4; i++)
            {
                float f = field.Sample(placed);
                if (Mathf.Abs(f) <= tolerance || float.IsNaN(f)) break;
                if (i == 0) projected++;
                Vector3 g = Gradient(field, placed, step * .001f);
                float g2 = g.sqrMagnitude;
                if (g2 < 1e-12f) break;
                placed = Vector3.Min(Vector3.Max(placed - g * (f / g2), lo), hi);
            }
            if (!TopologyReport.IsFinite(placed)) placed = mass;
            result = Cube(placed, lo, step);
            return true;
        }

        /// <summary>Blends a vertex toward its cell centre by <see cref="cubeness"/>.</summary>
        Vector3 Cube(Vector3 p, Vector3 lo, float step)
        {
            if (cubeness <= 0f) return p;
            Vector3 centre = lo + Vector3.one * (step * .5f);
            return cubeness >= 1f ? centre : p + (centre - p) * cubeness;
        }

        /// <summary>Zero crossing on segment a–b by false position (Illinois), from the sampled ends.</summary>
        Vector3 Crossing<TField>(TField field, Vector3 a, Vector3 b, float va, float vb) where TField : IScalarField
        {
            float ta = 0f, tb = 1f;
            int side = 0;
            float t = va / (va - vb);
            for (int i = 0; i < crossingIterations; i++)
            {
                t = (ta * vb - tb * va) / (vb - va);
                if (float.IsNaN(t)) { t = (ta + tb) * .5f; }
                float vt = field.Sample(Vector3.LerpUnclamped(a, b, t));
                if (float.IsNaN(vt) || vt == 0f) break;
                if ((vt < 0f) == (va < 0f))
                {
                    ta = t; va = vt;
                    if (side == -1) vb *= .5f;
                    side = -1;
                }
                else
                {
                    tb = t; vb = vt;
                    if (side == 1) va *= .5f;
                    side = 1;
                }
            }
            return Vector3.LerpUnclamped(a, b, Mathf.Clamp01(t));
        }

        static Vector3 Gradient<TField>(TField field, Vector3 p, float h) where TField : IScalarField
        {
            var dx = new Vector3(h, 0f, 0f); var dy = new Vector3(0f, h, 0f); var dz = new Vector3(0f, 0f, h);
            return new Vector3(field.Sample(p + dx) - field.Sample(p - dx),
                               field.Sample(p + dy) - field.Sample(p - dy),
                               field.Sample(p + dz) - field.Sample(p - dz)) / (2f * h);
        }

        /// <summary>
        /// Solves M x = r for symmetric 3×3 M via its eigen-decomposition, zeroing the components
        /// along eigenvectors whose eigenvalue is below <paramref name="relative"/> × the largest.
        /// <paramref name="a"/> and <paramref name="v"/> are 3×3 scratch, overwritten.
        /// </summary>
        public static void PseudoInverseSolve(double m00, double m01, double m02, double m11, double m12, double m22,
                                              double r0, double r1, double r2, double relative,
                                              double[,] a, double[,] v,
                                              out double x0, out double x1, out double x2)
        {
            a[0, 0] = m00; a[0, 1] = m01; a[0, 2] = m02;
            a[1, 0] = m01; a[1, 1] = m11; a[1, 2] = m12;
            a[2, 0] = m02; a[2, 1] = m12; a[2, 2] = m22;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) v[i, j] = i == j ? 1 : 0;
            Jacobi(a, v);
            double max = Math.Max(Math.Abs(a[0, 0]), Math.Max(Math.Abs(a[1, 1]), Math.Abs(a[2, 2])));
            double cut = Math.Max(max * relative, 1e-12);
            x0 = x1 = x2 = 0;
            for (int k = 0; k < 3; k++)
            {
                double lambda = a[k, k];
                if (Math.Abs(lambda) < cut) continue;
                double proj = (v[0, k] * r0 + v[1, k] * r1 + v[2, k] * r2) / lambda;
                x0 += v[0, k] * proj; x1 += v[1, k] * proj; x2 += v[2, k] * proj;
            }
        }

        /// <summary>Cyclic Jacobi eigenvalue iteration: a becomes diagonal, v's columns the eigenvectors.</summary>
        static void Jacobi(double[,] a, double[,] v)
        {
            for (int sweep = 0; sweep < 24; sweep++)
            {
                double off = a[0, 1] * a[0, 1] + a[0, 2] * a[0, 2] + a[1, 2] * a[1, 2];
                if (off < 1e-24) return;
                for (int p = 0; p < 2; p++)
                for (int q = p + 1; q < 3; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-30) continue;
                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double t = Math.Sign(theta == 0 ? 1 : theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    double c = 1 / Math.Sqrt(t * t + 1), s = t * c;
                    for (int k = 0; k < 3; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = c * akp - s * akq; a[k, q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk; a[q, k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq; v[k, q] = s * vkp + c * vkq;
                    }
                }
            }
        }
    }
}
