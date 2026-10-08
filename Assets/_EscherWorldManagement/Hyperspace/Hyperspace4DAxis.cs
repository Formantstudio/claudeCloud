using System;
using UnityEngine;
using PsychedelicLab.Control;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The 4-D camera axis: one shared orientation in 4-space that every 4-D shape in the scene can
    /// read, plus the randomisation source for it.
    ///
    /// SO(4) has **six** rotation planes, not three: XY, XZ, XW, YZ, YW, ZW. Three of them (XY, XZ,
    /// YZ) are ordinary 3-D rotations; the other three tilt space into W and are what actually makes
    /// a duocylinder or a 5-cell turn inside out. `Stage4DSystem` already carries a 3-value `tilt`
    /// (the W tilts) and publishes `_Cam4DRot` for the slicing shaders; this is the full six-plane
    /// rig for the geometry side, published separately as `_Hyper4DRot` so the two do not fight.
    ///
    /// Equal rates in two orthogonal planes (XY and ZW, or XZ and YW) give an **isoclinic**
    /// rotation, which is the one that reads as the shape turning through itself rather than just
    /// spinning. That pairing is worth reaching for deliberately.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class Hyperspace4DAxis : MonoBehaviour
    {
        public enum Plane { XY, XZ, XW, YZ, YW, ZW }

        /// <summary>One rotation plane: where it is, how fast it turns, how wide it randomises.</summary>
        [Serializable]
        public sealed class PlaneAxis
        {
            [Tooltip("Current angle in this plane, in degrees.")]
            [Range(-360f, 360f)] public float degrees;
            [Tooltip("Degrees per second. Use this for a continuous spiral.")]
            [Range(-180f, 180f)] public float rate;
            [Tooltip("Rocks back and forth by this many degrees either side of `degrees`. This is what makes 4-D motion readable: a continuous spin in W can look like nothing, where a rock goes out and comes back so the eye can follow it.")]
            [Range(0f, 180f)] public float rock;
            [Tooltip("Rocks per second.")]
            [Range(0f, 4f)] public float rockRate = .12f;
            [Tooltip("Offsets this plane's rock in its cycle, so planes do not all peak together.")]
            [Range(0f, 1f)] public float rockPhase;
            [Tooltip("Half-width of the random range for this plane's rate, in degrees per second.")]
            [Range(0f, 180f)] public float randomRate = 24f;
            [Tooltip("Half-width of the random range for this plane's angle, in degrees.")]
            [Range(0f, 360f)] public float randomAngle = 180f;
            public bool randomize = true;
        }

        /// <summary>The newest enabled rig. Shapes read this when no explicit axis is assigned.</summary>
        public static Hyperspace4DAxis Current { get; private set; }

        /// <summary>How the six plane angles become one rotation.</summary>
        public enum RotationModel
        {
            /// <summary>
            /// All six angles at once, as the exponential of one bivector, through
            /// <see cref="Rotor4"/>'s left/right quaternion pair. No plane order, no gimbal lock;
            /// constant rates trace a geodesic of SO(4).
            /// </summary>
            Bivector,
            /// <summary>The original chain of six Givens rotations in a fixed order.</summary>
            SequentialPlanes
        }

        [Header("Rotation model")]
        [Tooltip("Bivector applies all six planes simultaneously (true SO(4), order-free). SequentialPlanes is the original fixed-order chain, kept for scenes tuned against it.")]
        public RotationModel rotationModel = RotationModel.Bivector;

        [Header("Rotation planes (degrees)")]
        [Tooltip("XY, XZ, XW, YZ, YW, ZW — in that order. XW, YW and ZW are the ones that tilt into W.")]
        public PlaneAxis[] planes = DefaultPlanes();

        [Header("Projection")
        ]
        public Projection4D projection = Projection4D.Perspective;
        [Tooltip("Viewer distance along W. Near 1 the stereographic inversion gets violent.")]
        [Range(1.05f, 8f)] public float wDistance = 2.4f;
        [Tooltip("Where the 3-D hyperplane sits along W. This is the 4-D camera's own position.")]
        [Range(-2f, 2f)] public float cameraW;
        [Range(.1f, 8f)] public float scale = 1f;

        [Header("Motion")]
        public bool animate = true;
        [Tooltip("Scales every plane's rate at once, so one dial slows the whole rig.")]
        [Range(0f, 4f)] public float rateScale = 1f;

        [Header("Randomisation")]
        [Tooltip("Same seed gives the same roll, every time.")]
        public int seed = 1977;
        public bool randomizeOnEnable;
        [Tooltip("Re-rolls on a timer. 0 = never.")]
        [Range(0f, 120f)] public float reRollSeconds;
        [Tooltip("Keeps one pair of planes matched when rolling, so the result is an isoclinic turn more often than not.")]
        [Range(0f, 1f)] public float isoclinicBias = .5f;
        [Tooltip("Snaps rolled angles to this many degrees. 0 = free.")]
        [Range(0f, 90f)] public float angleSnap;

        [Header("Activation layer")]
        [Tooltip("Master. 0 = the hyperworld is off and every 4-D shape sits in its neutral 3-D form; 1 = fully active. Starts at 0 on purpose.")]
        [Range(0, 1)] public float activation;
        [Tooltip("Where activation rests between cues.")]
        [Range(0, 1)] public float idleActivation;
        [Tooltip("Peak reached by a cue.")]
        [Range(0, 1)] public float cuePeak = 1f;
        [Tooltip("Seconds to rise to the peak.")]
        [Range(.01f, 4f)] public float attack = .12f;
        [Tooltip("Seconds held at the peak.")]
        [Range(0, 8f)] public float hold = .35f;
        [Tooltip("Seconds to fall back to idle.")]
        [Range(.05f, 16f)] public float release = 2.4f;

        [Header("Cue sources")]
        [Tooltip("Clock to listen to. Empty = found in the scene.")]
        public MasterClock clock;
        public bool activateOnBeat;
        [Tooltip("Fire every Nth beat. 1 = every beat, 4 = once a bar.")]
        [Range(1, 32)] public int everyNBeats = 4;
        public bool activateOnBar = true;
        [Range(1, 16)] public int everyNBars = 2;
        [Tooltip("Kick drive. Empty = found in the scene.")]
        public KickReactivity kick;
        [Tooltip("Adds the kick level straight into activation, on top of the envelope.")]
        [Range(0, 1)] public float kickAmount;

        [Header("Rotation cues")]
        [Tooltip("Fires whenever the watched plane sweeps through this many degrees. 0 = off.")]
        [Range(0, 180f)] public float triggerEveryDegrees;
        public Plane watchPlane = Plane.ZW;
        [Tooltip("Re-rolls the whole axis on every cue, so each activation is a new hyperworld.")]
        public bool rerollOnCue;

        [Header("Publish")]
        [Tooltip("Publishes _Hyper4DRot and _Hyper4DParams as shader globals for 4-D shaders.")]
        public bool publishToShaders = true;

        public string Status { get; private set; } = "";

        static readonly int RotId = Shader.PropertyToID("_Hyper4DRot");
        static readonly int ParamsId = Shader.PropertyToID("_Hyper4DParams");
        static readonly int ActivationId = Shader.PropertyToID("_Hyper4DActivation");

        Matrix4x4 rotation = Matrix4x4.identity;
        Rotor4 rotor = Rotor4.identity;
        // Spiral angle plus the rock, per plane, for this frame.
        readonly float[] effective = new float[6];
        float nextRoll;
        double elapsed;

        // Activation envelope state.
        float envelope;
        float cueTime = -999f;
        int lastBeat = int.MinValue, lastBar = int.MinValue;
        float watchedAccumulated;
        float lastWatchedAngle;
        bool hooked;

        /// <summary>How active the hyperworld is right now, 0..1. Shapes blend with this.</summary>
        public float Activation => Mathf.Clamp01(activation);
        public static float SharedActivation => Current ? Current.Activation : 0f;

        public Matrix4x4 Rotation => rotation;
        /// <summary>
        /// This frame's rotation as a <see cref="Rotor4"/> (left/right quaternions), for geodesic
        /// blending with <see cref="Rotor4.Slerp"/>. Identity under <see cref="RotationModel.SequentialPlanes"/>;
        /// use <see cref="Rotation"/> there.
        /// </summary>
        public Rotor4 Rotor => rotor;

        static PlaneAxis[] DefaultPlanes()
        {
            var p = new PlaneAxis[6];
            for (int i = 0; i < 6; i++) p[i] = new PlaneAxis();
            // A gentle isoclinic default: XY and ZW matched.
            p[(int)Plane.XY].rate = 9f;
            p[(int)Plane.ZW].rate = 9f;
            p[(int)Plane.XZ].rate = 3f;
            return p;
        }

        void OnEnable()
        {
            Current = this;
            EnsurePlanes();
            if (randomizeOnEnable) Randomize(seed);
            Hook();
            Rebuild();
        }

        void OnDisable()
        {
            Unhook();
            if (Current == this) Current = null;
        }

        void Hook()
        {
            if (hooked) return;
            if (!clock) clock = FindFirstObjectByType<MasterClock>();
            if (!kick) kick = FindFirstObjectByType<KickReactivity>();
            if (clock)
            {
                clock.BeatCrossed += OnBeat;
                clock.BarCrossed += OnBar;
                hooked = true;
            }
        }

        void Unhook()
        {
            if (!hooked || !clock) { hooked = false; return; }
            clock.BeatCrossed -= OnBeat;
            clock.BarCrossed -= OnBar;
            hooked = false;
        }

        void OnBeat(int index)
        {
            if (!activateOnBeat || index == lastBeat) return;
            lastBeat = index;
            if (everyNBeats <= 1 || index % everyNBeats == 0) Trigger();
        }

        void OnBar(int index)
        {
            if (!activateOnBar || index == lastBar) return;
            lastBar = index;
            if (everyNBars <= 1 || index % everyNBars == 0) Trigger();
        }

        /// <summary>Fires the activation envelope. This is the cue hook anything can call.</summary>
        public void Trigger()
        {
            cueTime = Application.isPlaying ? Time.time : 0f;
            if (rerollOnCue) Randomize(unchecked(seed + Mathf.RoundToInt(cueTime * 1000f)));
        }

        /// <summary>
        /// Attack, hold, release, plus the kick level on top so the layer breathes with the low
        /// end between cues as well as snapping on them.
        /// </summary>
        void DriveActivation()
        {
            if (!Application.isPlaying)
            {
                envelope = 0f;
                activation = Mathf.Max(activation, idleActivation);
                return;
            }

            float since = Time.time - cueTime;
            if (since < 0f || cueTime < -100f) envelope = 0f;
            else if (since < attack) envelope = since / Mathf.Max(attack, .01f);
            else if (since < attack + hold) envelope = 1f;
            else
            {
                float t = (since - attack - hold) / Mathf.Max(release, .05f);
                envelope = t >= 1f ? 0f : 1f - t;
            }

            float drive = Mathf.Lerp(idleActivation, cuePeak, envelope);
            if (kick && kickAmount > 0f) drive += kick.KickLevel * kickAmount;
            activation = Mathf.Clamp01(drive);
        }

        /// <summary>Fires once per `triggerEveryDegrees` of sweep in the watched plane.</summary>
        void CheckRotationCue()
        {
            if (triggerEveryDegrees <= 0f) return;
            float now = planes[(int)watchPlane].degrees;
            watchedAccumulated += Mathf.Abs(Mathf.DeltaAngle(lastWatchedAngle, now));
            lastWatchedAngle = now;
            if (watchedAccumulated >= triggerEveryDegrees)
            {
                watchedAccumulated = 0f;
                Trigger();
            }
        }

        void Update()
        {
            EnsurePlanes();
            Hook();
            DriveActivation();
            if (Application.isPlaying && animate)
            {
                elapsed += Time.deltaTime;
                // Rates scale with activation, so at rest the hyperworld is still.
                float dt = Time.deltaTime * rateScale * Activation;
                for (int i = 0; i < 6; i++)
                    planes[i].degrees = Mathf.Repeat(planes[i].degrees + planes[i].rate * dt, 360f);

                if (reRollSeconds > 0f && Time.time >= nextRoll)
                {
                    nextRoll = Time.time + reRollSeconds;
                    Randomize(unchecked(seed + Mathf.RoundToInt(Time.time)));
                }
            }
            CheckRotationCue();
            Rebuild();
        }

        void EnsurePlanes()
        {
            if (planes == null || planes.Length != 6) planes = DefaultPlanes();
            for (int i = 0; i < 6; i++) if (planes[i] == null) planes[i] = new PlaneAxis();
        }

        /// <summary>Composes the six plane rotations into one 4x4 and publishes it.</summary>
        void Rebuild()
        {
            // Spiral (degrees, advanced by rate) plus a smooth rock either side of it.
            float now = (float)elapsed;
            for (int i = 0; i < 6; i++)
            {
                var p = planes[i];
                float rocked = p.rock > 0f && p.rockRate > 0f
                    ? p.rock * Mathf.Sin((now * p.rockRate + p.rockPhase) * Mathf.PI * 2f)
                    : 0f;
                effective[i] = p.degrees + rocked;
            }

            // The three W planes scale with activation: at 0 this is an ordinary 3-D rotation and
            // nothing leaves the hyperplane, which is what makes the layer neutral at rest.
            float act = Activation;
            if (rotationModel == RotationModel.Bivector)
            {
                float k = Mathf.Deg2Rad;
                rotor = Rotor4.FromBivector(
                    effective[(int)Plane.XY] * k, effective[(int)Plane.XZ] * k, effective[(int)Plane.XW] * act * k,
                    effective[(int)Plane.YZ] * k, effective[(int)Plane.YW] * act * k, effective[(int)Plane.ZW] * act * k);
                rotation = rotor.ToMatrix();
            }
            else
            {
                rotation = Matrix4x4.identity;
                // Order matters, but any fixed order is a valid parameterisation of SO(4); this one
                // puts the three 3-D planes first so the W tilts read as the outer motion.
                Compose(ref rotation, 0, 1, effective[(int)Plane.XY]);
                Compose(ref rotation, 0, 2, effective[(int)Plane.XZ]);
                Compose(ref rotation, 1, 2, effective[(int)Plane.YZ]);
                Compose(ref rotation, 0, 3, effective[(int)Plane.XW] * act);
                Compose(ref rotation, 1, 3, effective[(int)Plane.YW] * act);
                Compose(ref rotation, 2, 3, effective[(int)Plane.ZW] * act);
                rotor = Rotor4.identity;
            }

            if (publishToShaders)
            {
                Shader.SetGlobalMatrix(RotId, rotation);
                Shader.SetGlobalVector(ParamsId,
                    new Vector4(wDistance, cameraW * Activation, (int)projection, scale));
                Shader.SetGlobalFloat(ActivationId, Activation);
            }

            Status = string.Format("act {7:0.00} · XY {0:0} XZ {1:0} XW {2:0} YZ {3:0} YW {4:0} ZW {5:0}{6}",
                planes[0].degrees, planes[1].degrees, planes[2].degrees,
                planes[3].degrees, planes[4].degrees, planes[5].degrees,
                IsIsoclinic() ? "  · isoclinic" : "", Activation);
        }

        /// <summary>Left-multiplies a Givens rotation in the (a,b) plane.</summary>
        static void Compose(ref Matrix4x4 m, int a, int b, float degrees)
        {
            if (Mathf.Approximately(degrees, 0f)) return;
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            var g = Matrix4x4.identity;
            g[a, a] = c; g[a, b] = -s;
            g[b, a] = s; g[b, b] = c;
            m = g * m;
        }

        /// <summary>True when a matched orthogonal pair is turning at the same rate.</summary>
        public bool IsIsoclinic()
        {
            float e = .5f;
            return (Mathf.Abs(planes[(int)Plane.XY].rate - planes[(int)Plane.ZW].rate) < e &&
                    Mathf.Abs(planes[(int)Plane.XY].rate) > e)
                || (Mathf.Abs(planes[(int)Plane.XZ].rate - planes[(int)Plane.YW].rate) < e &&
                    Mathf.Abs(planes[(int)Plane.XZ].rate) > e);
        }

        // ---- use ------------------------------------------------------------

        public Vector4 Rotate(Vector4 v) => rotation * v;

        /// <summary>
        /// Rotation using only the three planes that involve W (XW, YW, ZW), scaled by activation.
        /// This is what a bubble uses: the ordinary 3-D planes would spin the geometry inside the
        /// bubble against the world around it, where the W planes slide it out of and back into the
        /// hyperplane without disturbing its 3-D orientation.
        /// </summary>
        public Vector4 RotateWOnly(Vector4 v)
        {
            float act = Activation;
            if (act <= .001f) return new Vector4(v.x, v.y, v.z, 0f);
            if (rotationModel == RotationModel.Bivector)
            {
                float k = Mathf.Deg2Rad * act;
                return Rotor4.FromBivector(0f, 0f, effective[(int)Plane.XW] * k,
                                           0f, effective[(int)Plane.YW] * k, effective[(int)Plane.ZW] * k).Rotate(v);
            }
            var m = Matrix4x4.identity;
            Compose(ref m, 0, 3, effective[(int)Plane.XW] * act);
            Compose(ref m, 1, 3, effective[(int)Plane.YW] * act);
            Compose(ref m, 2, 3, effective[(int)Plane.ZW] * act);
            return m * v;
        }

        /// <summary>
        /// Which W planes actually show from a given view direction.
        ///
        /// Looking straight down the tunnel (view forward = +Z), a ZW rotation slides geometry
        /// along the view axis, so it mostly reads as scale and is nearly invisible. XW and YW
        /// slide it across the screen, which is where the fourth dimension becomes legible. So the
        /// planes to rock are the two perpendicular to the view, and the one along it is the one to
        /// leave alone.
        /// </summary>
        public static void VisiblePlanesFor(Vector3 viewForward, out Plane a, out Plane b, out Plane hidden)
        {
            Vector3 f = viewForward.sqrMagnitude < 1e-6f ? Vector3.forward : viewForward.normalized;
            float ax = Mathf.Abs(f.x), ay = Mathf.Abs(f.y), az = Mathf.Abs(f.z);
            if (az >= ax && az >= ay) { a = Plane.XW; b = Plane.YW; hidden = Plane.ZW; }
            else if (ax >= ay) { a = Plane.YW; b = Plane.ZW; hidden = Plane.XW; }
            else { a = Plane.XW; b = Plane.ZW; hidden = Plane.YW; }
        }

        /// <summary>
        /// Sets the rig up for a steady camera looking down an axis: a continuous spiral in the
        /// 3-D plane across the view, and a smooth rock in the two W planes that show from there.
        /// The W plane along the view is zeroed because its motion cannot be seen.
        /// </summary>
        public void SetUpForView(Vector3 viewForward, float spiralDegreesPerSecond = 8f,
                                 float rockDegrees = 55f, float rocksPerSecond = .09f)
        {
            EnsurePlanes();
            VisiblePlanesFor(viewForward, out Plane a, out Plane b, out Plane hidden);

            foreach (var p in planes) { p.rate = 0f; p.rock = 0f; }

            // The roll the viewer sees as the tunnel turning.
            Plane spiral = hidden == Plane.ZW ? Plane.XY : hidden == Plane.XW ? Plane.YZ : Plane.XZ;
            planes[(int)spiral].rate = spiralDegreesPerSecond;

            // The two visible W planes rock, a quarter cycle apart so the motion circles rather
            // than pulsing in and out on one line.
            planes[(int)a].rock = rockDegrees;
            planes[(int)a].rockRate = rocksPerSecond;
            planes[(int)a].rockPhase = 0f;
            planes[(int)b].rock = rockDegrees;
            planes[(int)b].rockRate = rocksPerSecond;
            planes[(int)b].rockPhase = .25f;

            planes[(int)hidden].rock = 0f;
            planes[(int)hidden].rate = 0f;
            Rebuild();
        }

        /// <summary>4-D to 3-D, through this rig's projection and W camera position.</summary>
        public Vector3 Project(Vector4 v)
        {
            // Blend toward dropping W entirely as the layer deactivates.
            float act = Activation;
            Vector3 flat = new Vector3(v.x, v.y, v.z) * scale;
            if (act <= .001f) return flat;

            float w = (v.w - cameraW) * act;
            switch (projection)
            {
                case Projection4D.Stereographic:
                {
                    float d = 1f - w;
                    if (Mathf.Abs(d) < .08f) d = Mathf.Sign(d == 0f ? 1f : d) * .08f;
                    return Vector3.Lerp(flat, new Vector3(v.x, v.y, v.z) / d * .5f * scale, act);
                }
                case Projection4D.Perspective:
                {
                    float near = Mathf.Max(wDistance, 1.05f);
                    return Vector3.Lerp(flat,
                        new Vector3(v.x, v.y, v.z) * (near / Mathf.Max(near - w, .08f)) * scale, act);
                }
                default:
                    return flat;
            }
        }

        public Vector3 RotateAndProject(Vector4 v) => Project(Rotate(v));

        /// <summary>Rotate through the shared rig, or pass through when there is none.</summary>
        public static Vector4 SharedRotate(Vector4 v) => Current ? Current.Rotate(v) : v;

        /// <summary>Project through the shared rig, or drop W when there is none.</summary>
        public static Vector3 SharedProject(Vector4 v) =>
            Current ? Current.Project(v) : new Vector3(v.x, v.y, v.z);

        public static Vector3 SharedRotateAndProject(Vector4 v) =>
            Current ? Current.RotateAndProject(v) : new Vector3(v.x, v.y, v.z);

        // ---- randomisation --------------------------------------------------

        /// <summary>
        /// Rolls every plane marked `randomize`. Deterministic for a given seed, and it saves and
        /// restores Unity's random state so it never disturbs anything else drawing randoms.
        /// </summary>
        public void Randomize(int withSeed)
        {
            EnsurePlanes();
            var saved = UnityEngine.Random.state;
            UnityEngine.Random.InitState(withSeed);

            for (int i = 0; i < 6; i++)
            {
                var p = planes[i];
                if (!p.randomize) continue;
                p.degrees = Snap(UnityEngine.Random.Range(-p.randomAngle, p.randomAngle));
                p.rate = UnityEngine.Random.Range(-p.randomRate, p.randomRate);
            }

            // Bias toward an isoclinic turn by matching one orthogonal pair.
            if (UnityEngine.Random.value < isoclinicBias)
            {
                bool first = UnityEngine.Random.value < .5f;
                int a = (int)(first ? Plane.XY : Plane.XZ);
                int b = (int)(first ? Plane.ZW : Plane.YW);
                if (planes[a].randomize && planes[b].randomize) planes[b].rate = planes[a].rate;
            }

            UnityEngine.Random.state = saved;
            seed = withSeed;
            Rebuild();
        }

        public void Randomize() => Randomize(unchecked(seed + 1));

        float Snap(float degrees) =>
            angleSnap > 0f ? Mathf.Round(degrees / angleSnap) * angleSnap : degrees;

        /// <summary>
        /// A random unit vector in 4-space, for scattering shapes or seeding particle positions.
        /// Uses two independent circles, which is the standard uniform construction on S^3.
        /// </summary>
        public static Vector4 RandomOnSphere4()
        {
            float u = UnityEngine.Random.value;
            float a = UnityEngine.Random.value * Mathf.PI * 2f;
            float b = UnityEngine.Random.value * Mathf.PI * 2f;
            float r1 = Mathf.Sqrt(1f - u), r2 = Mathf.Sqrt(u);
            return new Vector4(r1 * Mathf.Sin(a), r1 * Mathf.Cos(a), r2 * Mathf.Sin(b), r2 * Mathf.Cos(b));
        }

        [ContextMenu("Set up for a camera looking down +Z")]
        void ContextTunnelZ() => SetUpForView(Vector3.forward);
        [ContextMenu("Set up for the main camera")]
        void ContextTunnelCamera()
        {
            var cam = Camera.main;
            SetUpForView(cam ? cam.transform.forward : Vector3.forward);
        }
        [ContextMenu("Trigger activation")] void ContextTrigger() => Trigger();
        [ContextMenu("Randomize")] void ContextRandomize() => Randomize();
        [ContextMenu("Zero all planes")]
        void ContextZero()
        {
            EnsurePlanes();
            foreach (var p in planes) { p.degrees = 0f; p.rate = 0f; }
            Rebuild();
        }
        [ContextMenu("Isoclinic XY + ZW")]
        void ContextIsoclinic()
        {
            EnsurePlanes();
            foreach (var p in planes) p.rate = 0f;
            planes[(int)Plane.XY].rate = 12f;
            planes[(int)Plane.ZW].rate = 12f;
        }
        [ContextMenu("Isoclinic XZ + YW")]
        void ContextIsoclinic2()
        {
            EnsurePlanes();
            foreach (var p in planes) p.rate = 0f;
            planes[(int)Plane.XZ].rate = 12f;
            planes[(int)Plane.YW].rate = 12f;
        }
    }
}
