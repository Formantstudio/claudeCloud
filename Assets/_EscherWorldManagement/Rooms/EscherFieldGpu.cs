using UnityEngine;
using PsychedelicLab.GeometryFX;

namespace PsychedelicLab.EscherWorld
{
    /// <summary>
    /// Packs an <see cref="EscherFieldSettings"/> into the uniforms <c>EscherField4D.hlsl</c> reads,
    /// so a raymarched portal and the CPU room it looks into are the same field. The layout is
    /// documented at the top of the HLSL file and written only here.
    ///
    /// Time enters only through the scroll, which is wrapped to one lattice period on the CPU
    /// (<see cref="EscherSpace"/>): the shader never sees a growing offset, so a portal can scroll
    /// forever without float drift.
    /// </summary>
    public static class EscherFieldGpu
    {
        public struct Uniforms
        {
            public Vector4 field, step, slice, shape, box, invert, droste, droste2, scroll;
        }

        static readonly int FieldId = Shader.PropertyToID("_EscherField");
        static readonly int StepId = Shader.PropertyToID("_EscherStep");
        static readonly int SliceId = Shader.PropertyToID("_EscherSlice");
        static readonly int MarchId = Shader.PropertyToID("_EscherMarch");
        static readonly int ShapeId = Shader.PropertyToID("_EscherShape");
        static readonly int BoxId = Shader.PropertyToID("_EscherBox");
        static readonly int InvertId = Shader.PropertyToID("_EscherInvert");
        static readonly int DrosteId = Shader.PropertyToID("_EscherDroste");
        static readonly int Droste2Id = Shader.PropertyToID("_EscherDroste2");
        static readonly int ScrollId = Shader.PropertyToID("_EscherScroll");

        /// <summary>The uniforms for <paramref name="s"/> at <paramref name="time"/> seconds.</summary>
        public static Uniforms Pack(EscherFieldSettings s, float time)
        {
            var u = new Uniforms
            {
                field = new Vector4((int)s.field, s.level, s.thickness, s.frequency),
                step = new Vector4(s.dislocation, (int)s.dislocationAxis, s.dislocationCore, s.wInfluence),
                slice = new Vector4(s.wRotation.x, s.wRotation.y, s.wRotation.z, s.w),
                shape = new Vector4(s.barthW, s.power, s.iterations, s.folds),
                box = new Vector4(s.boxScale, s.minRadius, s.fixedRadius, 0f),
            };
            var space = s.space;
            if (space != null)
            {
                if (space.invert)
                    u.invert = new Vector4(space.inversionCentre.x, space.inversionCentre.y, space.inversionCentre.z,
                                           Mathf.Max(space.inversionRadius, 1e-4f));
                if (space.droste)
                {
                    u.droste = new Vector4(Mathf.Max(space.drosteScale, 1.0001f), Mathf.Max(space.drosteSectors, 1),
                                           space.drosteTwist, space.drosteRadius);
                    u.droste2 = new Vector4((int)space.drosteAxis, space.drosteCore, 0f, 0f);
                }
                Vector3 offset = space.scroll + space.scrollVelocity * time;
                u.scroll = new Vector4(offset.x - Mathf.Floor(offset.x), offset.y - Mathf.Floor(offset.y),
                                       offset.z - Mathf.Floor(offset.z), 0f);
            }
            return u;
        }

        /// <summary>Raymarch controls: step budget, far distance, step relaxation (≤ 1), hit epsilon.</summary>
        public static Vector4 March(int maxSteps = 96, float maxDistance = 40f, float relax = .6f, float epsilon = .002f) =>
            new Vector4(Mathf.Max(maxSteps, 1), Mathf.Max(maxDistance, .01f), Mathf.Clamp(relax, .05f, 1f), Mathf.Max(epsilon, 1e-5f));

        /// <summary>Writes the uniforms into one portal's material.</summary>
        public static void Apply(Material material, in Uniforms u, Vector4 march)
        {
            if (!material) return;
            material.SetVector(FieldId, u.field);
            material.SetVector(StepId, u.step);
            material.SetVector(SliceId, u.slice);
            material.SetVector(ShapeId, u.shape);
            material.SetVector(BoxId, u.box);
            material.SetVector(InvertId, u.invert);
            material.SetVector(DrosteId, u.droste);
            material.SetVector(Droste2Id, u.droste2);
            material.SetVector(ScrollId, u.scroll);
            material.SetVector(MarchId, march);
        }

        /// <summary>Writes the uniforms globally, for a single shared portal field.</summary>
        public static void ApplyGlobal(in Uniforms u, Vector4 march)
        {
            Shader.SetGlobalVector(FieldId, u.field);
            Shader.SetGlobalVector(StepId, u.step);
            Shader.SetGlobalVector(SliceId, u.slice);
            Shader.SetGlobalVector(ShapeId, u.shape);
            Shader.SetGlobalVector(BoxId, u.box);
            Shader.SetGlobalVector(InvertId, u.invert);
            Shader.SetGlobalVector(DrosteId, u.droste);
            Shader.SetGlobalVector(Droste2Id, u.droste2);
            Shader.SetGlobalVector(ScrollId, u.scroll);
            Shader.SetGlobalVector(MarchId, march);
        }
    }
}
