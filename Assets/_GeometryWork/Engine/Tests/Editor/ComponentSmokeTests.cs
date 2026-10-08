using NUnit.Framework;
using UnityEngine;

namespace PsychedelicLab.GeometryFX.Tests
{
    /// <summary>
    /// Every engine component builds, emits no NaN or infinity, and its particle sampler stays
    /// finite across the whole (u, v) square — the pole-safety guarantee in Phase 5 of the plan.
    /// </summary>
    public class ComponentSmokeTests
    {
        static void Check<T>(System.Action<T> configure = null) where T : WireMeshComponent, IWireGeometry
        {
            var go = new GameObject("smoke " + typeof(T).Name);
            try
            {
                var c = go.AddComponent<T>();
                configure?.Invoke(c);
                c.Rebuild();
                var t = c.MeasureTopology();
                Assert.AreEqual(0, t.nonFinite, typeof(T).Name + ": " + t);
                Assert.Greater(t.faces, 0, typeof(T).Name + " built nothing");
                Assert.IsTrue(c.IsBuilt, typeof(T).Name + " reports unbuilt");
                for (int i = 0; i <= 16; i++)
                    for (int j = 0; j <= 16; j++)
                        Assert.IsTrue(TopologyReport.IsFinite(c.SampleGrid(i / 16f, j / 16f)),
                                      typeof(T).Name + " SampleGrid(" + i / 16f + ", " + j / 16f + ")");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test] public void Hopf() => Check<HopfFibrationEngine>();
        [Test] public void HopfWholeSphere() => Check<HopfFibrationEngine>(c => c.UseSphere());
        [Test] public void AttractorTube() => Check<AttractorEngine>(c => c.trajectoryPoints = 3000);
        [Test] public void AttractorMapDust() => Check<AttractorEngine>(c => { c.system = Attractor.DeJong; c.trajectoryPoints = 2000; c.visiblePoints = 2000; });
        [Test] public void Seifert() => Check<SeifertSurfaceBuilder>(c => c.UseT34());
        [Test] public void DualContourMenger() => Check<DualContourEngine>(c => { c.resolution = 24; c.UseMenger(); });
        [Test] public void ConwaySoccerBall() => Check<ConwayPolyhedronEngine>(c => c.UseSoccerBall());
        [Test] public void ConwaySnubDodecahedron() => Check<ConwayPolyhedronEngine>(c => c.UseSnubDodecahedron());
        [Test] public void ConwayBadNotationBuildsNothingButStaysFinite()
        {
            var go = new GameObject("conway bad");
            var c = go.AddComponent<ConwayPolyhedronEngine>();
            c.notation = "xyz";
            c.Rebuild();
            StringAssert.Contains("Notation error", c.Status);
            Assert.AreEqual(Vector3.zero, c.SampleGrid(.3f, .5f));
            Object.DestroyImmediate(go);
        }
        [Test] public void DualContourGyroidStair() => Check<DualContourEngine>(c => { c.resolution = 24; c.UseGyroidStair(); });
    }
}
