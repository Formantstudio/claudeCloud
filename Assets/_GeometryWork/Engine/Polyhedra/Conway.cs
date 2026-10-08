using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Conway polyhedron notation: seeds, operators, a parser, and Hart's canonical form.
    ///
    /// **Seeds.** T C O D I (the Platonic solids), Pn An Yn (n-gonal prism, antiprism, pyramid).
    ///
    /// **Primitive operators**, each written directly on the oriented face loops so every result is
    /// again a closed, consistently oriented manifold (V, E, F are the seed's):
    ///
    /// | op | name    | V′          | E′  | F′          |
    /// |----|---------|-------------|-----|-------------|
    /// | d  | dual    | F           | E   | V           |
    /// | a  | ambo    | E           | 2E  | F + V       |
    /// | k  | kis     | V + F       | 3E  | 2E          |
    /// | g  | gyro    | V + F + 2E  | 5E  | 2E          |
    /// | c  | chamfer | V + 2E      | 4E  | F + E       |
    /// | w  | whirl   | V + 4E      | 7E  | F + 2E      |
    /// | q  | quinto  | V + 3E      | 6E  | F + 2E      |
    /// | r  | reflect | V           | E   | F           |
    ///
    /// **Derived**, by their standard definitions: t = dkd (truncate), j = da (join), e = aa (expand),
    /// o = de (ortho), s = dgd (snub), b = ta (bevel), m = kj (meta), n = kd (needle). k and t take an
    /// optional side count, kn / tn: kis only the n-sided faces, truncate only the degree-n vertices.
    ///
    /// **Notation** reads right to left like function application: "tI" truncates the icosahedron
    /// (the soccer ball, 60 / 90 / 32), "dkdC" is the same as "tC", "gaD" gyrates the
    /// icosidodecahedron.
    ///
    /// **Positions.** Operators place new vertices simply (face centroids, edge thirds and midpoints;
    /// the dual by polar reciprocation) and rescale to unit mean radius, so the topology is exact and
    /// the shape is reasonable. <see cref="Canonicalize"/> then finds Hart's canonical form: every
    /// edge tangent to the unit sphere, every face planar, the tangent points centred on the origin.
    /// For an Archimedean result that is the uniform solid, all edges equal.
    /// </summary>
    public static class Conway
    {
        // ---- seeds -------------------------------------------------------------

        public static Polyhedron Tetrahedron()
        {
            var p = new Polyhedron { name = "T" };
            p.vertices.AddRange(new[] { new Vector3(1, 1, 1), new Vector3(1, -1, -1), new Vector3(-1, 1, -1), new Vector3(-1, -1, 1) });
            p.faces.Add(new[] { 0, 1, 2 }); p.faces.Add(new[] { 0, 2, 3 }); p.faces.Add(new[] { 0, 3, 1 }); p.faces.Add(new[] { 1, 3, 2 });
            return Seed(p);
        }

        public static Polyhedron Cube()
        {
            var p = new Polyhedron { name = "C" };
            for (int i = 0; i < 8; i++)
                p.vertices.Add(new Vector3((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1));
            for (int axis = 0; axis < 3; axis++)
            for (int side = 0; side < 2; side++)
            {
                int b = 1 << ((axis + 1) % 3), c = 1 << ((axis + 2) % 3), a = side == 1 ? 1 << axis : 0;
                p.faces.Add(new[] { a, a | b, a | b | c, a | c });
            }
            return Seed(p);
        }

        public static Polyhedron Octahedron() { var p = Dual(Cube()); p.name = "O"; return p; }

        public static Polyhedron Icosahedron()
        {
            var p = new Polyhedron { name = "I" };
            float phi = (1f + Mathf.Sqrt(5f)) * .5f;
            foreach (float s in new[] { -1f, 1f })
            foreach (float t in new[] { -phi, phi })
            {
                p.vertices.Add(new Vector3(0, s, t));
                p.vertices.Add(new Vector3(s, t, 0));
                p.vertices.Add(new Vector3(t, 0, s));
            }
            // Faces are the triples at mutual distance 2, the edge length.
            int n = p.vertices.Count;
            bool Adjacent(int i, int j) => Mathf.Abs((p.vertices[i] - p.vertices[j]).magnitude - 2f) < 1e-3f;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                if (!Adjacent(i, j)) continue;
                for (int k = j + 1; k < n; k++)
                    if (Adjacent(i, k) && Adjacent(j, k)) p.faces.Add(new[] { i, j, k });
            }
            return Seed(p);
        }

        public static Polyhedron Dodecahedron() { var p = Dual(Icosahedron()); p.name = "D"; return p; }

        /// <summary>n-gonal prism with unit edges.</summary>
        public static Polyhedron Prism(int n)
        {
            n = Mathf.Max(n, 3);
            var p = new Polyhedron { name = "P" + n };
            float r = .5f / Mathf.Sin(Mathf.PI / n);
            for (int i = 0; i < n; i++) p.vertices.Add(Ring(r, i, n, 0f, -.5f));
            for (int i = 0; i < n; i++) p.vertices.Add(Ring(r, i, n, 0f, .5f));
            p.faces.Add(Range(0, n)); p.faces.Add(Range(n, n));
            for (int i = 0; i < n; i++) p.faces.Add(new[] { i, (i + 1) % n, n + (i + 1) % n, n + i });
            return Seed(p);
        }

        /// <summary>n-gonal antiprism with unit edges (equilateral side triangles).</summary>
        public static Polyhedron Antiprism(int n)
        {
            n = Mathf.Max(n, 3);
            var p = new Polyhedron { name = "A" + n };
            float r = .5f / Mathf.Sin(Mathf.PI / n);
            float chord = 2f * r * Mathf.Sin(Mathf.PI / (2f * n));
            float h = Mathf.Sqrt(Mathf.Max(1f - chord * chord, .01f));
            for (int i = 0; i < n; i++) p.vertices.Add(Ring(r, i, n, 0f, -h * .5f));
            for (int i = 0; i < n; i++) p.vertices.Add(Ring(r, i, n, .5f, h * .5f));
            p.faces.Add(Range(0, n)); p.faces.Add(Range(n, n));
            for (int i = 0; i < n; i++)
            {
                int b0 = i, b1 = (i + 1) % n, t0 = n + i, t1 = n + (i + 1) % n;
                p.faces.Add(new[] { b0, b1, t0 });
                p.faces.Add(new[] { t0, b1, t1 });
            }
            return Seed(p);
        }

        /// <summary>n-gonal pyramid with unit edges where that is possible (n ≤ 5), otherwise a low cone.</summary>
        public static Polyhedron Pyramid(int n)
        {
            n = Mathf.Max(n, 3);
            var p = new Polyhedron { name = "Y" + n };
            float r = .5f / Mathf.Sin(Mathf.PI / n);
            float h = r < 1f ? Mathf.Sqrt(1f - r * r) : .5f;
            for (int i = 0; i < n; i++) p.vertices.Add(Ring(r, i, n, 0f, 0f));
            p.vertices.Add(new Vector3(0f, h, 0f));
            p.faces.Add(Range(0, n));
            for (int i = 0; i < n; i++) p.faces.Add(new[] { i, (i + 1) % n, n });
            return Seed(p);
        }

        static Vector3 Ring(float r, int i, int n, float offset, float y)
        {
            float a = (i + offset) * Mathf.PI * 2f / n;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        static int[] Range(int start, int count)
        {
            var a = new int[count];
            for (int i = 0; i < count; i++) a[i] = start + i;
            return a;
        }

        static Polyhedron Seed(Polyhedron p)
        {
            p.Normalize();
            p.OrientOutward();
            return p;
        }

        // ---- building blocks for the operators ----------------------------------

        /// <summary>New vertices are named by what they come from, so shared ones are made once.</summary>
        sealed class Builder
        {
            readonly Dictionary<(int, int, int), int> index = new Dictionary<(int, int, int), int>();
            public readonly Polyhedron result = new Polyhedron();

            public int V(int kind, int a, int b, Vector3 position)
            {
                if (!index.TryGetValue((kind, a, b), out int i))
                {
                    i = result.vertices.Count;
                    result.vertices.Add(position);
                    index[(kind, a, b)] = i;
                }
                return i;
            }

            public void Face(params int[] loop) => result.faces.Add(loop);

            public Polyhedron Done(string name)
            {
                result.name = name;
                result.Normalize();
                return result;
            }
        }

        // Vertex kinds.
        const int Original = 0, FaceCentre = 1, Midpoint = 2, EdgeThird = 3, FaceCorner = 4, Inner = 5;

        static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

        // ---- primitive operators ---------------------------------------------

        /// <summary>
        /// d: a vertex per face, at the pole of the face plane (polar reciprocation in the unit sphere,
        /// which takes a uniform solid to its true dual), and a face per vertex, through the faces
        /// around it in counter-clockwise order.
        /// </summary>
        public static Polyhedron Dual(Polyhedron p)
        {
            var b = new Builder();
            for (int f = 0; f < p.FaceCount; f++)
            {
                Vector3 n = p.FaceNormal(f), c = p.FaceCentroid(f);
                float h = Vector3.Dot(n, c);
                b.V(FaceCentre, f, 0, h > 1e-4f ? n / h : c);
            }
            var faceOf = p.EdgeFaces();
            var rings = p.NeighbourRings();
            for (int v = 0; v < p.VertexCount; v++)
            {
                var ring = rings[v];
                var loop = new int[ring.Count];
                for (int k = 0; k < ring.Count; k++) loop[k] = faceOf[Polyhedron.Key(v, ring[k])];
                b.Face(loop);
            }
            return b.Done("d" + p.name);
        }

        /// <summary>a: a vertex per edge midpoint; a face per face (its midpoints) and per vertex (the midpoints around it).</summary>
        public static Polyhedron Ambo(Polyhedron p)
        {
            var b = new Builder();
            int Mid(int u, int v) => b.V(Midpoint, Mathf.Min(u, v), Mathf.Max(u, v), (p.vertices[u] + p.vertices[v]) * .5f);
            foreach (var f in p.faces)
            {
                var loop = new int[f.Length];
                for (int i = 0; i < f.Length; i++) loop[i] = Mid(f[i], f[(i + 1) % f.Length]);
                b.Face(loop);
            }
            var rings = p.NeighbourRings();
            for (int v = 0; v < p.VertexCount; v++)
            {
                var ring = rings[v];
                var loop = new int[ring.Count];
                for (int k = 0; k < ring.Count; k++) loop[k] = Mid(v, ring[k]);
                b.Face(loop);
            }
            return b.Done("a" + p.name);
        }

        /// <summary>
        /// k: raises a pyramid on each face (or each n-sided face when <paramref name="sides"/> &gt; 0).
        /// The apex goes out along the face normal, halfway from the face to the mean radius;
        /// canonicalisation finds the true height.
        /// </summary>
        public static Polyhedron Kis(Polyhedron p, int sides = 0)
        {
            var b = new Builder();
            float radius = MeanRadius(p);
            for (int v = 0; v < p.VertexCount; v++) b.V(Original, v, 0, p.vertices[v]);
            for (int fi = 0; fi < p.FaceCount; fi++)
            {
                var f = p.faces[fi];
                if (sides > 0 && f.Length != sides) { b.Face((int[])f.Clone()); continue; }
                Vector3 c = p.FaceCentroid(fi), n = p.FaceNormal(fi);
                float lift = Mathf.Max(radius - Vector3.Dot(c, n), 0f) * .5f;
                int apex = b.V(FaceCentre, fi, 0, c + n * lift);
                for (int i = 0; i < f.Length; i++) b.Face(f[i], f[(i + 1) % f.Length], apex);
            }
            return b.Done("k" + (sides > 0 ? sides.ToString() : "") + p.name);
        }

        /// <summary>g: each face becomes a pinwheel of pentagons, one per corner, about its centre.</summary>
        public static Polyhedron Gyro(Polyhedron p)
        {
            var b = new Builder();
            for (int fi = 0; fi < p.FaceCount; fi++)
            {
                var f = p.faces[fi];
                int n = f.Length;
                int centre = b.V(FaceCentre, fi, 0, p.FaceCentroid(fi));
                for (int i = 0; i < n; i++)
                {
                    int v1 = f[i], v2 = f[(i + 1) % n], v3 = f[(i + 2) % n];
                    b.Face(centre, Third(b, p, v1, v2), Third(b, p, v2, v1), b.V(Original, v2, 0, p.vertices[v2]), Third(b, p, v2, v3));
                }
            }
            return b.Done("g" + p.name);
        }

        /// <summary>c: each face shrinks inside itself and each edge widens into a hexagon.</summary>
        public static Polyhedron Chamfer(Polyhedron p)
        {
            var b = new Builder();
            const float inset = .3f;
            var faceOf = p.EdgeFaces();
            int Corner(int f, int v) => b.V(FaceCorner, f, v, Lerp(p.vertices[v], p.FaceCentroid(f), inset));
            for (int fi = 0; fi < p.FaceCount; fi++)
            {
                var f = p.faces[fi];
                var loop = new int[f.Length];
                for (int i = 0; i < f.Length; i++) loop[i] = Corner(fi, f[i]);
                b.Face(loop);
            }
            for (int fi = 0; fi < p.FaceCount; fi++)
            {
                var f = p.faces[fi];
                for (int i = 0; i < f.Length; i++)
                {
                    int a = f[i], c = f[(i + 1) % f.Length];
                    if (a > c) continue;                       // once per edge, from the face holding a→c
                    int g = faceOf[Polyhedron.Key(c, a)];
                    int va = b.V(Original, a, 0, p.vertices[a]), vc = b.V(Original, c, 0, p.vertices[c]);
                    b.Face(Corner(fi, c), Corner(fi, a), va, Corner(g, a), Corner(g, c), vc);
                }
            }
            return b.Done("c" + p.name);
        }

        /// <summary>w: each face gets a smaller copy of itself at the centre, turned, ringed by hexagons.</summary>
        public static Polyhedron Whirl(Polyhedron p)
        {
            var b = new Builder();
            for (int fi = 0; fi < p.FaceCount; fi++)
            {
                var f = p.faces[fi];
                int n = f.Length;
                Vector3 centre = p.FaceCentroid(fi);
                int Inside(int v1, int v2) =>
                    b.V(Inner, fi, v1, Lerp(centre, Lerp(p.vertices[v1], p.vertices[v2], 1f / 3f), 1f / 3f));
                var core = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int v1 = f[i], v2 = f[(i + 1) % n], v3 = f[(i + 2) % n];
                    core[i] = Inside(v1, v2);
                    b.Face(Inside(v1, v2), Third(b, p, v1, v2), Third(b, p, v2, v1),
                           b.V(Original, v2, 0, p.vertices[v2]), Third(b, p, v2, v3), Inside(v2, v3));
                }
                b.Face(core);
            }
            return b.Done("w" + p.name);
        }

        /// <summary>q: each face gets an inner copy through its edge midpoints' halfway points, ringed by pentagons.</summary>
        public static Polyhedron Quinto(Polyhedron p)
        {
            var b = new Builder();
            int Mid(int u, int v) => b.V(Midpoint, Mathf.Min(u, v), Mathf.Max(u, v), (p.vertices[u] + p.vertices[v]) * .5f);
            for (int fi = 0; fi < p.FaceCount; fi++)
            {
                var f = p.faces[fi];
                int n = f.Length;
                Vector3 centre = p.FaceCentroid(fi);
                int Inside(int v1, int v2) => b.V(Inner, fi, v1, Lerp((p.vertices[v1] + p.vertices[v2]) * .5f, centre, .5f));
                var core = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int v1 = f[i], v2 = f[(i + 1) % n], v3 = f[(i + 2) % n];
                    core[i] = Inside(v1, v2);
                    b.Face(Inside(v1, v2), Mid(v1, v2), b.V(Original, v2, 0, p.vertices[v2]), Mid(v2, v3), Inside(v2, v3));
                }
                b.Face(core);
            }
            return b.Done("q" + p.name);
        }

        /// <summary>r: the mirror image (x negated, loops reversed so they still wind outwards).</summary>
        public static Polyhedron Reflect(Polyhedron p)
        {
            var r = p.Clone();
            for (int i = 0; i < r.vertices.Count; i++) r.vertices[i] = new Vector3(-r.vertices[i].x, r.vertices[i].y, r.vertices[i].z);
            foreach (var f in r.faces) Array.Reverse(f);
            r.name = "r" + p.name;
            return r;
        }

        static int Third(Builder b, Polyhedron p, int from, int to) =>
            b.V(EdgeThird, from, to, Lerp(p.vertices[from], p.vertices[to], 1f / 3f));

        static float MeanRadius(Polyhedron p)
        {
            double r = 0;
            foreach (var v in p.vertices) r += v.magnitude;
            return p.VertexCount > 0 ? (float)(r / p.VertexCount) : 1f;
        }

        // ---- notation ----------------------------------------------------------

        /// <summary>Applies one operator letter (with its optional side count).</summary>
        public static Polyhedron Apply(char op, int n, Polyhedron p)
        {
            Polyhedron result;
            switch (op)
            {
                case 'd': result = Dual(p); break;
                case 'a': result = Ambo(p); break;
                case 'k': result = Kis(p, n); break;
                case 'g': result = Gyro(p); break;
                case 'c': result = Chamfer(p); break;
                case 'w': result = Whirl(p); break;
                case 'q': result = Quinto(p); break;
                case 'r': result = Reflect(p); break;
                case 't': result = Dual(Kis(Dual(p), n)); break;
                case 'j': result = Dual(Ambo(p)); break;
                case 'e': result = Ambo(Ambo(p)); break;
                case 'o': result = Dual(Ambo(Ambo(p))); break;
                case 's': result = Dual(Gyro(Dual(p))); break;
                case 'b': result = Dual(Kis(Dual(Ambo(p)))); break;
                case 'm': result = Kis(Dual(Ambo(p))); break;
                case 'n': result = Kis(Dual(p)); break;
                default: throw new FormatException("unknown operator '" + op + "'");
            }
            result.name = op + (n > 0 ? n.ToString() : "") + p.name;
            return result;
        }

        /// <summary>Operators accepted by <see cref="Parse"/>, in the order the inspector lists them.</summary>
        public const string Operators = "dakgcwqrtjeosbmn";

        /// <summary>The primitive steps an operator stands for, applied right to left.</summary>
        public static string Expand(char op)
        {
            switch (op)
            {
                case 't': return "dkd";
                case 'j': return "da";
                case 'e': return "aa";
                case 'o': return "daa";
                case 's': return "dgd";
                case 'b': return "dkda";
                case 'm': return "kda";
                case 'n': return "kd";
                default: return op.ToString();
            }
        }

        /// <summary>
        /// Builds a polyhedron from Conway notation, e.g. "tI", "dkdC", "gaD", "k5A7". Operators apply
        /// right to left. Throws <see cref="FormatException"/> on bad input, and
        /// <see cref="InvalidOperationException"/> when the result would pass <paramref name="maxFaces"/>.
        ///
        /// With <paramref name="canonicalIterations"/> &gt; 0 the result is canonical
        /// (<see cref="Canonicalize"/>), and so is every intermediate step: each operator then starts
        /// from good positions, and for the snubs (dgd) the last reciprocation of a canonical gyro is
        /// already exactly canonical, where relaxing the finished snub from operator positions stalls.
        /// <paramref name="canonicalError"/> reports the final step's remaining error.
        /// </summary>
        public static Polyhedron Parse(string notation, int maxFaces = 100000, int canonicalIterations = 0)
            => Parse(notation, out _, maxFaces, canonicalIterations);

        public static Polyhedron Parse(string notation, out float canonicalError, int maxFaces = 100000, int canonicalIterations = 0)
        {
            canonicalError = 0f;
            var tokens = Tokens(notation);
            if (tokens.Count == 0) throw new FormatException("empty notation");
            var (seed, seedN) = tokens[tokens.Count - 1];
            Polyhedron p;
            switch (seed)
            {
                case 'T': p = Tetrahedron(); break;
                case 'C': p = Cube(); break;
                case 'O': p = Octahedron(); break;
                case 'D': p = Dodecahedron(); break;
                case 'I': p = Icosahedron(); break;
                case 'P': p = Prism(Need(seed, seedN)); break;
                case 'A': p = Antiprism(Need(seed, seedN)); break;
                case 'Y': p = Pyramid(Need(seed, seedN)); break;
                default: throw new FormatException("'" + notation + "' must end in a seed: T C O D I, or Pn An Yn");
            }
            bool canonical = canonicalIterations > 0;
            if (canonical) canonicalError = Canonicalize(p, canonicalIterations);
            for (int i = tokens.Count - 2; i >= 0; i--)
            {
                var (op, n) = tokens[i];
                if (n > 0 && op != 'k' && op != 't') throw new FormatException("only k and t take a side count, not '" + op + n + "'");
                if (EstimateFaces(op, p) > maxFaces)
                    throw new InvalidOperationException("'" + notation + "' passes " + maxFaces.ToString("N0") + " faces");
                if (!canonical) { p = Apply(op, n, p); continue; }
                string steps = Expand(op);
                for (int k = steps.Length - 1; k >= 0; k--)
                {
                    // The side count belongs to the k inside t = dkd.
                    p = Apply(steps[k], steps[k] == 'k' ? n : 0, p);
                    canonicalError = Canonicalize(p, canonicalIterations);
                }
            }
            p.name = notation.Trim();
            return p;
        }

        static int Need(char seed, int n)
        {
            if (n < 3) throw new FormatException(seed + " needs a side count of at least 3, e.g. " + seed + "5");
            return n;
        }

        static List<(char, int)> Tokens(string notation)
        {
            var list = new List<(char, int)>();
            if (notation == null) return list;
            for (int i = 0; i < notation.Length; i++)
            {
                char ch = notation[i];
                if (char.IsWhiteSpace(ch)) continue;
                if (!char.IsLetter(ch)) throw new FormatException("unexpected '" + ch + "' in '" + notation + "'");
                bool seed = "TCODIPAY".IndexOf(ch) >= 0;
                if (!seed && Operators.IndexOf(ch) < 0) throw new FormatException("unknown operator '" + ch + "'");
                int n = 0;
                while (i + 1 < notation.Length && char.IsDigit(notation[i + 1])) n = n * 10 + (notation[++i] - '0');
                list.Add((ch, n));
                if (seed && i != notation.TrimEnd().Length - 1) throw new FormatException("the seed '" + ch + "' must come last");
            }
            return list;
        }

        /// <summary>Face count after applying <paramref name="op"/>, from the table above.</summary>
        static long EstimateFaces(char op, Polyhedron p)
        {
            long v = p.VertexCount, e = p.EdgeCount, f = p.FaceCount;
            switch (op)
            {
                case 'd': return v;
                case 'a': return f + v;
                case 'k': case 'g': return 2 * e;
                case 'c': return f + e;
                case 'w': case 'q': return f + 2 * e;
                case 't': return f + v;
                case 'j': return e;
                case 'e': return f + v + e;
                case 'o': return 2 * e;
                case 's': return f + v + 2 * e;
                case 'b': return f + v + e;
                case 'm': case 'n': return 4 * e;
                default: return f;
            }
        }

        // ---- canonical form ----------------------------------------------------

        /// <summary>
        /// Hart's canonical form: every edge tangent to the unit sphere, every face planar, the tangent
        /// points' centroid at the origin. Found by relaxation (<see cref="Relax"/>). Canonical forms are
        /// closed under polar reciprocation, so when the polyhedron itself relaxes poorly from its
        /// operator positions (the snubs: dgd starts from a reciprocated gyro that is far from canonical)
        /// its dual is relaxed instead and reciprocated back, which lands on the same canonical form.
        /// Returns the remaining error (see <see cref="Relax"/>).
        /// </summary>
        public static float Canonicalize(Polyhedron p, int iterations = 600, float tolerance = 1e-6f)
        {
            var direct = p.Clone();
            float error = Relax(direct, iterations, tolerance);
            if (error > tolerance)
            {
                var dual = Dual(p);
                if (Relax(dual, iterations, tolerance) < error)
                {
                    // Dual(dual) lists its vertices in the order of p's vertices (vertex i of the double
                    // dual is face i of the dual, which is vertex i of p).
                    var back = Dual(dual);
                    var viaDual = p.Clone();
                    for (int i = 0; i < viaDual.VertexCount; i++) viaDual.vertices[i] = back.vertices[i];
                    float e2 = Relax(viaDual, iterations, tolerance);
                    if (e2 < error) { direct = viaDual; error = e2; }
                }
            }
            for (int i = 0; i < p.VertexCount; i++) p.vertices[i] = direct.vertices[i];
            return error;
        }

        /// <summary>
        /// The relaxation: repeatedly (1) centre the edges' tangent points on the origin, (2) move each
        /// edge's line to distance 1 from the origin along its closest point, and (3) pull each vertex
        /// onto the planes of its faces, each step averaged per vertex. Returns the worst of
        /// |closest distance − 1| over edges and of the distance from a face plane over vertices.
        /// </summary>
        public static float Relax(Polyhedron p, int iterations = 600, float tolerance = 1e-6f)
        {
            int nv = p.VertexCount;
            if (nv == 0) return 0f;
            var edges = p.Edges();
            var pos = new double[nv, 3];
            for (int i = 0; i < nv; i++) { pos[i, 0] = p.vertices[i].x; pos[i, 1] = p.vertices[i].y; pos[i, 2] = p.vertices[i].z; }
            var acc = new double[nv, 3];
            var count = new int[nv];
            double error = double.MaxValue;

            // Start from a sensible scale: mean edge distance 1.
            {
                double mean = 0;
                foreach (var e in edges) mean += Math.Sqrt(Norm2(Tangent(pos, e.x, e.y)));
                mean /= Math.Max(edges.Count, 1);
                double s = mean > 1e-12 ? 1.0 / mean : 1.0;
                for (int i = 0; i < nv; i++) for (int k = 0; k < 3; k++) pos[i, k] *= s;
            }

            for (int it = 0; it < iterations && error > tolerance; it++)
            {
                // (1) Recentre on the tangent points.
                double cx = 0, cy = 0, cz = 0;
                foreach (var e in edges) { var t = Tangent(pos, e.x, e.y); cx += t.x; cy += t.y; cz += t.z; }
                cx /= edges.Count; cy /= edges.Count; cz /= edges.Count;
                for (int i = 0; i < nv; i++) { pos[i, 0] -= cx; pos[i, 1] -= cy; pos[i, 2] -= cz; }

                // (2) Tangentify: translate each edge's line to unit distance; average per vertex.
                Array.Clear(acc, 0, acc.Length); Array.Clear(count, 0, count.Length);
                double tangentError = 0;
                foreach (var e in edges)
                {
                    var t = Tangent(pos, e.x, e.y);
                    double r = Math.Sqrt(Norm2(t));
                    if (r < 1e-9) continue;
                    tangentError = Math.Max(tangentError, Math.Abs(r - 1));
                    double s = (1 - r) / r;
                    foreach (int v in new[] { e.x, e.y }) { acc[v, 0] += t.x * s; acc[v, 1] += t.y * s; acc[v, 2] += t.z * s; count[v]++; }
                }
                for (int i = 0; i < nv; i++)
                    if (count[i] > 0) for (int k = 0; k < 3; k++) pos[i, k] += .5 * acc[i, k] / count[i];

                // (3) Planarise: project each vertex towards every face plane through it; average.
                Array.Clear(acc, 0, acc.Length); Array.Clear(count, 0, count.Length);
                double planeError = 0;
                foreach (var f in p.faces)
                {
                    var n = Newell(pos, f, out var c);
                    foreach (int v in f)
                    {
                        double d = n.x * (c.x - pos[v, 0]) + n.y * (c.y - pos[v, 1]) + n.z * (c.z - pos[v, 2]);
                        planeError = Math.Max(planeError, Math.Abs(d));
                        acc[v, 0] += n.x * d; acc[v, 1] += n.y * d; acc[v, 2] += n.z * d; count[v]++;
                    }
                }
                for (int i = 0; i < nv; i++)
                    if (count[i] > 0) for (int k = 0; k < 3; k++) pos[i, k] += .5 * acc[i, k] / count[i];

                error = Math.Max(tangentError, planeError);
            }

            for (int i = 0; i < nv; i++) p.vertices[i] = new Vector3((float)pos[i, 0], (float)pos[i, 1], (float)pos[i, 2]);
            return (float)error;
        }

        struct D3 { public double x, y, z; public D3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; } }

        static double Norm2(D3 a) => a.x * a.x + a.y * a.y + a.z * a.z;

        /// <summary>The point of line ab closest to the origin.</summary>
        static D3 Tangent(double[,] pos, int a, int b)
        {
            double ax = pos[a, 0], ay = pos[a, 1], az = pos[a, 2];
            double dx = pos[b, 0] - ax, dy = pos[b, 1] - ay, dz = pos[b, 2] - az;
            double dd = dx * dx + dy * dy + dz * dz;
            double s = dd > 1e-18 ? (ax * dx + ay * dy + az * dz) / dd : 0;
            return new D3(ax - s * dx, ay - s * dy, az - s * dz);
        }

        static D3 Newell(double[,] pos, int[] f, out D3 centroid)
        {
            double nx = 0, ny = 0, nz = 0, cx = 0, cy = 0, cz = 0;
            for (int i = 0; i < f.Length; i++)
            {
                int a = f[i], b = f[(i + 1) % f.Length];
                nx += (pos[a, 1] - pos[b, 1]) * (pos[a, 2] + pos[b, 2]);
                ny += (pos[a, 2] - pos[b, 2]) * (pos[a, 0] + pos[b, 0]);
                nz += (pos[a, 0] - pos[b, 0]) * (pos[a, 1] + pos[b, 1]);
                cx += pos[a, 0]; cy += pos[a, 1]; cz += pos[a, 2];
            }
            centroid = new D3(cx / f.Length, cy / f.Length, cz / f.Length);
            double m = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            return m > 1e-18 ? new D3(nx / m, ny / m, nz / m) : new D3(0, 0, 0);
        }

        // ---- names -------------------------------------------------------------

        static readonly Dictionary<string, string> names = new Dictionary<string, string>
        {
            { "T", "tetrahedron" }, { "C", "cube" }, { "O", "octahedron" }, { "D", "dodecahedron" }, { "I", "icosahedron" },
            { "tT", "truncated tetrahedron" }, { "aC", "cuboctahedron" }, { "tC", "truncated cube" },
            { "tO", "truncated octahedron" }, { "eC", "rhombicuboctahedron" }, { "bC", "truncated cuboctahedron" },
            { "sC", "snub cube" }, { "aD", "icosidodecahedron" }, { "tD", "truncated dodecahedron" },
            { "tI", "truncated icosahedron" }, { "eD", "rhombicosidodecahedron" }, { "bD", "truncated icosidodecahedron" },
            { "sD", "snub dodecahedron" },
            { "kT", "triakis tetrahedron" }, { "jC", "rhombic dodecahedron" }, { "kO", "triakis octahedron" },
            { "kC", "tetrakis hexahedron" }, { "oC", "deltoidal icositetrahedron" }, { "mC", "disdyakis dodecahedron" },
            { "gC", "pentagonal icositetrahedron" }, { "jD", "rhombic triacontahedron" }, { "kI", "triakis icosahedron" },
            { "kD", "pentakis dodecahedron" }, { "oD", "deltoidal hexecontahedron" }, { "mD", "disdyakis triacontahedron" },
            { "gD", "pentagonal hexecontahedron" }, { "cC", "chamfered cube" }, { "cD", "chamfered dodecahedron" },
            { "wT", "whirled tetrahedron" }, { "dtI", "pentakis dodecahedron" }, { "dkdC", "truncated cube" },
        };

        /// <summary>A common name for well-known notations, otherwise empty.</summary>
        public static string CommonName(string notation) =>
            notation != null && names.TryGetValue(notation.Trim(), out var n) ? n : "";
    }
}
