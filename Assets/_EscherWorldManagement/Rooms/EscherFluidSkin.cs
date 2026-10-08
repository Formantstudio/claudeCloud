using UnityEngine;
using Fluxy;

namespace PsychedelicLab.EscherWorld
{
    /// <summary>
    /// Feeds an <see cref="EscherRoom"/>'s extracted mesh into a FluXY container, so fluid and
    /// smoke run across the surface.
    ///
    /// This exists for one reason: `EscherRoom` builds its mesh at runtime from the implicit
    /// field, so nothing can drag it into `FluxyContainer.customMesh` in the inspector. Everything
    /// else in `AIPlans/FluxxyFillPlan.md` is inspector work; this is the only script needed.
    ///
    /// What it does, in order:
    ///   1. forces the container to Custom shape and hands it the room's mesh
    ///   2. sizes the container from the room's extent
    ///   3. sets the boundaries from whether the field actually tiles
    ///   4. calls UpdateContainerShape(), without which the sim keeps running on the old mesh
    ///
    /// **The boundary rule is the important part.** The TPMS family (Neovius, gyroid, Schwarz P/D,
    /// Lidinoid, Split-P) is triply periodic, so Periodic boundaries make the fluid wrap the way
    /// the surface does and the structure reads as endless. A bounded field such as the Barth
    /// sextic must use Open instead, or fluid leaving one side reappears on the other across empty
    /// space. <see cref="EscherFields.IsPeriodic"/> already knows which is which, so this is read
    /// from the field rather than left to be set by hand and got wrong.
    ///
    /// FluXY is a 2.5D solver (its own `FluxySolver.cs:13` says so), so this is fluid *on* the
    /// surface. It cannot fill the labyrinth interior — that is what the SDF particle layer is
    /// for.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class EscherFluidSkin : MonoBehaviour
    {
        [Header("Links")]
        [Tooltip("The room whose extracted mesh the fluid runs on. Defaults to one on this object or a parent.")]
        public EscherRoom room;
        [Tooltip("The FluXY container to drive. Defaults to one on this object or a child.")]
        public FluxyContainer container;

        [Header("Behaviour")]
        [Tooltip("Keeps checking for a new mesh. Needed while the room is animating, since each re-extraction makes a new mesh the container has not seen.")]
        public bool followRebuilds = true;
        [Tooltip("Seconds between checks. The container rebuild is not free, so do not run this every frame on a changing mesh.")]
        [Range(.05f, 4f)] public float checkInterval = .25f;
        [Tooltip("Overrides the container's boundaries from whether the field tiles. Turn off only to set them by hand.")]
        public bool driveBoundaries = true;
        [Tooltip("Scales the container's size relative to the room's sampled box. 1 matches it exactly.")]
        [Range(.25f, 4f)] public float sizeScale = 1f;

        public string Status { get; private set; } = "Not linked";

        Mesh applied;
        float nextCheck;

        void OnEnable()
        {
            Resolve();
            Apply(true);
        }

        void Update()
        {
            if (!Resolve()) return;

            if (!followRebuilds)
            {
                // Still catch the very first mesh, which may not have existed on enable.
                if (applied == null) Apply(false);
                return;
            }

            float now = Application.isPlaying ? Time.time : 0f;
            if (Application.isPlaying && now < nextCheck) return;
            nextCheck = now + Mathf.Max(checkInterval, .05f);
            Apply(false);
        }

        bool Resolve()
        {
            if (!room) room = GetComponentInParent<EscherRoom>() ?? GetComponent<EscherRoom>();
            if (!container) container = GetComponent<FluxyContainer>() ?? GetComponentInChildren<FluxyContainer>();

            if (!room) { Status = "No EscherRoom found"; return false; }
            if (!container) { Status = "No FluxyContainer found"; return false; }
            return true;
        }

        /// <summary>
        /// Hands the current mesh over, if it has changed. `force` re-applies even when the mesh
        /// is the same object, for the initial setup.
        /// </summary>
        void Apply(bool force)
        {
            var mesh = room.Mesh;
            if (!mesh)
            {
                Status = "Room has not extracted a mesh yet";
                return;
            }

            bool changed = force || !ReferenceEquals(mesh, applied);

            // Size and boundaries are cheap, so they are kept in step every check.
            container.size = Vector3.one * (room.extent * 2f * sizeScale);

            if (driveBoundaries)
            {
                // A field that tiles wraps; one that does not must not, or fluid leaving one side
                // reappears on the other with nothing in between.
                var type = EscherFields.IsPeriodic(room.field.field)
                    ? FluxyContainer.BoundaryConditions.BoundaryType.Periodic
                    : FluxyContainer.BoundaryConditions.BoundaryType.Open;

                if (container.boundaries.horizontalBoundary != type ||
                    container.boundaries.verticalBoundary != type)
                {
                    var b = container.boundaries;
                    b.horizontalBoundary = type;
                    b.verticalBoundary = type;
                    container.boundaries = b;
                }
            }

            if (!changed)
            {
                Status = Describe();
                return;
            }

            container.containerShape = FluxyContainer.ContainerShape.Custom;
            container.customMesh = mesh;
            // Without this the container keeps simulating on whatever mesh it had before.
            container.UpdateContainerShape();

            applied = mesh;
            Status = Describe();
        }

        string Describe()
        {
            bool periodic = EscherFields.IsPeriodic(room.field.field);
            return string.Format("{0} · {1} verts · {2} boundaries · size {3:0.#}{4}",
                room.field.field,
                applied ? applied.vertexCount.ToString("N0") : "0",
                periodic ? "Periodic" : "Open",
                container.size.x,
                EscherFields.IsPeriodic(room.field.field) ? "" : "  (bounded field, no wrap)");
        }

        [ContextMenu("Re-apply mesh now")]
        void ContextApply()
        {
            if (Resolve()) Apply(true);
        }
    }
}
