using System;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Kaleidoscopic folding in *shape* space, applied to points before they become geometry.
    ///
    /// This is deliberately not the same thing as `PsychedelicLab/Stage4D/KaleidoField`, which
    /// lenses the rendered image per camera, or FronkonGames' Kaleidoscope, which is a screen post
    /// effect. Both of those bend the picture. This bends the mesh, so a Klein bottle tunnel comes
    /// out of the generator already mirrored into sectors, with the wire and the particles agreeing.
    ///
    /// Three folds, each identity when its dial is at zero:
    /// - **sectors**: the angle about the axis is folded into N mirrored wedges (the kaleidoscope)
    /// - **plane folds**: repeated reflect-and-scale, which is the kaleidoscopic-IFS move that
    ///   produces endless temple walls
    /// - **W fold**: mirrors 4-D points through the W hyperplane before projection
    /// </summary>
    [Serializable]
    public sealed class KaleidoSettings
    {
        [Header("Sector mirror")]
        [Tooltip("Mirrored wedges about the axis. 0 or 1 = off.")]
        [Range(0, 32)] public int sectors;
        [Tooltip("Blends the fold in, so it can be dialled up rather than switched on.")]
        [Range(0, 1)] public float strength = 1f;
        [Tooltip("Rotates the wedge boundaries, in degrees.")]
        [Range(-180f, 180f)] public float offset;
        [Tooltip("Degrees per second the wedges rotate.")]
        [Range(-180f, 180f)] public float spin;
        public Axis3 axis = Axis3.Z;

        [Header("Plane folds (kaleidoscopic IFS)")]
        [Tooltip("Reflect-and-scale iterations. This is what makes procedural walls repeat inward.")]
        [Range(0, 8)] public int planeFolds;
        [Tooltip("Scale applied each iteration. Above 1 the detail grows inward.")]
        [Range(.2f, 3f)] public float foldScale = 1.6f;
        [Tooltip("Offset subtracted each iteration; the direction the pattern marches.")]
        public Vector3 foldOffset = new Vector3(.6f, .3f, 0f);
        [Tooltip("Normal of the reflecting plane. Normalised internally.")]
        public Vector3 foldNormal = new Vector3(.577f, .577f, .577f);

        [Header("Radial")]
        [Tooltip("Mirrors the radius about this value, turning a tube inside out through itself.")]
        [Range(0, 4f)] public float radialMirror;
        [Tooltip("Folds the axis coordinate, so the tunnel repeats along its length.")]
        [Range(0, 8f)] public float lengthRepeat;

        [Header("4-D")]
        [Tooltip("Mirrors W before projection: the two halves of 4-space land on top of each other.")]
        public bool foldW;

        public bool Any => (sectors > 1 && strength > 0f) || planeFolds > 0 ||
                           radialMirror > 0f || lengthRepeat > 0f || foldW;
    }

    // Axis3 lives in the geometry engine assembly (Engine/Implicit/ScrewDislocation.cs). It used to
    // be declared here too, which made every use ambiguous once the engine became its own assembly.

    public static class KaleidoFold
    {
        /// <summary>Applies every enabled fold to a point in shape space.</summary>
        public static Vector3 Apply(KaleidoSettings k, Vector3 p, float time)
        {
            if (k == null || !k.Any) return p;

            // Work with the fold axis as Z, then put it back (the same permutation the screw
            // dislocation uses).
            Vector3 q = ScrewDislocation.ToAxis(p, k.axis);

            if (k.sectors > 1 && k.strength > 0f)
            {
                float wedge = Mathf.PI * 2f / k.sectors;
                float r = new Vector2(q.x, q.y).magnitude;
                float a = Mathf.Atan2(q.y, q.x) - (k.offset + k.spin * time) * Mathf.Deg2Rad;
                // Fold the angle into one wedge, mirrored about its centre.
                float folded = Mathf.Abs(Mathf.Repeat(a, wedge) - wedge * .5f);
                float mixed = Mathf.Lerp(a, folded, k.strength) + (k.offset + k.spin * time) * Mathf.Deg2Rad;
                q = new Vector3(Mathf.Cos(mixed) * r, Mathf.Sin(mixed) * r, q.z);
            }

            if (k.radialMirror > 0f)
            {
                float r = new Vector2(q.x, q.y).magnitude;
                float mirrored = Mathf.Abs(r - k.radialMirror);
                float scale = r > 1e-5f ? mirrored / r : 0f;
                q = new Vector3(q.x * scale, q.y * scale, q.z);
            }

            if (k.lengthRepeat > 0f)
            {
                float period = k.lengthRepeat;
                // Triangle fold, so the repeat is mirrored rather than cut.
                float t = Mathf.Repeat(q.z, period * 2f);
                q.z = (t < period ? t : period * 2f - t) - period * .5f;
            }

            p = ScrewDislocation.FromAxis(q, k.axis);

            if (k.planeFolds > 0)
            {
                Vector3 n = k.foldNormal.sqrMagnitude < 1e-6f ? Vector3.one.normalized : k.foldNormal.normalized;
                for (int i = 0; i < k.planeFolds; i++)
                {
                    // Reflect across the plane when on the negative side, then scale and step.
                    float d = Vector3.Dot(p, n);
                    if (d < 0f) p -= 2f * d * n;
                    p = p * k.foldScale - k.foldOffset;
                }
                // Undo the accumulated scale so the shape keeps its size.
                p /= Mathf.Pow(Mathf.Max(k.foldScale, .2f), k.planeFolds);
            }

            return p;
        }

        /// <summary>The 4-D half of the fold, applied before projection.</summary>
        public static Vector4 Apply4(KaleidoSettings k, Vector4 v)
        {
            if (k == null) return v;
            if (k.foldW) v.w = Mathf.Abs(v.w);
            return v;
        }

        /// <summary>Sector counts that suit a given shape, for the roller to pick from.</summary>
        public static int SuggestSectors(ManifoldSurface surface)
        {
            switch (surface)
            {
                case ManifoldSurface.KleinBottle: return 6;
                case ManifoldSurface.CliffordTorus:
                case ManifoldSurface.Duocylinder: return 8;
                case ManifoldSurface.Helicoid: return 5;
                case ManifoldSurface.RomanSurface:
                case ManifoldSurface.BoysSurface: return 3;
                case ManifoldSurface.TorusKnotTube:
                case ManifoldSurface.TrefoilRibbon: return 3;
                default: return 6;
            }
        }
    }
}
