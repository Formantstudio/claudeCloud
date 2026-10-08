using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    public class ScrewDislocationTests
    {
        static readonly ImplicitShape[] Tpms =
        {
            ImplicitShape.Gyroid, ImplicitShape.SchwarzP, ImplicitShape.SchwarzD,
            ImplicitShape.Neovius, ImplicitShape.Lidinoid, ImplicitShape.SplitP
        };

        /// <summary>Largest jump in the field across the atan2 branch cut, outside the core.</summary>
        static float SeamJump(ImplicitShape shape, Axis3 axis, float dislocation, float frequency)
        {
            var s = new ImplicitSettings { shape = shape, frequency = frequency, dislocation = dislocation,
                                           dislocationAxis = axis, dislocationCore = .1f };
            float worst = 0f;
            for (int i = 0; i < 64; i++)
            {
                // The cut is at azimuth ±π: negative first coordinate, second coordinate ±ε.
                float r = .2f + i * .012f, h = -.9f + i * .028f;
                var above = ScrewDislocation.FromAxis(new Vector3(-r, 1e-5f, h), axis);
                var below = ScrewDislocation.FromAxis(new Vector3(-r, -1e-5f, h), axis);
                worst = Mathf.Max(worst, Mathf.Abs(Implicits.Field(s, above, 0f) - Implicits.Field(s, below, 0f)));
            }
            return worst;
        }

        [Test]
        public void WholeNumberDislocationsAreSeamlessOnEveryTpmsAndAxis()
        {
            foreach (var shape in Tpms)
                foreach (Axis3 axis in System.Enum.GetValues(typeof(Axis3)))
                    foreach (float d in new[] { 1f, -1f, 2f })
                        foreach (float f in new[] { 1f, 2f, 3.5f })
                            Assert.Less(SeamJump(shape, axis, d, f), 5e-3f,
                                        shape + " axis " + axis + " dislocation " + d + " frequency " + f);
        }

        [Test]
        public void TheSeamIsRealWhenThePeriodIsWrong()
        {
            // Guard against the test passing vacuously: a fractional dislocation must tear.
            Assert.Greater(SeamJump(ImplicitShape.Gyroid, Axis3.Z, .5f, 2f), .05f);
        }

        [Test]
        public void ZeroDislocationIsIdentity()
        {
            var p = new Vector3(.3f, -.4f, .7f);
            Assert.AreEqual(p, ScrewDislocation.Apply(p, Axis3.Y, 0f, 1f, .1f));
        }

        [Test]
        public void AxisPermutationRoundTrips()
        {
            var p = new Vector3(.3f, -.4f, .7f);
            foreach (Axis3 a in System.Enum.GetValues(typeof(Axis3)))
                Assert.AreEqual(p, ScrewDislocation.FromAxis(ScrewDislocation.ToAxis(p, a), a));
        }

        [Test]
        public void OneCircuitClimbsExactlyTheRequestedPeriods()
        {
            // Outside the core the shear is exactly dislocation · period · azimuth / 2π.
            float period = ScrewDislocation.TpmsPeriod(2f);
            var p = ScrewDislocation.Apply(new Vector3(0f, 1f, 0f), Axis3.Z, 3f, period, .1f);
            Assert.AreEqual(3f * period * .25f, p.z, 1e-6f);
        }

        [Test]
        public void CoreFadesTheShearOnTheAxis()
        {
            var p = ScrewDislocation.Apply(new Vector3(0f, 0f, .5f), Axis3.Z, 2f, 1f, .2f);
            Assert.AreEqual(.5f, p.z, 1e-7f);
            Assert.IsTrue(TopologyReport.IsFinite(p));
        }
    }
}
