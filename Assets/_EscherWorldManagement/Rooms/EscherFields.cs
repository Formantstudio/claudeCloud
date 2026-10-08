using System;
using UnityEngine;

namespace PsychedelicLab.EscherWorld
{
    /// <summary>Which axis something winds around.</summary>
    public enum RoomAxis { X, Y, Z }

    /// <summary>
    /// The surfaces a room can be made of. Split by job rather than by family:
    ///
    /// **Architecture** — the triply periodic minimal surfaces. They tile space in all three axes
    /// forever with no seam, so a room made of one is a window onto an infinite building rather
    /// than a mesh that has to be placed. These are the only ones a screw dislocation turns into a
    /// staircase, because a dislocation needs a repeat along its axis to advance into.
    ///
    /// **Chambers** — bounded shapes. A room with a ceiling.
    ///
    /// **Recursive** — folded fractals. Galleries and masonry that repeat inward.
    /// </summary>
    public enum RoomField
    {
        // Architecture: periodic, dislocatable
        Gyroid,
        SchwarzP,
        SchwarzD,
        Neovius,
        Lidinoid,
        SplitP,
        // Chambers: bounded
        BarthSextic,
        Goursat,
        // Recursive
        Mandelbox,
        Mandelbulb,
        MengerSponge,
        SierpinskiTetra
    }

    /// <summary>
    /// A room's field settings. Owned by the Escher world system, deliberately separate from the
    /// geometry folder's own implicit shapes so the two can be worked on at the same time without
    /// touching the same file.
    /// </summary>
    [Serializable]
    public sealed class EscherFieldSettings
    {
        [Header("Surface")]
        public RoomField field = RoomField.Gyroid;
        [Tooltip("Level set to extract. 0 is the minimal surface; sweeping it moves the walls into the openings and the openings into the walls, in place.")]
        [Range(-1.5f, 1.5f)] public float level;
        [Tooltip("Thickens the surface into a wall with two faces, instead of an infinitely thin sheet.")]
        [Range(0f, .8f)] public float thickness = .12f;
        [Tooltip("Lattice repeats per unit. This also sets the floor-to-floor height a dislocation steps by.")]
        [Range(.25f, 8f)] public float frequency = 2f;

        [Header("Fourth dimension")]
        [Tooltip("Where this room sits along W. The architecture fields are genuinely 4-D, so two rooms at different W are different buildings cut from one structure — consistent with each other, but not the same geometry. This is what a portal looks through.")]
        [Range(-8f, 8f)] public float w;
        [Tooltip("How strongly W participates. At 0 the field collapses to its ordinary 3-D form and every slice looks the same.")]
        [Range(0f, 1f)] public float wInfluence = 1f;
        [Tooltip("Rotation in the XW, YW and ZW planes, in turns. Re-orients the 4-D structure before slicing, which is what makes a slice look like a different room rather than the same one shifted.")]
        public Vector3 wRotation;

        [Header("Escher step")]
        [Tooltip("Vertical periods gained per full circuit of the axis. 1 means one trip round puts you exactly one floor higher, in geometry identical to where you started. 0 = an ordinary room.")]
        [Range(-4f, 4f)] public float dislocation;
        public RoomAxis dislocationAxis = RoomAxis.Y;
        [Tooltip("Radius inside which the shear eases off. A dislocation is singular on its own axis, so the core must be softened or the geometry tears along the spine.")]
        [Range(.01f, 2f)] public float dislocationCore = .25f;

        [Header("Recursive fields")]
        [Range(2f, 16f)] public float power = 8f;
        [Range(2, 20)] public int iterations = 8;
        [Tooltip("Mandelbox scale. Around -1.75 is the room-and-corridor register; positive is a crystalline shell.")]
        [Range(-3f, 3f)] public float boxScale = -1.75f;
        [Range(.1f, 2f)] public float minRadius = .5f;
        [Range(.5f, 3f)] public float fixedRadius = 1f;
        [Range(1, 6)] public int folds = 3;

        [Header("Barth sextic")]
        [Tooltip("Homogenising parameter. Animating it swells and splits the surface.")]
        [Range(.2f, 2f)] public float barthW = 1f;
    }

