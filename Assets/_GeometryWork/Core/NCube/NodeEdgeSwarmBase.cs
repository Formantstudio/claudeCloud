using System.Collections.Generic;
using UnityEngine;
using polyhedronGenerator.scripts.solids;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Shared machinery for every node-and-edge particle swarm: one ParticleSystem in mesh render
    /// mode with a generated tetrahedron, node particles on the vertices, travellers sliding along
    /// the edges with a travelling phase, scatter/reassemble to a Fibonacci sphere, exponential
    /// follow smoothing, cyan-to-gold palette, per-particle tumble, and recursive fractal
    /// clustering of each particle.
    ///
    /// DekeractTetraSwarm is deliberately untouched. Subclasses here supply only a topology and a
    /// per-frame node position, so every figure gets its own small script:
    /// <see cref="MetatronCubeSwarm"/>, <see cref="TesseractSwarm"/> and the rest of the n-cubes.
    /// </summary>
    [ExecuteAlways]
    public abstract class NodeEdgeSwarmBase : MonoBehaviour
    {
        [Header("Particles")]
        [Tooltip("polyhedronGenerator/prefabs/urp/wireframeParticle.prefab")]
        public GameObject particlePrefab;
        [Tooltip("Combo/Dekeract_Particles.mat")]
        public Material particleMaterial;
        public enum ParticleSolid { Tetrahedron, Cube, Octahedron, Dodecahedron, Icosahedron }
        [Tooltip("The actual 3D solid stamped at each node and travelling along each edge.")]
        public ParticleSolid particleSolid;
        [Tooltip("Aim for this many particles and work the density out from the structure's own edge count. A 32-edge tesseract needs hundreds of particles per edge to look like anything; a 6,720-edge root system needs two. Leaving this on is what keeps every shape at the same density regardless of topology.")]
        public bool autoDensity = true;
        [Tooltip("Particle budget. DekeractTetraSwarm runs at 11,264, which is the bar.")]
        [Range(1000, 80000)] public int targetParticles = 12000;
        [Tooltip("Travellers per edge. Driven by autoDensity unless that is off.")]
        [Range(0, 512)] public int particlesPerEdge = 2;
        [Tooltip("Parallel strands per edge, braided around it. This is how a structure with few edges still gets volume instead of a line of dots.")]
        [Range(1, 12)] public int strands = 1;
        [Tooltip("How far the strands sit from the edge centre line.")]
        [Range(0f, .3f)] public float strandRadius = .035f;
        [Tooltip("Turns of the braid along each edge.")]
        [Range(-4f, 4f)] public float strandTwist = 1f;
        [Tooltip("Hard ceiling, so a bad setting cannot lock the editor.")]
        [Range(500, 120000)] public int maxParticles = 40000;

        [Header("Fractal clustering")]
        [Tooltip("Recursive subdivision of each particle into a self-similar clump. Costs 4^levels particles each. Driven by autoDensity unless that is off.")]
        [Range(0, 3)] public int fractalLevels = 1;
        [Tooltip("Size of the first clump, as a fraction of the figure's radius.")]
        [Range(0, .5f)] public float clusterSpread = .06f;
        [Tooltip("Below 0.5 leaves gaps at every level, which is what makes the recursion read.")]
        [Range(.1f, .9f)] public float clusterRatio = .42f;
        [Range(.2f, 1f)] public float childSizeFalloff = .62f;

        [Header("Assembly")]
        [Range(0, 1)] public float scatter;
        public bool breatheSwarm = true;
        public bool assembleOnPlay = true;
        [Range(1, 20)] public float regroupSpeed = 5f;
        [Range(.1f, 6f)] public float freeRadius = 1.8f;

        [Header("Size and colour")]
        [Range(.002f, .12f)] public float vertexParticleSize = .035f;
        [Range(.001f, .06f)] public float edgeParticleSize = .008f;
        [Tooltip("Travellers slide along the edges at this rate.")]
        [Range(-2, 2)] public float edgeFlow = .09f;
        public Color cyan = new Color(.12f, .85f, 1f, .5f);
        public Color gold = new Color(1f, .55f, .13f, .8f);

        [Header("Swirl")]
        [Tooltip("Twist about the swirl axis, proportional to distance along it.")]
        [Range(-8, 8)] public float twist;
        [Tooltip("Vortex: angular offset that grows toward the centre, so the figure curls inward.")]
        [Range(-8, 8)] public float vortex;
        [Tooltip("Spherical inversion strength. Turns the figure inside out through its own radius.")]
        [Range(0, 1)] public float inversion;
        [Tooltip("Pulls nodes onto a sphere of this radius. 0 = off.")]
        [Range(0, 1)] public float spherize;
        public Vector3 swirlAxis = Vector3.forward;
        [Tooltip("Animates the swirl phase over time.")]
        [Range(-2, 2)] public float swirlSpin;

        [Header("Scale")]
        [Range(.1f, 6f)] public float radius = 1.35f;
        public bool previewInEditor = true;

        public int ParticleCount => active;
        public int NodeTotal => nodes == null ? 0 : nodes.Length;
        public int EdgeTotal => edgeA == null ? 0 : edgeA.Length;
        public string Status { get; private set; } = "";

        // ---- subclass contract ----------------------------------------------

        /// <summary>Name for the generated child object.</summary>
        protected abstract string SwarmName { get; }
        /// <summary>How many nodes this figure has. Must stay stable until <see cref="TopologyDirty"/> trips.</summary>
        protected abstract int NodeCount { get; }
        /// <summary>Append every edge as a pair of node indices. Called on rebuild only.</summary>
        protected abstract void BuildEdges(List<int> a, List<int> b);
        /// <summary>Write this frame's node positions, in local space.</summary>
        protected abstract void UpdateNodes(Vector3[] into, float time);
        /// <summary>True when a structural parameter changed and the topology must be rebuilt.</summary>
        protected abstract bool TopologyDirty { get; }
        /// <summary>Optional per-edge filter, for figures that reveal subsets.</summary>
        protected virtual bool EdgeVisible(int edge) => true;
        /// <summary>Extra line for the status readout.</summary>
        protected virtual string Describe() => NodeTotal + " nodes, " + EdgeTotal + " edges";

        // ---- state -----------------------------------------------------------

        ParticleSystem system;
        GameObject instance;
        Mesh tetra;
        ParticleSystem.Particle[] particles;
        Vector3[] nodes;
        int[] edgeA, edgeB;
        int builtSamples, builtCapacity, active;
        int builtStrands, builtLevels;
        bool initialized;
        ParticleSolid builtParticleSolid;
        double time;

        readonly List<int> scratchA = new List<int>();
        readonly List<int> scratchB = new List<int>();

        /// <summary>
        /// Optional per-node value in 0..1 that biases each particle's colour and size. A subclass
        /// sizes and fills this in <see cref="UpdateNodes"/>; leaving it null means no bias.
        /// Edge travellers take the blend of their two endpoints.
        /// </summary>
        protected float[] nodeAccent;
        [Tooltip("How strongly a per-node field value shifts colour toward the gold end.")]
        [Range(0f, 1f)] public float accentColour = .85f;
        [Tooltip("How strongly a per-node field value scales particle size.")]
        [Range(0f, 2f)] public float accentSize = .7f;

        void OnEnable() { Build(); }
        void OnDisable() { Release(); }

        protected void Build()
        {
            Release();
            if (!particlePrefab || !particleMaterial) { Status = "Assign the particle prefab and material"; return; }
            if (!Application.isPlaying && !previewInEditor) return;

            builtSamples = Mathf.Clamp(particlesPerEdge, 0, 512);
            builtStrands = Mathf.Clamp(strands, 1, 12);
            builtLevels = Mathf.Clamp(fractalLevels, 0, 3);
            builtCapacity = Mathf.Clamp(maxParticles, 500, 120000);

            instance = Instantiate(particlePrefab, transform);
            instance.name = SwarmName;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            system = instance.GetComponent<ParticleSystem>();
            if (!system)
            {
                Debug.LogError("Assign polyhedronGenerator's URP wireframeParticle prefab.", this);
                Release(); return;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles = new ParticleSystem.Particle[builtCapacity];

            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = builtCapacity;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startRotation3D = true;
            var mod0 = system.emission; mod0.enabled = false;
            var mod1 = system.shape; mod1.enabled = false;
            var mod2 = system.velocityOverLifetime; mod2.enabled = false;
            var mod3 = system.noise; mod3.enabled = false;
            var mod4 = system.trails; mod4.enabled = false;
            var mod5 = system.colorOverLifetime; mod5.enabled = false;
            var mod6 = system.sizeOverLifetime; mod6.enabled = false;

            builtParticleSolid = particleSolid;
            var particleBuilder = particleSolid switch
            {
                ParticleSolid.Cube => Cube.generate(1),
                ParticleSolid.Octahedron => Octahedron.generate(1),
                ParticleSolid.Dodecahedron => Dodecahedron.generate(1),
                ParticleSolid.Icosahedron => Icosahedron.generate(1),
                _ => Tetrahedon.generate(1)
            };
            tetra = particleBuilder.build(SwarmName + " " + particleSolid);
            tetra.hideFlags = HideFlags.HideAndDontSave;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = tetra;
            renderer.sharedMaterial = particleMaterial;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 40f);

            // Topology first: a subclass may only learn its node count while building edges
            // (PolyhedronSwarm reads the generator here), and allocating before that would
            // size the node array wrong and trigger a rebuild every frame.
            scratchA.Clear(); scratchB.Clear();
            BuildEdges(scratchA, scratchB);
            edgeA = scratchA.ToArray();
            edgeB = scratchB.ToArray();
            nodes = new Vector3[Mathf.Max(NodeCount, 1)];
            if (autoDensity) SolveDensity();

            initialized = false;
            Render(0f, 1f);
            system.Pause();
        }

        void Update()
        {
            if (!Application.isPlaying && !previewInEditor) { if (instance) Release(); return; }
            bool densityChanged = autoDensity
                ? false
                : builtSamples != Mathf.Clamp(particlesPerEdge, 0, 512) ||
                  builtStrands != Mathf.Clamp(strands, 1, 12) ||
                  builtLevels != Mathf.Clamp(fractalLevels, 0, 3);
            if (!instance || densityChanged || builtParticleSolid != particleSolid ||
                builtCapacity != Mathf.Clamp(maxParticles, 500, 120000) ||
                nodes == null || nodes.Length != Mathf.Max(NodeCount, 1) || TopologyDirty)
                Build();
            if (!system) return;

            if (Application.isPlaying) time += Time.deltaTime;
            float follow = Application.isPlaying
                ? 1f - Mathf.Exp(-regroupSpeed * Mathf.Min(Time.deltaTime, .1f))
                : 1f;
            Render((float)time, follow);
        }

        /// <summary>
        /// Works the density out from the structure that was just built, so every shape lands near
        /// `targetParticles` whatever its topology.
        ///
        /// Three dials are solved in order of how well they read:
        ///   1. clusters (4^levels) give each particle volume
        ///   2. strands braid parallel runs around each edge, so a sparse structure gains body
        ///   3. travellers per edge fill in along the length
        /// A 32-edge tesseract ends up with deep clusters, many strands and a long run per edge;
        /// a 6,720-edge root system ends up with two per edge and no strands at all.
        /// </summary>
        void SolveDensity()
        {
            int nodeCount = Mathf.Max(nodes == null ? 0 : nodes.Length, 1);
            int edgeCount = edgeA == null ? 0 : edgeA.Length;
            int budget = Mathf.Clamp(targetParticles, 1000, builtCapacity);

            if (edgeCount == 0)
            {
                builtLevels = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max((float)budget / nodeCount, 1f), 4f)), 0, 3);
                builtStrands = 1;
                builtSamples = 0;
                return;
            }

            // Richer structures need no help; sparse ones get clusters and strands.
            float perEdgeIfPlain = (float)budget / edgeCount;
            if (perEdgeIfPlain <= 4f) { builtLevels = 0; builtStrands = 1; }
            else if (perEdgeIfPlain <= 20f) { builtLevels = 1; builtStrands = 1; }
            else if (perEdgeIfPlain <= 80f) { builtLevels = 1; builtStrands = 3; }
            else { builtLevels = 2; builtStrands = 5; }

            int clusters = 1 << (2 * builtLevels);
            int spentOnNodes = nodeCount * clusters;
            int left = Mathf.Max(budget - spentOnNodes, edgeCount);
            builtSamples = Mathf.Clamp(
                Mathf.RoundToInt((float)left / Mathf.Max(edgeCount * builtStrands * clusters, 1)), 1, 512);

            // Keep the mirrored inspector fields in step so the numbers shown are the real ones.
            particlesPerEdge = builtSamples;
            strands = builtStrands;
            fractalLevels = builtLevels;
        }

        void Render(float t, float follow)
        {
            if (!system || particles == null || nodes == null) return;

            UpdateNodes(nodes, t);
            ApplySwirl(nodes, t);

            float release = Mathf.Clamp01(scatter + (breatheSwarm && Application.isPlaying
                ? .1f * Mathf.Pow(.5f + .5f * Mathf.Sin(t * .43f), 6)
                : 0f));
            if (assembleOnPlay && Application.isPlaying) release = Mathf.Max(release, Mathf.Exp(-t * 1.1f));

            int levels = builtLevels;
            int children = 1 << (2 * levels);
            int index = 0;

            bool hasAccent = nodeAccent != null && nodeAccent.Length == nodes.Length;

            for (int v = 0; v < nodes.Length; v++)
                if (!EmitCluster(ref index, nodes[v], true, levels, children, t, release, follow,
                                 hasAccent ? nodeAccent[v] : 0f))
                { Finish(index); return; }

            if (builtSamples > 0 && edgeA != null)
                for (int e = 0; e < edgeA.Length; e++)
                {
                    if (!EdgeVisible(e)) continue;
                    Vector3 a = nodes[edgeA[e]], b = nodes[edgeB[e]];
                    float accentA = hasAccent ? nodeAccent[edgeA[e]] : 0f;
                    float accentB = hasAccent ? nodeAccent[edgeB[e]] : 0f;
                    Vector3 dir = b - a;
                    float length = dir.magnitude;
                    if (length < 1e-5f) continue;
                    dir /= length;

                    // A frame across the edge, for braiding the strands around it. A reference
                    // vector rather than a Frenet frame, which would be undefined on a line.
                    Vector3 reference = Mathf.Abs(dir.z) < .9f ? Vector3.forward : Vector3.right;
                    Vector3 n1 = Vector3.Normalize(Vector3.Cross(dir, reference));
                    Vector3 n2 = Vector3.Cross(dir, n1);

                    for (int sIndex = 0; sIndex < builtStrands; sIndex++)
                    for (int j = 0; j < builtSamples; j++)
                    {
                        if (index >= builtCapacity) { Finish(index); return; }
                        float phase = Mathf.Repeat((j + .5f) / builtSamples + t * edgeFlow, 1f);
                        Vector3 p = a + dir * (length * phase);
                        if (builtStrands > 1)
                        {
                            float angle = (sIndex / (float)builtStrands + phase * strandTwist) * Mathf.PI * 2f;
                            p += (n1 * Mathf.Cos(angle) + n2 * Mathf.Sin(angle)) * strandRadius * radius;
                        }
                        Set(index++, p, false, 0, t, release, follow, Mathf.Lerp(accentA, accentB, phase));
                    }
                }

            Finish(index);
        }

        /// <summary>
        /// Shared deformers, applied to every figure after its own node update. Twist and vortex
        /// are rotations about <see cref="swirlAxis"/>; inversion and spherize reshape radially.
        /// All of them are identity at 0, so a figure is unaffected until a dial is moved.
        /// </summary>
        void ApplySwirl(Vector3[] p, float t)
        {
            if (twist == 0f && vortex == 0f && inversion == 0f && spherize == 0f) return;

            Vector3 axis = swirlAxis.sqrMagnitude < 1e-6f ? Vector3.forward : swirlAxis.normalized;
            float phase = swirlSpin * t * Mathf.PI * 2f;
            float scale = Mathf.Max(radius, 1e-4f);

            for (int i = 0; i < p.Length; i++)
            {
                Vector3 v = p[i];

                if (inversion > 0f)
                {
                    float r2 = v.sqrMagnitude;
                    if (r2 > 1e-6f)
                        v = Vector3.Lerp(v, v * (scale * scale / r2), inversion);
                }

                if (spherize > 0f)
                {
                    float len = v.magnitude;
                    if (len > 1e-6f) v = Vector3.Lerp(v, v / len * scale, spherize);
                }

                if (twist != 0f || vortex != 0f)
                {
                    // Split into the component along the axis and the perpendicular part.
                    float along = Vector3.Dot(v, axis);
                    Vector3 radial = v - axis * along;
                    float rad = radial.magnitude;

                    float angle = twist * (along / scale) + phase;
                    if (vortex != 0f && rad > 1e-4f) angle += vortex * (scale / rad) * .25f;

                    if (angle != 0f && rad > 1e-6f)
                    {
                        Vector3 tangent = Vector3.Cross(axis, radial);
                        v = axis * along + radial * Mathf.Cos(angle) + tangent * Mathf.Sin(angle);
                    }
                }

                p[i] = v;
            }
        }

        /// <summary>
        /// One base position's recursive clump. The child index is read as a base-4 address; each
        /// digit offsets toward one of four corners at a geometrically shrinking scale, so the
        /// cloud is self-similar rather than jittered.
        /// </summary>
        bool EmitCluster(ref int index, Vector3 centre, bool node, int levels, int children,
                         float t, float release, float follow, float accent = 0f)
        {
            if (levels == 0)
            {
                if (index >= builtCapacity) return false;
                Set(index++, centre, node, 0, t, release, follow, accent);
                return true;
            }

            for (int c = 0; c < children; c++)
            {
                if (index >= builtCapacity) return false;
                Vector3 offset = Vector3.zero;
                float scale = clusterSpread * radius;
                int address = c;
                for (int level = 0; level < levels; level++)
                {
                    int digit = address & 3;
                    address >>= 2;
                    float sx = (digit & 1) == 0 ? -1f : 1f;
                    float sy = (digit & 2) == 0 ? -1f : 1f;
                    offset += new Vector3(sx, sy, sx * sy) * scale;
                    scale *= clusterRatio;
                }
                Set(index++, centre + offset, node, levels, t, release, follow, accent);
            }
            return true;
        }

        void Finish(int count)
        {
            active = count;
            system.SetParticles(particles, count);
            initialized = count > 0;
            Status = Describe() + " · " + count.ToString("N0") + " particles"
                   + " (" + builtSamples + "/edge x " + builtStrands + " strand"
                   + (builtStrands == 1 ? "" : "s") + ", " + (1 << (2 * builtLevels)) + "/clump)"
                   + (count >= builtCapacity ? " — AT THE CAP" : "");
        }

        void Set(int i, Vector3 target, bool node, int depth, float t, float release, float follow,
                 float accent = 0f)
        {
            float h = Mathf.Repeat(i * .61803399f, 1f);
            float azimuth = h * Mathf.PI * 2f + t * .24f;
            float y = Mathf.Repeat(i * .75487766f, 1f) * 2f - 1f;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            Vector3 free = new Vector3(Mathf.Cos(azimuth) * ring, y, Mathf.Sin(azimuth) * ring) * (radius * freeRadius);
            target = Vector3.Lerp(target, free, release);

            ref var p = ref particles[i];
            p.position = initialized ? Vector3.Lerp(p.position, target, follow) : target;
            p.startLifetime = 1000f;
            p.remainingLifetime = 1000f;
            p.velocity = Vector3.zero;
            float size = (node ? vertexParticleSize : edgeParticleSize) * Mathf.Pow(childSizeFalloff, depth);
            if (accent != 0f) size *= 1f + accent * accentSize;
            p.startSize = size;
            float mix = node ? .6f + .4f * h : h * .3f;
            p.startColor = Color.Lerp(cyan, gold, Mathf.Clamp01(mix + accent * accentColour));
            p.rotation3D = new Vector3(h * 360f + t * 13f, h * 150f - t * 19f, h * 270f + t * 7f);
            p.randomSeed = (uint)i + 1;
        }

        protected void Release()
        {
            Dispose(instance); Dispose(tetra);
            instance = null; system = null; tetra = null; particles = null;
            nodes = null; edgeA = null; edgeB = null;
            initialized = false; active = 0; builtSamples = 0; builtCapacity = 0;
            builtStrands = 1; builtLevels = 0;
        }

        static void Dispose(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        /// <summary>Edge length this frame, for subclasses that filter by length class.</summary>
        protected float EdgeLength(int edge) =>
            nodes == null || edgeA == null || edge < 0 || edge >= edgeA.Length
                ? 0f : Vector3.Distance(nodes[edgeA[edge]], nodes[edgeB[edge]]);

        [ContextMenu("Scatter")] public void Scatter() { scatter = 1f; }
        [ContextMenu("Reassemble")] public void Reassemble() { scatter = 0f; }
        [ContextMenu("Rebuild")] public void Rebuild() { Build(); }
    }
}
