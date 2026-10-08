using UnityEngine;
using polyhedronGenerator.scripts.solids;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The Dekeract swarm's rendering, riding any <see cref="IWireGeometry"/> instead of a 10-cube.
    /// Point it at a FractalRingChamber or a CurvedManifoldChamber and every shape in either gets
    /// the same particle layer.
    ///
    /// Same mechanisms as DekeractTetraSwarm, because they are what makes that effect land: one
    /// ParticleSystem in mesh render mode with a generated tetrahedron, node particles on the grid
    /// and travellers sliding across it with a travelling phase, scatter/reassemble to a Fibonacci
    /// sphere, exponential follow smoothing, the cyan-to-gold palette and per-particle tumble.
    ///
    /// The addition is <see cref="fractalLevels"/>: each base sample spawns a recursive cluster of
    /// children through a 4-map IFS in the surface tangent frame, so the particles themselves are
    /// self-similar clumps rather than an even dusting.
    ///
    /// Runs in LateUpdate because MonoBehaviour order is undefined and the chambers fill their
    /// geometry in Update.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WireParticleSwarm : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The chamber to ride. Leave empty to use any wire geometry on this object.")]
        public CurvedGeometryChamber chamber;
        [Tooltip("A combo chamber to ride instead. Takes precedence over `chamber` when set.")]
        public ManifoldComboChamber comboChamber;

        [Header("Particles")]
        [Tooltip("polyhedronGenerator/prefabs/urp/wireframeParticle.prefab")]
        public GameObject particlePrefab;
        [Tooltip("Combo/Dekeract_Particles.mat")]
        public Material particleMaterial;
        [Tooltip("Node particles across the surface, along u.")]
        [Range(4, 192)] public int nodesU = 32;
        [Tooltip("Node particles across the surface, along v.")]
        [Range(2, 192)] public int nodesV = 24;
        [Tooltip("Travellers sliding along each v line.")]
        [Range(0, 48)] public int travellersPerLine = 6;
        [Tooltip("Hard ceiling. The Dekeract runs 11,264, which is the proven budget.")]
        [Range(1000, 60000)] public int maxParticles = 18000;

        [Header("Fractal clustering")]
        [Tooltip("Recursive subdivision of each particle into a self-similar clump. Costs 4^levels particles per base sample.")]
        [Range(0, 3)] public int fractalLevels = 1;
        [Tooltip("Size of the first clump, as a fraction of a grid cell.")]
        [Range(0, 2)] public float clusterSpread = .7f;
        [Tooltip("How much smaller each level is. Below 0.5 leaves visible gaps, which is what makes the recursion read.")]
        [Range(.1f, .9f)] public float clusterRatio = .42f;
        [Tooltip("Shrinks child particles with depth so the clumps have a hierarchy.")]
        [Range(.2f, 1f)] public float childSizeFalloff = .62f;

        [Header("Size and colour")]
        [Range(.002f, .12f)] public float nodeSize = .03f;
        [Range(.001f, .06f)] public float travellerSize = .012f;
        public Color cyan = new Color(.12f, .85f, 1f, .5f);
        public Color gold = new Color(1f, .55f, .13f, .8f);

        [Header("Motion")]
        [Range(-2, 2)] public float flow = .18f;
        [Range(0, 1)] public float scatter;
        public bool assembleOnPlay = true;
        [Range(1, 20)] public float regroupSpeed = 5f;
        [Range(.1f, 12f)] public float freeRadius = 3f;
        public bool previewInEditor = true;

        public int ParticleCount => active;
        public string Status { get; private set; } = "No source assigned";

        ParticleSystem system;
        GameObject instance;
        Mesh tetra;
        ParticleSystem.Particle[] particles;
        int active, builtCapacity;
        bool initialized;
        double time;

        IWireGeometry Source
        {
            get
            {
                if (comboChamber) return comboChamber;
                if (chamber) return chamber;
                // Any wire geometry on this object will do, so new chamber kinds need no change here.
                foreach (var c in GetComponents<MonoBehaviour>())
                    if (c != this && c is IWireGeometry geo) return geo;
                return null;
            }
        }

        void OnEnable() { Build(); }
        void OnDisable() { Release(); }

        void Build()
        {
            Release();
            if (!particlePrefab || !particleMaterial) { Status = "Assign the particle prefab and material"; return; }
            if (!Application.isPlaying && !previewInEditor) return;

            instance = Instantiate(particlePrefab, transform);
            instance.name = "Wire swarm particles";
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
            builtCapacity = Mathf.Clamp(maxParticles, 1000, 60000);
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

            tetra = Tetrahedon.generate(1).build("Wire swarm tetrahedron");
            tetra.hideFlags = HideFlags.HideAndDontSave;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = tetra;
            renderer.sharedMaterial = particleMaterial;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 200f);

            initialized = false;
            Render(0f, 1f);
            system.Pause();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying && !previewInEditor) { if (instance) Release(); return; }
            if (!instance || builtCapacity != Mathf.Clamp(maxParticles, 1000, 60000)) Build();
            if (!system) return;

            if (Application.isPlaying) time += Time.deltaTime;
            float follow = Application.isPlaying
                ? 1f - Mathf.Exp(-regroupSpeed * Mathf.Min(Time.deltaTime, .1f))
                : 1f;
            Render((float)time, follow);
        }

        void Render(float t, float follow)
        {
            if (!system || particles == null) return;
            var source = Source;
            if (source == null || !source.IsBuilt) { Publish(0); Status = "Waiting for the chamber to build"; return; }

            float release = Mathf.Clamp01(scatter);
            if (assembleOnPlay && Application.isPlaying) release = Mathf.Max(release, Mathf.Exp(-t * 1.1f));

            int levels = Mathf.Clamp(fractalLevels, 0, 3);
            int children = 1 << (2 * levels);          // 4^levels
            float cellU = 1f / Mathf.Max(nodesU, 1);
            float cellV = 1f / Mathf.Max(nodesV, 1);
            int index = 0;

            // Node particles: a grid over the surface, each one a recursive clump.
            for (int j = 0; j < nodesV; j++)
            for (int i = 0; i < nodesU; i++)
            {
                float u = (float)i / nodesU, v = nodesV > 1 ? (float)j / (nodesV - 1) : 0f;
                if (!EmitCluster(source, ref index, u, v, cellU, cellV, levels, children, true, t, release, follow))
                    { Finish(index); return; }
            }

            // Travellers sliding along v, so the surface reads as flowing.
            if (travellersPerLine > 0)
            for (int i = 0; i < nodesU; i++)
            for (int k = 0; k < travellersPerLine; k++)
            {
                float dir = (i & 1) == 0 ? 1f : -1f;
                float u = (float)i / nodesU;
                float v = Mathf.Repeat((k + .5f) / travellersPerLine + t * flow * dir, 1f);
                if (index >= builtCapacity) { Finish(index); return; }
                Set(index++, source.SampleGrid(u, v), false, 0, t, release, follow);
            }

            Finish(index);
        }

        /// <summary>
        /// Writes one base sample's recursive cluster. The child index is read as a base-4 address
        /// over `levels` digits; each digit offsets by one of four corner directions in the surface
        /// tangent frame at a geometrically shrinking scale. That is a 4-map IFS, so the cloud is
        /// genuinely self-similar rather than jittered.
        /// </summary>
        bool EmitCluster(IWireGeometry source, ref int index, float u, float v, float cellU, float cellV,
                         int levels, int children, bool node, float t, float release, float follow)
        {
            Vector3 centre = source.SampleGrid(u, v);
            if (levels == 0)
            {
                if (index >= builtCapacity) return false;
                Set(index++, centre, node, 0, t, release, follow);
                return true;
            }

            // Tangent frame by finite difference, so clusters lie along the surface.
            Vector3 du = source.SampleGrid(u + cellU * .5f, v) - centre;
            Vector3 dv = source.SampleGrid(u, Mathf.Clamp01(v + cellV * .5f)) - centre;

            for (int c = 0; c < children; c++)
            {
                if (index >= builtCapacity) return false;
                Vector3 offset = Vector3.zero;
                float scale = clusterSpread;
                int address = c;
                for (int level = 0; level < levels; level++)
                {
                    int digit = address & 3;
                    address >>= 2;
                    float su = (digit & 1) == 0 ? -1f : 1f;
                    float sv = (digit & 2) == 0 ? -1f : 1f;
                    offset += (du * su + dv * sv) * scale;
                    scale *= clusterRatio;
                }
                int depth = levels;
                Set(index++, centre + offset, node, depth, t, release, follow);
            }
            return true;
        }

        void Finish(int count)
        {
            Publish(count);
            Status = count + " particles" + (fractalLevels > 0 ? ", " + (1 << (2 * Mathf.Clamp(fractalLevels, 0, 3))) + " per clump" : "")
                   + (count >= builtCapacity ? " (at the cap — raise maxParticles or lower density)" : "");
        }

        void Publish(int count)
        {
            active = count;
            system.SetParticles(particles, count);
            initialized = count > 0;
        }

        void Set(int i, Vector3 target, bool node, int depth, float t, float release, float follow)
        {
            // Golden-ratio hash gives an even spread of colour and tumble without storing anything.
            float h = Mathf.Repeat(i * .61803399f, 1f);
            float azimuth = h * Mathf.PI * 2f + t * .24f;
            float y = Mathf.Repeat(i * .75487766f, 1f) * 2f - 1f;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            Vector3 free = new Vector3(Mathf.Cos(azimuth) * ring, y, Mathf.Sin(azimuth) * ring) * freeRadius;
            target = Vector3.Lerp(target, free, release);

            ref var p = ref particles[i];
            p.position = initialized ? Vector3.Lerp(p.position, target, follow) : target;
            p.startLifetime = 1000f;
            p.remainingLifetime = 1000f;
            p.velocity = Vector3.zero;
            float size = node ? nodeSize : travellerSize;
            p.startSize = size * Mathf.Pow(childSizeFalloff, depth);
            p.startColor = Color.Lerp(cyan, gold, node ? .6f + .4f * h : h * .3f);
            p.rotation3D = new Vector3(h * 360f + t * 13f, h * 150f - t * 19f, h * 270f + t * 7f);
            p.randomSeed = (uint)i + 1;
        }

        void Release()
        {
            Dispose(instance); Dispose(tetra);
            instance = null; system = null; tetra = null; particles = null;
            initialized = false; active = 0; builtCapacity = 0;
        }

        static void Dispose(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        [ContextMenu("Scatter")] public void Scatter() { scatter = 1f; }
        [ContextMenu("Reassemble")] public void Reassemble() { scatter = 0f; }
    }
}