    public static class EscherFields
    {
        const float Phi = 1.6180339887f;

        /// <summary>
        /// Field value at a point. Negative inside, positive outside, zero on the surface.
        /// The screw dislocation is applied first, so every surface gets it for free.
        /// </summary>
        public static float Sample(EscherFieldSettings s, Vector3 p, float time) =>
            Sample(s, p, s.w, time);

        /// <summary>
        /// Field value at a point, on the W slice given. Negative inside, positive outside.
        ///
        /// The periodic family is evaluated in **four** dimensions, so moving along W cuts a
        /// genuinely different 3-D room out of one continuous 4-D structure. Every slice is
        /// gyroid-like and they all belong to the same building, but no two are the same geometry.
        /// That is what lets a portal be an outlet of the fourth dimension rather than a link to
        /// somewhere else: the camera never moves, only its slice does.
        ///
        /// The bounded and recursive fields have no natural fourth coordinate, so W rotates their
        /// input through the XW, YW and ZW planes instead — a different cut through the same solid.
        /// </summary>
        public static float Sample(EscherFieldSettings s, Vector3 p, float w, float time)
        {
            // The Escher step. Shearing the field along its own axis by the azimuth means one
            // circuit advances the lattice by `dislocation` periods: walk round and you are a
            // floor higher, in geometry identical to where you started. It is a property of the
            // field rather than of the projection, so it holds from every angle, under
            // perspective, with the camera free — unlike a view-dependent shear or a hidden cut.
            //
            // Same defect that makes a crystal grow in a spiral.
            if (s.dislocation != 0f) p = Dislocate(s, p);

            // Re-orient through the W planes before slicing. On a 4-D field this turns the
            // structure; on a 3-D one it is the only thing W does.
            float wEff = w * s.wInfluence;
            if (s.wRotation != Vector3.zero && s.wInfluence > 0f)
                RotateW(s.wRotation, ref p, ref wEff);

            float v = Raw(s, p, wEff, time);
            v -= s.level;
            // A thickened level set: the wall around the surface rather than the surface itself.
            if (s.thickness > 0f) v = Mathf.Abs(v) - s.thickness;
            return v;
        }

