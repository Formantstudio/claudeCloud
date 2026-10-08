using System;
using System.Collections.Generic;
using UnityEngine;
using polyhedronGenerator.scripts;          // MeshBuilder
using polyhedronGenerator.scripts.solids;   // Tetrahedon, Cube, Octahedron, ...

namespace PsychedelicLab.GeometryFX
{
    /// <summary>Which solid a role draws its particles with.</summary>
    public enum SwarmMesh { Tetrahedron, Cube, Octahedron, Dodecahedron, Icosahedron, Custom }

    /// <summary>
    /// Which local axis of the particle mesh runs along a strut. Depends on how the mesh was
    /// generated, so it is exposed rather than assumed.
    /// </summary>
    public enum MeshAxis { X, Y, Z }

    /// <summary>
    /// How a role orients its particles.
    /// - <see cref="Tumble"/> is the Dekeract's look: free rotation, read as sparks.
    /// - <see cref="AlongEdge"/> aligns and stretches each particle down its strut, which is what
    ///   turns a set of solids into a continuous beam and a frame into a skeleton.
    /// - <see cref="Fixed"/> keeps a constant rotation, for crystalline reads.
    /// </summary>
    public enum SwarmOrient { Tumble, AlongEdge, Fixed }

    /// <summary>One rendering role: its own mesh, material, size, orientation and particle system.</summary>
    [Serializable]
    public sealed class SwarmRole
    {
        public bool on = true;
        public string label = "";

        [Header("Look")]
        public SwarmMesh mesh = SwarmMesh.Tetrahedron;
        [Tooltip("Used when mesh is Custom. Any mesh works, including the GeometryFXParticles meshes.")]
        public Mesh customMesh;
        [Tooltip("Material for this role. The GeometryFXParticles library has 173 of them.")]
        public Material material;
        [Tooltip("polyhedronGenerator/prefabs/urp/wireframeParticle.prefab, or any prefab with a ParticleSystem.")]
        public GameObject particlePrefab;

        [Header("Shape")]
        public SwarmOrient orient = SwarmOrient.Tumble;
        public MeshAxis meshAxis = MeshAxis.Z;
        [Tooltip("Cross-section size.")]
        [Range(.002f, .5f)] public float thickness = .02f;
        [Tooltip("For AlongEdge: how much of each segment's slot the solid fills. Above 1 the segments overlap and read as one continuous beam.")]
        [Range(.2f, 2f)] public float fill = 1.15f;
        [Tooltip("For Tumble and Fixed: uniform size.")]
        [Range(.002f, .5f)] public float size = .035f;

        [Header("Density")]
        [Tooltip("Solids chained along each edge. This is what sets how solid a strut looks.")]
        [Range(1, 64)] public int segments = 6;
        [Tooltip("Only edges at least this long get this role. Lets rails pick out the long runs.")]
        [Range(0f, 10f)] public float minEdgeLength;
        [Tooltip("Only the N longest edges get this role. 0 = no limit. This is how a rail role selects the spine of a shape.")]
        [Range(0, 4000)] public int longestEdgesOnly;

        [Header("Building block")]
        [Tooltip("Stamp a whole smaller shape at every node instead of a single solid. This is what makes a dekeract whose every vertex is a Metatron.")]
        public bool substitute;
        [Tooltip("The shape stamped at each node. Keep it small: its node count multiplies.")]
        public ShapeSource block = new ShapeSource { kind = ShapeSourceKind.Metatron3D, maxEdges = 400 };
        [Tooltip("Size of each stamped block, relative to the host shape's radius.")]
        [Range(.01f, .5f)] public float blockScale = .09f;
        [Tooltip("Stamp at every Nth node, so a 1,024-vertex host does not cost 1,024 blocks.")]
        [Range(1, 64)] public int blockStride = 1;
        [Tooltip("Hard cap on how many blocks get stamped.")]
        [Range(1, 2000)] public int blockLimit = 256;
        [Tooltip("Spins each block on its own axis.")]
        public Vector3 blockSpin = new Vector3(0f, 30f, 11f);
        [Tooltip("Draws each block's own edges as struts as well as its vertices.")]
        public bool blockEdges = true;

        [Header("Motion")]
        [Tooltip("Slides the chain along its edge, so a frame reads as flowing.")]
        [Range(-2f, 2f)] public float flow;
        public Vector3 spinDegrees = new Vector3(13f, -19f, 7f);

        [Header("Colour")]
        public Color nearColor = new Color(.12f, .85f, 1f, .5f);
        public Color farColor = new Color(1f, .55f, .13f, .8f);
        [Tooltip("Shifts colour by the per-node field value, when a source writes one.")]
        [Range(0f, 1f)] public float accentColour = .7f;

