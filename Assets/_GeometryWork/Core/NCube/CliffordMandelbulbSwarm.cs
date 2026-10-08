using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Clifford torus Mandelbulb: a flat torus living in the 3-sphere, rotated in four dimensions,
    /// projected down to three, and then encrusted by the Mandelbulb's distance field.
    ///
    /// Why the combination is worth building rather than either half alone:
    ///
    /// - The **Clifford torus** is the flat torus in S^3, `(cos a, sin a, cos b, sin b)/sqrt(2)`.
    ///   Under a 4-D isoclinic rotation its stereographic projection turns completely inside out —
    ///   the hole closes, everything passes through itself, and it comes back. That motion has no
    ///   3-D equivalent, so it cannot be faked by spinning a doughnut.
    /// - The **Mandelbulb** supplies a field that is different at every point and self-similar at
    ///   every scale. Displacing the torus along its own surface normal by that field turns a
    ///   smooth sheet into a fractal crust, and because the crust is a function of *world*
    ///   position, the torus slides through its own detail as it rotates rather than carrying it
    ///   along. The surface boils.
    /// - The escape count drives each particle's **colour and size**, so the fractal is visible in
    ///   the particles themselves, not only in where they sit.
    ///
    /// Topology is the torus grid: `uRes * vRes` nodes, each joined to its neighbour in u and in v,
    /// wrapping both ways. 48 x 48 is 2,304 nodes and 4,608 edges, which lands on
    /// DekeractTetraSwarm's 11,264-particle budget at two travellers an edge. The base class's
    /// auto-density solver handles the arithmetic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CliffordMandelbulbSwarm : NodeEdgeSwarmBase
    {
        [Header("Clifford torus")]
        [Tooltip("Grid steps around the first circle.")]
        [Range(8, 128)] public int uRes = 48;
        [Tooltip("Grid steps around the second circle.")]
        [Range(8, 128)] public int vRes = 48;
        [Tooltip("Use the scene's Hyperspace4DAxis, so this shares the 4-D camera with everything else.")]
        public bool useSharedAxis = true;
        public Projection4D projection = Projection4D.Stereographic;
        [Tooltip("Viewer distance along W for the perspective projection.")]
        [Range(1.05f, 6f)] public float wDistance = 2.2f;
        [Tooltip("Own rotation rates in the xy and zw planes, used when no shared axis is set. Equal rates give the isoclinic turn that everts the torus.")]
        public Vector2 simpleRates = new Vector2(.08f, .08f);
        [Tooltip("Own rates in the mixed xz and yw planes.")]
        public Vector2 mixedRates = new Vector2(.02f, 0f);

        [Header("Mandelbulb crust")]
        [Tooltip("0 = a clean Clifford torus, 1 = fully encrusted. Sweeping this is the headline move.")]
        [Range(0f, 1f)] public float crust = .65f;
        [Tooltip("Mandelbulb power. 8 is the classic; animating it is the most striking thing it does.")]
        [Range(2f, 16f)] public float power = 8f;
        [Range(2, 16)] public int iterations = 8;
        [Tooltip("Scales world position into the bulb's domain before sampling. Lower zooms into the fractal.")]
        [Range(.05f, 2f)] public float fieldScale = .55f;
        [Tooltip("How far the field pushes the surface, as a fraction of the radius.")]
        [Range(0f, 1.5f)] public float displace = .45f;
        [Tooltip("Drifts the field through the shape over time, so the crust boils instead of sitting still.")]
        [Range(0f, 1f)] public float fieldDrift = .12f;
        [Tooltip("Animates the power, which makes the crust bloom and collapse.")]
        [Range(0f, 1f)] public float powerPulse = .25f;

        protected override string SwarmName => "Clifford Mandelbulb swarm";
        protected override int NodeCount => Mathf.Clamp(uRes, 8, 128) * Mathf.Clamp(vRes, 8, 128);
        protected override bool TopologyDirty =>
            builtU != Mathf.Clamp(uRes, 8, 128) || builtV != Mathf.Clamp(vRes, 8, 128);

        int builtU, builtV;
        readonly ImplicitSettings bulb = new ImplicitSettings { shape = ImplicitShape.Mandelbulb };

        protected override void BuildEdges(List<int> a, List<int> b)
        {
            builtU = Mathf.Clamp(uRes, 8, 128);
            builtV = Mathf.Clamp(vRes, 8, 128);
            int u = builtU, v = builtV;

            // Torus lattice: every node joins its neighbour in u and in v, wrapping both ways.
            for (int j = 0; j < v; j++)
            for (int i = 0; i < u; i++)
            {
                int here = j * u + i;
                a.Add(here); b.Add(j * u + (i + 1) % u);
                a.Add(here); b.Add(((j + 1) % v) * u + i);
            }

            nodeAccent = new float[u * v];
        }

        protected override void UpdateNodes(Vector3[] into, float time)
        {
            int u = builtU, v = builtV;
            if (nodeAccent == null || nodeAccent.Length != u * v) nodeAccent = new float[u * v];

            var shared = useSharedAxis ? Hyperspace4DAxis.Current : null;
            const float inv = .70710678f;
            float tau = Mathf.PI * 2f;

            // The field drifts and the power pulses, so the crust is never the same twice.
            bulb.power = power + (powerPulse > 0f ? Mathf.Sin(time * .23f) * powerPulse * 4f : 0f);
            bulb.iterations = Mathf.Clamp(iterations, 2, 16);
            Vector3 drift = new Vector3(Mathf.Sin(time * .17f), Mathf.Cos(time * .13f),
                                        Mathf.Sin(time * .11f)) * fieldDrift;

            for (int j = 0; j < v; j++)
            for (int i = 0; i < u; i++)
            {
                float a = (float)i / u * tau, b = (float)j / v * tau;
                var p4 = new Vector4(Mathf.Cos(a) * inv, Mathf.Sin(a) * inv,
                                     Mathf.Cos(b) * inv, Mathf.Sin(b) * inv);

                Vector3 p;
                if (shared) p = shared.RotateAndProject(p4);
                else
                {
                    float x = p4.x, y = p4.y, z = p4.z, w = p4.w;
                    Rot(ref x, ref y, simpleRates.x * time * tau);
                    Rot(ref z, ref w, simpleRates.y * time * tau);
                    Rot(ref x, ref z, mixedRates.x * time * tau);
                    Rot(ref y, ref w, mixedRates.y * time * tau);
                    p = Project(x, y, z, w);
                }
                p *= radius;

                float accent = 0f;
                if (crust > 0f && displace > 0f)
                {
                    // Sample the bulb at this point's *world* position, so the torus slides through
                    // its own detail as it turns instead of carrying the crust along with it.
                    Vector3 probe = p * fieldScale + drift;
                    float de = Implicits.Field(bulb, probe, time);

                    // The DE is signed: negative inside the set. Fold it into 0..1 so it reads as
                    // a height, and push along the surface normal, which for a torus about the
                    // origin is close enough to the radial direction.
                    float height = 1f / (1f + Mathf.Abs(de) * 6f);
                    accent = Mathf.Clamp01(height);
                    Vector3 normal = p.sqrMagnitude > 1e-6f ? p.normalized : Vector3.up;
                    p += normal * (height - .5f) * displace * radius * crust;
                }

                int index = j * u + i;
                into[index] = p;
                nodeAccent[index] = accent * crust;
            }
        }

        static void Rot(ref float p, ref float q, float angle)
        {
            if (angle == 0f) return;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float p0 = p, q0 = q;
            p = p0 * c - q0 * s;
            q = p0 * s + q0 * c;
        }

        Vector3 Project(float x, float y, float z, float w)
        {
            switch (projection)
            {
                case Projection4D.Stereographic:
                {
                    // Guarded: w -> 1 sends the point to infinity, which is also the moment the
                    // torus turns inside out, so the clamp is what keeps that readable.
                    float d = 1f - w;
                    if (Mathf.Abs(d) < .08f) d = Mathf.Sign(d == 0f ? 1f : d) * .08f;
                    return new Vector3(x, y, z) / d * .5f;
                }
                case Projection4D.Perspective:
                {
                    float near = Mathf.Max(wDistance, 1.05f);
                    return new Vector3(x, y, z) * (near / Mathf.Max(near - w, .08f));
                }
                default:
                    return new Vector3(x, y, z);
            }
        }

        protected override string Describe() =>
            "Clifford Mandelbulb " + builtU + "x" + builtV + " · " + NodeTotal.ToString("N0")
            + " nodes, " + EdgeTotal.ToString("N0") + " edges · crust " + crust.ToString("0.00")
            + " p" + bulb.power.ToString("0.0");

        [ContextMenu("Clean torus (no crust)")] public void Clean() { crust = 0f; }
        [ContextMenu("Full crust")] public void Full() { crust = 1f; }
        [ContextMenu("Isoclinic evert")]
        public void Evert()
        {
            useSharedAxis = false;
            projection = Projection4D.Stereographic;
            simpleRates = new Vector2(.08f, .08f);
            mixedRates = Vector2.zero;
        }
    }
}
