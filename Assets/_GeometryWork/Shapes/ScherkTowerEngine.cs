using System.Collections.Generic;
using UnityEngine;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Scherk tower arrangements, built the same way <see cref="EnneperFoldReality"/> is: a pool of
    /// generated <see cref="CurvedGeometryChamber"/> objects whose vertices come from this component's
    /// <c>surfaceDeformation</c> hook. Nothing outside this file changes — no new enum entries, no new
    /// extractor, no edits to the chamber.
    ///
    /// **Why Scherk rather than Enneper for a room of pillars.** The Enneper engine has to *arrange*
    /// copies: the surface is a single finite flower, so a colonnade is a placement decision, and the
    /// repetition is only as coherent as the spacing numbers. Scherk's second surface is **singly
    /// periodic by construction** — it repeats along its axis with period 2*pi forever — so a tower is
    /// one surface sampled over a longer parameter range, not N copies stacked. Pillar spacing comes
    /// out of the mathematics instead of out of the inspector.
    ///
    /// **The surface.** Scherk's second surface (the saddle tower) is the zero set of
    ///
    ///     sinh(x) * sinh(y) = sin(z)
    ///
    /// This engine does not extract that implicitly; it parametrises it exactly. Write c = sin(z).
    /// The level curve at height z is sinh(x)*sinh(y) = c, which is solved in closed form by
    ///
    ///     sinh(x) =  sqrt(|c|) * e^w          sinh(y) = sign(c) * sqrt(|c|) * e^-w
    ///
    /// because the two factors then multiply to exactly c for every w. So
    ///
    ///     x(w, z) =            asinh( sqrt(|c|) * e^w  )
    ///     y(w, z) = sign(c) *  asinh( sqrt(|c|) * e^-w )
    ///     z(w, z) = z
    ///
    /// with w running across the saddle and z running up the tower. Every point this produces is on
    /// the real surface: it is an exact chart, not an approximation of one.
    ///
    /// **What the chart does and does not cover — stated plainly, because it is visible.** At each
    /// half period (z = 0, pi, 2pi, ...) c passes through zero, both coordinates collapse to the
    /// origin, and the chart pinches to a point. The true surface at those heights is the full pair of
    /// lines x = 0 and y = 0, so the chart traces one saddle lobe per half period and pinches at the
    /// waists between them. That reads as a column of saddle lobes joined at narrow waists, which is
    /// the pillar look wanted here, but it is a genuine sub-sheet of Scherk's surface rather than all
    /// of it. <see cref="branches"/> 2 adds the x-negative chart, which is the other half of each
    /// lobe pair and makes the four-wing cross section read correctly.
    ///
    /// Everything other than the chart — stacking in a grid, twisting, truncating the wings — is a
    /// placement or deformation choice and is labelled as such below. Only <see cref="wingSpan"/> and
    /// <see cref="periods"/> stay inside the exact surface.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class ScherkTowerEngine : MonoBehaviour
    {
        /// <summary>How the towers are placed. The surface is the same in all three.</summary>
        public enum Arrangement
        {
            /// <summary>A square grid of towers: the hall of pillars.</summary>
            PillarHall,
            /// <summary>A single row, for a corridor with a clear sightline down it.</summary>
            Colonnade,
            /// <summary>A ring of towers around the origin, for an orbiting camera.</summary>
            Rotunda,
            /// <summary>One tower, centred. The reference view.</summary>
            SingleTower
        }

        public Arrangement arrangement = Arrangement.PillarHall;

        [Header("The tower")]
        [Tooltip("Half periods of sin(z) sampled along the axis. Each one is a saddle lobe, so this is literally how many stacked lobes the pillar has. 6 reads as a tall fluted column.")]
        [Range(1, 24)] public int periods = 6;
        [Tooltip("How far out along w the wings are sampled. The surface extends forever; this is where it is truncated. Large values flatten towards the asymptotic planes.")]
        [Range(.5f, 5f)] public float wingSpan = 2.4f;
        [Tooltip("1 samples only the x-positive chart. 2 adds the x-negative chart, which completes the four-wing cross section. Costs a second chamber per tower.")]
        [Range(1, 2)] public int branches = 2;
        [Tooltip("World height of one half period. Total tower height is this times Periods.")]
        [Min(.05f)] public float periodHeight = 1.1f;
        [Tooltip("World scale of the cross section.")]
        [Min(.05f)] public float towerRadius = 1.3f;
        [Tooltip("Hall and Colonnade: how tall the shared walls between cells rise before the tanh compression flattens them off. This is the pillar height.")]
        [Range(.2f, 8f)] public float wallHeight = 2.2f;

        [Header("Deformations (not minimal surfaces)")]
        [Tooltip("Degrees of rotation about the axis per half period. 0 is the exact Scherk surface; nonzero twists it into a helical column, which is a designed deformation and no longer minimal.")]
        [Range(-180, 180)] public float twistPerPeriod;
        [Tooltip("Narrows the waists between lobes. 0 leaves the chart's own pinch; 1 holds the waist open at a visible thickness so the column reads as continuous stone rather than beads on a string.")]
        [Range(0, 1)] public float waistHold = .35f;
        [Tooltip("Tapers the cross section towards the top, as a column entasis. 1 is no taper.")]
        [Range(.2f, 2f)] public float taper = 1f;
        [Tooltip("Blends from the chamber's own base manifold into the tower. 1 is the full tower; below 1 morphs out of whatever Base Surface is set to.")]
        [Range(0, 1)] public float towerForm = 1f;
        [Tooltip("The manifold the blend starts from when Tower Form is below 1.")]
        public ManifoldSurface baseSurface = ManifoldSurface.Cylinder;

        [Header("Placement")]
        [Tooltip("Towers per side for Pillar Hall, towers in the row for Colonnade, towers in the ring for Rotunda.")]
        [Range(1, 8)] public int columns = 3;
        [Tooltip("Centre-to-centre spacing. Below about 2x Tower Radius the wings interpenetrate.")]
        [Min(.1f)] public float spacing = 4f;
        [Tooltip("Rotunda radius.")]
        [Min(.1f)] public float ringRadius = 7f;
        [Tooltip("Degrees of yaw added per tower, so neighbours do not present identical silhouettes.")]
        [Range(-180, 180)] public float yawPerTower = 45f;
        [Tooltip("Raises alternate towers by this fraction of a half period, breaking the flat band the aligned waists otherwise make across the hall.")]
        [Range(0, 1)] public float staggerPhase = .5f;

        [Header("Camera framing")]
        public Camera framingCamera;
        [Min(.5f)] public float framingDistance = 9f;
        [Range(.5f, 2f)] public float viewFill = 1.05f;

        [Header("Animation")]
        public bool animateTower;
        [Min(1)] public float cycleSeconds = 72f;
        [Tooltip("Degrees of twist swing over a cycle.")]
        [Range(0, 180)] public float twistSwing = 20f;
        [Range(-30, 30)] public float rotationDegreesPerSecond;
        [Tooltip("Explicit editor preview, off by default. The sliders always work when paused.")]
        public bool previewAnimation;

        [Header("Surface and shared effects")]
        public Material towerMaterial;
        public WorldGridScan worldGridScan;
        public CurvedWorldBridge bridge;
        [Tooltip("Grid across the wings. The w direction carries the saddle curvature, so it wants detail.")]
        [Range(8, 192)] public int resolution = 120;
        [Range(1, 8)] public int latticeDivisor = 3;
        public bool hideQuadDiagonals;
        [Tooltip("Total grid cells across every tower and branch, so adding towers shares the budget instead of multiplying it.")]
        [Range(32768, 524288)] public int surfaceCellBudget = 131072;
        [Range(0, 1)] public float wireOpacity = .4f;

        [Header("Optional particles on the first tower")]
        public bool particleLayer;
        public GameObject particlePrefab;
        public Material particleMaterial;
        [Range(1000, 12000)] public int totalParticleBudget = 6000;

        public int TowerCount => towers.Count;
        public string Status { get; private set; } = "Not built";

        GameObject generated;
        readonly List<CurvedGeometryChamber> towers = new List<CurvedGeometryChamber>();
        int builtTowers, builtBranches, builtResolution, builtRings, builtBudget;
        bool builtParticles;
        Material builtMaterial, builtParticleMaterial;
        GameObject builtPrefab;
        double elapsed, previous;

        // Live twist, including the animated swing. Read by the deformation hook.
        float ActiveTwist => twistPerPeriod + (animateTower
            ? Mathf.Sin((float)elapsed * 2f * Mathf.PI / Mathf.Max(1, cycleSeconds)) * twistSwing
            : 0f);

        static float Asinh(float x) => Mathf.Log(x + Mathf.Sqrt(x * x + 1f));

        /// <summary>
        /// True when the arrangement is generated from the field's own double periodicity rather than
        /// by repeating one chart. A hall and a colonnade are periodic, so they are one surface; a
        /// rotunda is a ring, which no periodicity produces, so that one really is copies.
        /// </summary>
        bool IsPeriodicArrangement =>
            arrangement == Arrangement.PillarHall || arrangement == Arrangement.Colonnade;

        /// <summary>
        /// **Scherk's first surface — the doubly periodic one.** This is how an array of towers is
        /// supposed to be made, and it is not N copies of anything:
        ///
        ///     z = ln |cos x / cos y|
        ///
        /// is periodic with period pi in **both** x and y. So sampling N periods across and M along
        /// yields N*M saddle cells that are joined to each other along their own walls, as a single
        /// connected surface with a single continuous parameterisation. Every grid line therefore runs
        /// from one tower straight into the next, because the surface was never cut apart to begin
        /// with — there is nothing to glue.
        ///
        /// Each cell is a saddle whose walls run up at x = pi/2 + k*pi (where cos x -> 0, z -> +inf)
        /// and down at y = pi/2 + k*pi (z -> -inf). Those walls are what neighbouring cells share.
        ///
        /// The infinities are compressed with tanh rather than clamped: `wallHeight * tanh(z /
        /// wallHeight)` is smooth and monotone, approaches the wall height asymptotically, and leaves
        /// the saddle untouched near z = 0. A hard clamp would put a flat plateau and a visible crease
        /// along the top of every wall.
        /// </summary>
        Vector3 ScherkHallPoint(float u, float v, Vector3 source)
        {
            if (towerForm <= 0f) return source;

            int across = Mathf.Max(columns, 1);
            // A colonnade is the same surface, one cell deep: a row of towers, not a field of them.
            int along = arrangement == Arrangement.Colonnade ? 1 : across;

            // Half a cell of inset keeps the sampled range centred on cell interiors, so the walls
            // land on cell boundaries where neighbours meet.
            float x = (Mathf.Clamp01(u) * across - across * .5f) * Mathf.PI;
            float y = (Mathf.Clamp01(v) * along - along * .5f) * Mathf.PI;

            float cx = Mathf.Max(Mathf.Abs(Mathf.Cos(x)), 1e-6f);
            float cy = Mathf.Max(Mathf.Abs(Mathf.Cos(y)), 1e-6f);
            float height = Mathf.Max(wallHeight, .05f);
            float z = height * (float)System.Math.Tanh(Mathf.Log(cx / cy) / height);

            // Twist about the vertical, applied to the whole lattice so it stays one surface.
            float angle = ActiveTwist * Mathf.Deg2Rad * (z / height);
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            float sx = x * towerRadius, sy = y * towerRadius;

            var point = new Vector3(sx * ca - sy * sa,
                                    z * periodHeight,
                                    sx * sa + sy * ca);

            return towerForm >= 1f ? point : Vector3.Lerp(source, point, towerForm);
        }

        /// <summary>
        /// The exact singly periodic Scherk chart (the saddle tower). <paramref name="u"/> runs across
        /// the saddle (w), <paramref name="v"/> runs up the tower (z). <paramref name="sign"/> picks
        /// the x-positive or x-negative branch. Used for the single tower and the rotunda; the hall
        /// and colonnade use the doubly periodic surface instead.
        /// </summary>
        Vector3 ScherkPoint(float u, float v, int sign, Vector3 source)
        {
            if (towerForm <= 0f) return source;
            if (IsPeriodicArrangement) return ScherkHallPoint(u, v, source);

            // w across the saddle. Symmetric about 0, so the lobe is centred.
            float w = (Mathf.Clamp01(u) * 2f - 1f) * Mathf.Max(wingSpan, .01f);
            // z up the tower, in half periods of sin(z).
            float halfPeriods = Mathf.Max(periods, 1);
            float z = Mathf.Clamp01(v) * halfPeriods * Mathf.PI;

            float c = Mathf.Sin(z);
            // waistHold floors |c| so the chart does not collapse to a point at each half period.
            // This is a deformation: it moves those rings off the true surface by exactly the floor.
            float magnitude = Mathf.Lerp(Mathf.Abs(c), Mathf.Max(Mathf.Abs(c), .18f), waistHold);
            float branchSign = c < 0f ? -1f : 1f;
            float root = Mathf.Sqrt(magnitude);

            // sinh(x) = root * e^w and sinh(y) = branchSign * root * e^-w multiply to exactly c,
            // which is the defining relation. Clamped because e^w overflows asinh's input well
            // before w reaches the slider maximum on a wide span.
            float ex = Mathf.Exp(Mathf.Clamp(w, -12f, 12f));
            float x = Asinh(root * ex) * sign;
            float y = Asinh(root / ex) * branchSign * sign;

            // Twist and taper: placement-style deformations applied after the chart, so the exact
            // surface is recoverable by setting both to neutral.
            float up = Mathf.Clamp01(v);
            float angle = ActiveTwist * Mathf.Deg2Rad * up * halfPeriods;
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            float scale = towerRadius * Mathf.Lerp(1f, taper, up);

            var point = new Vector3((x * ca - y * sa) * scale,
                                    (z / Mathf.PI - halfPeriods * .5f) * periodHeight,
                                    (x * sa + y * ca) * scale);

            return towerForm >= 1f ? point : Vector3.Lerp(source, point, towerForm);
        }

        [ContextMenu("Frame single Scherk tower for camera")]
        public void FrameSingleTower()
        {
            var cam = framingCamera ? framingCamera : Camera.main;
            if (!cam) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RecordObjects(new Object[] { this, transform }, "Frame Scherk tower");
#endif
            arrangement = Arrangement.SingleTower;
            columns = 1;
            elapsed = 0;
            rotationDegreesPerSecond = 0;
            transform.SetPositionAndRotation(cam.transform.position + cam.transform.forward * framingDistance,
                                             Quaternion.identity);

            // Fill the frame vertically: a tower is tall and narrow, so height is the binding
            // dimension, unlike the Enneper flower where width was.
            float halfHeight = cam.orthographic
                ? cam.orthographicSize
                : framingDistance * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            periodHeight = halfHeight * 2f * viewFill / Mathf.Max(periods, 1);
            towerRadius = halfHeight * .45f * viewFill;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(transform);
            }
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
            if (!Application.isPlaying && previewAnimation && animateTower && isActiveAndEnabled)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
        }