        [NonSerialized] public ParticleSystem system;
        [NonSerialized] public GameObject host;
        [NonSerialized] public Mesh builtMesh;
        [NonSerialized] public ParticleSystem.Particle[] buffer;
        [NonSerialized] public int live;
        [NonSerialized] public int[] edgeFilter;
        [NonSerialized] public Vector3[] blockPoints;
        [NonSerialized] public int[] blockEdgePairs;
    }

    /// <summary>
    /// Builds skeletal frames and rails out of solids.
    ///
    /// The difference from <see cref="GeometryFractalEngine"/> is not the shapes, which are the
    /// same source stack, but the rendering. That engine puts a particle at a point. This one has
    /// **roles**, each with its own mesh, material and orientation, and each role draws a different
    /// part of the structure:
    ///
    ///   nodes   - a solid at every vertex, the joints of the frame
    ///   struts  - solids chained along every edge, aligned and stretched so a run of tetrahedra
    ///             reads as one continuous beam rather than a line of sparks
    ///   rails   - the same, restricted to the longest edges, thicker, for the spine of a shape
    ///   sparks  - free-tumbling solids, the Dekeract's own look, layered on top
    ///
    /// `SwarmOrient.AlongEdge` with `startSize3D` is the mechanism that makes this possible: each
    /// particle is rotated onto its strut's direction and scaled thin-thin-long, so solids tile
    /// end to end. A tetrahedron stretched that way is a girder.
    ///
    /// Every role is a separate ParticleSystem, which is what lets each use a different material
    /// from the GeometryFXParticles library instead of all sharing one.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GeometricParticleSwarmEngine : MonoBehaviour
    {
        public enum Frame
        {
            Custom,
            TetrahedronSkeleton,
            PolyhedronFrame,
            RailSpine,
            E8Girders,
            DekeractSparkFrame,
            TesseractBlock,
            PenteractBlock,
            Metatron3DBlock,
            Metatron4DBlock,
            DekeractOfMetatrons,
            TesseractOfHedrons
        }

        [Header("Frame")]
        [Tooltip("Fills the source and role stacks with a known-good skeleton, then returns to Custom.")]
        public Frame applyFrame = Frame.TetrahedronSkeleton;

        [Header("Shape sources")]
        [Tooltip("The same source stack the fractal engine uses, so any shape can be framed.")]
        public List<ShapeSource> sources = new List<ShapeSource>();
        [Range(64, 20000)] public int maxNodes = 8000;
        [Min(.1f)] public float radius = 3f;

        [Header("Roles")]
        public List<SwarmRole> roles = new List<SwarmRole>();

        [Header("4-D")]
        public bool useSharedAxis = true;
        public Projection4D projection = Projection4D.Perspective;
        [Range(1.05f, 8f)] public float wDistance = 2.6f;
        [Range(0f, 2f)] public float ownRate = .1f;

        [Header("Motion")]
        public bool animate = true;
        [Range(0f, 1f)] public float scatter;
        public bool assembleOnPlay = true;
        [Range(1f, 20f)] public float regroupSpeed = 5f;
        public bool previewInEditor = true;

        public string Status { get; private set; } = "";

        readonly List<Vector3> nodes = new List<Vector3>();
        readonly List<float> accents = new List<float>();
        readonly List<int> edgeA = new List<int>();
        readonly List<int> edgeB = new List<int>();
        GameObject root;
        string signature = "";
        double elapsed;
        bool initialized;

        void OnEnable() { Rebuild(); }
        void OnDisable() { Release(); }

        void Update()
        {
            if (applyFrame != Frame.Custom) { ApplyFrame(applyFrame); applyFrame = Frame.Custom; Rebuild(); }
            if (!Application.isPlaying && !previewInEditor) { Release(); return; }
            if (root == null || signature != Signature()) Rebuild();
            if (root == null) return;

            if (Application.isPlaying && animate) elapsed += Time.deltaTime;
            float follow = Application.isPlaying
                ? 1f - Mathf.Exp(-regroupSpeed * Mathf.Min(Time.deltaTime, .1f))
                : 1f;
            Render((float)elapsed, follow);
        }

        string Signature()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(maxNodes);
            foreach (var s in sources)
                if (s != null)
                    sb.Append('|').Append(s.on ? 1 : 0).Append((int)s.kind).Append(s.dimensions)
                      .Append((int)s.polytope).Append((int)s.roots).Append((int)s.solid)
                      .Append((int)s.surface).Append(s.uRes).Append(s.vRes).Append(s.edgeRank);
            foreach (var r in roles)
                if (r != null)
                    sb.Append('/').Append(r.on ? 1 : 0).Append((int)r.mesh).Append(r.segments)
                      .Append((int)r.orient).Append(r.longestEdgesOnly)
                      .Append(r.minEdgeLength.ToString("0.00"));
            return sb.ToString();
        }

