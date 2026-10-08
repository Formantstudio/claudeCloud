using System;
using NUnit.Framework;
using UnityEngine;
using PsychedelicLab.EscherWorld;

/// <summary>The threaded room build is the serial build, bit for bit, for every field and extraction.</summary>
public class RoomBuild
{
    static long Hash(EscherSurfaceBuilder.Scratch s)
    {
        long h = 17;
        unchecked
        {
            foreach (var v in s.vertices) h = h * 31 + BitConverter.SingleToInt32Bits(v.x) * 7 + BitConverter.SingleToInt32Bits(v.y) * 13 + BitConverter.SingleToInt32Bits(v.z);
            foreach (var v in s.normals) h = h * 31 + BitConverter.SingleToInt32Bits(v.x) + BitConverter.SingleToInt32Bits(v.y) * 3 + BitConverter.SingleToInt32Bits(v.z) * 5;
            foreach (var v in s.uv) h = h * 31 + BitConverter.SingleToInt32Bits(v.x) + BitConverter.SingleToInt32Bits(v.y) * 3;
            foreach (var t in s.triangles) h = h * 31 + t;
        }
        return h;
    }

    static int built;

    static long Build(RoomField kind, RoomExtraction extraction, bool threads)
    {
        bool was = EscherSurfaceBuilder.UseWorkerThreads;
        try
        {
            EscherSurfaceBuilder.UseWorkerThreads = threads;
            var field = new EscherFieldSettings { field = kind, dislocation = .7f, w = .3f };
            var scratch = new EscherSurfaceBuilder.Scratch();
            EscherSurfaceBuilder.Build(new Mesh(), scratch, field, extraction, 40, 5f, .4f, true, new Vector3(.3f, -.2f, .1f), .2f);
            if (scratch.vertices.Count > 0) built++;
            return Hash(scratch);
        }
        finally { EscherSurfaceBuilder.UseWorkerThreads = was; }
    }

    [Test]
    public void ThreadedBuildIsTheSerialBuild()
    {
        built = 0;
        int cases = 0;
        foreach (RoomField kind in Enum.GetValues(typeof(RoomField)))
        foreach (RoomExtraction extraction in Enum.GetValues(typeof(RoomExtraction)))
        {
            Assert.AreEqual(Build(kind, extraction, false), Build(kind, extraction, true), kind + " " + extraction);
            cases += 2;
        }
        // A bounded field can miss this box entirely; most must not, or the comparison proves nothing.
        Assert.Greater(built, cases * 3 / 4);
    }
}
