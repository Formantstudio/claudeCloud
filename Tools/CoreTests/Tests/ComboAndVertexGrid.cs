using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PsychedelicLab.GeometryFX;

/// <summary>
/// The other two surface grids: ManifoldComboChamber's per-layer quad lattice, which must give the
/// same topology as the chamber, and the node-and-edge vertex grid GeometryFractalEngine draws, whose
/// closing edges must follow each seam's gluing.
/// </summary>
public class ComboAndVertexGrid
{
    static Vector3[] ComboLayer(ManifoldSurface surface, int u, int v)
    {
        var combo = new GameObject("combo").AddComponent<ManifoldComboChamber>();
        combo.applyPreset = ManifoldComboChamber.Preset.Custom;
        combo.wireMaterial = new Material(null);
        combo.animate = false;
        var layer = new ComboLayer { surface = surface, morphTo = surface, uResolution = u, vResolution = v };
        layer.shape.useSharedAxis = false;
        combo.layers.Add(layer);
        combo.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(combo, null);
        var built = (IList)typeof(ManifoldComboChamber).GetField("built", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combo);
        var b = built[0];
        return (Vector3[])b.GetType().GetField("vertices").GetValue(b);
    }

    [TestCaseSource(typeof(ChamberTopology), nameof(ChamberTopology.Surfaces))]
    public void ComboLayersHaveTheSurfacesTopology(ManifoldSurface surface)
    {
        ChamberTopology.AssertTopology(surface, ChamberTopology.Weld(ComboLayer(surface, 37, 23)), $"combo {surface} 37x23");
    }

    [Test]
    public void ComboLayerMatchesTheChamberLattice()
    {
        // Same surface, same grid, no morph, kaleidoscope or bubbles: the two builders agree vertex
        // for vertex (the chamber's radius and extent are its own; the combo uses the layer's shape).
        foreach (var surface in new[] { ManifoldSurface.KleinBottle, ManifoldSurface.Enneper, ManifoldSurface.Sphere })
        {
            var combo = ComboLayer(surface, 37, 23);
            var s = new ManifoldSettings { useSharedAxis = false };
            for (int j = 0; j < 23; j++)
            for (int i = 0; i < 37; i++)
            {
                float inset = Manifolds.HasPoles(surface, s) ? .5f / 23 : 0f;
                Vector3 expected = Manifolds.Evaluate(surface, s, i / 37f, Mathf.Lerp(inset, 1f - inset, j / 23f), 0f);
                Assert.Less((combo[(j * 37 + i) * 6] - expected).magnitude, 1e-4f, $"{surface} node {i},{j}");
            }
        }
    }

    [TestCaseSource(typeof(ChamberTopology), nameof(ChamberTopology.Surfaces))]
    public void VertexGridSeamEdgesAreAsShortAsItsOtherEdges(ManifoldSurface surface)
    {
        var s = new ManifoldSettings { useSharedAxis = false };
        foreach (var (u, v) in new[] { (41, 23), (72, 40) })
        {
            var points = new List<Vector3>();
            var edges = new List<int>();
            ManifoldVertexGrid.Build(surface, s, u, v, points, edges);
            Assert.AreEqual(u * v, points.Count);
            // A seam edge spans one grid step like any other, so it may not be much longer than the
            // longest interior edge in its own row or column neighbourhood; a mis-glued seam is a chord.
            float interior = 0f, seam = 0f;
            for (int e = 0; e < edges.Count; e += 2)
            {
                int a = edges[e], b = edges[e + 1];
                float len = (points[a] - points[b]).magnitude;
                bool wraps = Mathf.Abs(a % u - b % u) > 1 || Mathf.Abs(a / u - b / u) > 1;
                if (wraps) seam = Mathf.Max(seam, len); else interior = Mathf.Max(interior, len);
            }
            Assert.LessOrEqual(seam, interior * 1.5f + 1e-4f, $"{surface} {u}x{v}");
        }
    }

    [Test]
    public void FlippedSeamJoinsMirroredRows()
    {
        var edges = new List<int>();
        ManifoldVertexGrid.Edges(4, 5, true, false, edges, flipU: true);
        var pairs = new HashSet<(int, int)>();
        for (int e = 0; e < edges.Count; e += 2) pairs.Add((edges[e], edges[e + 1]));
        for (int j = 0; j < 5; j++) Assert.IsTrue(pairs.Contains((j * 4 + 3, (4 - j) * 4)), "open v: row " + j);
        edges.Clear(); pairs.Clear();
        ManifoldVertexGrid.Edges(4, 5, true, true, edges, flipU: true);
        for (int e = 0; e < edges.Count; e += 2) pairs.Add((edges[e], edges[e + 1]));
        for (int j = 0; j < 5; j++) Assert.IsTrue(pairs.Contains((j * 4 + 3, ((5 - j) % 5) * 4)), "wrapping v: row " + j);
        Assert.AreEqual(2 * 4 * 5 * 2, edges.Count);
    }
}

/// <summary>Particles and wire agree: SampleGrid at a lattice node is that node's vertex, for every surface.</summary>
public class ChamberSampling
{
    [TestCaseSource(typeof(ChamberTopology), nameof(ChamberTopology.Surfaces))]
    public void SampleGridHitsTheLatticeNodes(ManifoldSurface surface)
    {
        const int S = 37, R = 23;
        var c = ChamberTopology.Build(surface, S, R);
        var verts = (Vector3[])typeof(CurvedGeometryChamber).GetField("vertices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);
        for (int r = 0; r < R; r++)
        for (int s = 0; s < S; s++)
        {
            Vector3 node = verts[(r * S + s) * 6];
            Assert.Less((c.SampleGrid((float)s / S, (float)r / R) - node).magnitude, 1e-5f * (1f + node.magnitude), $"{surface} node {s},{r}");
        }
    }
}
