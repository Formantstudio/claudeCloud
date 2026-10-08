using NUnit.Framework;
using UnityEngine;
using PsychedelicLab.GeometryFX;
using PsychedelicLab.EscherWorld;

public class FieldMerge
{
    [TestCase(ImplicitShape.Gyroid, RoomField.Gyroid)]
    [TestCase(ImplicitShape.SchwarzP, RoomField.SchwarzP)]
    [TestCase(ImplicitShape.SchwarzD, RoomField.SchwarzD)]
    [TestCase(ImplicitShape.Neovius, RoomField.Neovius)]
    [TestCase(ImplicitShape.Lidinoid, RoomField.Lidinoid)]
    [TestCase(ImplicitShape.SplitP, RoomField.SplitP)]
    public void RoomAndEngineFieldsAgreeUnderEveryWarp(ImplicitShape shape, RoomField room)
    {
        var spaces = new[]
        {
            new EscherSpace(),
            new EscherSpace { invert = true, inversionRadius = .8f },
            new EscherSpace { droste = true, drosteTwist = 1, drosteSectors = 5 },
            new EscherSpace { scroll = new Vector3(.3f, .1f, .7f), scrollVelocity = new Vector3(0, .2f, 0) },
        };
        foreach (var space in spaces)
        {
            var engine = new ImplicitSettings { shape = shape, frequency = 2f, dislocation = 1f, dislocationAxis = Axis3.Y, dislocationCore = .25f, space = space };
            var rooms = new EscherFieldSettings { field = room, frequency = 2f, thickness = 0f, w = 0f, wInfluence = 0f,
                                                  dislocation = 1f, dislocationAxis = RoomAxis.Y, dislocationCore = .25f, space = space };
            float worst = 0f;
            for (int i = 0; i < 100; i++)
            {
                var p = new Vector3(Mathf.Sin(i * 1.7f), Mathf.Cos(i * .9f), Mathf.Sin(i * .37f + 1f)) * .9f;
                worst = Mathf.Max(worst, Mathf.Abs(Implicits.Field(engine, p, 1.3f) - EscherFields.Sample(rooms, p, 0f, 1.3f)));
            }
            System.Console.WriteLine($"MERGE {shape} invert={space.invert} droste={space.droste} scroll={space.scroll}: worst {worst:E2}");
            Assert.Less(worst, 1e-4f, shape + " differs between the engine and the rooms");
        }
    }
}
