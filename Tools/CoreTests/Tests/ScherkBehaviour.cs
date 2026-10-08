using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PsychedelicLab.GeometryFX;

/// <summary>
/// Plan P3: the Scherk engine's claims, checked on the vertices its chambers actually emit.
/// </summary>
public class ScherkBehaviour
{
    static void Call(object o, string m) =>
        o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, null);

    static List<CurvedGeometryChamber> Towers(ScherkTowerEngine e) =>
        (List<CurvedGeometryChamber>)typeof(ScherkTowerEngine).GetField("towers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(e);

    static Vector3[] Vertices(CurvedGeometryChamber c) =>
        (Vector3[])typeof(CurvedGeometryChamber).GetField("vertices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);

    static ScherkTowerEngine Run(Action<ScherkTowerEngine> setup)
    {
        var e = new GameObject("scherk").AddComponent<ScherkTowerEngine>();
        e.towerMaterial = new Material(null);
        e.surfaceCellBudget = 32768;
        setup(e);
        Call(e, "Update");
        foreach (var c in Towers(e)) Call(c, "Update");
        return e;
    }

    static void Exact(ScherkTowerEngine e)
    {
        e.twistPerPeriod = 0f; e.waistHold = 0f; e.taper = 1f; e.towerForm = 1f; e.animateTower = false;
    }

    /// <summary>All vertices of every chart, in the engine's space.</summary>
    static List<Vector3> Points(ScherkTowerEngine e)
    {
        var all = new List<Vector3>();
        foreach (var c in Towers(e))
            foreach (var p in Vertices(c))
                all.Add(c.transform.localPosition + c.transform.localRotation * p);
        return all;
    }

    [TestCase(ScherkTowerEngine.TowerChart.Lobes, 1)]
    [TestCase(ScherkTowerEngine.TowerChart.Lobes, 2)]
    [TestCase(ScherkTowerEngine.TowerChart.Wings, 1)]
    [TestCase(ScherkTowerEngine.TowerChart.Wings, 2)]
    public void ExactTowerLiesOnSinhXSinhYEqualsSinZ(ScherkTowerEngine.TowerChart chart, int branches)
    {
        var e = Run(x => { x.arrangement = ScherkTowerEngine.Arrangement.SingleTower; x.chart = chart; x.branches = branches; Exact(x); });
        float worst = 0f;
        foreach (var p in Points(e))
        {
            // Undo the placement: surface units across, half periods of sin z up.
            float x = p.x / e.towerRadius, y = p.z / e.towerRadius;
            float z = (p.y / e.periodHeight + e.periods * .5f) * Mathf.PI;
            float lhs = (float)(Math.Sinh(x) * Math.Sinh(y)), rhs = Mathf.Sin(z);
            // Relative to the size of the terms: far out on a wing sinh x is large and sinh y small.
            float err = Mathf.Abs(lhs - rhs) / (1f + Mathf.Abs((float)Math.Sinh(x)) + Mathf.Abs((float)Math.Sinh(y)));
            worst = Mathf.Max(worst, err);
        }
        Assert.Less(worst, 2e-4f);
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(6)]
    [TestCase(11)]
    public void WingsTowerIsOneEmbeddedSurfaceWithNoPinch(int periods)
    {
        var e = Run(x => { x.arrangement = ScherkTowerEngine.Arrangement.SingleTower; x.chart = ScherkTowerEngine.TowerChart.Wings; x.branches = 2; x.periods = periods; Exact(x); });
        Assert.AreEqual(4, Towers(e).Count);
        var t = ChamberTopology.Weld(Points(e).ToArray());
        string at = t.ToString();
        Assert.AreEqual(0, t.degenerateFaces, at);
        Assert.AreEqual(0, t.nonManifoldEdges, at);
        Assert.AreEqual(1, t.components, at);
        Assert.IsTrue(t.IsOrientable, at);
        // Four disks (one per arm) glued along their corner edges: per half period two glued pairs,
        // and all four corners meet at each of the P + 1 waists. The glued locus has P + 1 vertices
        // and 2P edges, so chi = 4 - (4 - (1 - P)) = 1 - P.
        Assert.AreEqual(1 - periods, t.EulerCharacteristic, at);
    }

    [Test]
    public void LobesTowerHasNoZeroAreaRowsEvenWhenExact()
    {
        foreach (int periods in new[] { 1, 4, 6, 12 })
        {
            var e = Run(x => { x.arrangement = ScherkTowerEngine.Arrangement.SingleTower; x.chart = ScherkTowerEngine.TowerChart.Lobes; x.periods = periods; Exact(x); });
            foreach (var c in Towers(e))
            {
                var t = ChamberTopology.Weld(Vertices(c));
                Assert.AreEqual(0, t.degenerateFaces, $"periods {periods}: {t}");
            }
        }
    }

    [TestCase(ScherkTowerEngine.Arrangement.PillarHall, 3)]
    [TestCase(ScherkTowerEngine.Arrangement.PillarHall, 2)]
    [TestCase(ScherkTowerEngine.Arrangement.PillarHall, 4)]
    [TestCase(ScherkTowerEngine.Arrangement.Colonnade, 3)]
    [TestCase(ScherkTowerEngine.Arrangement.Colonnade, 4)]
    public void HallIsTheCompressedFirstSurfaceInWholeCells(ScherkTowerEngine.Arrangement arrangement, int columns)
    {
        var e = Run(x => { x.arrangement = arrangement; x.columns = columns; Exact(x); });
        Assert.AreEqual(1, Towers(e).Count);
        float H = e.wallHeight, worst = 0f;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        var pts = Points(e);
        foreach (var p in pts)
        {
            float x = p.x / e.towerRadius, y = p.z / e.towerRadius, h = p.y / e.periodHeight;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            float cx = Mathf.Abs(Mathf.Cos(x + ScherkTowerEngine.HallPhase(columns))), cy = Mathf.Abs(Mathf.Cos(y + ScherkTowerEngine.HallPhase(arrangement == ScherkTowerEngine.Arrangement.Colonnade ? 1 : columns)));
            if (cx < 1e-3f || cy < 1e-3f) continue;   // on a wall or a corner line: tanh has saturated
            worst = Mathf.Max(worst, Mathf.Abs(h - H * (float)Math.Tanh(Mathf.Log(cx / cy) / H)));
        }
        Assert.Less(worst, 1e-3f, "height law");
        // Whole cells: the hall's edges are walls, so its width is a whole number of periods.
        int along = arrangement == ScherkTowerEngine.Arrangement.Colonnade ? 1 : columns;
        Assert.AreEqual(columns * Mathf.PI, maxX - minX, 1e-3f);
        Assert.AreEqual(along * Mathf.PI, maxY - minY, 1e-3f);
        Assert.Less(Mathf.Abs(Mathf.Cos(minX + ScherkTowerEngine.HallPhase(columns))), 1e-3f, "left edge is a wall");
        Assert.Less(Mathf.Abs(Mathf.Cos(minY + ScherkTowerEngine.HallPhase(along))), 1e-3f, "near edge is a wall");
        // One connected sheet, a disk.
        var t = ChamberTopology.Weld(pts.ToArray());
        Assert.AreEqual(1, t.components, t.ToString());
        Assert.AreEqual(0, t.nonManifoldEdges, t.ToString());
        Assert.AreEqual(1, t.EulerCharacteristic, t.ToString());
    }
}
