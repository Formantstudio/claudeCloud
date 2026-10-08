using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PsychedelicLab.GeometryFX;

/// <summary>
/// Plan §5: no surface emits NaN or infinity under any valid parameter input. Every ManifoldSettings
/// field is randomised inside its declared [Range] / [Min] (or a generous default where it has none),
/// and every surface is evaluated over its whole (u, v) square, edges and corners included.
/// </summary>
public class ManifoldSweep
{
    static readonly System.Random Rng = new System.Random(20261008);

    static float Pick(float lo, float hi, int mode) =>
        mode == 0 ? lo : mode == 1 ? hi : lo + (float)Rng.NextDouble() * (hi - lo);

    /// <summary>mode 0: every field at its minimum, 1: maximum, 2+: random.</summary>
    public static ManifoldSettings RandomSettings(int mode)
    {
        var s = new ManifoldSettings();
        foreach (var f in typeof(ManifoldSettings).GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var range = f.GetCustomAttribute<UnityEngine.RangeAttribute>();
            var min = f.GetCustomAttribute<UnityEngine.MinAttribute>();
            float lo = range != null ? range.min : min != null ? min.min : -4f;
            float hi = range != null ? range.max : min != null ? Math.Max(min.min * 50f, min.min + 20f) : 4f;
            if (f.FieldType == typeof(float)) f.SetValue(s, Pick(lo, hi, mode));
            else if (f.FieldType == typeof(int)) f.SetValue(s, (int)Math.Round(Pick(lo, hi, mode)));
            else if (f.FieldType == typeof(bool)) f.SetValue(s, false);   // useSharedAxis: no rig in tests
            else if (f.FieldType == typeof(Vector2)) f.SetValue(s, new Vector2(Pick(lo, hi, mode), Pick(lo, hi, mode)));
            else if (f.FieldType == typeof(Vector3)) f.SetValue(s, new Vector3(Pick(lo, hi, mode), Pick(lo, hi, mode), Pick(lo, hi, mode)));
            else if (f.FieldType.IsEnum)
            {
                var values = Enum.GetValues(f.FieldType);
                f.SetValue(s, values.GetValue(mode == 1 ? values.Length - 1 : mode == 0 ? 0 : Rng.Next(values.Length)));
            }
        }
        // The supershape exponents have no declared range; keep them to the physically meaningful side.
        s.superN = new Vector3(Mathf.Abs(s.superN.x) + .05f, Mathf.Abs(s.superN.y), Mathf.Abs(s.superN.z));
        s.superM = new Vector2(Mathf.Abs(s.superM.x) * 3f, Mathf.Abs(s.superM.y) * 3f);
        return s;
    }

    static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                                      float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));

    [Test]
    public void EverySurfaceIsFiniteEverywhere()
    {
        var failures = new List<string>();
        foreach (ManifoldSurface surface in Enum.GetValues(typeof(ManifoldSurface)))
        {
            int bad = 0; string first = null;
            for (int trial = 0; trial < 60 && bad == 0; trial++)
            {
                var s = RandomSettings(trial);
                float time = trial * .73f;
                for (int i = 0; i <= 32 && bad == 0; i++)
                for (int j = 0; j <= 32; j++)
                {
                    float u = i / 32f, v = j / 32f;
                    var p = Manifolds.Evaluate(surface, s, u, v, time);
                    if (Finite(p)) continue;
                    bad++;
                    first = $"u={u} v={v} trial={trial} -> {p}";
                    break;
                }
            }
            if (bad > 0) failures.Add(surface + ": " + first);
        }
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    [Test]
    public void CalabiYauIsFiniteEverywhere()
    {
        for (int n = 2; n <= 8; n++)
            for (int patch = 0; patch < n * n; patch++)
                for (int i = 0; i <= 16; i++)
                    for (int j = 0; j <= 16; j++)
                        Assert.IsTrue(Finite(Manifolds.CalabiYau(n, patch, i / 16f, j / 16f, .7f, 2.4f)), $"n={n} patch={patch} {i},{j}");
    }
}