        // ---- build -----------------------------------------------------------

        void Rebuild()
        {
            Release();
            if (sources.Count == 0 || roles.Count == 0) ApplyFrame(Frame.TetrahedronSkeleton);
            signature = Signature();

            BuildStructure();
            if (nodes.Count == 0) { Status = "No geometry from the sources"; return; }

            root = new GameObject("Generated particle frame") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.SetParent(transform, false);

            foreach (var role in roles)
            {
                if (role == null || !role.on) continue;
                BuildRole(role);
            }
            initialized = false;
        }

        /// <summary>Runs every source through the shared generator and flattens the result.</summary>
        void BuildStructure()
        {
            nodes.Clear(); accents.Clear(); edgeA.Clear(); edgeB.Clear();

            foreach (var source in sources)
            {
                if (source == null || !source.on) continue;

                var points = new List<float[]>();
                var pairs = new List<int>();
                GeometryFractalEngine.GenerateSource(source, points, pairs);
                if (points.Count == 0) continue;
                if (nodes.Count + points.Count > maxNodes) continue;

                int first = nodes.Count;
                source.firstNode = first;
                source.dim = points[0].Length;
                source.pointCount = points.Count;
                source.raw = new float[points.Count * source.dim];
                for (int i = 0; i < points.Count; i++)
                {
                    Array.Copy(points[i], 0, source.raw, i * source.dim, source.dim);
                    nodes.Add(Vector3.zero);
                    accents.Add(0f);
                }
                for (int i = 0; i + 1 < pairs.Count; i += 2)
                {
                    edgeA.Add(first + pairs[i]);
                    edgeB.Add(first + pairs[i + 1]);
                }
            }
        }

        void BuildRole(SwarmRole role)
        {
            if (!role.particlePrefab || !role.material) return;

            role.host = Instantiate(role.particlePrefab, root.transform);
            role.host.name = string.IsNullOrEmpty(role.label) ? role.mesh + " role" : role.label;
            role.host.hideFlags = HideFlags.HideAndDontSave;
            role.host.transform.localPosition = Vector3.zero;
            role.host.transform.localRotation = Quaternion.identity;
            role.host.transform.localScale = Vector3.one;

            role.system = role.host.GetComponent<ParticleSystem>();
            if (!role.system)
            {
                Debug.LogError("Role " + role.host.name + ": prefab has no ParticleSystem.", this);
                return;
            }
            role.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            role.edgeFilter = FilterEdges(role);
            BuildBlock(role);
            int wanted = role.orient == SwarmOrient.AlongEdge || role.segments > 0
                ? role.edgeFilter.Length * Mathf.Max(role.segments, 1)
                : 0;
            // A role with no edges selected falls back to drawing the nodes.
            if (role.edgeFilter.Length == 0) wanted = nodes.Count;
            if (role.substitute && role.block != null)
            {
                // One stamp per selected host node, each costing its block's nodes plus, if the
                // block's edges are drawn, one solid per block edge.
                int stamps = Mathf.Min(Mathf.CeilToInt(nodes.Count / (float)Mathf.Max(role.blockStride, 1)),
                                       role.blockLimit);
                var probePts = new List<float[]>();
                var probePairs = new List<int>();
                GeometryFractalEngine.GenerateSource(role.block, probePts, probePairs);
                int perStamp = probePts.Count + (role.blockEdges ? probePairs.Count / 2 : 0);
                wanted += stamps * Mathf.Max(perStamp, 1);
            }
            wanted = Mathf.Clamp(wanted + nodes.Count, 16, 120000);
            role.buffer = new ParticleSystem.Particle[wanted];

            var main = role.system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = wanted;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startRotation3D = true;
            // Non-uniform size is what lets a solid be stretched into a girder.
            main.startSize3D = true;

            // Each module has to be copied to a local before being written: they are returned by
            // value, so `system.emission.enabled = false` does not compile.
            var em = role.system.emission; em.enabled = false;
            var sh = role.system.shape; sh.enabled = false;
            var vel = role.system.velocityOverLifetime; vel.enabled = false;
            var noi = role.system.noise; noi.enabled = false;
            var tr = role.system.trails; tr.enabled = false;
            var col = role.system.colorOverLifetime; col.enabled = false;
            var siz = role.system.sizeOverLifetime; siz.enabled = false;

            role.builtMesh = BuildMesh(role);

            var renderer = role.system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = role.builtMesh;
            renderer.sharedMaterial = role.material;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 200f);

