using System;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The fractal accordion ring tunnel's parameters, as one serializable block on the chamber.
    /// </summary>
    [Serializable]
    public sealed class AccordionSettings
    {
        [Header("Bellows")]
        [Tooltip("0 = even spacing, 1 = rings fully bunched into the folds.")]
        [Range(0, 1)] public float accordion = .65f;
        [Tooltip("How many folds along the tunnel.")]
        [Range(.25f, 24f)] public float bellows = 4f;
        [Tooltip("Overall stretch. Animating this is the accordion opening and closing.")]
        [Range(.05f, 1f)] public float extend = 1f;
        [Range(1, 4)] public int octaves = 2;
        [Range(0, 1)] public float gain = .5f;
        [Range(.05f, 1f)] public float extendMin = .25f;

        [Header("Fractal ring spacing")]
        [Tooltip("Blend from even spacing to a self-similar Cantor clustering: groups of groups of rings.")]
        [Range(0, 1)] public float cantorSpacing = .6f;
        [Tooltip("Recursion depth. Each level splits every group in two.")]
        [Range(0, 8)] public int cantorDepth = 4;
        [Tooltip("Sub-group size. Below 0.5 leaves a gap at every level; 0.333 is the middle-thirds set.")]
        [Range(.05f, .5f)] public float cantorRatio = .34f;

        [Header("Ring profile")]
        public RingProfile profileA = RingProfile.Circle;
        public RingProfile profileB = RingProfile.Star;
        [Range(0, 1)] public float profileMorph = .35f;
        [Tooltip("Advances the morph along the tunnel, so the cross-section changes as you travel it.")]
        [Range(-2, 2)] public float profileMorphAlong = .6f;
        [Range(3, 24)] public int polygonSides = 6;
        [Range(2, 12)] public float superellipseN = 4f;
        [Range(2, 24)] public int starPoints = 7;
        [Range(0, .6f)] public float starDepth = .28f;

        [Header("Fractal edge")]
        [Tooltip("Master dial. 0 = clean rings, 1 = full recursive edge.")]
        [Range(0, 1)] public float fractalOut = .5f;
        [Tooltip("Recursion depth of the edge. Teeth on teeth on teeth.")]
        [Range(0, 10)] public int fractalDepth = 5;
        [Range(1, 24)] public float fractalBaseFrequency = 5f;
        [Tooltip("gain x lacunarity > 1 keeps the edge a true fractal curve.")]
        [Range(1.2f, 4f)] public float fractalLacunarity = 2.3f;
        [Range(.1f, .9f)] public float fractalGain = .62f;
        [Range(0, .6f)] public float fractalAmplitude = .3f;
        [Range(-2, 2)] public float fractalTwistAlong = .35f;

        [Header("Rings and connectors")]
        [Tooltip("1 = solid tube, lower leaves gaps so each ring reads as its own band.")]
        [Range(.05f, 1f)] public float bandFraction = .45f;
        [Tooltip("Longitudinal struts linking each ring to the next. 0 = unlinked rings.")]
        [Range(0, 32)] public int connectors = 8;
        [Range(.02f, 1f)] public float connectorWidth = .18f;

        [Header("Motion")]
        [Range(0, 2)] public float breathe = 1f;
        [Range(-2, 2)] public float spin = .15f;
    }

    /// <summary>Cross-section shapes for the accordion rings.</summary>
    public enum RingProfile { Circle, Polygon, Superellipse, Star, Gear }

    /// <summary>Radius as a function of angle, for the ring profiles.</summary>
    public static class RingProfiles
    {
        public static float Radius(RingProfile profile, AccordionSettings s, float theta)
        {
            switch (profile)
            {
                case RingProfile.Polygon:
                {
                    int k = Mathf.Max(s.polygonSides, 3);
                    float wedge = Mathf.PI * 2f / k;
                    return Mathf.Cos(Mathf.PI / k) /
                           Mathf.Max(Mathf.Cos(Mathf.Repeat(theta, wedge) - wedge * .5f), 1e-3f);
                }
                case RingProfile.Superellipse:
                {
                    float n = Mathf.Max(s.superellipseN, 2f);
                    float cx = Mathf.Pow(Mathf.Abs(Mathf.Cos(theta)), n);
                    float cy = Mathf.Pow(Mathf.Abs(Mathf.Sin(theta)), n);
                    return Mathf.Pow(Mathf.Max(cx + cy, 1e-5f), -1f / n);
                }
                case RingProfile.Star:
                    return 1f + s.starDepth * Mathf.Cos(Mathf.Max(s.starPoints, 2) * theta);
                case RingProfile.Gear:
                    return 1f + s.starDepth *
                           (float)System.Math.Tanh(4f * Mathf.Cos(Mathf.Max(s.starPoints, 2) * theta));
                default:
                    return 1f;
            }
        }
    }
}