        static float Raw(EscherFieldSettings s, Vector3 p, float w, float time)
        {
            float k = Mathf.PI * s.frequency;
            float qw = w * k;

            switch (s.field)
            {
                // 4-D Schwarz P: the same alternating sum with a fourth term.
                case RoomField.SchwarzP:
                {
                    Vector3 q = p * k;
                    return Mathf.Cos(q.x) + Mathf.Cos(q.y) + Mathf.Cos(q.z) + Mathf.Cos(qw);
                }
                case RoomField.SchwarzD:
                {
                    Vector3 q = p * k;
                    float sx = Mathf.Sin(q.x), sy = Mathf.Sin(q.y), sz = Mathf.Sin(q.z), sw = Mathf.Sin(qw);
                    float cx = Mathf.Cos(q.x), cy = Mathf.Cos(q.y), cz = Mathf.Cos(q.z), cw = Mathf.Cos(qw);
                    // The 3-D D-surface, with W mixed into each term so the slice shifts phase.
                    return sx * sy * sz * cw + sx * cy * cz + cx * sy * cz + cx * cy * sz * cw
                         + sw * sx * sy * .5f;
                }
                case RoomField.Neovius:
                {
                    Vector3 q = p * k;
                    float cx = Mathf.Cos(q.x), cy = Mathf.Cos(q.y), cz = Mathf.Cos(q.z), cw = Mathf.Cos(qw);
                    return 3f * (cx + cy + cz + cw) + 4f * cx * cy * cz * cw;
                }
                case RoomField.Lidinoid:
                {
                    Vector3 q = p * k;
                    q.z += qw * .5f;   // W shears the third axis: a different Lidinoid cut
                    float s2x = Mathf.Sin(2f * q.x), s2y = Mathf.Sin(2f * q.y), s2z = Mathf.Sin(2f * q.z);
                    float c2x = Mathf.Cos(2f * q.x), c2y = Mathf.Cos(2f * q.y), c2z = Mathf.Cos(2f * q.z);
                    return s2x * Mathf.Cos(q.y) * Mathf.Sin(q.z)
                         + s2y * Mathf.Cos(q.z) * Mathf.Sin(q.x)
                         + s2z * Mathf.Cos(q.x) * Mathf.Sin(q.y)
                         - c2x * c2y - c2y * c2z - c2z * c2x;
                }
                case RoomField.SplitP:
                {
                    Vector3 q = p * k;
                    q.x += qw * .5f;
                    float s2x = Mathf.Sin(2f * q.x), s2y = Mathf.Sin(2f * q.y), s2z = Mathf.Sin(2f * q.z);
                    float c2x = Mathf.Cos(2f * q.x), c2y = Mathf.Cos(2f * q.y), c2z = Mathf.Cos(2f * q.z);
                    return 1.1f * (s2x * Mathf.Sin(q.z) * Mathf.Cos(q.y)
                                 + s2y * Mathf.Sin(q.x) * Mathf.Cos(q.z)
                                 + s2z * Mathf.Sin(q.y) * Mathf.Cos(q.x))
                         - .2f * (c2x * c2y + c2y * c2z + c2z * c2x)
                         - .4f * (c2x + c2y + c2z);
                }

                // Barth sextic: degree 6, 65 ordinary double points, icosahedral symmetry.
                case RoomField.BarthSextic:
                {
                    Vector3 q = p * 1.8f;
                    float x2 = q.x * q.x, y2 = q.y * q.y, z2 = q.z * q.z;
                    float p2 = Phi * Phi, w2 = s.barthW * s.barthW;
                    float a = p2 * x2 - y2, b = p2 * y2 - z2, c = p2 * z2 - x2;
                    float r = x2 + y2 + z2 - w2;
                    float v = 4f * a * b * c - (1f + 2f * Phi) * r * r * w2;
                    // Degree 6 grows fast; compress so the extractor sees a usable range.
                    return Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), .25f);
                }
                case RoomField.Goursat:
                {
                    Vector3 q = p * 1.5f;
                    float r2 = q.sqrMagnitude;
                    return Mathf.Pow(q.x, 4) + Mathf.Pow(q.y, 4) + Mathf.Pow(q.z, 4)
                         - 1.5f * r2 * r2 + 1f;
                }

                // Mandelbox: box fold, sphere fold, scale. The endless-rooms fractal.
                case RoomField.Mandelbox:
                {
                    Vector3 c = p * 3f, z = c;
                    float dr = 1f;
                    float minR2 = s.minRadius * s.minRadius, fixR2 = s.fixedRadius * s.fixedRadius;
                    int it = Mathf.Clamp(s.iterations, 2, 20);
                    for (int i = 0; i < it; i++)
                    {
                        z = new Vector3(Mathf.Clamp(z.x, -1f, 1f) * 2f - z.x,
                                        Mathf.Clamp(z.y, -1f, 1f) * 2f - z.y,
                                        Mathf.Clamp(z.z, -1f, 1f) * 2f - z.z);
                        float m = z.sqrMagnitude;
                        if (m < minR2) { float foldScale = fixR2 / minR2; z *= foldScale; dr *= foldScale; }
                        else if (m < fixR2) { float foldScale = fixR2 / m; z *= foldScale; dr *= foldScale; }
                        z = z * s.boxScale + c;
                        dr = dr * Mathf.Abs(s.boxScale) + 1f;
                    }
                    return z.magnitude / Mathf.Max(Mathf.Abs(dr), 1e-6f);
                }

