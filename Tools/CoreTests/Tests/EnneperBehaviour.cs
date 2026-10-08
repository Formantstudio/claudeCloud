using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PsychedelicLab.GeometryFX;

public class EnneperBehaviour
{
    static void Call(object o, string m) =>
        o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, null);
    static List<CurvedGeometryChamber> Folds(EnneperFoldReality r) =>
        (List<CurvedGeometryChamber>)typeof(EnneperFoldReality).GetField("folds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(r);

    static EnneperFoldReality Run(System.Action<EnneperFoldReality> setup)
    {
        var r = new GameObject("r").AddComponent<EnneperFoldReality>();
        r.foldMaterial = new Material(null);
        r.resolution = 97;
        setup(r);
        Call(r, "Update");
        foreach (var c in Folds(r)) Call(c, "Update");
        Call(r, "Update");
        foreach (var c in Folds(r)) Call(c, "Update");
        return r;
    }

    static Vector3 World(CurvedGeometryChamber c, float u, float v) =>
        c.transform.localPosition + c.transform.localRotation * (c.SampleGrid(u, v) * c.transform.localScale.x);

    [Test]
    public void HallCeilingsMeetOnSharedEdges()
    {
        var r = Run(x => { x.copiesPerLevel = 3; x.levels = 2; x.pillarClosure = 1; x.consistentCopies = true; x.spread = 6f; x.depthSpacing = 5f; });
        var folds = Folds(r);
        Assert.AreEqual(6, folds.Count);
        // copy 0 at column 0, copy 1 at column +1: they share the edge x = +3 (half of 6).
        var a = folds[0]; var b = folds[1];
        int shared = 0;
        foreach (float v in new[] { 0f, 1f })
        for (int i = 0; i <= 400; i++)
        {
            Vector3 p = World(a, i / 400f, v);
            if (Mathf.Abs(p.x - 3f) > 1e-3f) continue;
            shared++;
            // The same point must be on B's end ring: B's ring is the cell boundary around (6, 0, 0).
            float best = float.MaxValue;
            for (int j = 0; j <= 400; j++)
            {
                Vector3 q = World(b, j / 400f, v);
                // Distance from p to the chord q_j q_{j+1} of B's ring.
                Vector3 q1 = World(b, (j + 1) / 400f, v);
                Vector3 d = q1 - q;
                float t = Mathf.Clamp01(Vector3.Dot(p - q, d) / Mathf.Max(d.sqrMagnitude, 1e-12f));
                best = Mathf.Min(best, (q + d * t - p).magnitude);
            }
            Assert.Less(best, 2e-3f, "A's end point " + p + " is not on B's end ring");
        }
        Assert.Greater(shared, 40);
    }

    [Test]
    public void FlowerCrownIsExactlyNFoldSymmetric()
    {
        var r = Run(x => { x.copiesPerLevel = 5; x.levels = 1; x.pillarClosure = 0; x.consistentCopies = false;
                           x.arrangement = EnneperFoldReality.Arrangement.NestedFlower; x.phasePerCopy = 0; x.spread = 4f; });
        var folds = Folds(r);
        var turn = Quaternion.AngleAxis(72f, Vector3.forward);
        for (int k = 0; k < 4; k++)
            for (int i = 0; i <= 8; i++)
                for (int j = 0; j <= 8; j++)
                {
                    Vector3 p = World(folds[k], i / 8f, j / 8f), q = World(folds[k + 1], i / 8f, j / 8f);
                    Assert.Less((turn * p - q).magnitude, 1e-3f, "copy " + k + " turned 72° is not copy " + (k + 1));
                }
    }

    [Test]
    public void PillarFlutesFollowEachCopysOwnPhase()
    {
        // Two copies with different fold phases: their pillars must differ (the flutes turn with
        // the copy), whereas the old global phase made them identical.
        var r = Run(x => { x.copiesPerLevel = 2; x.levels = 1; x.pillarClosure = 1; x.consistentCopies = true; x.phasePerCopy = 45; x.foldFluting = .3f; });
        var folds = Folds(r);
        float diff = 0;
        for (int i = 0; i <= 16; i++)
            diff = Mathf.Max(diff, ((folds[0].SampleGrid(i / 16f, .5f)) - (folds[1].SampleGrid(i / 16f, .5f))).magnitude);
        Assert.Greater(diff, 1e-2f);
    }
}
