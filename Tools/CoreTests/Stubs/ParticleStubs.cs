// Just enough of UnityEngine's particle API for the node-and-edge swarms to compile. Nothing here
// simulates; the tests use the swarms' geometry, not their particles.
namespace UnityEngine
{
    public enum ParticleSystemStopBehavior { StopEmittingAndClear, StopEmitting }
    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemScalingMode { Hierarchy, Local, Shape }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None }
    public enum ParticleSystemRenderSpace { View, World, Local, Facing, Velocity }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color32(Color c) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(c.a) * 255f));
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    public sealed class ParticleSystem : Component
    {
        public struct Particle
        {
            public Vector3 position, velocity, rotation3D, startSize3D, axisOfRotation, angularVelocity3D;
            public float startSize, rotation, remainingLifetime, startLifetime, angularVelocity;
            public Color32 startColor;
            public uint randomSeed;
        }

        public struct MinMaxCurve
        {
            public float constant;
            public MinMaxCurve(float c) { constant = c; }
            public static implicit operator MinMaxCurve(float c) => new MinMaxCurve(c);
        }

        public struct MinMaxGradient
        {
            public Color color;
            public MinMaxGradient(Color c) { color = c; }
            public static implicit operator MinMaxGradient(Color c) => new MinMaxGradient(c);
        }

        public struct MainModule
        {
            public ParticleSystemSimulationSpace simulationSpace { get; set; }
            public ParticleSystemScalingMode scalingMode { get; set; }
            public int maxParticles { get; set; }
            public bool playOnAwake { get; set; }
            public bool loop { get; set; }
            public bool startSize3D { get; set; }
            public bool startRotation3D { get; set; }
            public MinMaxCurve startLifetime { get; set; }
            public MinMaxCurve startSpeed { get; set; }
            public MinMaxCurve startSize { get; set; }
            public MinMaxCurve gravityModifier { get; set; }
            public MinMaxGradient startColor { get; set; }
        }

        public struct EmissionModule { public bool enabled { get; set; } public MinMaxCurve rateOverTime { get; set; } }
        public struct ShapeModule { public bool enabled { get; set; } }
        public struct Module { public bool enabled { get; set; } }

        public MainModule main => new MainModule();
        public EmissionModule emission => new EmissionModule();
        public ShapeModule shape => new ShapeModule();
        public Module velocityOverLifetime => new Module();
        public Module noise => new Module();
        public Module trails => new Module();
        public Module colorOverLifetime => new Module();
        public Module sizeOverLifetime => new Module();
        public void Pause() { }
        public int particleCount => 0;
        public bool isPlaying => false;

        public void Stop(bool withChildren, ParticleSystemStopBehavior behavior) { }
        public void Stop() { }
        public void Play() { }
        public void Play(bool withChildren) { }
        public void Clear() { }
        public void SetParticles(Particle[] particles, int size) { }
        public void SetParticles(Particle[] particles) { }
        public int GetParticles(Particle[] particles) => 0;
    }

    public sealed class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode { get; set; }
        public ParticleSystemRenderSpace alignment { get; set; }
        public Mesh mesh { get; set; }
        public float minParticleSize { get; set; }
        public float maxParticleSize { get; set; }
        public bool enableGPUInstancing { get; set; }
        public Bounds localBounds { get; set; }
    }
}