                // Mandelbulb, escape time turned into a distance estimate by the running
                // derivative: DE = 0.5 * log(r) * r / dr.
                case RoomField.Mandelbulb:
                {
                    Vector3 c = p * 1.3f, z = c;
                    float dr = 1f, r = 0f;
                    int it = Mathf.Clamp(s.iterations, 2, 20);
                    for (int i = 0; i < it; i++)
                    {
                        r = z.magnitude;
                        if (r > 2.2f) break;
                        float theta = Mathf.Acos(Mathf.Clamp(z.z / Mathf.Max(r, 1e-6f), -1f, 1f));
                        float phi = Mathf.Atan2(z.y, z.x);
                        dr = Mathf.Pow(r, s.power - 1f) * s.power * dr + 1f;
                        float zr = Mathf.Pow(r, s.power);
                        theta *= s.power; phi *= s.power;
                        z = zr * new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi),
                                             Mathf.Sin(theta) * Mathf.Sin(phi),
                                             Mathf.Cos(theta)) + c;
                    }
                    return r < 1e-5f ? -1f
                         : .5f * Mathf.Log(Mathf.Max(r, 1.0001f)) * r / Mathf.Max(dr, 1e-6f);
                }

                case RoomField.MengerSponge:
                {
                    Vector3 q = p * 1.1f;
                    float d = Box(q, Vector3.one), scale = 1f;
                    int depth = Mathf.Clamp(s.folds, 1, 6);
                    for (int i = 0; i < depth; i++)
                    {
                        Vector3 a = new Vector3(Mathf.Repeat(q.x * scale, 2f) - 1f,
                                                Mathf.Repeat(q.y * scale, 2f) - 1f,
                                                Mathf.Repeat(q.z * scale, 2f) - 1f);
                        scale *= 3f;
                        Vector3 r = new Vector3(1f - 3f * Mathf.Abs(a.x),
                                                1f - 3f * Mathf.Abs(a.y),
                                                1f - 3f * Mathf.Abs(a.z));
                        d = Mathf.Max(d, Cross(r) / scale);
                    }
                    return d;
                }

                case RoomField.SierpinskiTetra:
                {
                    Vector3 z = p * 1.4f;
                    const float scale = 2f;
                    int depth = Mathf.Clamp(s.folds, 1, 6);
                    for (int i = 0; i < depth; i++)
                    {
                        if (z.x + z.y < 0f) z = new Vector3(-z.y, -z.x, z.z);
                        if (z.x + z.z < 0f) z = new Vector3(-z.z, z.y, -z.x);
                        if (z.y + z.z < 0f) z = new Vector3(z.x, -z.z, -z.y);
                        z = z * scale - Vector3.one * (scale - 1f);
                    }
                    return z.magnitude * Mathf.Pow(scale, -depth) - .02f;
                }

                // The gyroid, in four dimensions. The 3-D form is the cyclic sum
                // sin x cos y + sin y cos z + sin z cos x; extending the cycle through W gives
                // sin x cos y + sin y cos z + sin z cos w + sin w cos x, which is still triply
                // periodic in every 3-D slice and still splits space into two interpenetrating
                // labyrinths — but a different pair for every W. That one extra term is what makes
                // the whole portal idea work.
                default:
                {
                    Vector3 q = p * k;
                    float sw = Mathf.Sin(qw), cw = Mathf.Cos(qw);
                    return Mathf.Sin(q.x) * Mathf.Cos(q.y)
                         + Mathf.Sin(q.y) * Mathf.Cos(q.z)
                         + Mathf.Sin(q.z) * cw
                         + sw * Mathf.Cos(q.x);
                }
            }
        }

        /// <summary>
        /// Applies the screw shear: bring the axis to Z, measure the azimuth, advance Z by
        /// `dislocation` lattice periods per turn, put the axis back.
        ///
        /// The period has to be the field's own (`2pi / frequency`). Any other value and the
        /// floors do not line up, which reads as a bug rather than a staircase.
        /// </summary>
        public static Vector3 Dislocate(EscherFieldSettings s, Vector3 p)
        {
            Vector3 q = s.dislocationAxis == RoomAxis.X ? new Vector3(p.y, p.z, p.x)
                      : s.dislocationAxis == RoomAxis.Y ? new Vector3(p.z, p.x, p.y)
                      : p;

            float radius = new Vector2(q.x, q.y).magnitude;
            float core = Mathf.Max(s.dislocationCore, .01f);
            // 0 on the axis, 1 outside the core: fades out the singularity where every azimuth
            // meets at once. Without this the geometry tears along the spine.
            float ease = radius <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Min(radius / core, 1f));

            if (ease > 0f)
            {
                float azimuth = Mathf.Atan2(q.y, q.x);                  // -pi .. pi
                float period = Mathf.PI * 2f / Mathf.Max(s.frequency, .01f);
                q.z += s.dislocation * period * (azimuth / (Mathf.PI * 2f)) * ease;
            }

            return s.dislocationAxis == RoomAxis.X ? new Vector3(q.z, q.x, q.y)
                 : s.dislocationAxis == RoomAxis.Y ? new Vector3(q.y, q.z, q.x)
                 : q;
        }

        /// <summary>
        /// Rotates a 4-D point through the XW, YW and ZW planes. Turns given in turns, not
        /// radians, so a value of 1 is a full revolution and a random seed in 0..1 covers the
        /// whole range — which is what makes a random outlet easy to generate.
        /// </summary>
        public static void RotateW(Vector3 turns, ref Vector3 p, ref float w)
        {
            float tau = Mathf.PI * 2f;
            Rot(ref p.x, ref w, turns.x * tau);
            Rot(ref p.y, ref w, turns.y * tau);
            Rot(ref p.z, ref w, turns.z * tau);
        }

        static void Rot(ref float a, ref float b, float angle)
        {
            if (angle == 0f) return;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float a0 = a, b0 = b;
            a = a0 * c - b0 * s;
            b = a0 * s + b0 * c;
        }

        /// <summary>
        /// True where the field itself has a fourth coordinate, so moving along W cuts a different
        /// room. For everything else W only re-orients the shape through <see cref="RotateW"/>,
        /// which still varies the view but does not produce new architecture.
        /// </summary>
        public static bool IsFourDimensional(RoomField field) => IsPeriodic(field);

        /// <summary>Central-difference gradient, for normals and for normalising a level set.</summary>
        public static Vector3 Gradient(EscherFieldSettings s, Vector3 p, float time, float h = .01f)
        {
            float dx = Sample(s, p + Vector3.right * h, time) - Sample(s, p - Vector3.right * h, time);
            float dy = Sample(s, p + Vector3.up * h, time) - Sample(s, p - Vector3.up * h, time);
            float dz = Sample(s, p + Vector3.forward * h, time) - Sample(s, p - Vector3.forward * h, time);
            return new Vector3(dx, dy, dz) / (2f * h);
        }

        static float Box(Vector3 p, Vector3 b)
        {
            Vector3 q = new Vector3(Mathf.Abs(p.x) - b.x, Mathf.Abs(p.y) - b.y, Mathf.Abs(p.z) - b.z);
            Vector3 m = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f));
            return m.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }

        /// <summary>Infinite cross of three square tubes: the piece a Menger fold removes.</summary>
        static float Cross(Vector3 r) =>
            Mathf.Min(Mathf.Max(r.x, r.y), Mathf.Min(Mathf.Max(r.y, r.z), Mathf.Max(r.z, r.x)));

        /// <summary>
        /// True where a screw dislocation gives a staircase: it needs a repeat along the shear
        /// axis, so only the triply periodic family qualifies. A bounded surface such as the Barth
        /// sextic just gets twisted.
        /// </summary>
        public static bool SupportsDislocation(RoomField field) => IsPeriodic(field);

        /// <summary>True where the surface tiles, so a camera can travel through it endlessly.</summary>
        public static bool IsPeriodic(RoomField field)
        {
            switch (field)
            {
                case RoomField.Gyroid:
                case RoomField.SchwarzP:
                case RoomField.SchwarzD:
                case RoomField.Neovius:
                case RoomField.Lidinoid:
                case RoomField.SplitP:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True where the field is a usable signed distance rather than a bare level set. The TPMS
        /// family and Barth are level sets: the gradient magnitude varies, so marching normals and
        /// any distance-based behaviour come out uneven unless divided by |grad f|.
        /// </summary>
        public static bool IsSignedDistance(RoomField field)
        {
            switch (field)
            {
                case RoomField.Mandelbulb:
                case RoomField.Mandelbox:
                case RoomField.MengerSponge:
                case RoomField.SierpinskiTetra:
                    return true;
                default:
                    return false;
            }
        }
    }
}