#endif

        int TowerTotal()
        {
            switch (arrangement)
            {
                // The hall and the colonnade are a single doubly periodic surface covering every
                // cell, so there is exactly one object however many towers are showing. Only the
                // rotunda is genuinely a repetition, because a ring is not a periodicity.
                case Arrangement.PillarHall:
                case Arrangement.Colonnade:
                case Arrangement.SingleTower: return 1;
                default:                      return Mathf.Clamp(columns, 1, 8);
            }
        }

        void Update()
        {
            int count = TowerTotal();
            int branchCount = Mathf.Clamp(branches, 1, 2);
            int charts = count * branchCount;
            // Share one cell budget across every chart, so a 3x3 hall does not cost nine full grids.
            int cells = Mathf.Max(surfaceCellBudget / Mathf.Max(charts, 1), 256);

            // Not a square grid. A tall tower needs its rings spent along the axis: each lobe has to
            // get its own band of them, or a 12-lobe column resolves at three rings per lobe and the
            // saddles turn into facets. So the budget is spent as a rectangle whose aspect follows the
            // lobe count rather than as sqrt(cells) on both axes.
            // Hall: square, because the lattice is square in (x, y). Colonnade: wide and shallow.
            // Tower: tall, because the lobes stack along v.
            float aspect = arrangement == Arrangement.PillarHall ? 1f
                         : arrangement == Arrangement.Colonnade ? 1f / Mathf.Max(columns, 1)
                         : Mathf.Clamp(Mathf.Max(periods, 1) * .5f, 1f, 6f);
            aspect = Mathf.Clamp(aspect, 1f / 8f, 6f);
            int sideGrid = Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(cells / aspect)), 8, resolution);
            int ringGrid = Mathf.Clamp(Mathf.FloorToInt(sideGrid * aspect), 8, 384);

            if (!towerMaterial) { Release(); Status = "No tower material"; return; }

            if (!generated || builtTowers != count || builtBranches != branchCount ||
                builtResolution != sideGrid || builtRings != ringGrid || builtMaterial != towerMaterial ||
                builtParticles != particleLayer || builtPrefab != particlePrefab ||
                builtParticleMaterial != particleMaterial || builtBudget != totalParticleBudget)
                Build(count, branchCount, sideGrid, ringGrid);

            double now = Time.realtimeSinceStartupAsDouble;
            if (animateTower && (Application.isPlaying || previewAnimation))
                elapsed += System.Math.Min(.1, now - previous);
            previous = now;
            float time = (float)elapsed;

            for (int i = 0; i < towers.Count; i++)
            {
                int tower = i / branchCount;
                var chamber = towers[i];

                chamber.transform.localPosition = Place(tower, count);
                chamber.transform.localRotation = Quaternion.Euler(0,
                    (IsPeriodicArrangement ? 0f : tower * yawPerTower)
                    + time * rotationDegreesPerSecond, 0);
                chamber.transform.localScale = Vector3.one;

                chamber.from = chamber.to = baseSurface;
                chamber.radius = Mathf.Max(2f, towerRadius * 2f);
                chamber.length = Mathf.Max(4f, periodHeight * Mathf.Max(periods, 1) * 2f);
                chamber.wireOpacity = wireOpacity;
                chamber.latticeDivisor = latticeDivisor;
                chamber.hideQuadDiagonals = hideQuadDiagonals;
                chamber.bridge = bridge;
                chamber.worldGridScan = worldGridScan;
            }

            if (IsPeriodicArrangement)
            {
                int across = Mathf.Max(columns, 1);
                int towerCells = arrangement == Arrangement.Colonnade ? across : across * across;
                Status = string.Format("{0} · {1} cells as ONE doubly periodic surface · {2}x{3} grid"
                    + " ({4:N0} cells) · lines continuous across every tower",
                    arrangement, towerCells, builtResolution, builtRings, builtResolution * builtRings);
            }
            else
            {
                Status = string.Format("{0} · {1} tower{2} x {3} branch{4} · {5} lobes · {6}x{7} grid"
                    + " ({8:N0} cells each){9}", arrangement, count, count == 1 ? "" : "s",
                    branchCount, branchCount == 1 ? "" : "es", Mathf.Max(periods, 1),
                    builtResolution, builtRings, builtResolution * builtRings,
                    arrangement == Arrangement.Rotunda ? " · copies, so lines break between towers" : "");
            }
        }

        /// <summary>Tower placement. Pure layout; the surface is identical at every slot.</summary>
        Vector3 Place(int tower, int count)
        {
            // Alternate towers ride half a lobe higher, so the waists do not line up into one flat
            // band across the whole hall.
            // The periodic arrangements are one surface centred on the engine: the tower lattice is
            // inside the parameterisation, so there is nothing to place.
            if (IsPeriodicArrangement || arrangement == Arrangement.SingleTower) return Vector3.zero;

            // Rotunda only. A ring of towers is a real repetition, so it really does transform copies,
            // and the grid lines genuinely do break between them. Stated rather than hidden.
            float lift = (tower % 2) * staggerPhase * periodHeight;
            float a = 2f * Mathf.PI * tower / Mathf.Max(count, 1);
            return new Vector3(Mathf.Cos(a) * ringRadius, lift, Mathf.Sin(a) * ringRadius);
        }

        void Build(int count, int branchCount, int sideGrid, int ringGrid)
        {
            Release();
            generated = new GameObject("Generated Scherk towers") { hideFlags = HideFlags.HideAndDontSave };
            generated.SetActive(false);
            generated.transform.SetParent(transform, false);

            builtTowers = count; builtBranches = branchCount;
            builtResolution = sideGrid; builtRings = ringGrid;
            builtMaterial = towerMaterial; builtParticles = particleLayer;
            builtPrefab = particlePrefab; builtParticleMaterial = particleMaterial;
            builtBudget = totalParticleBudget;

            for (int tower = 0; tower < count; tower++)
            for (int branch = 0; branch < branchCount; branch++)
            {
                var go = new GameObject("Scherk " + tower + ":" + branch) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(generated.transform, false);

                var c = go.AddComponent<CurvedGeometryChamber>();
                c.mode = CurvedGeometryChamber.Mode.Manifold;
                c.from = c.to = baseSurface;
                c.autoCycle = false;
                c.animate = false;
                // u runs across the saddle, v up the tower. The ring count is already scaled
                // by lobe count in Update, so each lobe gets its own band of rings.
                c.sides = sideGrid;
                c.rings = ringGrid;

                int sign = branch == 0 ? 1 : -1;
                c.surfaceDeformation = (u, v, source) => ScherkPoint(u, v, sign, source);

                c.chamberMaterial = towerMaterial;
                c.hideQuadDiagonals = hideQuadDiagonals;
                c.latticeDivisor = latticeDivisor;
                c.bridge = bridge;
                c.worldGridScan = worldGridScan;
                towers.Add(c);

                if (particleLayer && tower == 0 && particlePrefab && particleMaterial)
                {
                    var swarm = go.AddComponent<WireParticleSwarm>();
                    swarm.chamber = c;
                    swarm.particlePrefab = particlePrefab;
                    swarm.particleMaterial = particleMaterial;
                    int quota = Mathf.Max(8, totalParticleBudget / branchCount);
                    swarm.nodesU = Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(quota)), 4, 24);
                    swarm.nodesV = Mathf.Clamp(quota / Mathf.Max(swarm.nodesU, 1), 2, 24);
                    swarm.fractalLevels = 0;
                    swarm.travellersPerLine = 0;
                    swarm.maxParticles = Mathf.Max(1000, quota);
                    swarm.nodeSize = .018f;
                    swarm.assembleOnPlay = false;
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
            generated = null;
            towers.Clear();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= PreviewTick;
#endif
            Release();
        }

        [ContextMenu("Exact Scherk surface (neutral deformations)")]
        void UseExact()
        {
            twistPerPeriod = 0f;
            waistHold = 0f;
            taper = 1f;
            towerForm = 1f;
            animateTower = false;
        }
    }
}
