using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// A 4-D bubble: a sphere of space inside which geometry is lifted into the fourth dimension,
    /// rotated there, and projected back. Outside the bubble nothing moves at all.
    ///
    /// That locality is the whole point. The infinite scroller's grid, rails and tunnel stay
    /// exactly as they are; activating the hyperworld opens bubbles inside them rather than bending
    /// the world. A point's displacement falls smoothly to zero at the bubble edge, so there is no
    /// seam where the effect stops.
    ///
    /// How a point is lifted: the fourth coordinate is the height of a hypersphere cap over the
    /// bubble, `w = sqrt(R^2 - d^2)`, so the centre of the bubble sticks furthest out into W and the
    /// rim sits flat in the 3-D hyperplane. Rotating that cap in the XW or YW plane and dropping W
    /// again slides the centre sideways on screen while the rim stays put — which is what reads as
    /// something emerging from a direction that was not there before.
    /// </summary>
    [ExecuteAlways]
    public sealed class Hyper4DBubble : MonoBehaviour
    {
        /// <summary>Every enabled bubble. Shapes read this through <see cref="Hyper4DField"/>.</summary>
        public static readonly List<Hyper4DBubble> All = new List<Hyper4DBubble>();

        [Header("Bubble")]
        [Min(.01f)] public float radius = 3f;
        [Tooltip("Fraction of the radius that is fully inside. Below this the lift is at full strength; between here and the rim it eases out.")]
        [Range(0f, .95f)] public float core = .35f;
        [Tooltip("How far out into W the centre of the bubble reaches, as a multiple of the radius.")]
        [Range(0f, 3f)] public float bulge = 1f;

        [Header("Opening")]
        [Tooltip("How open this bubble is. 0 = closed and the geometry inside is untouched.")]
        [Range(0f, 1f)] public float openness;
        [Tooltip("Multiplies openness by the shared 4-D activation layer, so cues open every bubble at once.")]
        public bool followActivation = true;
        [Tooltip("Seconds to open and close when the openness target changes.")]
        [Range(.01f, 6f)] public float ease = .6f;
        [Tooltip("Target openness while the layer is active. Lets one bubble stay shallower than another.")]
        [Range(0f, 1f)] public float openTo = 1f;

        [Header("Axis")]
        [Tooltip("The 4-D axis rig this bubble rotates with. Empty = the scene's current one.")]
        public Hyperspace4DAxis axis;

        public float Current { get; private set; }

        float target;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            var rig = axis ? axis : Hyperspace4DAxis.Current;
            target = openTo * (followActivation && rig ? rig.Activation : 1f) * openness;
            if (Application.isPlaying)
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(ease, .01f));
                Current = Mathf.Lerp(Current, target, k);
            }
            else Current = target;
        }

        /// <summary>
        /// Weight of this bubble at a world point: 1 inside the core, easing to 0 at the rim.
        /// </summary>
        public float Weight(Vector3 world)
        {
            if (Current <= .001f) return 0f;
            float d = Vector3.Distance(world, transform.position);
            if (d >= radius) return 0f;
            float inner = radius * core;
            float t = d <= inner ? 1f : 1f - Mathf.SmoothStep(inner, radius, d);
            return t * Current;
        }

        /// <summary>Displacement this bubble adds at a world point. Zero outside.</summary>
        public Vector3 Displace(Vector3 world)
        {
            float weight = Weight(world);
            if (weight <= .001f) return Vector3.zero;

            var rig = axis ? axis : Hyperspace4DAxis.Current;
            if (!rig) return Vector3.zero;

            Vector3 local = world - transform.position;
            float d = local.magnitude;

            // Lift onto a hypersphere cap: furthest into W at the centre, flat at the rim.
            float w = Mathf.Sqrt(Mathf.Max(radius * radius - d * d, 0f)) * bulge;

            // Rotate the cap in 4-D, then drop W. Dropping W rather than projecting keeps the
            // displacement linear in the rotation, which is what makes the motion readable.
            Vector4 lifted = rig.RotateWOnly(new Vector4(local.x, local.y, local.z, w));
            Vector3 moved = new Vector3(lifted.x, lifted.y, lifted.z);

            return (moved - local) * weight;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.62f, .42f, .98f, .5f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.color = new Color(.18f, .85f, .9f, .35f);
            Gizmos.DrawWireSphere(transform.position, radius * core);
        }
    }

    /// <summary>
    /// The shared bubble field. Shapes call <see cref="Apply"/> after building their own geometry,
    /// so their 3-D form is produced first and the bubbles only add a local displacement on top.
    /// With no bubbles open this is a no-op, which is why the scroller is unaffected at rest.
    /// </summary>
    public static class Hyper4DField
    {
        /// <summary>Adds every open bubble's displacement to a world-space point.</summary>
        public static Vector3 Apply(Vector3 world)
        {
            var list = Hyper4DBubble.All;
            if (list.Count == 0) return world;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b) sum += b.Displace(world);
            }
            return world + sum;
        }

        /// <summary>
        /// Same, for a point in some object's local space: converts out, displaces, converts back,
        /// so a chamber can keep working in its own space.
        /// </summary>
        public static Vector3 ApplyLocal(Transform space, Vector3 local)
        {
            if (Hyper4DBubble.All.Count == 0 || !space) return local;
            Vector3 world = space.TransformPoint(local);
            Vector3 moved = Apply(world);
            // Vector3 is a struct, so this has to be a value comparison, not a reference one.
            return moved == world ? local : space.InverseTransformPoint(moved);
        }

        /// <summary>Strongest bubble weight at a point, for tinting or fading with the effect.</summary>
        public static float Weight(Vector3 world)
        {
            var list = Hyper4DBubble.All;
            float max = 0f;
            for (int i = 0; i < list.Count; i++)
                if (list[i]) max = Mathf.Max(max, list[i].Weight(world));
            return max;
        }

        public static bool AnyOpen
        {
            get
            {
                var list = Hyper4DBubble.All;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] && list[i].Current > .001f) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// The 4-D manipulation ring: a ring of bubbles around the tunnel axis, which is the thing you
    /// actually steer. The ring sits across the view, so from a camera looking straight down the
    /// tunnel the bubbles open to the sides and above and below rather than toward or away — the
    /// displacement lands across the screen, where it can be seen.
    ///
    /// It builds and owns its bubbles, so there is nothing to place by hand.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class Hyper4DRing : MonoBehaviour
    {
        [Header("Ring")]
        [Range(1, 24)] public int count = 6;
        [Tooltip("Distance from the tunnel axis to each bubble centre.")]
        [Min(.1f)] public float ringRadius = 4.5f;
        [Tooltip("Radius of each bubble.")]
        [Min(.1f)] public float bubbleRadius = 2.4f;
        [Range(0f, .95f)] public float core = .35f;
        [Range(0f, 3f)] public float bulge = 1f;
        [Tooltip("Axis the ring encircles. Z is straight down the tunnel.")]
        public Axis3 tunnelAxis = Axis3.Z;

        [Header("Travel")]
        [Tooltip("Degrees per second the ring rolls about the tunnel axis.")]
        [Range(-180f, 180f)] public float spin = 14f;
        [Tooltip("Rings repeat along the tunnel at this spacing. 0 = a single ring.")]
        [Min(0f)] public float spacing;
        [Range(1, 8)] public int repeats = 1;
        [Tooltip("Units per second the rings slide along the tunnel, for the scroller.")]
        [Range(-40f, 40f)] public float scrollSpeed;
        [Tooltip("Length the rings wrap over while scrolling. 0 = no wrap.")]
        [Min(0f)] public float wrapLength = 40f;

        [Header("Opening")]
        [Tooltip("Opens the bubbles one after another rather than all at once.")]
        public bool stagger = true;
        [Range(0f, 2f)] public float staggerSeconds = .45f;
        public Hyperspace4DAxis axis;

        public string Status { get; private set; } = "";

        readonly List<Hyper4DBubble> owned = new List<Hyper4DBubble>();
        GameObject root;
        int builtCount, builtRepeats;
        double elapsed;

        void OnEnable() { Build(); }
        void OnDisable() { Release(); }

        void Update()
        {
            if (!axis) axis = Hyperspace4DAxis.Current;
            int wanted = Mathf.Clamp(count, 1, 24) * Mathf.Clamp(repeats, 1, 8);
            if (root == null || owned.Count != wanted ||
                builtCount != count || builtRepeats != repeats) Build();
            if (root == null) return;

            if (Application.isPlaying) elapsed += Time.deltaTime;
            float t = (float)elapsed;

            Vector3 axisDir = Dir(tunnelAxis);
            Vector3 u = Perp(tunnelAxis), v = Vector3.Cross(axisDir, u);
            float roll = spin * t * Mathf.Deg2Rad;
            float slide = scrollSpeed * t;

            int n = Mathf.Clamp(count, 1, 24);
            int reps = Mathf.Clamp(repeats, 1, 8);
            int open = 0;

            for (int r = 0; r < reps; r++)
            for (int i = 0; i < n; i++)
            {
                int index = r * n + i;
                if (index >= owned.Count) break;
                var bubble = owned[index];
                if (!bubble) continue;

                float angle = roll + Mathf.PI * 2f * i / n;
                float along = r * spacing + slide;
                if (wrapLength > 0f) along = Mathf.Repeat(along, wrapLength) - wrapLength * .5f;

                bubble.transform.localPosition =
                    (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * ringRadius + axisDir * along;
                bubble.radius = bubbleRadius;
                bubble.core = core;
                bubble.bulge = bulge;
                bubble.axis = axis;
                // Stagger so the ring opens as a travelling wave rather than a single pop.
                bubble.openness = stagger
                    ? Mathf.Clamp01(1f - Mathf.Abs(Mathf.Repeat(t / Mathf.Max(staggerSeconds * n, .01f) - (float)i / n, 1f) * 2f - 1f) * .4f)
                    : 1f;
                if (bubble.Current > .001f) open++;
            }

            Status = owned.Count + " bubbles, " + open + " open · act "
                   + (axis ? axis.Activation.ToString("0.00") : "no axis");
        }

        void Build()
        {
            Release();
            builtCount = count; builtRepeats = repeats;

            root = new GameObject("Generated 4D bubbles") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.SetParent(transform, false);

            int total = Mathf.Clamp(count, 1, 24) * Mathf.Clamp(repeats, 1, 8);
            for (int i = 0; i < total; i++)
            {
                var go = new GameObject("Bubble " + i) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(root.transform, false);
                var bubble = go.AddComponent<Hyper4DBubble>();
                bubble.radius = bubbleRadius;
                bubble.core = core;
                bubble.bulge = bulge;
                bubble.axis = axis;
                owned.Add(bubble);
            }
        }

        void Release()
        {
            owned.Clear();
            if (!root) return;
            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
            root = null;
        }

        static Vector3 Dir(Axis3 a) =>
            a == Axis3.X ? Vector3.right : a == Axis3.Y ? Vector3.up : Vector3.forward;

        static Vector3 Perp(Axis3 a) =>
            a == Axis3.X ? Vector3.up : a == Axis3.Y ? Vector3.forward : Vector3.right;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.62f, .42f, .98f, .35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Vector3 axisDir = Dir(tunnelAxis), u = Perp(tunnelAxis);
            Vector3 v = Vector3.Cross(axisDir, u);
            const int steps = 48;
            Vector3 prev = u * ringRadius;
            for (int i = 1; i <= steps; i++)
            {
                float a = Mathf.PI * 2f * i / steps;
                Vector3 next = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * ringRadius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
