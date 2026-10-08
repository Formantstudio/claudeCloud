using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// What a source stage produces. These are the structures worth having, not one structure with
    /// a parameter: an n-cube and a 10-cube are the same object, where E8 and a 120-cell are not.
    /// </summary>
    public enum ShapeSourceKind
    {
        NCube,            // the hypercube family. n = 10 is the dekeract
        Demicube,         // half a hypercube: alternate corners, edges along face diagonals
        CrossPolytope,    // the dual: 2n vertices on the axes
        Polytope4D,       // 5-cell, 8-cell, 16-cell, 24-cell, 120-cell, 600-cell
        RootSystem,       // E8, E7, E6, F4, D4, D8 - the genuinely non-cubic lattices
        Polyhedron,       // Platonics, prisms, antiprisms, Johnson solids
        Metatron,         // cuboctahedron plus centre, flattened: the 2-D glyph, all 78 lines
        Metatron3D,       // the same 13 points unflattened: the Vector Equilibrium in 3-D
        Metatron4D,       // the 24-cell plus centre - the 4-D analogue of the cuboctahedron
        CliffordGrid,     // flat torus in S^3, a 4-D grid
        ManifoldGrid,     // any of the parametric surfaces, as a grid
        HypersphereCloud, // an even cloud on S^3
        LatticeShell      // integer lattice points at a fixed radius
    }

    /// <summary>What an operator stage does to the points, in order, every frame.</summary>
    public enum ShapeOperatorKind
    {
        Rotate4D,         // the six SO(4) planes, from the shared axis or its own rates
        Project,          // 4-D down to 3-D
        FractalDisplace,  // push along the normal by an implicit field, and tint by it
        Kaleidoscope,     // sector mirroring and plane folds, in shape space
        Swirl,            // twist, vortex, inversion
        Spherize,         // pull onto a sphere
        Explode,          // push outward per point
        Jitter,           // hash displacement
        Scale             // uniform or per-axis
    }

    /// <summary>Which root system. Counts are checked at build and reported in the status line.</summary>
    public enum RootSystemKind { E8, E7, E6, F4, D4, D8, B4, A4 }

    [Serializable]
    public sealed class ShapeSource
    {
        public bool on = true;
        public string label = "";
        public ShapeSourceKind kind = ShapeSourceKind.RootSystem;

        [Header("Which")]
        [Tooltip("Dimensions for NCube, Demicube, CrossPolytope and LatticeShell. 10 makes an n-cube a dekeract.")]
        [Range(2, 10)] public int dimensions = 8;
        public Polytope4DSwarm.Polytope polytope = Polytope4DSwarm.Polytope.Cell120;
        public RootSystemKind roots = RootSystemKind.E8;
        public PolyhedronSwarm.Solid solid = PolyhedronSwarm.Solid.Icosahedron;
        public ManifoldSurface surface = ManifoldSurface.KleinBottle;
        public ManifoldSettings shape = new ManifoldSettings();

        [Header("Grid / cloud size")]
        [Range(4, 160)] public int uRes = 40;
        [Range(4, 160)] public int vRes = 28;
        [Range(16, 6000)] public int cloudCount = 800;
        [Tooltip("LatticeShell: the squared radius of the shell to take.")]
        [Range(1, 32)] public int shellRadiusSquared = 6;

        [Header("Topology")]
        [Tooltip("How close a pair must be to the shortest distance to count as an edge, for the sets that derive edges by distance.")]
        [Range(.002f, .3f)] public float edgeTolerance = .04f;
        [Tooltip("Connect at the Nth distinct distance instead of the shortest. 2 and 3 give stellated, star-polytope style figures from the same vertices.")]
        [Range(1, 6)] public int edgeRank = 1;
        [Tooltip("Hard cap on edges from this source, so a bad tolerance cannot hang the editor.")]
        [Range(100, 40000)] public int maxEdges = 12000;

        [Header("Placement")]
        public Vector3 offset;
        public Vector3 euler;
        [Min(.01f)] public float scale = 1f;
        [Tooltip("Degrees per second about each local axis, after projection.")]
        public Vector3 spin;
        [Tooltip("Rates for this source's own N-D rotation, when it has more than four dimensions.")]
        [Range(0f, 2f)] public float ndRate = .25f;
        [Range(1, 20)] public int ndPlanes = 20;

        [NonSerialized] public float[] raw;      // dimensions-per-point, flattened
        [NonSerialized] public int pointCount;
        [NonSerialized] public int dim;
        [NonSerialized] public int firstNode;
        [NonSerialized] public int builtEdges;
    }

    [Serializable]
    public sealed class ShapeOperator
    {
        public bool on = true;
        public ShapeOperatorKind kind = ShapeOperatorKind.Rotate4D;
        [Tooltip("How much of this operator to apply.")]
        [Range(0f, 2f)] public float amount = 1f;

        [Header("Rotate4D / Project")]
        [Tooltip("Use the scene's Hyperspace4DAxis, so this shares the 4-D camera and its activation layer.")]
        public bool useSharedAxis = true;
        public Vector2 simpleRates = new Vector2(.08f, .08f);
        public Vector2 mixedRates = new Vector2(.02f, 0f);
        public Projection4D projection = Projection4D.Stereographic;
        [Range(1.05f, 8f)] public float wDistance = 2.4f;

        [Header("FractalDisplace")]
        public ImplicitSettings field = new ImplicitSettings { shape = ImplicitShape.Mandelbulb };
        [Tooltip("Scales position into the field's domain. Lower zooms in.")]
        [Range(.05f, 3f)] public float fieldScale = .55f;
        [Tooltip("Drifts the field so the crust boils rather than sitting still.")]
        [Range(0f, 1f)] public float fieldDrift = .12f;
        [Tooltip("Writes the field value into the per-particle accent, so colour and size follow the fractal.")]
        public bool tintByField = true;

        [Header("Kaleidoscope")]
        public KaleidoSettings kaleido = new KaleidoSettings();

        [Header("Swirl")]
        [Range(-8f, 8f)] public float twist;
        [Range(-8f, 8f)] public float vortex;
        [Range(0f, 1f)] public float inversion;
        public Vector3 swirlAxis = Vector3.forward;

        [Header("Explode / Jitter / Scale")]
        [Range(-4f, 4f)] public float radial;
        [Range(0f, 2f)] public float jitter;
        public Vector3 axisScale = Vector3.one;
    }

    /// <summary>
    /// The engine: a stack of shape sources and a stack of operators, rendered by the Dekeract's
    /// particle layer.
    ///
    /// DekeractTetraSwarm is one hard-wired instance of this idea - a 10-cube, 20 rotation planes,
    /// a projection, mesh-particle tetrahedra on its vertices and edges. Pulling those apart into
    /// sources and operators is what lets any combination be built instead of one:
    ///
    ///   RootSystem E8 + Rotate4D + Project                      -> 240 nodes, 6,720 edges
    ///   CliffordGrid + FractalDisplace(Mandelbulb) + Rotate4D    -> a boiling everting torus
    ///   Polytope4D 120-cell with edgeRank 2 + Kaleidoscope       -> a stellated, mirrored 120-cell
    ///   NCube 10 + Project                                       -> the dekeract, reproduced
    ///
    /// Sources run on rebuild and produce points plus edges. Operators run every frame in order.
    /// Everything below that - density, strands, clusters, scatter, breathing, follow smoothing,
    /// colour, tumble - comes from <see cref="NodeEdgeSwarmBase"/>, so every combination arrives at
    /// the same particle budget automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeometryFractalEngine : NodeEdgeSwarmBase
    {
        public enum Recipe
        {
            Custom,
            DekeractReproduced,
            E8RootSystem,
            CliffordMandelbulb,
            Stellated120Cell,
            KleinKaleidoTunnel,
            JohnsonCluster,
            HyperLattice
        }

        [Header("Recipe")]
        [Tooltip("Fills the stacks with a known-good combination, then returns to Custom.")]
        public Recipe applyRecipe = Recipe.E8RootSystem;

        [Header("Stacks")]
        public List<ShapeSource> sources = new List<ShapeSource>();
        public List<ShapeOperator> operators = new List<ShapeOperator>();

        [Header("Limits")]
        [Tooltip("Hard cap on nodes across every source.")]
        [Range(64, 40000)] public int maxNodes = 20000;

        protected override string SwarmName => "Geometry fractal engine";
        protected override int NodeCount => Mathf.Max(totalNodes, 1);
        protected override bool TopologyDirty => signature != Signature();

        int totalNodes;
        string signature = "";
        string report = "";

        void Reset()
        {
            applyRecipe = Recipe.E8RootSystem;
        }

        /// <summary>Everything that changes topology, as one string, so a rebuild is cheap to detect.</summary>
        string Signature()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(maxNodes).Append('|');
            foreach (var s in sources)
            {
                if (s == null) continue;
                sb.Append(s.on ? '1' : '0').Append((int)s.kind).Append(',')
                  .Append(s.dimensions).Append(',').Append((int)s.polytope).Append(',')
                  .Append((int)s.roots).Append(',').Append((int)s.solid).Append(',')
                  .Append((int)s.surface).Append(',').Append(s.uRes).Append(',')
                  .Append(s.vRes).Append(',').Append(s.cloudCount).Append(',')
                  .Append(s.shellRadiusSquared).Append(',')
                  .Append(s.edgeRank).Append(',').Append(s.edgeTolerance.ToString("0.000")).Append(';');
            }
            return sb.ToString();
        }

        protected override void BuildEdges(List<int> a, List<int> b)
        {
            if (applyRecipe != Recipe.Custom) { ApplyRecipe(applyRecipe); applyRecipe = Recipe.Custom; }
            if (sources.Count == 0) ApplyRecipe(Recipe.E8RootSystem);

            signature = Signature();
            totalNodes = 0;
            report = "";

            foreach (var source in sources)
            {
                if (source == null || !source.on) continue;

                var points = new List<float[]>();
                var edges = new List<int>();
                GenerateSource(source, points, edges);

                if (points.Count == 0) continue;
                if (totalNodes + points.Count > maxNodes)
                {
                    report += (source.label ?? source.kind.ToString()) + ": skipped, node cap\n";
                    continue;
                }

                source.dim = points[0].Length;
                source.pointCount = points.Count;
                source.firstNode = totalNodes;
                source.raw = new float[points.Count * source.dim];
                for (int i = 0; i < points.Count; i++)
                    Array.Copy(points[i], 0, source.raw, i * source.dim, source.dim);

                for (int i = 0; i + 1 < edges.Count; i += 2)
                {
                    a.Add(source.firstNode + edges[i]);
                    b.Add(source.firstNode + edges[i + 1]);
                }
                source.builtEdges = edges.Count / 2;
                totalNodes += points.Count;

                report += string.Format("{0}: {1} nodes, {2} edges\n",
                    string.IsNullOrEmpty(source.label) ? SourceName(source) : source.label,
                    points.Count, source.builtEdges);
            }

            nodeAccent = new float[Mathf.Max(totalNodes, 1)];
        }

        static string SourceName(ShapeSource s)
        {
            switch (s.kind)
            {
                case ShapeSourceKind.NCube: return s.dimensions + "-cube";
                case ShapeSourceKind.Demicube: return "demi " + s.dimensions + "-cube";
                case ShapeSourceKind.CrossPolytope: return s.dimensions + "-orthoplex";
                case ShapeSourceKind.Polytope4D: return s.polytope.ToString();
                case ShapeSourceKind.RootSystem: return s.roots.ToString();
                case ShapeSourceKind.Polyhedron: return s.solid.ToString();
                case ShapeSourceKind.ManifoldGrid: return s.surface.ToString();
                default: return s.kind.ToString();
            }
        }

        // ---- sources ---------------------------------------------------------

        /// <summary>
        /// Generates one source's points and edge pairs. Public and static so the particle swarm
        /// engine builds its skeletons from the same definitions rather than a second copy.
        /// </summary>
        public static void GenerateSource(ShapeSource s, List<float[]> points, List<int> edges)
        {
            switch (s.kind)
            {
                case ShapeSourceKind.NCube: NCube(s, points, edges); break;
                case ShapeSourceKind.Demicube: Demicube(s, points, edges); break;
                case ShapeSourceKind.CrossPolytope: CrossPolytope(s, points, edges); break;
                case ShapeSourceKind.Polytope4D: FromVectors(s, Polytope4DVertices(s.polytope), points, edges); break;
                case ShapeSourceKind.RootSystem: FromFloats(s, RootVectors(s.roots), points, edges); break;
                case ShapeSourceKind.Polyhedron: Polyhedron(s, points, edges); break;
                case ShapeSourceKind.Metatron: Metatron(s, points, edges, 1f); break;
                case ShapeSourceKind.Metatron3D: Metatron(s, points, edges, 0f); break;
                case ShapeSourceKind.Metatron4D: Metatron4D(s, points, edges); break;
                case ShapeSourceKind.CliffordGrid: CliffordGrid(s, points, edges); break;
                case ShapeSourceKind.ManifoldGrid: ManifoldGrid(s, points, edges); break;
                case ShapeSourceKind.HypersphereCloud: HypersphereCloud(s, points, edges); break;
                case ShapeSourceKind.LatticeShell: LatticeShell(s, points, edges); break;
            }
        }

        static void NCube(ShapeSource s, List<float[]> points, List<int> edges)
        {
            int n = Mathf.Clamp(s.dimensions, 2, 10);
            int count = 1 << n;
            float c = 1f / Mathf.Sqrt(n);
            for (int v = 0; v < count; v++)
            {
                var p = new float[n];
                for (int d = 0; d < n; d++) p[d] = (v & (1 << d)) != 0 ? c : -c;
                points.Add(p);
            }
            for (int v = 0; v < count; v++)
                for (int d = 0; d < n; d++)
                    if ((v & (1 << d)) == 0) { edges.Add(v); edges.Add(v | (1 << d)); }
        }

        /// <summary>
        /// Half a hypercube: only the corners with an even number of minus signs. Its edges run
        /// along the cube's face diagonals, not its edges, so it is a genuinely different figure.
        /// In 4-D it is the 16-cell; in 5-D and up it is its own family.
        /// </summary>
        static void Demicube(ShapeSource s, List<float[]> points, List<int> edges)
        {
            int n = Mathf.Clamp(s.dimensions, 3, 10);
            float c = 1f / Mathf.Sqrt(n);
            var kept = new List<float[]>();
            for (int v = 0; v < (1 << n); v++)
            {
                int bits = 0;
                for (int d = 0; d < n; d++) if ((v & (1 << d)) != 0) bits++;
                if ((bits & 1) != 0) continue;
                var p = new float[n];
                for (int d = 0; d < n; d++) p[d] = (v & (1 << d)) != 0 ? c : -c;
                kept.Add(p);
            }
            points.AddRange(kept);
            DistanceEdges(s, kept, edges);
        }

        static void CrossPolytope(ShapeSource s, List<float[]> points, List<int> edges)
        {
            int n = Mathf.Clamp(s.dimensions, 2, 10);
            for (int d = 0; d < n; d++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var p = new float[n];
                    p[d] = sign;
                    points.Add(p);
                }
            // Every pair except the two antipodes on the same axis.
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                    if (i / 2 != j / 2) { edges.Add(i); edges.Add(j); }
        }

        /// <summary>
        /// Root systems. These are the real higher-dimensional structures: E8's 240 roots have 56
        /// nearest neighbours each, so 6,720 edges - more than the dekeract's 5,120, and not a cube.
        /// </summary>
        static List<float[]> RootVectors(RootSystemKind kind)
        {
            var list = new List<float[]>();
            switch (kind)
            {
                case RootSystemKind.E8: AddE8(list); break;
                case RootSystemKind.E7:
                    // E8 roots orthogonal to one fixed root: 126 of them.
                    foreach (var r in Filter(AllE8(), new float[] { 0, 0, 0, 0, 0, 0, 1, -1 })) list.Add(r);
                    break;
                case RootSystemKind.E6:
                    // Orthogonal to two: 72.
                    foreach (var r in Filter(Filter(AllE8(), new float[] { 0, 0, 0, 0, 0, 0, 1, -1 }),
                                             new float[] { 0, 0, 0, 0, 0, 1, -1, 0 })) list.Add(r);
                    break;
                case RootSystemKind.F4: AddF4(list); break;
                case RootSystemKind.D4: AddD(list, 4); break;
                case RootSystemKind.D8: AddD(list, 8); break;
                case RootSystemKind.B4: AddD(list, 4); AddShort(list, 4); break;
                default: AddA(list, 4); break;
            }
            return list;
        }

        static List<float[]> AllE8() { var l = new List<float[]>(); AddE8(l); return l; }

        static void AddE8(List<float[]> list)
        {
            // 112: (+-1, +-1, 0...) over every coordinate pair.
            AddD(list, 8);
            // 128: all-half-integer with an even number of minus signs.
            for (int m = 0; m < 256; m++)
            {
                int minus = 0;
                for (int k = 0; k < 8; k++) if ((m & (1 << k)) != 0) minus++;
                if ((minus & 1) != 0) continue;
                var p = new float[8];
                for (int k = 0; k < 8; k++) p[k] = (m & (1 << k)) != 0 ? -.5f : .5f;
                list.Add(p);
            }
        }

        static void AddD(List<float[]> list, int n)
        {
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    for (int si = -1; si <= 1; si += 2)
                        for (int sj = -1; sj <= 1; sj += 2)
                        {
                            var p = new float[n];
                            p[i] = si; p[j] = sj;
                            list.Add(p);
                        }
        }

        static void AddShort(List<float[]> list, int n)
        {
            for (int i = 0; i < n; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    var p = new float[n];
                    p[i] = s;
                    list.Add(p);
                }
        }

        static void AddF4(List<float[]> list)
        {
            AddD(list, 4);        // 24 long roots
            AddShort(list, 4);    // 8
            for (int m = 0; m < 16; m++)   // 16 half-integer
            {
                var p = new float[4];
                for (int k = 0; k < 4; k++) p[k] = (m & (1 << k)) != 0 ? -.5f : .5f;
                list.Add(p);
            }
        }

        static void AddA(List<float[]> list, int n)
        {
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    if (i == j) continue;
                    var p = new float[n + 1];
                    p[i] = 1; p[j] = -1;
                    list.Add(p);
                }
        }

        static List<float[]> Filter(List<float[]> roots, float[] against)
        {
            var kept = new List<float[]>();
            foreach (var r in roots)
            {
                float dot = 0f;
                for (int k = 0; k < r.Length && k < against.Length; k++) dot += r[k] * against[k];
                if (Mathf.Abs(dot) < 1e-4f) kept.Add(r);
            }
            return kept;
        }

        // Reuses the swarm's own generator, so there is one definition of each polytope.
        static Vector4[] Polytope4DVertices(Polytope4DSwarm.Polytope kind) =>
            Polytope4DSwarm.VerticesFor(kind);

        static void FromVectors(ShapeSource s, Vector4[] verts, List<float[]> points, List<int> edges)
        {
            if (verts == null) return;
            var list = new List<float[]>(verts.Length);
            foreach (var v in verts)
            {
                float m = ((Vector4)v).magnitude;
                var p = new float[4];
                if (m > 1e-6f) { p[0] = v.x / m; p[1] = v.y / m; p[2] = v.z / m; p[3] = v.w / m; }
                list.Add(p);
            }
            points.AddRange(list);
            DistanceEdges(s, list, edges);
        }

        static void FromFloats(ShapeSource s, List<float[]> raw, List<float[]> points, List<int> edges)
        {
            var list = new List<float[]>(raw.Count);
            foreach (var r in raw)
            {
                float m = 0f;
                foreach (var x in r) m += x * x;
                m = Mathf.Sqrt(m);
                var p = new float[r.Length];
                for (int k = 0; k < r.Length; k++) p[k] = m > 1e-6f ? r[k] / m : 0f;
                list.Add(p);
            }
            points.AddRange(list);
            DistanceEdges(s, list, edges);
        }

        /// <summary>
        /// Edges from the Nth distinct pairwise distance. Rank 1 is the convex figure; higher ranks
        /// connect across the shape, which is how the regular star polytopes arise from the same
        /// vertices as the convex ones.
        /// </summary>
        static void DistanceEdges(ShapeSource s, List<float[]> pts, List<int> edges)
        {
            int n = pts.Count;
            if (n < 2) return;

            // Collect distinct squared distances up to the rank asked for.
            var bands = new List<float>();
            float tol = s.edgeTolerance;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float d = Sq(pts[i], pts[j]);
                if (d <= 1e-9f) continue;
                bool found = false;
                for (int k = 0; k < bands.Count; k++)
                    if (Mathf.Abs(bands[k] - d) <= tol * Mathf.Max(bands[k], 1e-4f)) { found = true; break; }
                if (!found) bands.Add(d);
                if (bands.Count > 24) break;
            }
            if (bands.Count == 0) return;
            bands.Sort();
            float want = bands[Mathf.Clamp(s.edgeRank - 1, 0, bands.Count - 1)];
            float limit = want * (1f + tol);
            float floor = want * (1f - tol);

            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float d = Sq(pts[i], pts[j]);
                if (d < floor || d > limit) continue;
                edges.Add(i); edges.Add(j);
                if (edges.Count / 2 >= s.maxEdges) return;
            }
        }

        static float Sq(float[] a, float[] b)
        {
            float d = 0f;
            int n = Mathf.Min(a.Length, b.Length);
            for (int k = 0; k < n; k++) { float t = a[k] - b[k]; d += t * t; }
            return d;
        }

        static void Polyhedron(ShapeSource s, List<float[]> points, List<int> edges)
        {
            var verts = PolyhedronSwarm.VerticesFor(s.solid, s.uRes, 1.4f, s.shellRadiusSquared, out var pairs);
            if (verts == null) return;
            foreach (var v in verts) points.Add(new[] { v.x, v.y, v.z });
            if (pairs != null) edges.AddRange(pairs);
        }

        static void Metatron(ShapeSource s, List<float[]> points, List<int> edges, float flatten)
        {
            foreach (var v in MetatronCubeSwarm.NodePositions(flatten))
                points.Add(new[] { v.x, v.y, v.z });
            AllPairs(points.Count, s, edges);
        }

        /// <summary>
        /// Metatron in four dimensions. The flat glyph is the cuboctahedron plus its centre viewed
        /// down a 3-fold axis, and the cuboctahedron's 4-D analogue is the **24-cell** - the only
        /// regular 4-polytope that is its own dual, 24 vertices all the same distance from the
        /// centre. So the 4-D figure is the 24-cell plus its centre, with every pair joined:
        /// 25 nodes and C(25,2) = 300 lines, against the glyph's 13 and 78.
        /// </summary>
        static void Metatron4D(ShapeSource s, List<float[]> points, List<int> edges)
        {
            foreach (var v in Polytope4DSwarm.VerticesFor(Polytope4DSwarm.Polytope.Cell24))
            {
                float m = ((Vector4)v).magnitude;
                points.Add(m > 1e-6f ? new[] { v.x / m, v.y / m, v.z / m, v.w / m }
                                     : new[] { 0f, 0f, 0f, 0f });
            }
            points.Add(new[] { 0f, 0f, 0f, 0f });   // the centre
            AllPairs(points.Count, s, edges);
        }

        /// <summary>Every pair joined, which is what Metatron's line count means.</summary>
        static void AllPairs(int n, ShapeSource s, List<int> edges)
        {
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    edges.Add(i); edges.Add(j);
                    if (edges.Count / 2 >= s.maxEdges) return;
                }
        }

        static void CliffordGrid(ShapeSource s, List<float[]> points, List<int> edges)
        {
            int u = Mathf.Clamp(s.uRes, 4, 160), v = Mathf.Clamp(s.vRes, 4, 160);
            const float inv = .70710678f;
            float tau = Mathf.PI * 2f;
            for (int j = 0; j < v; j++)
            for (int i = 0; i < u; i++)
            {
                float a = (float)i / u * tau, b = (float)j / v * tau;
                points.Add(new[] { Mathf.Cos(a) * inv, Mathf.Sin(a) * inv,
                                   Mathf.Cos(b) * inv, Mathf.Sin(b) * inv });
            }
            GridEdges(u, v, true, true, edges);
        }

        static void ManifoldGrid(ShapeSource s, List<float[]> points, List<int> edges)
        {
            int u = Mathf.Clamp(s.uRes, 4, 160), v = Mathf.Clamp(s.vRes, 4, 160);
            bool wrapU = Manifolds.WrapsU(s.surface), wrapV = Manifolds.WrapsV(s.surface);
            bool poles = Manifolds.HasPoles(s.surface);
            float inset = poles ? .5f / v : 0f;
            for (int j = 0; j < v; j++)
            for (int i = 0; i < u; i++)
            {
                float uu = wrapU ? (float)i / u : (float)i / Mathf.Max(u - 1, 1);
                float vvRaw = wrapV ? (float)j / v : (float)j / Mathf.Max(v - 1, 1);
                float vv = Mathf.Lerp(inset, 1f - inset, vvRaw);
                Vector3 p = Manifolds.Evaluate(s.surface, s.shape, uu, vv, 0f);
                points.Add(new[] { p.x, p.y, p.z });
            }
            GridEdges(u, v, wrapU, wrapV, edges);
        }

        static void GridEdges(int u, int v, bool wrapU, bool wrapV, List<int> edges)
        {
            for (int j = 0; j < v; j++)
            for (int i = 0; i < u; i++)
            {
                int here = j * u + i;
                if (wrapU || i < u - 1) { edges.Add(here); edges.Add(j * u + (i + 1) % u); }
                if (wrapV || j < v - 1) { edges.Add(here); edges.Add(((j + 1) % v) * u + i); }
            }
        }

        static void HypersphereCloud(ShapeSource s, List<float[]> points, List<int> edges)
        {
            int count = Mathf.Clamp(s.cloudCount, 16, 6000);
            var state = UnityEngine.Random.state;
            UnityEngine.Random.InitState(1977);
            for (int i = 0; i < count; i++)
            {
                Vector4 v = Hyperspace4DAxis.RandomOnSphere4();
                points.Add(new[] { v.x, v.y, v.z, v.w });
            }
            UnityEngine.Random.state = state;
            DistanceEdges(s, points, edges);
        }

        /// <summary>
        /// Integer lattice points at one squared radius. A shell of Z^n is a genuinely different
        /// cluster from any polytope: the counts jump around with number theory rather than
        /// following a formula.
        /// </summary>
        static void LatticeShell(ShapeSource s, List<float[]> points, List<int> edges)
        {
            int n = Mathf.Clamp(s.dimensions, 2, 6);
            int target = Mathf.Clamp(s.shellRadiusSquared, 1, 32);
            int reach = Mathf.CeilToInt(Mathf.Sqrt(target));
            var coords = new int[n];
            var found = new List<float[]>();
            void Walk(int depth, int sumSq)
            {
                if (sumSq > target) return;
                if (depth == n)
                {
                    if (sumSq != target) return;
                    var p = new float[n];
                    float norm = Mathf.Sqrt(target);
                    for (int k = 0; k < n; k++) p[k] = coords[k] / norm;
                    found.Add(p);
                    return;
                }
                for (int x = -reach; x <= reach; x++)
                {
                    coords[depth] = x;
                    Walk(depth + 1, sumSq + x * x);
                }
            }
            Walk(0, 0);
            points.AddRange(found);
            DistanceEdges(s, found, edges);
        }

        // ---- per frame -------------------------------------------------------

        protected override void UpdateNodes(Vector3[] into, float time)
        {
            if (nodeAccent == null || nodeAccent.Length != into.Length)
                nodeAccent = new float[into.Length];
            Array.Clear(nodeAccent, 0, nodeAccent.Length);

            foreach (var source in sources)
            {
                if (source == null || !source.on || source.raw == null || source.pointCount == 0) continue;

                var spin = Quaternion.Euler(source.euler + source.spin * time);
                var coord = new float[source.dim];

                for (int i = 0; i < source.pointCount; i++)
                {
                    int node = source.firstNode + i;
                    if (node >= into.Length) break;
                    Array.Copy(source.raw, i * source.dim, coord, 0, source.dim);

                    // The source's own N-D rotation, for anything above four dimensions.
                    if (source.dim > 4 && source.ndRate > 0f)
                    {
                        int planes = Mathf.Clamp(source.ndPlanes, 1, 20);
                        for (int r = 0; r < planes; r++)
                        {
                            int a = r % source.dim, b = (a + 3) % source.dim;
                            if (a == b) continue;
                            float ang = .27f * (r + 1) + time * source.ndRate * (.3f + .07f * r);
                            float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                            float x = coord[a], y = coord[b];
                            coord[a] = x * c - y * sn;
                            coord[b] = x * sn + y * c;
                        }
                    }

                    // Collapse to 4-D by perspective division from the top dimension down.
                    Vector4 v4 = Collapse(coord, source.dim);

                    // Then the operator stack.
                    Vector3 p = RunOperators(v4, time, node, out float accent);
                    p = spin * (p * source.scale) + source.offset;

                    into[node] = p * radius;
                    nodeAccent[node] = accent;
                }
            }
        }

        static Vector4 Collapse(float[] coord, int dim)
        {
            if (dim <= 4)
                return new Vector4(coord[0],
                                   dim > 1 ? coord[1] : 0f,
                                   dim > 2 ? coord[2] : 0f,
                                   dim > 3 ? coord[3] : 0f);
            float x = coord[0], y = coord[1], z = coord[2], w = coord[3];
            const float d = 2.6f;
            for (int k = dim - 1; k >= 4; k--)
            {
                float scale = 1f / Mathf.Max(d - coord[k], .25f);
                x *= scale; y *= scale; z *= scale; w *= scale;
            }
            float boost = Mathf.Pow(d - 1f, (dim - 4) * .5f);
            return new Vector4(x, y, z, w) * boost;
        }

        Vector3 RunOperators(Vector4 v4, float time, int node, out float accent)
        {
            accent = 0f;
            Vector3 p = new Vector3(v4.x, v4.y, v4.z);
            bool projected = false;

            foreach (var op in operators)
            {
                if (op == null || !op.on || op.amount <= 0f) continue;
                switch (op.kind)
                {
                    case ShapeOperatorKind.Rotate4D:
                    {
                        if (projected) break;
                        if (op.useSharedAxis && Hyperspace4DAxis.Current)
                            v4 = Hyperspace4DAxis.Current.Rotate(v4);
                        else
                        {
                            float x = v4.x, y = v4.y, z = v4.z, w = v4.w, tau = Mathf.PI * 2f;
                            Rot(ref x, ref y, op.simpleRates.x * time * tau);
                            Rot(ref z, ref w, op.simpleRates.y * time * tau);
                            Rot(ref x, ref z, op.mixedRates.x * time * tau);
                            Rot(ref y, ref w, op.mixedRates.y * time * tau);
                            v4 = new Vector4(x, y, z, w);
                        }
                        p = new Vector3(v4.x, v4.y, v4.z);
                        break;
                    }
                    case ShapeOperatorKind.Project:
                    {
                        p = Project(op, v4);
                        projected = true;
                        break;
                    }
                    case ShapeOperatorKind.FractalDisplace:
                    {
                        if (!projected) { p = Project(op, v4); projected = true; }
                        Vector3 drift = new Vector3(Mathf.Sin(time * .17f), Mathf.Cos(time * .13f),
                                                    Mathf.Sin(time * .11f)) * op.fieldDrift;
                        float de = Implicits.Field(op.field, p * op.fieldScale + drift, time);
                        float height = 1f / (1f + Mathf.Abs(de) * 6f);
                        Vector3 normal = p.sqrMagnitude > 1e-6f ? p.normalized : Vector3.up;
                        p += normal * (height - .5f) * op.amount;
                        if (op.tintByField) accent = Mathf.Max(accent, Mathf.Clamp01(height) * op.amount);
                        break;
                    }
                    case ShapeOperatorKind.Kaleidoscope:
                        if (!projected) { p = Project(op, v4); projected = true; }
                        p = Vector3.Lerp(p, KaleidoFold.Apply(op.kaleido, p, time), op.amount);
                        break;
                    case ShapeOperatorKind.Swirl:
                        if (!projected) { p = Project(op, v4); projected = true; }
                        p = Swirl(op, p, time);
                        break;
                    case ShapeOperatorKind.Spherize:
                        if (!projected) { p = Project(op, v4); projected = true; }
                        if (p.sqrMagnitude > 1e-6f) p = Vector3.Lerp(p, p.normalized, Mathf.Clamp01(op.amount));
                        break;
                    case ShapeOperatorKind.Explode:
                        if (!projected) { p = Project(op, v4); projected = true; }
                        if (p.sqrMagnitude > 1e-6f) p += p.normalized * op.radial * op.amount;
                        break;
                    case ShapeOperatorKind.Jitter:
                    {
                        if (!projected) { p = Project(op, v4); projected = true; }
                        float h1 = Hash(node * .61803399f), h2 = Hash(node * .75487766f + 7f),
                              h3 = Hash(node * .38196601f + 13f);
                        p += new Vector3(h1 - .5f, h2 - .5f, h3 - .5f) * op.jitter * op.amount;
                        break;
                    }
                    case ShapeOperatorKind.Scale:
                        if (!projected) { p = Project(op, v4); projected = true; }
                        p = Vector3.Scale(p, Vector3.Lerp(Vector3.one, op.axisScale, op.amount));
                        break;
                }
            }

            // No explicit Project stage: drop W, which is the honest default.
            if (!projected) p = new Vector3(v4.x, v4.y, v4.z);
            return p;
        }

        static void Rot(ref float a, ref float b, float angle)
        {
            if (angle == 0f) return;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float a0 = a, b0 = b;
            a = a0 * c - b0 * s;
            b = a0 * s + b0 * c;
        }

        static Vector3 Project(ShapeOperator op, Vector4 v)
        {
            switch (op.projection)
            {
                case Projection4D.Stereographic:
                {
                    float d = 1f - v.w;
                    if (Mathf.Abs(d) < .08f) d = Mathf.Sign(d == 0f ? 1f : d) * .08f;
                    return new Vector3(v.x, v.y, v.z) / d * .5f;
                }
                case Projection4D.Perspective:
                {
                    float near = Mathf.Max(op.wDistance, 1.05f);
                    return new Vector3(v.x, v.y, v.z) * (near / Mathf.Max(near - v.w, .08f));
                }
                default:
                    return new Vector3(v.x, v.y, v.z);
            }
        }

        static Vector3 Swirl(ShapeOperator op, Vector3 v, float time)
        {
            Vector3 axis = op.swirlAxis.sqrMagnitude < 1e-6f ? Vector3.forward : op.swirlAxis.normalized;
            if (op.inversion > 0f)
            {
                float r2 = v.sqrMagnitude;
                if (r2 > 1e-6f) v = Vector3.Lerp(v, v / r2, op.inversion * op.amount);
            }
            if (op.twist != 0f || op.vortex != 0f)
            {
                float along = Vector3.Dot(v, axis);
                Vector3 radial = v - axis * along;
                float rad = radial.magnitude;
                float angle = op.twist * along * op.amount;
                if (op.vortex != 0f && rad > 1e-4f) angle += op.vortex * op.amount / rad * .25f;
                if (rad > 1e-6f && angle != 0f)
                {
                    Vector3 tangent = Vector3.Cross(axis, radial);
                    v = axis * along + radial * Mathf.Cos(angle) + tangent * Mathf.Sin(angle);
                }
            }
            return v;
        }

        static float Hash(float x)
        {
            float s = Mathf.Sin(x * 127.1f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        protected override string Describe() =>
            report.Replace("\n", " · ").TrimEnd(' ', '·') + " · " + NodeTotal.ToString("N0")
            + " nodes, " + EdgeTotal.ToString("N0") + " edges";

        // ---- recipes ---------------------------------------------------------

        void ApplyRecipe(Recipe recipe)
        {
            if (recipe == Recipe.Custom) return;
            sources.Clear();
            operators.Clear();

            switch (recipe)
            {
                case Recipe.DekeractReproduced:
                    // The dekeract, through the engine: 1,024 nodes, 5,120 edges.
                    sources.Add(new ShapeSource { label = "10-cube", kind = ShapeSourceKind.NCube, dimensions = 10, ndRate = .25f });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Project, projection = Projection4D.Perspective, wDistance = 2.6f });
                    break;

                case Recipe.E8RootSystem:
                    // 240 roots, 56 neighbours each, 6,720 edges. Not a cube.
                    sources.Add(new ShapeSource { label = "E8", kind = ShapeSourceKind.RootSystem, roots = RootSystemKind.E8, ndRate = .18f, maxEdges = 8000 });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Rotate4D });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Project, projection = Projection4D.Perspective, wDistance = 2.4f });
                    break;

                case Recipe.CliffordMandelbulb:
                    sources.Add(new ShapeSource { label = "Clifford torus", kind = ShapeSourceKind.CliffordGrid, uRes = 48, vRes = 48 });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Rotate4D });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Project, projection = Projection4D.Stereographic });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.FractalDisplace, amount = .45f, fieldScale = .55f });
                    break;

                case Recipe.Stellated120Cell:
                    // Same 600 vertices, connected at the second distance band: a star figure.
                    sources.Add(new ShapeSource { label = "120-cell stellated", kind = ShapeSourceKind.Polytope4D, polytope = Polytope4DSwarm.Polytope.Cell120, edgeRank = 2, maxEdges = 16000 });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Rotate4D });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Project, projection = Projection4D.Perspective, wDistance = 2.2f });
                    break;

                case Recipe.KleinKaleidoTunnel:
                    sources.Add(new ShapeSource { label = "Klein bottle", kind = ShapeSourceKind.ManifoldGrid, surface = ManifoldSurface.KleinBottle, uRes = 72, vRes = 40 });
                    var kal = new ShapeOperator { kind = ShapeOperatorKind.Kaleidoscope };
                    kal.kaleido.sectors = 6;
                    kal.kaleido.lengthRepeat = 2.5f;
                    operators.Add(kal);
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Swirl, twist = 1.2f, amount = 1f });
                    break;

                case Recipe.JohnsonCluster:
                    for (int i = 0; i < 4; i++)
                        sources.Add(new ShapeSource
                        {
                            label = "Johnson " + (i + 1),
                            kind = ShapeSourceKind.Polyhedron,
                            solid = PolyhedronSwarm.Solid.Johnson,
                            shellRadiusSquared = i * 7,
                            offset = new Vector3(Mathf.Cos(i * 1.57f) * 3f, 0f, Mathf.Sin(i * 1.57f) * 3f),
                            spin = new Vector3(0f, 18f + i * 7f, 0f)
                        });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Jitter, jitter = .05f });
                    break;

                case Recipe.HyperLattice:
                    sources.Add(new ShapeSource { label = "Z^5 shell", kind = ShapeSourceKind.LatticeShell, dimensions = 5, shellRadiusSquared = 6, ndRate = .2f, maxEdges = 16000 });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Rotate4D });
                    operators.Add(new ShapeOperator { kind = ShapeOperatorKind.Project, projection = Projection4D.Perspective });
                    break;
            }
            signature = "";
        }

        [ContextMenu("Recipe: dekeract reproduced")] void R0() { applyRecipe = Recipe.DekeractReproduced; }
        [ContextMenu("Recipe: E8 root system")] void R1() { applyRecipe = Recipe.E8RootSystem; }
        [ContextMenu("Recipe: Clifford Mandelbulb")] void R2() { applyRecipe = Recipe.CliffordMandelbulb; }
        [ContextMenu("Recipe: stellated 120-cell")] void R3() { applyRecipe = Recipe.Stellated120Cell; }
        [ContextMenu("Recipe: Klein kaleido tunnel")] void R4() { applyRecipe = Recipe.KleinKaleidoTunnel; }
        [ContextMenu("Recipe: Johnson cluster")] void R5() { applyRecipe = Recipe.JohnsonCluster; }
        [ContextMenu("Recipe: hyper lattice")] void R6() { applyRecipe = Recipe.HyperLattice; }
    }
}
