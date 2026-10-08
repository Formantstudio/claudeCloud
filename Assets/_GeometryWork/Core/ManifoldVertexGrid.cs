using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// A surface sampled as a vertex grid with its edge list, the form node-and-edge renderers use
    /// (GeometryFractalEngine). Unlike the chamber's quad lattice, a wrapping axis stops one step short
    /// of 1 and joins back to 0; an open axis puts its last point on 1.
    /// </summary>
    public static class ManifoldVertexGrid
    {
        /// <summary>Samples <paramref name="u"/> × <paramref name="v"/> points, row-major (v outer), and their grid edges.</summary>
        public static void Build(ManifoldSurface surface, ManifoldSettings s, int u, int v,
                                 List<Vector3> points, List<int> edges, float time = 0f)
        {
            bool wrapU = Manifolds.WrapsU(surface, s), wrapV = Manifolds.WrapsV(surface, s);
            bool poles = Manifolds.HasPoles(surface, s);
            float inset = poles ? .5f / v : 0f;
            for (int j = 0; j < v; j++)
            {
                // Rows keep off a pole row (it collapses to a point): an open axis insets both ends by
                // half a step; a wrapping one (the Roman surface) shifts every row by half a step, so
                // the closing edge stays one step long instead of stretching across the inset.
                float vv = wrapV ? (j + (poles ? .5f : 0f)) / v
                                 : Mathf.Lerp(inset, 1f - inset, (float)j / Mathf.Max(v - 1, 1));
                for (int i = 0; i < u; i++)
                {
                    float uu = wrapU ? (float)i / u : (float)i / Mathf.Max(u - 1, 1);
                    points.Add(Manifolds.Evaluate(surface, s, uu, vv, time));
                }
            }
            Edges(u, v, wrapU, wrapV, edges, wrapU && Manifolds.FlipsAcrossUSeam(surface, s));
        }

        /// <param name="flipU">
        /// The u-seam glues (1, v) to (0, 1 − v) (Klein bottle, odd Möbius band), so the closing edge of
        /// row j lands on the mirrored row: v − 1 − j on an open v-axis, (v − j) mod v on a wrapping one.
        /// Joining row j to row j there drew a chord straight across the surface.
        /// </param>
        public static void Edges(int u, int v, bool wrapU, bool wrapV, List<int> edges, bool flipU = false)
        {
            for (int j = 0; j < v; j++)
            for (int i = 0; i < u; i++)
            {
                int here = j * u + i;
                if (i < u - 1) { edges.Add(here); edges.Add(here + 1); }
                else if (wrapU)
                {
                    int row = !flipU ? j : wrapV ? (v - j) % v : v - 1 - j;
                    edges.Add(here); edges.Add(row * u);
                }
                if (wrapV || j < v - 1) { edges.Add(here); edges.Add(((j + 1) % v) * u + i); }
            }
        }
    }
}
