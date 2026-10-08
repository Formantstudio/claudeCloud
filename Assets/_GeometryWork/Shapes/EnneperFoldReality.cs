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
        int builtCopies, builtLevels, builtResolution, builtBudget;
        bool builtParticles;
        Material builtMaterial, builtParticleMaterial;
        GameObject builtPrefab;
        double elapsed, previous;

        Vector3 ClosePillar(float u, float v, Vector3 source)
        {
            if (pillarClosure <= 0) return source;
            float t = Mathf.Clamp01(v) * 2f - 1f;
            float theta = Mathf.Clamp01(u) * Mathf.PI * 2f;
            float twist = pillarTwist * Mathf.Deg2Rad * t;
            float phase = phaseDegrees * Mathf.Deg2Rad + (animateFolds ? Mathf.Sin((float)elapsed * 2f * Mathf.PI / Mathf.Max(1, cycleSeconds)) * foldSwing * Mathf.Deg2Rad : 0);
            // Both flares meet at one continuous waist; vertical slope vanishes at the ends.
            float radial = foldSize * (waistRadius + endFlare * Mathf.Pow(Mathf.Abs(t), 4f));
            radial *= 1f + foldFluting * Mathf.Cos(4f * theta + phase + twist);
            var pillar = new Vector3(radial * Mathf.Cos(theta + twist),
                foldSize * pillarHalfHeight * Mathf.Sin(t * Mathf.PI * .5f), radial * Mathf.Sin(theta + twist));
            pillar = Quaternion.Inverse(Quaternion.Euler(foldTilt, 0, 0)) * pillar;
            return Vector3.Lerp(source, pillar, pillarClosure);
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
            if (!foldMaterial) { Release(); return; }
            if (!generated || builtCopies != count || builtLevels != depth || builtResolution != grid ||
                builtMaterial != foldMaterial || builtParticles != particleLayer || builtPrefab != particlePrefab ||
                builtParticleMaterial != particleMaterial || builtBudget != totalParticleBudget)
                Build(count, depth, grid);
            double now = Time.realtimeSinceStartupAsDouble;
            if (animateFolds && (Application.isPlaying || previewAnimation)) elapsed += System.Math.Min(.1, now - previous);
            previous = now;
            float time = (float)elapsed;
            for (int i = 0; i < folds.Count; i++)
            {
                int level = i / count, copy = i % count;
                float scale = Mathf.Pow(Mathf.Clamp(recursiveScale, .35f, .95f), level);
                float angle = 360f * copy / count + level * levelTwist + time * rotationDegreesPerSecond;
                float radians = angle * Mathf.Deg2Rad;
                var chamber = folds[i];
                float radius = spread * (arrangement == Arrangement.NestedFlower ? scale : 1f);
                float z = arrangement == Arrangement.NestedFlower ? depthSpacing * (1f - scale) : level * depthSpacing;
                chamber.transform.localPosition = new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, z);
                chamber.transform.localRotation = Quaternion.Euler(foldTilt, 0, angle);
                chamber.transform.localScale = Vector3.one * scale;
                if (arrangement == Arrangement.SpiralPillars)
                {
                    // Keep the reference fold's pillar orientation; repeat the entire form.
                    chamber.transform.localPosition = new Vector3((copy - (count - 1) * .5f) * spread, 0, level * depthSpacing);
                    chamber.transform.localRotation = Quaternion.Euler(foldTilt, level * levelTwist, 0);
                    chamber.transform.localScale = Vector3.one * scale;
                }
                if (count == 1 && depth == 1)
                {
                    chamber.transform.localPosition = Vector3.zero;
                    chamber.transform.localRotation = Quaternion.Euler(foldTilt, 0, 0);
                    chamber.transform.localScale = Vector3.one;
                }
                if (consistentCopies && pillarClosure >= .999f)
                {
                    int column = copy == 0 ? 0 : (copy + 1) / 2 * (copy % 2 == 1 ? 1 : -1);
                    float minimumSpacing = foldSize * waistRadius * 3f;
                    chamber.transform.localPosition = new Vector3(column * Mathf.Max(spread, minimumSpacing), 0, level * Mathf.Max(depthSpacing, minimumSpacing));
                    chamber.transform.localRotation = Quaternion.Euler(foldTilt, 0, 0);
                    chamber.transform.localScale = Vector3.one;
                }
                float phase = phaseDegrees + level * phasePerLevel + copy * phasePerCopy;
                chamber.shape.enneperPhase = (phase + Mathf.Sin(time * 2f * Mathf.PI / Mathf.Max(1, cycleSeconds)) * foldSwing) * Mathf.Deg2Rad;
                chamber.shape.enneperDomain = domain;
                chamber.radius = Mathf.Max(.1f, foldSize);
                chamber.wireOpacity = wireOpacity;
                chamber.latticeDivisor = latticeDivisor;
                chamber.hideQuadDiagonals = hideQuadDiagonals;
                chamber.bridge = bridge; chamber.worldGridScan = worldGridScan;
            }
        }
        void Build(int count, int depth, int grid)
        {
            Release();
            generated = new GameObject("Generated Enneper folds") { hideFlags = HideFlags.HideAndDontSave };
            generated.SetActive(false); generated.transform.SetParent(transform, false);
            builtCopies = count; builtLevels = depth; builtResolution = grid; builtMaterial = foldMaterial;
            builtParticles = particleLayer; builtPrefab = particlePrefab; builtParticleMaterial = particleMaterial; builtBudget = totalParticleBudget;
            for (int level = 0; level < depth; level++) for (int copy = 0; copy < count; copy++)
            {
                var go = new GameObject("Enneper " + level + ":" + copy) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(generated.transform, false);
                var c = go.AddComponent<CurvedGeometryChamber>();
                c.mode = CurvedGeometryChamber.Mode.Manifold; c.from = c.to = ManifoldSurface.Enneper;
                c.autoCycle = false; c.animate = false; c.sides = c.rings = grid;
                c.surfaceDeformation = ClosePillar;
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
            generated = null; folds.Clear();
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
