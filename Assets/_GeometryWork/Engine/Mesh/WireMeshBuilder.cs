using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The one place the wire pipeline's invariants are written down, so every engine in
    /// <c>_GeometryWork/Engine</c> builds meshes the same way:
    ///
    /// - unwelded: every triangle owns its vertices, six per quad, so each can carry its own
    ///   barycentric coordinates in UV1;
    /// - quad diagonals suppressed by adding 1 to the bary component that vanishes along the shared
    ///   diagonal (keeping it in [1,2]), exactly as the chambers do;
    /// - 32-bit indices always, and fixed 200-unit bounds so GPU displacement is never culled.
    ///
    /// Buffers are kept between builds, so a rebuild at the same resolution does not allocate.
    /// </summary>
    public sealed class WireMeshBuilder
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector3> bary = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();

        /// <summary>When true the diagonal splitting each quad does not draw.</summary>
        public bool hideQuadDiagonals = true;

        /// <summary>Half-size of the bounds written to the mesh. Never smaller than 100 (200 across).</summary>
        public float boundsExtent = 100f;

        public int VertexCount => vertices.Count;
        public int TriangleCount => triangles.Count / 3;

        public void Clear()
        {
            vertices.Clear(); normals.Clear(); bary.Clear(); uv.Clear(); triangles.Clear();
        }

        /// <summary>One unwelded triangle with every edge drawn.</summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 n = FaceNormal(a, b, c);
            int i = vertices.Count;
            Add(a, n, new Vector3(1, 0, 0), ua);
            Add(b, n, new Vector3(0, 1, 0), ub);
            Add(c, n, new Vector3(0, 0, 1), uc);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }

        /// <summary>
        /// One unwelded quad a-b-c-d (counter-clockwise seen from the front) as (a,b,c) and (a,c,d).
        /// The a-c diagonal vanishes in bary component 1 of the first triangle and component 2 of
        /// the second; those get +1 when diagonals are hidden.
        /// </summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            float diag = hideQuadDiagonals ? 1f : 0f;
            Vector3 n1 = FaceNormal(a, b, c), n2 = FaceNormal(a, c, d);
            int i = vertices.Count;
            Add(a, n1, new Vector3(1, diag, 0), ua);
            Add(b, n1, new Vector3(0, 1 + diag, 0), ub);
            Add(c, n1, new Vector3(0, diag, 1), uc);
            Add(a, n2, new Vector3(1, 0, diag), ua);
            Add(c, n2, new Vector3(0, 1, diag), uc);
            Add(d, n2, new Vector3(0, 0, 1 + diag), ud);
            for (int k = 0; k < 6; k++) triangles.Add(i + k);
        }

        /// <summary>Quad with a plain unit-square UV.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) =>
            Quad(a, b, c, d, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));

        /// <summary>
        /// A grid of (columns + 1) x (rows + 1) points, row-major (<c>points[row * (columns + 1) + column]</c>),
        /// emitted as columns x rows quads with normalised UV0. Seams and poles are the caller's:
        /// sample u = 1 equal to u = 0 for a closed direction, and the welded topology follows.
        /// </summary>
        public void Grid(IList<Vector3> points, int columns, int rows)
        {
            int stride = columns + 1;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                float u0 = (float)c / columns, u1 = (float)(c + 1) / columns;
                float v0 = (float)r / rows, v1 = (float)(r + 1) / rows;
                Quad(points[r * stride + c], points[r * stride + c + 1],
                     points[(r + 1) * stride + c + 1], points[(r + 1) * stride + c],
                     new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1));
            }
        }

        void Add(Vector3 p, Vector3 n, Vector3 b, Vector2 t)
        {
            vertices.Add(p); normals.Add(n); bary.Add(b); uv.Add(t);
        }

        /// <summary>Unit face normal, or zero for a degenerate triangle (never NaN).</summary>
        public static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            float m = n.magnitude;
            return m > 1e-12f ? n / m : Vector3.zero;
        }

        /// <summary>Writes the buffers into <paramref name="mesh"/> with the pipeline invariants applied.</summary>
        public void Apply(Mesh mesh)
        {
            if (mesh == null) return;
            mesh.Clear();
            // Unwelding costs 6 vertices per quad, so 65,535 goes quickly. Always 32-bit.
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, bary);
            mesh.SetTriangles(triangles, 0);
            // GPU displacement (Curved World, 4-D projection shaders) moves vertices outside the
            // straight bounds; fixed generous bounds keep the renderer from culling them.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (2f * Mathf.Max(boundsExtent, 100f)));
        }
    }
}
