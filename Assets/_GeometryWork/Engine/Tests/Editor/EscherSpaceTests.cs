using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class EscherSpaceTests
    {
        static readonly ImplicitShape[] Tpms =
        {
            ImplicitShape.Gyroid, ImplicitShape.SchwarzP, ImplicitShape.SchwarzD,
            ImplicitShape.Neovius, ImplicitShape.Lidinoid, ImplicitShape.SplitP
        };

        static ImplicitSettings Droste(ImplicitShape shape, int twist, int sectors, float scale = 4f) => new ImplicitSettings
        {
            shape = shape, frequency = 2f,
            space = new EscherSpace { droste = true, drosteTwist = twist, drosteSectors = sectors, drosteScale = scale, drosteAxis = Axis3.Z }
        };

        [Test]
        public void InversionIsAnInvolutionThatFixesItsSphere()
        {
            var c = new Vector3(.1f, -.2f, .3f);
            foreach (var p in new[] { new Vector3(.5f, .2f, -.7f), new Vector3(-1.3f, .4f, .9f), new Vector3(.02f, .01f, .3f) })
                Assert.Less((EscherSpace.Invert(EscherSpace.Invert(p, c, .8f), c, .8f) - p).magnitude, 1e-4f);
            var onSphere = c + new Vector3(.6f, 0f, .8f) * .8f;
            Assert.Less((EscherSpace.Invert(onSphere, c, .8f) - onSphere).magnitude, 1e-5f);
            Assert.IsTrue(TopologyReport.IsFinite(EscherSpace.Invert(c, c, .8f)), "the centre must stay finite");
        }

        [Test]
        public void InversionIsConformal()
        {
            // Two small orthogonal steps stay orthogonal after the map.
            var c = Vector3.zero;
            var p = new Vector3(.4f, .3f, -.2f);
            Vector3 a = new Vector3(1e-3f, 0, 0), b = new Vector3(0, 0, 1e-3f);
            // Central differences, so the check measures the map rather than the stencil.
            Vector3 da = EscherSpace.Invert(p + a, c, 1f) - EscherSpace.Invert(p - a, c, 1f);
            Vector3 db = EscherSpace.Invert(p + b, c, 1f) - EscherSpace.Invert(p - b, c, 1f);
            Assert.AreEqual(0f, Vector3.Dot(da.normalized, db.normalized), 2e-3f);
            Assert.AreEqual(1f, da.magnitude / db.magnitude, 2e-3f, "equal scaling in every direction");
        }

        [Test]
        public void DrosteFieldsAreExactlySelfSimilar()
        {
            foreach (var shape in Tpms)
                foreach (int twist in new[] { 0, 1, -2 })
                {
                    var s = Droste(shape, twist, 6);
                    for (int i = 0; i < 40; i++)
                    {
                        var p = new Vector3(.3f + .01f * i, -.2f + .013f * i, .05f * Mathf.Sin(i));
                        float a = Implicits.Field(s, p, 0f), b = Implicits.Field(s, p * 4f, 0f);
                        Assert.AreEqual(a, b, 2e-3f, shape + " twist " + twist + " not invariant under scaling by 4 at " + p);
                    }
                }
        }

        [Test]
        public void DrosteSeamIsInvisibleForWholeNumbers()
        {
            foreach (var shape in Tpms)
                foreach (int twist in new[] { -1, 0, 1, 2 })
                    foreach (int sectors in new[] { 3, 5, 8 })
                    {
                        var s = Droste(shape, twist, sectors);
                        float worst = 0f;
                        for (int i = 0; i < 48; i++)
                        {
                            float r = .1f + i * .02f, z = -.4f + i * .017f;
                            worst = Mathf.Max(worst, Mathf.Abs(Implicits.Field(s, new Vector3(-r, 1e-6f, z), 0f) -
                                                               Implicits.Field(s, new Vector3(-r, -1e-6f, z), 0f)));
                        }
                        Assert.Less(worst, 5e-3f, shape + " twist " + twist + " sectors " + sectors);
                    }
        }

        [Test]
        public void ScrollWrapsExactlyAndClimbsTheStaircase()
        {
            var baseSettings = new ImplicitSettings { shape = ImplicitShape.Gyroid, frequency = 2f, dislocation = 1f, dislocationAxis = Axis3.Z };
            var whole = new ImplicitSettings { shape = ImplicitShape.Gyroid, frequency = 2f, dislocation = 1f, dislocationAxis = Axis3.Z,
                                               space = new EscherSpace { scroll = new Vector3(0, 0, 3f) } };
            var climbing = new ImplicitSettings { shape = ImplicitShape.Gyroid, frequency = 2f, dislocation = 1f, dislocationAxis = Axis3.Z,
                                                  space = new EscherSpace { scrollVelocity = new Vector3(0, 0, .25f) } };
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector3(.4f - .03f * i, .2f + .02f * i, -.5f + .04f * i);
                float f0 = Implicits.Field(baseSettings, p, 0f);
                Assert.AreEqual(f0, Implicits.Field(whole, p, 0f), 1e-4f, "a whole number of periods is the same room");
                // Four seconds at a quarter period per second is one full floor: back where it started.
                Assert.AreEqual(Implicits.Field(climbing, p, 0f), Implicits.Field(climbing, p, 4f), 1e-4f);
                // And continuous through the wrap itself.
                Assert.AreEqual(Implicits.Field(climbing, p, 3.9999f), Implicits.Field(climbing, p, 4.0001f), 2e-3f);
            }
        }

        /// <summary>Largest field jump across the window's atan2 cut (negative x, y = ±ε) over a range of radii.</summary>
        static float WindowSeam(ImplicitSettings s, float rMin, float rMax)
        {
            float worst = 0f;
            for (int i = 0; i < 80; i++)
            {
                float r = Mathf.Lerp(rMin, rMax, i / 79f), z = -.6f + i * .015f;
                worst = Mathf.Max(worst, Mathf.Abs(Implicits.Field(s, new Vector3(-r, 1e-6f, z), 0f) -
                                                   Implicits.Field(s, new Vector3(-r, -1e-6f, z), 0f)));
            }
            return worst;
        }

        [Test]
        public void DislocationIsSeamlessInsideItsCoreToo()
        {
            // Easing the shear by the core weight left a crack along the cut inside the core: the
            // jump there was ease * periods, not a whole number. The core now blends fields instead.
            foreach (var shape in Tpms)
            {
                var s = new ImplicitSettings { shape = shape, frequency = 2f, dislocation = 1f, dislocationAxis = Axis3.Z, dislocationCore = .4f };
                Assert.Less(WindowSeam(s, .01f, .39f), 5e-3f, shape + " cracks inside the core");
            }
        }

        [Test]
        public void DrosteWithAStaircaseStaysSeamlessAndSelfSimilar()
        {
            foreach (int d in new[] { 1, -1, 2 })
            {
                var s = Droste(ImplicitShape.Gyroid, 1, 6);
                s.dislocation = d;
                Assert.Less(WindowSeam(s, .1f, .9f), 5e-3f, "seam with dislocation " + d);
                for (int i = 0; i < 30; i++)
                {
                    var p = new Vector3(.25f + .01f * i, -.15f + .011f * i, .04f * Mathf.Cos(i));
                    Assert.AreEqual(Implicits.Field(s, p, 0f), Implicits.Field(s, p * 4f, 0f), 2e-3f, "scale invariance with dislocation " + d);
                }
            }
        }

        [Test]
        public void InversionWithAStaircaseStaysSeamless()
        {
            var s = new ImplicitSettings { shape = ImplicitShape.Gyroid, frequency = 2f, dislocation = 1f, dislocationAxis = Axis3.Z, dislocationCore = .1f,
                                           space = new EscherSpace { invert = true, inversionRadius = .6f } };
            Assert.Less(WindowSeam(s, .1f, .9f), 5e-3f);
        }

        [Test]
        public void InactiveSpaceIsIdentity()
        {
            var a = new ImplicitSettings { shape = ImplicitShape.Neovius };
            var b = new ImplicitSettings { shape = ImplicitShape.Neovius, space = null };
            var p = new Vector3(.3f, -.1f, .6f);
            Assert.AreEqual(Implicits.Field(a, p, 0f), Implicits.Field(b, p, 0f));
            Assert.IsFalse(new EscherSpace().Active);
        }

        [Test]
        public void SignatureTracksEverySetting()
        {
            var s = new EscherSpace();
            int h = s.Signature();
            s.drosteSectors++;
            Assert.AreNotEqual(h, s.Signature());
            h = s.Signature();
            s.scrollVelocity = new Vector3(0, .1f, 0);
            Assert.AreNotEqual(h, s.Signature());
        }

        [Test]
        public void InvertedMandelbulbStaysFinite()
        {
            var s = new ImplicitSettings { shape = ImplicitShape.Mandelbulb, space = new EscherSpace { invert = true, inversionRadius = .7f } };
            for (int i = 0; i < 200; i++)
            {
                var p = new Vector3(Mathf.Sin(i * 1.3f), Mathf.Cos(i * .7f), Mathf.Sin(i * .31f)) * (i / 200f);
                Assert.IsFalse(float.IsNaN(Implicits.Field(s, p, 0f)), p.ToString());
            }
        }
    }
}
