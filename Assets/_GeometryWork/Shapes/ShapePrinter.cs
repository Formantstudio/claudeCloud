using System.Collections.Generic;
using UnityEngine;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Drop this on a GameObject in the hierarchy and it prints shapes as real child objects.
    ///
    /// This replaces the `Tools > Geometry FX > ...` menu surface for printing shapes. The setup
    /// logic is the same logic that lived in the editor scripts — per-arrangement tuning values,
    /// scene-reference discovery, budget sharing — it just lives on a component now, so picking a
    /// shape and ticking Print does it, with no menu walk and nothing to install.
    ///
    /// "Prints" means it creates an ordinary child GameObject that persists in the scene and can be
    /// selected, moved, duplicated and saved. It is deliberately *not* HideFlags.HideAndDontSave:
    /// the engines themselves generate transient geometry under their own hidden child, but the
    /// printed object is yours to keep.
    ///
    /// Reuses whatever is already in the scene: on print it finds the WorldGridScan and
    /// CurvedWorldBridge from the scene if they are not set here, rather than making new ones.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ShapePrinter : MonoBehaviour
    {
        public enum Family
        {
            ScherkTower,
            Manifold,
            Implicit,
            Swarm
        }

        /// <summary>
        /// The node/edge swarms, each its own component deriving from NodeEdgeSwarmBase. Listed as an
        /// enum rather than a type field so it shows as a dropdown in the inspector.
        /// </summary>
        public enum SwarmKind
        {
            MetatronCube,
            Tesseract4D,
            Penteract5D,
            Hexeract6D,
            Hepteract7D,
            Octeract8D,
            Enneract9D,
            Dekeract10D,
            Polyhedron
        }

        [Header("What to print")]
        public Family family = Family.ScherkTower;

        [Tooltip("Scherk only: which arrangement. Each one comes with its own tuned lobe count, spacing and branch count.")]
        public ScherkTowerEngine.Arrangement scherkArrangement = ScherkTowerEngine.Arrangement.SingleTower;

        [Tooltip("Manifold only: which surface from the 30 in ManifoldSurfaces.cs.")]
        public ManifoldSurface manifold = ManifoldSurface.Enneper;

        [Tooltip("Implicit only: which field from the 13 in ImplicitShapes.cs.")]
        public ImplicitShape implicitShape = ImplicitShape.Gyroid;

        [Tooltip("Swarm only: which node/edge swarm.")]
        public SwarmKind swarm = SwarmKind.Tesseract4D;

        [Header("Print")]
        [Tooltip("Tick to print. It unticks itself, so one tick is one shape.")]
        public bool printNow;
        [Tooltip("Where the printed object goes, relative to this one.")]
        public Vector3 printOffset;
        [Tooltip("Steps each successive print along +X by this much, so a run of prints lines up instead of stacking.")]
        public float printStride = 6f;
        [Tooltip("Prints disabled, so it does not change what is on screen until you tick it on.")]
        public bool printDisabled;

        [Header("Shared references (found in the scene if left empty)")]
        public Material wireMaterial;
        public WorldGridScan worldGridScan;
        public CurvedWorldBridge bridge;
        public GameObject particlePrefab;
        public Material particleMaterial;

        [Header("Detail")]
        [Range(8, 192)] public int resolution = 140;
        [Range(1, 8)] public int latticeDivisor = 3;
        [Range(32768, 524288)] public int surfaceCellBudget = 131072;
        [Range(0, 1)] public float wireOpacity = .6f;

        [Tooltip("Everything this component has printed, newest last. Clear Printed removes them.")]
        public List<GameObject> printed = new List<GameObject>();

        public string Status { get; private set; } = "Ready";

        void Update()
        {
            if (!printNow) return;
            printNow = false;   // one tick, one shape
            Print();
        }

        /// <summary>Prints the currently selected shape as a child object.</summary>
        [ContextMenu("Print shape")]
        public void Print()
        {
            Resolve();
            if (!wireMaterial)
            {
                Status = "No wire material: assign one, or put a shape with one in the scene";
                Debug.LogWarning("ShapePrinter: no wire material to print with.", this);
                return;
            }

            var go = new GameObject(NameFor());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = printOffset + new Vector3(printed.Count * printStride, 0, 0);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Print shape");
#endif

            switch (family)
            {
                case Family.ScherkTower: BuildScherk(go); break;
                case Family.Manifold:    BuildManifold(go); break;
                case Family.Swarm:       BuildSwarm(go); break;
                default:                 BuildImplicit(go); break;
            }

            if (printDisabled) go.SetActive(false);
            printed.Add(go);
            Status = "Printed " + go.name + " (" + printed.Count + " total)";

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.Selection.activeGameObject = go;
            }
#endif
        }

        [ContextMenu("Clear printed")]
        public void ClearPrinted()
        {
            for (int i = printed.Count - 1; i >= 0; i--)
            {
                if (!printed[i]) continue;
                if (Application.isPlaying) Destroy(printed[i]); else DestroyImmediate(printed[i]);
            }
            printed.Clear();
            Status = "Cleared";
        }

        string NameFor()
        {
            switch (family)
            {
                case Family.ScherkTower: return "Scherk Tower - " + scherkArrangement;
                case Family.Manifold:    return "Manifold - " + manifold;
                case Family.Swarm:       return swarm + " Swarm";
                default:                 return "Implicit - " + implicitShape;
            }
        }

        /// <summary>
        /// Fills in anything left empty from what is already in the scene, rather than creating a
        /// second WorldGridScan or bridge. The material falls back to whatever an existing chamber
        /// is already using, which is the one known to render here.
        /// </summary>
        void Resolve()
        {
            if (!worldGridScan)
            {
#if UNITY_2023_1_OR_NEWER
                worldGridScan = FindAnyObjectByType<WorldGridScan>(FindObjectsInactive.Include);
#else
                worldGridScan = FindObjectOfType<WorldGridScan>(true);
#endif
            }
            if (!bridge)
            {
#if UNITY_2023_1_OR_NEWER
                bridge = FindAnyObjectByType<CurvedWorldBridge>(FindObjectsInactive.Include);
#else
                bridge = FindObjectOfType<CurvedWorldBridge>(true);
#endif
            }
            if (!wireMaterial)
            {
#if UNITY_2023_1_OR_NEWER
                var donor = FindAnyObjectByType<CurvedGeometryChamber>(FindObjectsInactive.Include);
#else
                var donor = FindObjectOfType<CurvedGeometryChamber>(true);
#endif
                if (donor) wireMaterial = donor.chamberMaterial;
            }
        }

        void BuildScherk(GameObject go)
        {
            var engine = go.AddComponent<ScherkTowerEngine>();
            engine.arrangement = scherkArrangement;
            engine.towerMaterial = wireMaterial;
            engine.worldGridScan = worldGridScan;
            engine.bridge = bridge;
            engine.particlePrefab = particlePrefab;
            engine.particleMaterial = particleMaterial;
            engine.resolution = resolution;
            engine.latticeDivisor = latticeDivisor;
            engine.surfaceCellBudget = surfaceCellBudget;
            engine.wireOpacity = wireOpacity;
            TuneScherk(engine, scherkArrangement);
        }

        /// <summary>
        /// Per-arrangement starting values. A hall wants fewer, shorter lobes than a single reference
        /// tower, because the cell budget is shared across every tower *and* branch — nine tall towers
        /// at the single-tower settings resolve each one too coarsely to read.
        /// </summary>
        public static void TuneScherk(ScherkTowerEngine engine, ScherkTowerEngine.Arrangement arrangement)
        {
            switch (arrangement)
            {
                case ScherkTowerEngine.Arrangement.SingleTower:
                    engine.periods = 6;
                    engine.branches = 2;
                    engine.wingSpan = 2.4f;
                    engine.waistHold = .35f;
                    engine.towerRadius = 1.2f;
                    engine.periodHeight = 1f;
                    engine.columns = 1;
                    engine.wallHeight = 2.2f;
                    break;

                case ScherkTowerEngine.Arrangement.Colonnade:
                    engine.periods = 5;
                    engine.branches = 2;
                    engine.wingSpan = 2.2f;
                    engine.waistHold = .35f;
                    // Colonnade is ONE doubly periodic surface, 5 cells wide: the row of towers is
                    // in the parameterisation, so spacing and yaw do not apply and are left alone.
                    engine.towerRadius = .9f;
                    engine.columns = 5;
                    engine.wallHeight = 2.4f;
                    engine.periodHeight = 1f;
                    break;

                case ScherkTowerEngine.Arrangement.Rotunda:
                    engine.periods = 5;
                    engine.branches = 2;
                    engine.wingSpan = 2.2f;
                    engine.waistHold = .35f;
                    engine.towerRadius = .9f;
                    engine.columns = 8;
                    engine.ringRadius = 6.5f;
                    engine.yawPerTower = 45f;
                    engine.staggerPhase = 0f;   // a ring reads better with the waists aligned
                    break;

                default: // PillarHall
                    engine.periods = 4;
                    // One branch in a hall: nine towers times two charts is eighteen grids out of one
                    // budget, and the wings matter less than the silhouette at hall distances.
                    engine.branches = 1;
                    engine.wingSpan = 2f;
                    engine.waistHold = .4f;
                    // Hall is ONE doubly periodic surface covering 3x3 cells. branches/wingSpan/
                    // waistHold belong to the singly periodic tower chart and are ignored here.
                    engine.towerRadius = .8f;
                    engine.columns = 3;
                    engine.wallHeight = 2.6f;
                    engine.periodHeight = 1f;
                    break;
            }
        }

        void BuildManifold(GameObject go)
        {
            var chamber = go.AddComponent<CurvedGeometryChamber>();
            chamber.mode = CurvedGeometryChamber.Mode.Manifold;
            chamber.from = chamber.to = manifold;
            chamber.autoCycle = false;
            chamber.animate = false;
            // Square-ish grid out of the budget, capped by the resolution slider.
            int grid = Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(surfaceCellBudget)), 8, resolution);
            chamber.sides = grid;
            chamber.rings = grid;
            chamber.chamberMaterial = wireMaterial;
            chamber.latticeDivisor = latticeDivisor;
            chamber.wireOpacity = wireOpacity;
            chamber.worldGridScan = worldGridScan;
            chamber.bridge = bridge;
        }

        /// <summary>
        /// Same two assignments the editor path made: prefab and material. Everything else on
        /// NodeEdgeSwarmBase has a working default, and autoDensity solves the per-edge count from the
        /// structure's own edge count — which is why these must not be given a hand-set
        /// particlesPerEdge, or sparse figures come out starved.
        /// </summary>
        void BuildSwarm(GameObject go)
        {
            NodeEdgeSwarmBase s;
            switch (swarm)
            {
                case SwarmKind.MetatronCube: s = go.AddComponent<MetatronCubeSwarm>(); break;
                case SwarmKind.Penteract5D:  s = go.AddComponent<PenteractSwarm>();    break;
                case SwarmKind.Hexeract6D:   s = go.AddComponent<HexeractSwarm>();     break;
                case SwarmKind.Hepteract7D:  s = go.AddComponent<HepteractSwarm>();    break;
                case SwarmKind.Octeract8D:   s = go.AddComponent<OcteractSwarm>();     break;
                case SwarmKind.Enneract9D:   s = go.AddComponent<EnneractSwarm>();     break;
                case SwarmKind.Dekeract10D:  s = go.AddComponent<DekeractSwarm>();     break;
                case SwarmKind.Polyhedron:   s = go.AddComponent<PolyhedronSwarm>();   break;
                default:                     s = go.AddComponent<TesseractSwarm>();    break;
            }
            s.particlePrefab = particlePrefab;
            s.particleMaterial = particleMaterial;
        }

        void BuildImplicit(GameObject go)
        {
            var chamber = go.AddComponent<ImplicitSurfaceChamber>();
            chamber.field.shape = implicitShape;
            chamber.wireMaterial = wireMaterial;
            chamber.latticeDivisor = latticeDivisor;
            chamber.wireOpacity = wireOpacity;
            chamber.worldGridScan = worldGridScan;
            chamber.bridge = bridge;
            // The extractor cost is res^3, not res^2, so it gets its own much lower ceiling.
            chamber.resolution = Mathf.Clamp(resolution / 3, 12, 96);
            // Mandelbox at negative scale is the rooms-and-corridors interior; the TPMS family wants
            // a frequency that puts a few cells in the box rather than one.
            if (implicitShape == ImplicitShape.Mandelbox) chamber.field.boxScale = -1.75f;
            if (Implicits.IsPeriodic(implicitShape)) chamber.field.frequency = 2f;
        }
    }
}
