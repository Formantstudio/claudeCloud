using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PsychedelicLab.EscherWorld
{
    /// <summary>
    /// How a room's surface is turned into geometry. The same field through either of these is the
    /// same room built of stone or of flesh, which is the register the Escher plan is after.
    /// </summary>
    public enum RoomExtraction
    {
        /// <summary>Marching cubes. Smooth, follows the surface, organic.</summary>
        Rounded,
        /// <summary>Voxel faces. Blocky — steps, landings, masonry.</summary>
        Cubed,
        /// <summary>Marching cubes with vertices snapped to the lattice. Blocks that keep the surface's normals, so it can crossfade from Rounded.</summary>
        Quantised
    }

    /// <summary>
    /// Builds a room's mesh from an <see cref="EscherFieldSettings"/>.
    ///
    /// Marching cubes rather than surface nets because the table is exact and the triangles follow
    /// the surface properly at thin walls, which a room full of corridors is made of. The table
    /// and its conventions come from <see cref="EscherMarchingTable"/>.
    ///
    /// Vertices are unwelded — six per quad, nothing shared — so each triangle can carry its own
    /// barycentric coordinates in UV1 and the chamber wire shaders work on the result. That is the
    /// one thing a stock marching-cubes implementation does not give you.
    ///
    /// Cost is O(resolution^3) field samples per build, so this is not a per-frame operation.
    /// `Rebuild` is called when something changes, not every frame. Sampling runs on worker
    /// threads from 24 cells up, each z-slab writing its own part of the sample array, and so does
    /// marching, each run of slabs into its own buffers appended in z order — the result is
    /// identical to the serial pass. Marching computes only the edges a case uses.
    /// </summary>
    public static class EscherSurfaceBuilder
    {
        /// <summary>Worker threads for sampling and marching. Off gives the serial pass, for profiling and the parity test.</summary>
        public static bool UseWorkerThreads = true;

        /// <summary>Reusable buffers, so a rebuild does not allocate.</summary>
        public sealed class Scratch
        {
            public float[] samples;
            public int resolution;
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector3> bary = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            /// <summary>Crossing points of the current cell, per scratch so two rooms can build at once.</summary>
            public readonly Vector3[] edgePoints = new Vector3[12];
            /// <summary>One scratch per z-chunk when marching runs on worker threads; merged in order.</summary>
            public readonly List<Scratch> parts = new List<Scratch>();

            public void Clear()
            {
                vertices.Clear(); normals.Clear(); bary.Clear(); uv.Clear(); triangles.Clear();
            }
        }

        /// <summary>
        /// Fills `mesh` with the surface. `extent` is the half-size of the sampled box in local
        /// units; `resolution` is cells per axis.
        /// </summary>
        public static void Build(Mesh mesh, Scratch scratch, EscherFieldSettings field,
                                 RoomExtraction extraction, int resolution, float extent,
                                 float time, bool hideQuadDiagonals = true,
                                 Vector3 latticeOffset = default, float wSlice = 0f)
        {
            if (mesh == null || scratch == null || field == null) return;
            resolution = Mathf.Clamp(resolution, 8, 96);

            Sample(scratch, field, resolution, extent, time, latticeOffset, wSlice);
            scratch.Clear();

            switch (extraction)
            {
                case RoomExtraction.Cubed:
                    Voxels(scratch, resolution, extent, hideQuadDiagonals);
                    break;
                default:
                    March(scratch, field, resolution, extent, time,
                          extraction == RoomExtraction.Quantised);
                    break;
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(scratch.vertices);
            if (scratch.normals.Count == scratch.vertices.Count) mesh.SetNormals(scratch.normals);
            mesh.SetUVs(0, scratch.uv);
            mesh.SetUVs(1, scratch.bary);
            mesh.SetTriangles(scratch.triangles, 0);
            // Curved World displaces vertices on the GPU, outside the straight mesh bounds.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * Mathf.Max(200f, extent * 4f));
            if (scratch.normals.Count != scratch.vertices.Count) mesh.RecalculateNormals();
        }

        // ---- sampling --------------------------------------------------------

        static void Sample(Scratch scratch, EscherFieldSettings field, int resolution,
                           float extent, float time, Vector3 latticeOffset, float wSlice)
        {
            int corners = resolution + 1;
            int count = corners * corners * corners;
            if (scratch.samples == null || scratch.samples.Length != count)
                scratch.samples = new float[count];
            scratch.resolution = resolution;

            var samples = scratch.samples;
            float step = 2f / resolution, w = field.w + wSlice;
            void Slab(int z)
            {
                int i = z * corners * corners;
                for (int y = 0; y < corners; y++)
                for (int x = 0; x < corners; x++)
                    // The offset is added to the *sample* position, not to the transform: the room
                    // stays still in world space while the infinite lattice slides through it. That
                    // is what makes the corridor endless without new geometry.
                    samples[i++] = EscherFields.Sample(field,
                        new Vector3(-1f + x * step, -1f + y * step, -1f + z * step) * extent
                        + latticeOffset, w, time);
            }
            // The fields are pure functions of their settings, so slabs are independent. Below 24
            // cells the thread hand-off costs more than it saves.
            if (UseWorkerThreads && resolution >= 24) System.Threading.Tasks.Parallel.For(0, corners, Slab);
            else for (int z = 0; z < corners; z++) Slab(z);
        }

        static float At(Scratch s, int x, int y, int z)
        {
            int c = s.resolution + 1;
            return s.samples[(z * c + y) * c + x];
        }

        static Vector3 Position(int resolution, float extent, Vector3 cell) =>
            new Vector3(-1f + cell.x * (2f / resolution),
                        -1f + cell.y * (2f / resolution),
                        -1f + cell.z * (2f / resolution)) * extent;

        // ---- marching cubes --------------------------------------------------

        /// <summary>Edges each case's triangles use, as a 12-bit mask, so a cell computes only those.</summary>
        // Declared before caseEdges: static initialisers run in textual order, and BuildCaseEdges fills these.
        static readonly int[] edgeA = new int[12], edgeB = new int[12];
        static readonly int[] caseEdges = BuildCaseEdges();
        static readonly Vector3Int[] cornerOffsets = BuildCorners();

        static int[] BuildCaseEdges()
        {
            var masks = new int[256];
            for (int c = 0; c < 256; c++)
                for (int slot = 0; slot < 15; slot++)
                {
                    int e = EscherMarchingTable.Edge(EscherMarchingTable.Cases[c], slot);
                    if (e == 0xf) break;
                    masks[c] |= 1 << e;
                }
            for (int e = 0; e < 12; e++) EscherMarchingTable.EdgeCorners(e, out edgeA[e], out edgeB[e]);
            return masks;
        }

        static Vector3Int[] BuildCorners()
        {
            var o = new Vector3Int[8];
            for (int c = 0; c < 8; c++) o[c] = EscherMarchingTable.Corner(c);
            return o;
        }

        static void March(Scratch scratch, EscherFieldSettings field, int resolution,
                          float extent, float time, bool quantise)
        {
            int n = resolution;
            if (!UseWorkerThreads || n < 24) { MarchSlabs(scratch, scratch.samples, n, extent, quantise, 0, n); return; }

            // Worker threads, each marching a run of z-slabs into its own buffers, then appended in
            // z order: the same triangles in the same order as the serial pass.
            int chunks = Mathf.Clamp(Environment.ProcessorCount * 2, 2, n);
            while (scratch.parts.Count < chunks) scratch.parts.Add(new Scratch());
            System.Threading.Tasks.Parallel.For(0, chunks, k =>
            {
                var part = scratch.parts[k];
                part.Clear();
                MarchSlabs(part, scratch.samples, n, extent, quantise, n * k / chunks, n * (k + 1) / chunks);
            });
            for (int k = 0; k < chunks; k++)
            {
                var part = scratch.parts[k];
                int offset = scratch.vertices.Count;
                scratch.vertices.AddRange(part.vertices);
                scratch.normals.AddRange(part.normals);
                scratch.bary.AddRange(part.bary);
                scratch.uv.AddRange(part.uv);
                foreach (int t in part.triangles) scratch.triangles.Add(offset + t);
            }
        }

        static void MarchSlabs(Scratch scratch, float[] samples, int n, float extent, bool quantise, int z0, int z1)
        {
            int c1 = n + 1;
            float cellSize = 2f * extent / n;
            var edgePoints = scratch.edgePoints;
            // Sample-array offset of each cube corner from the cell's own corner.
            Span<int> offset = stackalloc int[8];
            for (int c = 0; c < 8; c++) offset[c] = (cornerOffsets[c].z * c1 + cornerOffsets[c].y) * c1 + cornerOffsets[c].x;

            for (int z = z0; z < z1; z++)
            for (int y = 0; y < n; y++)
            {
                int row = (z * c1 + y) * c1;
                for (int x = 0; x < n; x++)
                {
                    int cellBase = row + x;
                    // Case selector: bit i set when corner i is below the isovalue, which for a
                    // signed field at iso 0 means inside.
                    int selector = 0;
                    for (int c = 0; c < 8; c++)
                        if (samples[cellBase + offset[c]] < 0f) selector |= 1 << c;
                    if (selector == 0 || selector == 255) continue;

                    // Crossing point on each edge this case uses.
                    int mask = caseEdges[selector];
                    for (int e = 0; e < 12; e++)
                    {
                        if ((mask & (1 << e)) == 0) continue;
                        int ca = edgeA[e], cb = edgeB[e];
                        var oa = cornerOffsets[ca];
                        var ob = cornerOffsets[cb];
                        float va = samples[cellBase + offset[ca]];
                        float vb = samples[cellBase + offset[cb]];
                        float t = Mathf.Approximately(va, vb) ? .5f : Mathf.Clamp01(va / (va - vb));
                        Vector3 cell = Vector3.Lerp(
                            new Vector3(x + oa.x, y + oa.y, z + oa.z),
                            new Vector3(x + ob.x, y + ob.y, z + ob.z), t);
                        Vector3 p = Position(n, extent, cell);
                        // Quantised: snap onto the lattice so the surface reads as blocks while still
                        // carrying the real normals. This is what crossfades to Cubed.
                        if (quantise)
                            p = new Vector3(Mathf.Round(p.x / cellSize) * cellSize,
                                            Mathf.Round(p.y / cellSize) * cellSize,
                                            Mathf.Round(p.z / cellSize) * cellSize);
                        edgePoints[e] = p;
                    }

                    ulong packed = EscherMarchingTable.Cases[selector];
                    for (int slot = 0; slot < 15; slot += 3)
                    {
                        int e0 = EscherMarchingTable.Edge(packed, slot);
                        if (e0 == 0xf) break;
                        int e1 = EscherMarchingTable.Edge(packed, slot + 1);
                        int e2 = EscherMarchingTable.Edge(packed, slot + 2);

                        Vector3 a = edgePoints[e0], b = edgePoints[e1], c = edgePoints[e2];
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        if (normal.sqrMagnitude < 1e-12f) continue;     // degenerate, skip it
                        normal.Normalize();

                        EmitTriangle(scratch, a, b, c, normal, extent);
                    }
                }
            }
        }

        /// <summary>
        /// One unwelded triangle with barycentric coordinates in UV1 and triplanar UV0.
        ///
        /// All three bary components are written plainly here, so every edge of every triangle
        /// draws. Marching cubes has no quads to hide a diagonal of — that trick only applies to
        /// grid meshes.
        /// </summary>
        static void EmitTriangle(Scratch s, Vector3 a, Vector3 b, Vector3 c, Vector3 normal, float extent)
        {
            int i = s.vertices.Count;

            s.vertices.Add(a); s.vertices.Add(b); s.vertices.Add(c);
            s.normals.Add(normal); s.normals.Add(normal); s.normals.Add(normal);
            s.bary.Add(new Vector3(1, 0, 0));
            s.bary.Add(new Vector3(0, 1, 0));
            s.bary.Add(new Vector3(0, 0, 1));
            // No parameterisation exists on an implicit surface, so UV0 is triplanar: project on
            // whichever axis the normal points at least, which keeps the lattice square.
            s.uv.Add(Triplanar(a, normal, extent));
            s.uv.Add(Triplanar(b, normal, extent));
            s.uv.Add(Triplanar(c, normal, extent));
            s.triangles.Add(i); s.triangles.Add(i + 1); s.triangles.Add(i + 2);
        }

        static Vector2 Triplanar(Vector3 p, Vector3 n, float extent)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 uv = az >= ax && az >= ay ? new Vector2(p.x, p.y)
                       : ax >= ay ? new Vector2(p.z, p.y)
                       : new Vector2(p.x, p.z);
            // Normalised, so a shader multiplying UV by a lattice density behaves.
            return uv / Mathf.Max(extent * 2f, 1e-4f) + new Vector2(.5f, .5f);
        }

        // ---- cubed: voxel faces ---------------------------------------------

        static readonly Vector3[] axes =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        /// <summary>
        /// Emits a face wherever a solid cell meets an empty one. True blocks: steps, landings,
        /// masonry. A cell counts as solid when its own corner is inside.
        /// </summary>
        static void Voxels(Scratch scratch, int resolution, float extent, bool hideQuadDiagonals)
        {
            int n = resolution;
            float cell = 2f * extent / n;
            float half = cell * .5f;
            float diag = hideQuadDiagonals ? 1f : 0f;

            bool Solid(int x, int y, int z) =>
                x >= 0 && y >= 0 && z >= 0 && x < n && y < n && z < n &&
                At(scratch, x, y, z) < 0f;

            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (!Solid(x, y, z)) continue;
                Vector3 centre = Position(n, extent, new Vector3(x + .5f, y + .5f, z + .5f));

                for (int f = 0; f < 6; f++)
                {
                    Vector3 d = axes[f];
                    if (Solid(x + (int)d.x, y + (int)d.y, z + (int)d.z)) continue;

                    // Two in-plane axes for this face.
                    Vector3 u = new Vector3(d.y, d.z, d.x);
                    Vector3 v = Vector3.Cross(d, u);

                    Vector3 p0 = centre + (d - u - v) * half;
                    Vector3 p1 = centre + (d + u - v) * half;
                    Vector3 p2 = centre + (d + u + v) * half;
                    Vector3 p3 = centre + (d - u + v) * half;
                    EmitQuad(scratch, p0, p1, p2, p3, d, diag, extent);
                }
            }
        }

        /// <summary>
        /// An unwelded quad as two triangles. The shared diagonal is suppressed by adding 1 to the
        /// barycentric component that vanishes along it, keeping that component in [1,2] so the
        /// wire never draws there — masonry wants square panels, not triangles.
        /// </summary>
        static void EmitQuad(Scratch s, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                             Vector3 normal, float diag, float extent)
        {
            int i = s.vertices.Count;

            s.vertices.Add(a); s.vertices.Add(b); s.vertices.Add(c);
            s.vertices.Add(a); s.vertices.Add(c); s.vertices.Add(d);
            for (int k = 0; k < 6; k++) s.normals.Add(normal);

            // (a,b,c): the diagonal a-c vanishes in component 1.
            s.bary.Add(new Vector3(1, diag, 0));
            s.bary.Add(new Vector3(0, 1 + diag, 0));
            s.bary.Add(new Vector3(0, diag, 1));
            // (a,c,d): it vanishes in component 2.
            s.bary.Add(new Vector3(1, 0, diag));
            s.bary.Add(new Vector3(0, 1, diag));
            s.bary.Add(new Vector3(0, 0, 1 + diag));

            s.uv.Add(new Vector2(0, 0)); s.uv.Add(new Vector2(1, 0)); s.uv.Add(new Vector2(1, 1));
            s.uv.Add(new Vector2(0, 0)); s.uv.Add(new Vector2(1, 1)); s.uv.Add(new Vector2(0, 1));

            for (int k = 0; k < 6; k++) s.triangles.Add(i + k);
        }
    }
}