            role.system.Pause();
        }

        /// <summary>
        /// Generates the role's building block once. Its points are cached in 3-D and stamped at
        /// each host node every frame, so the cost per frame is the stamping, not the generation.
        /// </summary>
        static void BuildBlock(SwarmRole role)
        {
            role.blockPoints = null;
            role.blockEdgePairs = null;
            if (!role.substitute || role.block == null) return;

            var pts = new List<float[]>();
            var pairs = new List<int>();
            GeometryFractalEngine.GenerateSource(role.block, pts, pairs);
            if (pts.Count == 0) return;

            var flat = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                var c = pts[i];
                // Blocks are stamped in 3-D: drop anything above the third coordinate, which keeps
                // a 4-D block such as the 24-cell readable as a solid cluster.
                flat[i] = new Vector3(c.Length > 0 ? c[0] : 0f,
                                      c.Length > 1 ? c[1] : 0f,
                                      c.Length > 2 ? c[2] : 0f);
            }
            role.blockPoints = flat;
            role.blockEdgePairs = role.blockEdges ? pairs.ToArray() : System.Array.Empty<int>();
        }

        static Mesh BuildMesh(SwarmRole role)
        {
            if (role.mesh == SwarmMesh.Custom && role.customMesh) return role.customMesh;
            MeshBuilder builder;
            switch (role.mesh)
            {
                case SwarmMesh.Cube: builder = Cube.generate(1); break;
                case SwarmMesh.Octahedron: builder = Octahedron.generate(1); break;
                case SwarmMesh.Dodecahedron: builder = Dodecahedron.generate(1); break;
                case SwarmMesh.Icosahedron: builder = Icosahedron.generate(1); break;
                default: builder = Tetrahedon.generate(1); break;
            }
            var mesh = builder.build(role.mesh + " particle");
            mesh.hideFlags = HideFlags.HideAndDontSave;
            return mesh;
        }

        /// <summary>
        /// Which edges this role draws. `minEdgeLength` and `longestEdgesOnly` are what let a rail
        /// role pick out a shape's long spine while a strut role takes everything.
        /// </summary>
        int[] FilterEdges(SwarmRole role)
        {
            int count = edgeA.Count;
            if (count == 0) return Array.Empty<int>();

            var kept = new List<int>(count);
            for (int e = 0; e < count; e++) kept.Add(e);

            if (role.longestEdgesOnly > 0 && role.longestEdgesOnly < count)
            {
                // Length is measured on the structure's rest pose, which is enough for selection.
                var lengths = new float[count];
                for (int e = 0; e < count; e++) lengths[e] = Length(e);
                kept.Sort((x, y) => lengths[y].CompareTo(lengths[x]));
                kept.RemoveRange(role.longestEdgesOnly, kept.Count - role.longestEdgesOnly);
            }
            if (role.minEdgeLength > 0f)
                kept.RemoveAll(e => Length(e) < role.minEdgeLength);

            return kept.ToArray();
        }

        float Length(int edge) =>
            edge >= 0 && edge < edgeA.Count && edgeA[edge] < nodes.Count && edgeB[edge] < nodes.Count
                ? Vector3.Distance(nodes[edgeA[edge]], nodes[edgeB[edge]])
                : 0f;

        // ---- per frame -------------------------------------------------------

        void Render(float time, float follow)
        {
            UpdateNodes(time);

            float release = Mathf.Clamp01(scatter);
            if (assembleOnPlay && Application.isPlaying)
                release = Mathf.Max(release, Mathf.Exp(-time * 1.1f));

            int totalLive = 0;
            foreach (var role in roles)
            {
                if (role == null || !role.on || role.system == null || role.buffer == null) continue;
                FillRole(role, time, release, follow);
                totalLive += role.live;
            }
            initialized = true;

            Status = nodes.Count.ToString("N0") + " nodes, " + edgeA.Count.ToString("N0") + " edges · "
                   + totalLive.ToString("N0") + " particles across " + CountRoles() + " roles";
        }

        int CountRoles()
        {
            int n = 0;
            foreach (var r in roles) if (r != null && r.on && r.system) n++;
            return n;
        }

        /// <summary>Runs each source's own N-D rotation, collapses to 4-D, then projects.</summary>
        void UpdateNodes(float time)
        {
            var shared = useSharedAxis ? Hyperspace4DAxis.Current : null;

            foreach (var source in sources)
            {
                if (source == null || !source.on || source.raw == null) continue;
                var coord = new float[source.dim];
                var spin = Quaternion.Euler(source.euler + source.spin * time);

                for (int i = 0; i < source.pointCount; i++)
                {
                    int node = source.firstNode + i;
                    if (node >= nodes.Count) break;
                    Array.Copy(source.raw, i * source.dim, coord, 0, source.dim);

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

                    Vector4 v4 = Collapse(coord, source.dim);
                    if (shared) v4 = shared.Rotate(v4);
                    else if (ownRate > 0f)
                    {
                        float x = v4.x, y = v4.y, z = v4.z, w = v4.w, tau = Mathf.PI * 2f;
                        Rot(ref x, ref y, ownRate * time * tau);
                        Rot(ref z, ref w, ownRate * time * tau);
                        v4 = new Vector4(x, y, z, w);
                    }

                    Vector3 p = shared ? shared.Project(v4) : Project(v4);
                    nodes[node] = spin * (p * source.scale * radius) + source.offset;
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
            return new Vector4(x, y, z, w) * Mathf.Pow(d - 1f, (dim - 4) * .5f);
        }

        static void Rot(ref float a, ref float b, float angle)
        {
            if (angle == 0f) return;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float a0 = a, b0 = b;
            a = a0 * c - b0 * s;
            b = a0 * s + b0 * c;
        }

        Vector3 Project(Vector4 v)
        {
            switch (projection)
            {
                case Projection4D.Stereographic:
                {
                    float d = 1f - v.w;
                    if (Mathf.Abs(d) < .08f) d = Mathf.Sign(d == 0f ? 1f : d) * .08f;
                    return new Vector3(v.x, v.y, v.z) / d * .5f;
                }
                case Projection4D.Perspective:
                {
                    float near = Mathf.Max(wDistance, 1.05f);
                    return new Vector3(v.x, v.y, v.z) * (near / Mathf.Max(near - v.w, .08f));
                }
                default:
                    return new Vector3(v.x, v.y, v.z);
            }
        }

        void FillRole(SwarmRole role, float time, float release, float follow)
        {
            int index = 0;
            var buffer = role.buffer;

            // Struts and rails: chained solids down each selected edge.
            if (role.edgeFilter != null && role.edgeFilter.Length > 0)
            {
                int segments = Mathf.Max(role.segments, 1);
                foreach (int e in role.edgeFilter)
                {
                    Vector3 a = nodes[edgeA[e]], b = nodes[edgeB[e]];
                    Vector3 delta = b - a;
                    float length = delta.magnitude;
                    if (length < 1e-5f) continue;
                    Vector3 dir = delta / length;
                    float accent = (accents[edgeA[e]] + accents[edgeB[e]]) * .5f;

                    for (int seg = 0; seg < segments; seg++)
                    {
                        if (index >= buffer.Length) { Publish(role, index); return; }
                        float t = role.flow != 0f
                            ? Mathf.Repeat((seg + .5f) / segments + time * role.flow, 1f)
                            : (seg + .5f) / segments;
                        Vector3 p = a + dir * (length * t);
                        WriteParticle(role, ref buffer[index], index, p, dir, length / segments,
                                      true, accent, time, release, follow);
                        index++;
                    }
                }
            }

            // Building blocks: a whole smaller shape stamped at every selected host node.
            if (role.substitute && role.blockPoints != null && role.blockPoints.Length > 0)
            {
                int stride = Mathf.Max(role.blockStride, 1);
                float scale = role.blockScale * radius;
                int stamped = 0;

                for (int n = 0; n < nodes.Count && stamped < role.blockLimit; n += stride, stamped++)
                {
                    Vector3 centre = nodes[n];
                    var turn = Quaternion.Euler(role.blockSpin * time + Vector3.one * (n * 17f));

                    for (int i = 0; i < role.blockPoints.Length; i++)
                    {
                        if (index >= buffer.Length) { Publish(role, index); return; }
                        Vector3 p = centre + turn * role.blockPoints[i] * scale;
                        WriteParticle(role, ref buffer[index], index, p, Vector3.forward, 0f,
                                      false, accents[n], time, release, follow);
                        index++;
                    }

                    if (role.blockEdgePairs != null)
                        for (int e = 0; e + 1 < role.blockEdgePairs.Length; e += 2)
                        {
                            if (index >= buffer.Length) { Publish(role, index); return; }
                            Vector3 a = centre + turn * role.blockPoints[role.blockEdgePairs[e]] * scale;
                            Vector3 b = centre + turn * role.blockPoints[role.blockEdgePairs[e + 1]] * scale;
                            Vector3 delta = b - a;
                            float len = delta.magnitude;
                            Vector3 dir = len > 1e-5f ? delta / len : Vector3.forward;
                            WriteParticle(role, ref buffer[index], index, a + delta * .5f, dir, len,
                                          true, accents[n], time, release, follow);
                            index++;
                        }
                }
            }
            else
            {
                // Nodes: one solid at every joint.
                for (int n = 0; n < nodes.Count; n++)
                {
                    if (index >= buffer.Length) break;
                    WriteParticle(role, ref buffer[index], index, nodes[n], Vector3.forward, 0f,
                                  false, accents[n], time, release, follow);
                    index++;
                }
            }

            Publish(role, index);
        }

        void Publish(SwarmRole role, int count)
        {
            role.live = count;
            role.system.SetParticles(role.buffer, count);
        }

        void WriteParticle(SwarmRole role, ref ParticleSystem.Particle p, int index,
                           Vector3 target, Vector3 dir, float segmentLength,
                           bool strut, float accent, float time, float release, float follow)
        {
            float h = Mathf.Repeat(index * .61803399f, 1f);

            if (release > 0f)
            {
                // Scatter to an even cloud, same construction as the Dekeract's.
                float azimuth = h * Mathf.PI * 2f + time * .24f;
                float y = Mathf.Repeat(index * .75487766f, 1f) * 2f - 1f;
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                Vector3 free = new Vector3(Mathf.Cos(azimuth) * ring, y, Mathf.Sin(azimuth) * ring) * radius * 1.8f;
                target = Vector3.Lerp(target, free, release);
            }

            p.position = initialized ? Vector3.Lerp(p.position, target, follow) : target;
            p.startLifetime = 1000f;
            p.remainingLifetime = 1000f;
            p.velocity = Vector3.zero;
            p.randomSeed = (uint)index + 1;

            if (strut && role.orient == SwarmOrient.AlongEdge)
            {
                // Rotate the mesh's chosen axis onto the strut, then stretch it along that axis so
                // consecutive solids tile into one beam.
                p.rotation3D = AlignEuler(role.meshAxis, dir);
                float along = segmentLength * role.fill;
                p.startSize3D = SizeAlong(role.meshAxis, role.thickness, along);
            }
            else if (role.orient == SwarmOrient.Fixed)
            {
                p.rotation3D = Vector3.zero;
                p.startSize3D = Vector3.one * role.size;
            }
            else
            {
                p.rotation3D = new Vector3(h * 360f + time * role.spinDegrees.x,
                                           h * 150f + time * role.spinDegrees.y,
                                           h * 270f + time * role.spinDegrees.z);
                p.startSize3D = Vector3.one * role.size;
            }

            float mix = Mathf.Clamp01((strut ? h * .3f : .6f + .4f * h) + accent * role.accentColour);
            p.startColor = Color.Lerp(role.nearColor, role.farColor, mix);
        }

        static Vector3 AlignEuler(MeshAxis axis, Vector3 dir)
        {
            Vector3 from = axis == MeshAxis.X ? Vector3.right : axis == MeshAxis.Y ? Vector3.up : Vector3.forward;
            return Quaternion.FromToRotation(from, dir).eulerAngles;
        }

        static Vector3 SizeAlong(MeshAxis axis, float thickness, float along)
        {
            switch (axis)
            {
                case MeshAxis.X: return new Vector3(along, thickness, thickness);
                case MeshAxis.Y: return new Vector3(thickness, along, thickness);
                default: return new Vector3(thickness, thickness, along);
            }
        }

        // ---- teardown --------------------------------------------------------

        void Release()
        {
            foreach (var role in roles)
            {
                if (role == null) continue;
                if (role.builtMesh && role.mesh != SwarmMesh.Custom) Dispose(role.builtMesh);
                role.builtMesh = null;
                role.system = null;
                role.buffer = null;
                role.host = null;
                role.edgeFilter = null;
                role.live = 0;
            }
            Dispose(root);
            root = null;
            signature = "";
            initialized = false;
        }

        static void Dispose(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        // ---- frames ----------------------------------------------------------

        void ApplyFrame(Frame frame)
        {
            if (frame == Frame.Custom) return;
            sources.Clear();
            roles.Clear();

            var prefab = FindPrefab();
            var strutMat = FindMaterial("Particles_Cyan");
            var nodeMat = FindMaterial("Particles_Gold");
            var sparkMat = FindMaterial("Particles_Ember");

            switch (frame)
            {
                case Frame.TetrahedronSkeleton:
                    sources.Add(new ShapeSource { label = "Icosahedron", kind = ShapeSourceKind.Polyhedron, solid = PolyhedronSwarm.Solid.Icosahedron });
                    roles.Add(Strut("Tetra girders", SwarmMesh.Tetrahedron, prefab, strutMat, 8, .022f));
                    roles.Add(Node("Joints", SwarmMesh.Octahedron, prefab, nodeMat, .07f));
                    break;

                case Frame.PolyhedronFrame:
                    sources.Add(new ShapeSource { label = "Dodecahedron", kind = ShapeSourceKind.Polyhedron, solid = PolyhedronSwarm.Solid.Dodecahedron });
                    roles.Add(Strut("Cube girders", SwarmMesh.Cube, prefab, strutMat, 10, .016f));
                    roles.Add(Node("Icosa joints", SwarmMesh.Icosahedron, prefab, nodeMat, .085f));
                    roles.Add(Spark("Sparks", prefab, sparkMat, .012f));
                    break;

                case Frame.RailSpine:
                    sources.Add(new ShapeSource { label = "10-cube", kind = ShapeSourceKind.NCube, dimensions = 10, ndRate = .22f });
                    var rail = Strut("Rails", SwarmMesh.Cube, prefab, strutMat, 24, .05f);
                    rail.longestEdgesOnly = 48;
                    rail.flow = .25f;
                    roles.Add(rail);
                    roles.Add(Spark("Dekeract sparks", prefab, sparkMat, .008f));
                    break;

                case Frame.E8Girders:
                    sources.Add(new ShapeSource { label = "E8", kind = ShapeSourceKind.RootSystem, roots = RootSystemKind.E8, ndRate = .16f, maxEdges = 8000 });
                    var thin = Strut("E8 struts", SwarmMesh.Tetrahedron, prefab, strutMat, 2, .008f);
                    roles.Add(thin);
                    var spine = Strut("E8 spine", SwarmMesh.Octahedron, prefab, nodeMat, 8, .03f);
                    spine.longestEdgesOnly = 120;
                    roles.Add(spine);
                    break;

                case Frame.TesseractBlock:
                    // The dekeract swarm at four dimensions: 16 vertices, 32 edges. Sparse, so the
                    // struts carry the density instead of the edge count.
                    sources.Add(new ShapeSource { label = "Tesseract", kind = ShapeSourceKind.NCube, dimensions = 4, ndRate = .2f });
                    roles.Add(Strut("Tesseract girders", SwarmMesh.Tetrahedron, prefab, strutMat, 28, .02f));
                    roles.Add(Node("Vertices", SwarmMesh.Octahedron, prefab, nodeMat, .08f));
                    break;

                case Frame.PenteractBlock:
                    // Five dimensions: 32 vertices, 80 edges.
                    sources.Add(new ShapeSource { label = "Penteract", kind = ShapeSourceKind.NCube, dimensions = 5, ndRate = .2f });
                    roles.Add(Strut("Penteract girders", SwarmMesh.Tetrahedron, prefab, strutMat, 18, .016f));
                    roles.Add(Node("Vertices", SwarmMesh.Octahedron, prefab, nodeMat, .06f));
                    break;

                case Frame.Metatron3DBlock:
                    // The Vector Equilibrium, unflattened: 13 nodes, all 78 lines.
                    sources.Add(new ShapeSource { label = "Metatron 3D", kind = ShapeSourceKind.Metatron3D, maxEdges = 200 });
                    roles.Add(Strut("78 lines", SwarmMesh.Tetrahedron, prefab, strutMat, 14, .014f));
                    roles.Add(Node("13 nodes", SwarmMesh.Icosahedron, prefab, nodeMat, .1f));
                    break;

                case Frame.Metatron4DBlock:
                    // The 24-cell plus centre: 25 nodes, 300 lines.
                    sources.Add(new ShapeSource { label = "Metatron 4D", kind = ShapeSourceKind.Metatron4D, maxEdges = 400, ndRate = .14f });
                    roles.Add(Strut("300 lines", SwarmMesh.Tetrahedron, prefab, strutMat, 6, .01f));
                    roles.Add(Node("25 nodes", SwarmMesh.Icosahedron, prefab, nodeMat, .07f));
                    break;

                case Frame.DekeractOfMetatrons:
                    // The building-block idea: a 10-cube whose vertices each host a Metatron.
                    sources.Add(new ShapeSource { label = "10-cube host", kind = ShapeSourceKind.NCube, dimensions = 10, ndRate = .22f });
                    var frameRole = Strut("Host struts", SwarmMesh.Cube, prefab, strutMat, 2, .006f);
                    frameRole.longestEdgesOnly = 600;
                    roles.Add(frameRole);
                    var blocks = Node("Metatron blocks", SwarmMesh.Tetrahedron, prefab, nodeMat, .01f);
                    blocks.substitute = true;
                    blocks.block = new ShapeSource { kind = ShapeSourceKind.Metatron3D, maxEdges = 200 };
                    blocks.blockScale = .055f;
                    blocks.blockStride = 6;
                    blocks.blockLimit = 170;
                    blocks.orient = SwarmOrient.AlongEdge;
                    blocks.thickness = .006f;
                    roles.Add(blocks);
                    break;

                case Frame.TesseractOfHedrons:
                    // A tesseract whose 16 vertices each host an icosahedron.
                    sources.Add(new ShapeSource { label = "Tesseract host", kind = ShapeSourceKind.NCube, dimensions = 4, ndRate = .18f });
                    roles.Add(Strut("Host girders", SwarmMesh.Cube, prefab, strutMat, 20, .014f));
                    var hedrons = Node("Icosa blocks", SwarmMesh.Tetrahedron, prefab, sparkMat, .012f);
                    hedrons.substitute = true;
                    hedrons.block = new ShapeSource { kind = ShapeSourceKind.Polyhedron, solid = PolyhedronSwarm.Solid.Icosahedron, maxEdges = 200 };
                    hedrons.blockScale = .16f;
                    hedrons.blockStride = 1;
                    hedrons.blockLimit = 16;
                    hedrons.orient = SwarmOrient.AlongEdge;
                    hedrons.thickness = .01f;
                    roles.Add(hedrons);
                    break;

                case Frame.DekeractSparkFrame:
                    sources.Add(new ShapeSource { label = "10-cube", kind = ShapeSourceKind.NCube, dimensions = 10, ndRate = .25f });
                    roles.Add(Spark("Tumbling tetrahedra", prefab, strutMat, .008f));
                    var joints = Node("Vertices", SwarmMesh.Tetrahedron, prefab, nodeMat, .035f);
                    roles.Add(joints);
                    break;
            }
            signature = "";
        }

        static SwarmRole Strut(string label, SwarmMesh mesh, GameObject prefab, Material mat,
                               int segments, float thickness)
        {
            return new SwarmRole
            {
                label = label, mesh = mesh, particlePrefab = prefab, material = mat,
                orient = SwarmOrient.AlongEdge, segments = segments, thickness = thickness,
                fill = 1.15f
            };
        }

        static SwarmRole Node(string label, SwarmMesh mesh, GameObject prefab, Material mat, float size)
        {
            return new SwarmRole
            {
                label = label, mesh = mesh, particlePrefab = prefab, material = mat,
                orient = SwarmOrient.Tumble, segments = 0, size = size, longestEdgesOnly = 0,
                minEdgeLength = 999f   // no edges, so this role draws only the joints
            };
        }

        static SwarmRole Spark(string label, GameObject prefab, Material mat, float size)
        {
            return new SwarmRole
            {
                label = label, mesh = SwarmMesh.Tetrahedron, particlePrefab = prefab, material = mat,
                orient = SwarmOrient.Tumble, segments = 2, size = size
            };
        }

        static GameObject FindPrefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/polyhedronGenerator/prefabs/urp/wireframeParticle.prefab");
#else
            return null;
#endif
        }

        static Material FindMaterial(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_GeometryWork/Materials/" + name + ".mat");
#else
            return null;
#endif
        }

        [ContextMenu("Frame: tetrahedron skeleton")] void F0() { applyFrame = Frame.TetrahedronSkeleton; }
        [ContextMenu("Frame: polyhedron frame")] void F1() { applyFrame = Frame.PolyhedronFrame; }
        [ContextMenu("Frame: rail spine")] void F2() { applyFrame = Frame.RailSpine; }
        [ContextMenu("Frame: E8 girders")] void F3() { applyFrame = Frame.E8Girders; }
        [ContextMenu("Frame: dekeract spark frame")] void F4() { applyFrame = Frame.DekeractSparkFrame; }
        [ContextMenu("Frame: tesseract (4D)")] void F5() { applyFrame = Frame.TesseractBlock; }
        [ContextMenu("Frame: penteract (5D)")] void F6() { applyFrame = Frame.PenteractBlock; }
        [ContextMenu("Frame: Metatron 3D block")] void F7() { applyFrame = Frame.Metatron3DBlock; }
        [ContextMenu("Frame: Metatron 4D block (24-cell)")] void F8() { applyFrame = Frame.Metatron4DBlock; }
        [ContextMenu("Frame: dekeract OF Metatrons")] void F9() { applyFrame = Frame.DekeractOfMetatrons; }
        [ContextMenu("Frame: tesseract OF hedrons")] void F10() { applyFrame = Frame.TesseractOfHedrons; }
        [ContextMenu("Scatter")] public void Scatter() { scatter = 1f; }
        [ContextMenu("Reassemble")] public void Reassemble() { scatter = 0f; }
    }
}
