using NUnit.Framework;
using UnityEngine;

/// <summary>The stand-in's own maths, where tests depend on it matching Unity.</summary>
public class ShimSanity
{
    [Test]
    public void EulerAnglesRoundTrip()
    {
        var rng = new System.Random(5);
        for (int i = 0; i < 200; i++)
        {
            var e = new Vector3((float)rng.NextDouble() * 170f - 85f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
            var q = Quaternion.Euler(e);
            var back = Quaternion.Euler(q.eulerAngles);
            foreach (var v in new[] { Vector3.right, Vector3.up, Vector3.forward })
                Assert.Less((q * v - back * v).magnitude, 1e-4f, e.ToString());
        }
    }

    [Test]
    public void FromToRotationTakesFromOntoTo()
    {
        var rng = new System.Random(6);
        for (int i = 0; i < 200; i++)
        {
            var a = new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f);
            var b = new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f);
            Assert.Less((Quaternion.FromToRotation(a, b) * a.normalized - b.normalized).magnitude, 1e-4f);
        }
        Assert.Less((Quaternion.FromToRotation(Vector3.up, Vector3.down) * Vector3.up - Vector3.down).magnitude, 1e-4f);
    }
}
