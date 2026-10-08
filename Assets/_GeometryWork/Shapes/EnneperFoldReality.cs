using System.Collections.Generic;
using UnityEngine;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>Finite, self-similar arrangements of the existing Enneper fold surface.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class EnneperFoldReality : MonoBehaviour
    {
        public enum Arrangement { NestedFlower, FoldCorridor, SpiralPillars }
        public Arrangement arrangement;
        [Tooltip("Full pillars share scale, orientation and stable positions as copies are added. Their grid uses common world coordinates.")]
        public bool consistentCopies = true;
        [Header("Repeat the fold")]
        [Range(1, 12)] public int copiesPerLevel = 1;
        [Range(1, 8)] public int levels = 1;
        [Range(.35f, .95f)] public float recursiveScale = .72f;
        [Min(.1f)] public float foldSize = 3.8f;
        [Min(0)] public float spread = 3.5f;
        [Min(0)] public float depthSpacing = 4f;
        [Range(-180, 180)] public float levelTwist = 30f;
        [Range(-180, 180)] public float foldTilt = 45f;
        [HideInInspector] public float pillarStretch = 1f; // Legacy scenes only; preserve the surface proportions.
        [Header("Single fold camera framing")]
        public Camera framingCamera;
        [Min(.5f)] public float framingDistance = 7.2f;
        [Range(.5f, 2f)] public float viewFill = 1.08f;
        [Header("Turn the fold into a pillar")]
        [Tooltip("0 keeps the flower. 1 closes the return into a vertical waist with flared floor and ceiling.")]
        [Range(0, 1)] public float pillarClosure = 1f;
        [Range(.03f, .5f)] public float waistRadius = .16f;
        [Range(.3f, 2f)] public float pillarHalfHeight = .9f;
        [Range(.5f, 4f)] public float endFlare = 2.1f;
        [Range(0, .4f)] public float foldFluting = .1f;
        [Range(-180, 180)] public float pillarTwist = 25f;
        [Tooltip("What the flared floor and ceiling open out to. Square and Hexagonal make each end the pillar's lattice cell, so neighbouring pillars meet edge to edge in one continuous vault. Auto: Round for a single pillar, Square for a hall.")]
        public PillarFootprint footprint = PillarFootprint.Auto;

        [Header("Enneper folding")]
        [Range(.3f, 3f)] public float domain = 2f;
        [Range(-180, 180)] public float phaseDegrees;
        [Range(-180, 180)] public float phasePerLevel = 35f;
        [Range(-180, 180)] public float phasePerCopy = 15f;
        public bool animateFolds = true;
        [Min(1)] public float cycleSeconds = 96f;
        [Range(0, 180)] public float foldSwing = 12f;
        [Range(-30, 30)] public float rotationDegreesPerSecond;
        [Tooltip("Explicit editor preview; off by default. Phase Degrees always works when paused.")]
        public bool previewAnimation;

        [Header("Surface and shared effects")]
        public Material foldMaterial;
        public WorldGridScan worldGridScan;
        public CurvedWorldBridge bridge;
        [Range(8, 192)] public int resolution = 180;
        [Range(1, 8)] public int latticeDivisor = 3;
        public bool hideQuadDiagonals;
        [Tooltip("Total grid cells across all copies. Repetition shares the budget rather than allocating 180x180 per copy.")]
        [Range(32768, 524288)] public int surfaceCellBudget = 131072;
        [Range(0, 1)] public float wireOpacity = .3f;
        [Header("Optional particles on the first level")]
        public bool particleLayer;
        public GameObject particlePrefab;
        public Material particleMaterial;
        [Range(1000, 12000)] public int totalParticleBudget = 6000;
        public int FoldCount => folds.Count;

        GameObject generated;
        readonly List<CurvedGeometryChamber> folds = new List<CurvedGeometryChamber>();
        readonly List<PillarDeformer> deformers = new List<PillarDeformer>();
        int builtCopies, builtLevels, builtResolution, builtColumns, builtBudget;
        bool builtParticles;
        Material builtMaterial, builtParticleMaterial;
        GameObject builtPrefab;
        double elapsed, previous;

        // ---- the pillar deformation ---------------------------------------------
        //
        // Computed once per frame in Update (this component runs before the chambers), then read by
        // every vertex of every copy. The per-vertex work is EnneperPillar.Point plus one rotation.

        EnneperPillar.Shape frameShape;
        Quaternion frameUntilt = Quaternion.identity;
        float frameClosure;

        /// <summary>
        /// One per generated fold. Carries that copy's own fold phase, so the pillar's flutes turn
        /// with the Enneper surface they were closed from rather than with a global phase.
        /// </summary>
        sealed class PillarDeformer
        {
            readonly EnneperFoldReality owner;
            public float phase;
            // The chamber fills a row at a time, so the ring's u-independent terms are reused
            // until v changes. Invalidated every frame, since the shape may have changed.
            EnneperPillar.Row row;
            bool rowValid;

            public PillarDeformer(EnneperFoldReality owner) { this.owner = owner; }

            public void NewFrame() { rowValid = false; }

            public Vector3 Apply(float u, float v, Vector3 source)
            {
                float closure = owner.frameClosure;
                if (closure <= 0f) return source;
                if (!rowValid || row.v != v) { row = EnneperPillar.MakeRow(owner.frameShape, v); rowValid = true; }
                // The pillar is built upright in arrangement space; undo the fold tilt so it stays
                // upright once the chamber's own rotation is applied.
                Vector3 pillar = owner.frameUntilt * EnneperPillar.Point(owner.frameShape, row, u, phase);
                return closure >= 1f ? pillar : Vector3.LerpUnclamped(source, pillar, closure);
            }
        }

        /// <summary>True when the copies stand as a hall of identical upright pillars on a lattice.</summary>
        bool PillarHall => consistentCopies && pillarClosure >= .999f;

        PillarFootprint ResolveFootprint(int pillars) =>
            footprint != PillarFootprint.Auto ? footprint
            : PillarHall && pillars > 1 ? PillarFootprint.Square : PillarFootprint.Round;

        /// <summary>Lattice spacing: never closer than one and a half waist diameters.</summary>
        Vector2 HallSpacing(PillarFootprint resolved)
        {
            float minimum = foldSize * waistRadius * 3f;
            float across = Mathf.Max(spread, minimum);
            return resolved == PillarFootprint.Hexagonal ? new Vector2(across, across)
                 : new Vector2(across, Mathf.Max(depthSpacing, minimum));
        }

        [ContextMenu("Frame single Enneper pillar for camera")]
        public void FrameSingleFold()
        {
            var cam = framingCamera ? framingCamera : Camera.main;
            if (!cam) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObjects(new Object[] { this, transform }, "Frame Enneper fold");
#endif
            copiesPerLevel = levels = 1;
            foldTilt = 45; phaseDegrees = 0; elapsed = 0;
            rotationDegreesPerSecond = 0;
            transform.SetPositionAndRotation(cam.transform.position + cam.transform.forward * framingDistance, cam.transform.rotation);
            transform.localScale = Vector3.one;
            float halfHeight = cam.orthographic ? cam.orthographicSize : framingDistance * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            foldSize = halfHeight * cam.aspect * .75f * viewFill;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEditor.EditorUtility.SetDirty(this); UnityEditor.EditorUtility.SetDirty(transform); }
#endif
        }

        void OnEnable()
        {
            previous = Time.realtimeSinceStartupAsDouble;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += PreviewTick;
#endif
        }
