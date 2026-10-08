using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.Control
{
    // Builds real fractal geometry as ordinary meshes, so they can be warped by Object Warp and reconstructed by the Metavido stage.
    //   Menger sponge     : a cube with the centre of every face and the middle removed, again on every remaining cube (level 2 = 400
    //                       cubes, level 3 = 8000).
    //   Sierpinski pyramid: a tetrahedron made of four half-size tetrahedra, repeated (level 5 = 1024 tetrahedra).
    //   Mandelbulb        : the 3D Mandelbulb set (power 8 by default) sampled on a grid and turned into voxels, keeping only the faces
    //                       that see the outside.
    // All are about 1 unit across (half-size 0.5) and centred on the origin.
    public static class FractalMeshFactory
    {
        static readonly Vector3[] Axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        public static Mesh MengerSponge(int level)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Menger(vertices, triangles, Vector3.zero, 0.5f, Mathf.Clamp(level, 0, 3));
            return Finish("Menger sponge L" + level, vertices, triangles);
        }

        static void Menger(List<Vector3> v, List<int> t, Vector3 center, float half, int level)
        {
            if (level == 0) { AddBox(v, t, center, half); return; }
            float third = half * 2f / 3f;   // size of a sub-cube
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        int middles = (x == 0 ? 1 : 0) + (y == 0 ? 1 : 0) + (z == 0 ? 1 : 0);
                        if (middles >= 2) continue;   // the centre of each face and the middle of the cube are removed
                        Menger(v, t, center + new Vector3(x, y, z) * third, half / 3f, level - 1);
                    }
        }

        static void AddBox(List<Vector3> v, List<int> t, Vector3 center, float half)
        {
            foreach (var n in Axes)
            {
                var u = new Vector3(n.y, n.z, n.x);
                var w = Vector3.Cross(n, u);
                int i = v.Count;
                v.Add(center + (n - u - w) * half); v.Add(center + (n + u - w) * half);
                v.Add(center + (n + u + w) * half); v.Add(center + (n - u + w) * half);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
        }

        public static Mesh SierpinskiPyramid(int level)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Sierpinski(vertices, triangles, Vector3.zero, 0.5f, Mathf.Clamp(level, 0, 6));
            return Finish("Sierpinski pyramid L" + level, vertices, triangles);
        }

        static readonly Vector3[] Tetra = { new Vector3(1, 1, 1), new Vector3(1, -1, -1), new Vector3(-1, 1, -1), new Vector3(-1, -1, 1) };

        static void Sierpinski(List<Vector3> v, List<int> t, Vector3 center, float scale, int level)
        {
            if (level == 0)
            {
                var c = new Vector3[4];
                for (int k = 0; k < 4; k++) c[k] = center + Tetra[k] * scale;
                AddFace(v, t, center, c[0], c[1], c[2]); AddFace(v, t, center, c[0], c[3], c[1]);
                AddFace(v, t, center, c[0], c[2], c[3]); AddFace(v, t, center, c[1], c[3], c[2]);
                return;
            }
            for (int k = 0; k < 4; k++) Sierpinski(v, t, center + Tetra[k] * scale * 0.5f, scale * 0.5f, level - 1);
        }

        // One triangle, wound so it faces away from `inside`.
        static void AddFace(List<Vector3> v, List<int> t, Vector3 inside, Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f) { var s = b; b = c; c = s; }
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public static Mesh MandelbulbVoxels(int resolution, float power, int iterations)
        {
            int n = Mathf.Clamp(resolution, 16, 96);
            iterations = Mathf.Clamp(iterations, 4, 16);
            const float extent = 1.25f;   // the bulb fits inside a cube of half-size ~1.2
            var solid = new bool[n, n, n];
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    for (int z = 0; z < n; z++)
                    {
                        var c = new Vector3(((x + 0.5f) / n * 2f - 1f) * extent, ((y + 0.5f) / n * 2f - 1f) * extent, ((z + 0.5f) / n * 2f - 1f) * extent);
                        solid[x, y, z] = InsideBulb(c, power, iterations);
                    }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float cell = 2f * extent / n;
            float half = cell * 0.5f;
            float scale = 0.5f / extent;   // bring the bulb to about 1 unit across
            for (int x = 0; x < n; x++)
                for (int y = 0; y < n; y++)
                    for (int z = 0; z < n; z++)
                    {
                        if (!solid[x, y, z]) continue;
                        var center = new Vector3(((x + 0.5f) / n * 2f - 1f) * extent, ((y + 0.5f) / n * 2f - 1f) * extent, ((z + 0.5f) / n * 2f - 1f) * extent);
                        foreach (var d in Axes)
                        {
                            int nx = x + (int)d.x, ny = y + (int)d.y, nz = z + (int)d.z;
                            bool open = nx < 0 || ny < 0 || nz < 0 || nx >= n || ny >= n || nz >= n || !solid[nx, ny, nz];
                            if (!open) continue;
                            var u = new Vector3(d.y, d.z, d.x);
                            var w = Vector3.Cross(d, u);
                            int i = vertices.Count;
                            vertices.Add((center + (d - u - w) * half) * scale); vertices.Add((center + (d + u - w) * half) * scale);
                            vertices.Add((center + (d + u + w) * half) * scale); vertices.Add((center + (d - u + w) * half) * scale);
                            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
                        }
                    }
            return Finish("Mandelbulb " + n + "^3 p" + power, vertices, triangles);
        }

        // Does the point stay bounded under z -> z^power + c ? (the Mandelbulb iteration)
        static bool InsideBulb(Vector3 c, float power, int iterations)
        {
            Vector3 z = c;
            for (int i = 0; i < iterations; i++)
            {
                float r = z.magnitude;
                if (r > 2f) return false;
                if (r < 1e-6f) { z = c; continue; }
                float theta = Mathf.Acos(Mathf.Clamp(z.z / r, -1f, 1f)) * power;
                float phi = Mathf.Atan2(z.y, z.x) * power;
                float zr = Mathf.Pow(r, power);
                z = zr * new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta)) + c;
            }
            return true;
        }

        static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
