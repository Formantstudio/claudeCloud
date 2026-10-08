using System;
using UnityEngine;
using UnityEngine.VFX;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Everything that shapes an SDF particle cloud, in one serializable block.
    /// Branching a variant: duplicate Combo/SdfSparkleParticles.vfx, drop the copy in
    /// <see cref="graph"/>, and the same controls drive it. Nothing here is a
    /// ScriptableObject, so a variant lives on the GameObject you can see.
    /// </summary>
    [Serializable]
    public sealed class SdfParticleSettings
    {
        public enum Preset { Custom, FineMist, Sparkle, Embers, Dense, Chunky }

        [Tooltip("Duplicate SdfSparkleParticles.vfx to branch a variant, then drop it here.")]
        public VisualEffectAsset graph;
        [Tooltip("Sprite each particle draws with. Leave empty to keep the graph's own.")]
        public Texture2D sprite;

        [Header("Size")]
        [Tooltip("Multiplies the size curve. This is the control the stock SDF graph did not have.")]
        [Range(0.02f, 8f)] public float sizeScale = 0.35f;
        [Tooltip("Particle size across its lifetime, before sizeScale. Local-space units.")]
        public AnimationCurve sizeOverLife = DefaultSizeCurve();

        [Header("Density")]
        [Range(100f, 200000f)] public float spawnRate = 24000f;
        [Tooltip("x = shortest life, y = longest life, in seconds.")]
        public Vector2 lifetime = new Vector2(1.8f, 3.2f);

        [Header("Surface behaviour")]
        [Tooltip("How close to the surface a particle sticks, as a fraction of the field cube.")]
        [Range(0.0002f, 0.08f)] public float stickDistance = 0.007f;
        [Range(0f, 400f)] public float stickForce = 65f;
        [Range(0f, 8000f)] public float attractionForce = 1800f;
        [Range(0f, 40f)] public float attractionSpeed = 2.5f;

        [Header("Colour")]
        public Gradient palette = DefaultPalette();

        public static AnimationCurve DefaultSizeCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0.0026f), new Keyframe(1f, 0.0062f));
        }

        public static Gradient DefaultPalette()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(.18f, .85f, .9f), 0f),
                    new GradientColorKey(new Color(.8f, .65f, .3f), .55f),
                    new GradientColorKey(new Color(.28f, .3f, .65f), 1f)
                },
                new[] { new GradientAlphaKey(.85f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>Overwrites the tuning fields with a named starting point. Leaves graph/sprite/palette alone.</summary>
        public void Apply(Preset preset)
        {
            switch (preset)
            {
                case Preset.FineMist:
                    sizeScale = .18f; spawnRate = 60000f; lifetime = new Vector2(2.4f, 4.5f);
                    stickDistance = .004f; stickForce = 45f; attractionForce = 1200f; attractionSpeed = 1.6f; break;
                case Preset.Sparkle:
                    sizeScale = .35f; spawnRate = 24000f; lifetime = new Vector2(1.8f, 3.2f);
                    stickDistance = .007f; stickForce = 65f; attractionForce = 1800f; attractionSpeed = 2.5f; break;
                case Preset.Embers:
                    sizeScale = .9f; spawnRate = 6000f; lifetime = new Vector2(3f, 6f);
                    stickDistance = .014f; stickForce = 22f; attractionForce = 700f; attractionSpeed = 1f; break;
                case Preset.Dense:
                    sizeScale = .22f; spawnRate = 120000f; lifetime = new Vector2(1.2f, 2.2f);
                    stickDistance = .003f; stickForce = 90f; attractionForce = 2600f; attractionSpeed = 3.5f; break;
                case Preset.Chunky:
                    sizeScale = 2.2f; spawnRate = 3000f; lifetime = new Vector2(2.5f, 5f);
                    stickDistance = .02f; stickForce = 30f; attractionForce = 900f; attractionSpeed = 1.3f; break;
            }
        }
    }

    /// <summary>
    /// Owns the child GameObject + VisualEffect for one SDF particle cloud and pushes
    /// <see cref="SdfParticleSettings"/> into it. Separate from any one effect so the
    /// geometry combo, the tunnel, and future variants all drive particles the same way.
    /// Every property write is guarded, so a graph missing the newer parameters still runs.
    /// </summary>
    public sealed class SdfParticleRig
    {
        const string SizeProperty = "ParticleSize";
        const string SpriteProperty = "ParticleTexture";

        VisualEffect effect;
        GameObject host;
        VisualEffectAsset boundGraph;
        readonly AnimationCurve scratch = new AnimationCurve();

        public VisualEffect Effect => effect;
        public bool HasSizeControl { get; private set; }
        /// <summary>Side of the field cube in world units; particle sizes are quoted against it.</summary>
        public float WorldCubeSide { get; private set; } = 1f;

        /// <summary>Creates (or re-creates) the cloud under <paramref name="parent"/>. localCubeSide is in parent-local units.</summary>
        public void Build(Transform parent, string name, VisualEffectAsset graph, Vector3 localCentre, float localCubeSide, int seed = 1977)
        {
            Release();
            if (!parent || !graph) return;
            host = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            host.transform.SetParent(parent, false);
            host.transform.localPosition = localCentre;
            host.transform.localScale = Vector3.one * Mathf.Max(localCubeSide, 1e-4f);
            effect = host.AddComponent<VisualEffect>();
            effect.visualEffectAsset = graph;
            effect.startSeed = (uint)seed;
            effect.resetSeedOnPlay = false;
            boundGraph = graph;
            HasSizeControl = effect.HasAnimationCurve(SizeProperty);
            WorldCubeSide = ((Vector3)host.transform.localToWorldMatrix.GetColumn(0)).magnitude;
        }

        public bool NeedsRebuild(VisualEffectAsset graph) => effect == null || boundGraph != graph;

        public void SetField(RenderTexture volume)
        {
            if (effect && effect.HasTexture("_Texture3D")) effect.SetTexture("_Texture3D", volume);
        }

        public void Reinit() { if (effect) effect.Reinit(); }

        /// <summary>Pushes the whole settings block. Call every frame; it is all cheap property writes.</summary>
        public void Push(SdfParticleSettings s)
        {
            if (!effect || s == null) return;

            if (effect.HasFloat("SpawnRate")) effect.SetFloat("SpawnRate", s.spawnRate);
            float lo = Mathf.Max(.05f, Mathf.Min(s.lifetime.x, s.lifetime.y));
            float hi = Mathf.Max(lo + .01f, Mathf.Max(s.lifetime.x, s.lifetime.y));
            if (effect.HasFloat("LifetimeMin")) effect.SetFloat("LifetimeMin", lo);
            if (effect.HasFloat("LifetimeMax")) effect.SetFloat("LifetimeMax", hi);
            if (effect.HasFloat("StickDistance")) effect.SetFloat("StickDistance", s.stickDistance);
            if (effect.HasFloat("StickForce")) effect.SetFloat("StickForce", s.stickForce);
            if (effect.HasFloat("AttractionForce")) effect.SetFloat("AttractionForce", s.attractionForce);
            if (effect.HasFloat("AttractionSpeed")) effect.SetFloat("AttractionSpeed", s.attractionSpeed);
            if (s.palette != null && effect.HasGradient("ParticleColor")) effect.SetGradient("ParticleColor", s.palette);

            HasSizeControl = effect.HasAnimationCurve(SizeProperty);
            if (HasSizeControl) effect.SetAnimationCurve(SizeProperty, Scaled(s));
            if (s.sprite && effect.HasTexture(SpriteProperty)) effect.SetTexture(SpriteProperty, s.sprite);
        }

        AnimationCurve Scaled(SdfParticleSettings s)
        {
            var source = s.sizeOverLife != null && s.sizeOverLife.length > 0
                ? s.sizeOverLife
                : SdfParticleSettings.DefaultSizeCurve();
            var keys = source.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= s.sizeScale;
                keys[i].inTangent *= s.sizeScale;
                keys[i].outTangent *= s.sizeScale;
            }
            scratch.keys = keys;
            scratch.preWrapMode = source.preWrapMode;
            scratch.postWrapMode = source.postWrapMode;
            return scratch;
        }

        /// <summary>Largest particle diameter in world units, for the on-screen readout.</summary>
        public float WorldParticleSize(SdfParticleSettings s)
        {
            if (s == null) return 0f;
            var source = s.sizeOverLife != null && s.sizeOverLife.length > 0 ? s.sizeOverLife : SdfParticleSettings.DefaultSizeCurve();
            float peak = 0f;
            foreach (var k in source.keys) peak = Mathf.Max(peak, k.value);
            return peak * s.sizeScale * WorldCubeSide;
        }

        public void Release()
        {
            if (host)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(host);
                else UnityEngine.Object.DestroyImmediate(host);
            }
            host = null; effect = null; boundGraph = null; HasSizeControl = false;
        }
    }
}