#if UNITY_EDITOR
        void PreviewTick()
        {
            if (!Application.isPlaying && previewAnimation && animateFolds && isActiveAndEnabled)
            { UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); UnityEditor.SceneView.RepaintAll(); }
        }
#endif
        void Update()
        {
            int count = Mathf.Clamp(copiesPerLevel, 1, 12), depth = Mathf.Clamp(levels, 1, 8);
            int grid = Mathf.Clamp(Mathf.Min(resolution, Mathf.FloorToInt(Mathf.Sqrt((float)surfaceCellBudget / (count * depth)))), 8, 192);
            var resolved = ResolveFootprint(count * depth);
            bool lattice = resolved == PillarFootprint.Square || resolved == PillarFootprint.Hexagonal;
            // Lattice ends need a vertex on every cell corner: (columns - 1) divisible by 24.
            int columns = lattice ? EnneperPillar.SnapColumns(grid) : grid;
            if (!foldMaterial) { Release(); return; }
            if (!generated || builtCopies != count || builtLevels != depth || builtResolution != grid ||
                builtColumns != columns || builtMaterial != foldMaterial || builtParticles != particleLayer ||
                builtPrefab != particlePrefab || builtParticleMaterial != particleMaterial ||
                builtBudget != totalParticleBudget)
                Build(count, depth, grid, columns);
            double now = Time.realtimeSinceStartupAsDouble;
            if (animateFolds && (Application.isPlaying || previewAnimation)) elapsed += System.Math.Min(.1, now - previous);
            previous = now;
            float time = (float)elapsed;

            // Shared per-frame state for every vertex of every copy.
            Vector2 spacing = HallSpacing(resolved);
            frameShape = new EnneperPillar.Shape
            {
                size = foldSize, waist = waistRadius, halfHeight = pillarHalfHeight, endFlare = endFlare,
                fluting = foldFluting, twist = pillarTwist * Mathf.Deg2Rad, footprint = resolved,
                cellHalf = EnneperPillar.CellHalf(resolved, spacing)
            };
            frameUntilt = Quaternion.Inverse(Quaternion.Euler(foldTilt, 0, 0));
            frameClosure = pillarClosure;
            float swing = Mathf.Sin(time * 2f * Mathf.PI / Mathf.Max(1, cycleSeconds)) * foldSwing;
            var tilt = Quaternion.Euler(foldTilt, 0, 0);

            for (int i = 0; i < folds.Count; i++)
            {
                int level = i / count, copy = i % count;
                var chamber = folds[i];
                Vector3 position;
                Quaternion rotation;
                float scale = 1f;

                if (PillarHall)
                {
                    // A hall: identical upright pillars on the lattice, stable as copies are added
                    // (columns fill 0, +1, -1, +2, ...). With a lattice footprint each pillar's floor
                    // and ceiling are its cell, so neighbours meet edge to edge.
                    int column = copy == 0 ? 0 : (copy + 1) / 2 * (copy % 2 == 1 ? 1 : -1);
                    position = EnneperPillar.LatticeCentre(resolved, column, level, spacing);
                    rotation = tilt;
                }
                else if (count == 1 && depth == 1)
                {
                    position = Vector3.zero;
                    rotation = tilt;
                }
                else
                {
                    scale = Mathf.Pow(Mathf.Clamp(recursiveScale, .35f, .95f), level);
                    float angle = 360f * copy / count + level * levelTwist + time * rotationDegreesPerSecond;
                    if (arrangement == Arrangement.SpiralPillars)
                    {
                        // Keep the reference fold's pillar orientation; repeat the entire form.
                        position = new Vector3((copy - (count - 1) * .5f) * spread, 0, level * depthSpacing);
                        rotation = Quaternion.Euler(0, level * levelTwist, 0) * tilt;
                    }
                    else
                    {
                        float radians = angle * Mathf.Deg2Rad;
                        float radius = spread * (arrangement == Arrangement.NestedFlower ? scale : 1f);
                        float z = arrangement == Arrangement.NestedFlower ? depthSpacing * (1f - scale) : level * depthSpacing;
                        position = new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, z);
                        // Turn about the sightline *after* the tilt, so every copy is the same object
                        // rotated about Z — the crown is exactly n-fold symmetric, and closed pillars
                        // stand as spokes. (Euler(tilt, 0, angle) spun each copy about its own tilted
                        // axis instead, which broke the symmetry and leaned the pillars every which way.)
                        rotation = Quaternion.Euler(0, 0, angle) * tilt;
                    }
                }

                chamber.transform.localPosition = position;
                chamber.transform.localRotation = rotation;
                chamber.transform.localScale = Vector3.one * scale;

                // One phase per copy, shared by the Enneper surface and its pillar flutes.
                float phase = (phaseDegrees + level * phasePerLevel + copy * phasePerCopy + swing) * Mathf.Deg2Rad;
                chamber.shape.enneperPhase = phase;
                deformers[i].phase = phase;
                deformers[i].NewFrame();
                // Fully closed, the pillar ignores the Enneper point under it; skip computing it.
                chamber.deformationReplacesSurface = pillarClosure >= 1f;
                chamber.shape.enneperDomain = domain;
                chamber.radius = Mathf.Max(.1f, foldSize);
                chamber.wireOpacity = wireOpacity;
                chamber.latticeDivisor = latticeDivisor;
                chamber.hideQuadDiagonals = hideQuadDiagonals;
                chamber.bridge = bridge; chamber.worldGridScan = worldGridScan;
            }
        }

        void Build(int count, int depth, int grid, int columns)
        {
            Release();
            generated = new GameObject("Generated Enneper folds") { hideFlags = HideFlags.HideAndDontSave };
            generated.SetActive(false); generated.transform.SetParent(transform, false);
            builtCopies = count; builtLevels = depth; builtResolution = grid; builtColumns = columns; builtMaterial = foldMaterial;
            builtParticles = particleLayer; builtPrefab = particlePrefab; builtParticleMaterial = particleMaterial; builtBudget = totalParticleBudget;
            for (int level = 0; level < depth; level++) for (int copy = 0; copy < count; copy++)
            {
                var go = new GameObject("Enneper " + level + ":" + copy) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(generated.transform, false);
                var c = go.AddComponent<CurvedGeometryChamber>();
                c.mode = CurvedGeometryChamber.Mode.Manifold; c.from = c.to = ManifoldSurface.Enneper;
                c.autoCycle = false; c.animate = false; c.sides = columns; c.rings = grid;
                var deformer = new PillarDeformer(this);
                deformers.Add(deformer);
                c.surfaceDeformation = deformer.Apply;
                c.chamberMaterial = foldMaterial; c.hideQuadDiagonals = hideQuadDiagonals;
                c.latticeDivisor = latticeDivisor;
                c.bridge = bridge; c.worldGridScan = worldGridScan; folds.Add(c);
                if (particleLayer && level == 0 && particlePrefab && particleMaterial)
                {
                    var swarm = go.AddComponent<WireParticleSwarm>(); swarm.chamber = c;
                    swarm.particlePrefab = particlePrefab; swarm.particleMaterial = particleMaterial;
                    int quota = Mathf.Max(8, totalParticleBudget / count);
                    swarm.nodesU = Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(quota)), 4, 24);
                    swarm.nodesV = Mathf.Clamp(quota / swarm.nodesU, 2, 24); swarm.fractalLevels = 0;
                    swarm.travellersPerLine = 0; swarm.maxParticles = Mathf.Max(1000, quota);
                    swarm.nodeSize = .018f; swarm.assembleOnPlay = false;
                }
            }
            generated.SetActive(true);
        }
        void Release()
        {
            if (generated)
            {
                generated.SetActive(false);
                if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
            }
            generated = null; folds.Clear(); deformers.Clear();
        }
        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= PreviewTick;
#endif
            Release();
        }
    }
}
