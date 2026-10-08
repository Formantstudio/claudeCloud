using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PsychedelicLab.GeometryFX;

/// <summary>
/// Plan P2: every surface built through the real chamber, welded, and measured with TopologyReport,
/// against the topology its parameterisation is meant to have; and the WrapsU / WrapsV / HasPoles
/// flags checked against what the formulas actually do at their seams, across random settings.
/// </summary>
public class ChamberTopology
{
    public static CurvedGeometryChamber Build(ManifoldSurface surface, int sides = 37, int rings = 23)
    {
        var c = new GameObject(surface.ToString()).AddComponent<CurvedGeometryChamber>();
        c.chamberMaterial = new Material(null);
        c.mode = CurvedGeometryChamber.Mode.Manifold;
        c.from = c.to = surface;
        c.sides = sides; c.rings = rings;
        c.animate = false;
        c.shape.useSharedAxis = false;
        c.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, null);
        return c;
    }

    public static TopologyReport Measure(CurvedGeometryChamber c)
    {
        return Weld((Vector3[])typeof(CurvedGeometryChamber).GetField("vertices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c));
    }

    /// <summary>
    /// Seams evaluate the same point from both ends to float precision, so a tight weld still closes
    /// them; a looser one also merges the near-coincident lattice points on either side of a cuspidal
    /// line (Kuen at U = 0, Henneberg's collapsed U = 0 circle) and reports slivers.
    /// </summary>
    internal static TopologyReport Weld(Vector3[] verts)
    {
        var idx = new int[verts.Length];
        for (int i = 0; i < idx.Length; i++) idx[i] = i;
        float scale = 0f;
        foreach (var v in verts) scale = Mathf.Max(scale, v.magnitude);
        return TopologyReport.Measure(verts, idx, Mathf.Max(scale, 1e-3f) * 4e-6f);
    }

    // What the chamber's grid of each surface is, as a surface with boundary. Pole-inset surfaces lose
    // a half-cell cap at each pole, so a sphere arrives as an annulus (χ 0, two loops) by design.
    internal enum Kind { ClosedTorus, KleinBottle, Disk, Annulus, MobiusBand, PoleInsetSphere }

    internal static Kind Expected(ManifoldSurface s)
    {
        switch (s)
        {
            case ManifoldSurface.Torus: case ManifoldSurface.TorusKnotTube: case ManifoldSurface.CliffordTorus:
            case ManifoldSurface.HornTorus: case ManifoldSurface.TwistedTorus: case ManifoldSurface.FigureEightTorus:
                return Kind.ClosedTorus;
            case ManifoldSurface.KleinBottle:
                return Kind.KleinBottle;
            // Open sheets, and Dini / Bour, whose u-ends do not meet (Bour's centre is inset, which
            // with its slit leaves a disk).
            case ManifoldSurface.Helicoid: case ManifoldSurface.Enneper: case ManifoldSurface.KuenSurface:
            case ManifoldSurface.BreatherSurface: case ManifoldSurface.MonkeySaddle: case ManifoldSurface.WhitneyUmbrella:
            case ManifoldSurface.DinisSurface: case ManifoldSurface.BoursSurface:
                return Kind.Disk;
            case ManifoldSurface.MobiusStrip: case ManifoldSurface.TrefoilRibbon:
                return Kind.MobiusBand;
            case ManifoldSurface.Sphere: case ManifoldSurface.Superellipsoid: case ManifoldSurface.Supershape:
            case ManifoldSurface.ConicalSpiral: case ManifoldSurface.BoysSurface: case ManifoldSurface.RomanSurface:
            case ManifoldSurface.CrossCap:
                return Kind.PoleInsetSphere;
            // Cylinder, Catenoid, Pseudosphere, Hyperboloid, Henneberg, and the default duocylinder
            // (a filled cell runs rim to rim, its full-fill rim inset like a pole).
            default:
                return Kind.Annulus;
        }
    }

    internal static void Shape(Kind k, out int chi, out int loops, out bool orientable)
    {
        switch (k)
        {
            case Kind.ClosedTorus: chi = 0; loops = 0; orientable = true; return;
            case Kind.KleinBottle: chi = 0; loops = 0; orientable = false; return;
            case Kind.Disk: chi = 1; loops = 1; orientable = true; return;
            case Kind.MobiusBand: chi = 0; loops = 1; orientable = false; return;
            default: chi = 0; loops = 2; orientable = true; return;
        }
    }

    internal static IEnumerable<ManifoldSurface> Surfaces()
    {
        foreach (ManifoldSurface s in Enum.GetValues(typeof(ManifoldSurface))) yield return s;
    }

    [TestCaseSource(nameof(Surfaces))]
    public void ChamberMeshHasTheSurfacesTopology(ManifoldSurface surface)
    {
        // Odd counts on both axes keep lattice lines off the midlines, where real singularities sit (the
        // horn torus pinch at v = ½, the Whitney and Kuen pinch points) and where the 2:1 Roman, cross-cap
        // and Henneberg maps would land two grid points on one position and over-weld.
        foreach (var (sides, rings) in new[] { (37, 23), (61, 41) })
        {
            AssertTopology(surface, Measure(Build(surface, sides, rings)), $"{surface} {sides}x{rings}");
        }
    }

    internal static void AssertTopology(ManifoldSurface surface, TopologyReport t, string label)
    {
        {
            Shape(Expected(surface), out int chi, out int loops, out bool orientable);
            string at = $"{label}: {t}";
            Assert.AreEqual(0, t.degenerateFaces, at);
            Assert.AreEqual(0, t.nonFinite, at);
            Assert.AreEqual(0, t.nonManifoldEdges, at);
            Assert.AreEqual(1, t.components, at);
            Assert.AreEqual(chi, t.EulerCharacteristic, at);
            Assert.AreEqual(loops, t.boundaryLoops, at);
            Assert.AreEqual(orientable, t.IsOrientable, at);
        }
    }

    static float Scale(ManifoldSurface f, ManifoldSettings s)
    {
        float scale = 1e-6f;
        for (int i = 0; i <= 16; i++)
            for (int j = 0; j <= 16; j++)
                scale = Mathf.Max(scale, Manifolds.Evaluate(f, s, i / 16f, j / 16f, 0f).magnitude);
        return scale;
    }

    [Test]
    public void SeamFlagsMatchTheFormulas()
    {
        var failures = new List<string>();
        foreach (var f in Surfaces())
        {
            for (int trial = 0; trial < 40; trial++)
            {
                var s = trial == 0 ? new ManifoldSettings { useSharedAxis = false } : ManifoldSweep.RandomSettings(trial + 2);
                float tol = 2e-3f * Scale(f, s);
                bool wrapU = Manifolds.WrapsU(f, s), wrapV = Manifolds.WrapsV(f, s);
                bool flips = Manifolds.FlipsAcrossUSeam(f, s), poles = Manifolds.HasPoles(f, s);
                float du = 0f, dv = 0f, row0 = 0f, row1 = 0f;
                Vector3 p0 = Manifolds.Evaluate(f, s, 0f, 0f, 0f), p1 = Manifolds.Evaluate(f, s, 0f, 1f, 0f);
                for (int i = 0; i <= 48; i++)
                {
                    float t = i / 48f;
                    du = Mathf.Max(du, (Manifolds.Evaluate(f, s, 1f, t, 0f) - Manifolds.Evaluate(f, s, 0f, flips ? 1f - t : t, 0f)).magnitude);
                    dv = Mathf.Max(dv, (Manifolds.Evaluate(f, s, t, 1f, 0f) - Manifolds.Evaluate(f, s, t, 0f, 0f)).magnitude);
                    row0 = Mathf.Max(row0, (Manifolds.Evaluate(f, s, t, 0f, 0f) - p0).magnitude);
                    row1 = Mathf.Max(row1, (Manifolds.Evaluate(f, s, t, 1f, 0f) - p1).magnitude);
                }
                string at = $"{f} trial {trial}";
                if (wrapU && du > tol) failures.Add($"{at}: WrapsU{(flips ? " (flipped)" : "")} but the u-seam opens by {du / tol * 2e-3f:E1} of scale");
                if (wrapV && dv > tol) failures.Add($"{at}: WrapsV but the v-seam opens by {dv / tol * 2e-3f:E1} of scale");
                if (poles != (row0 < tol || row1 < tol)) failures.Add($"{at}: HasPoles {poles} but end rows span {row0 / tol * 2e-3f:E1} / {row1 / tol * 2e-3f:E1} of scale");
                // Open flags are only claims about the default shape: random settings can close a
                // sheet by accident (a zero amplitude, say), which costs nothing but a duplicate line.
                if (trial == 0 && !wrapU && du < tol) failures.Add($"{at}: u-seam closes but WrapsU is false");
                if (trial == 0 && !wrapV && dv < tol && !poles) failures.Add($"{at}: v-seam closes but WrapsV is false");
                if (failures.Count > 40) break;
            }
        }
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }
}
