using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Implicit shapes: a scalar field whose zero crossing is the surface. These are the ones that
    /// have no parameterisation, so they cannot go through the manifold grid path and need a
    /// surface extractor instead (<see cref="ImplicitSurfaceChamber"/>).
    ///
    /// Unlike the parametric immersions, most of these are genuinely signed — a gyroid really does
    /// divide space into two interlocking labyrinths — so they also work on the SDF particle path.
    /// </summary>
    public enum ImplicitShape
    {
        Gyroid,
        SchwarzP,
        SchwarzD,
        Neovius,
        Lidinoid,
        SplitP,
        BarthSextic,
        Mandelbulb,
        Mandelbox,
        MengerSponge,
        Sierpinski,
        Torus,
        Goursat
    }

    [System.Serializable]
    public sealed class ImplicitSettings
    {
        [Header("Field")]
        public ImplicitShape shape = ImplicitShape.Gyroid;
        [Tooltip("Level set to extract. 0 is the minimal surface; offsetting sweeps from one labyrinth to the other.")]
        [Range(-1.5f, 1.5f)] public float level;
        [Tooltip("Thickens the level set into a shell, so a minimal surface becomes a solid wall.")]
        [Range(0, .8f)] public float thickness;
        [Tooltip("Repeats per unit for the periodic surfaces. The TPMS family tiles, which is what makes rooms possible.")]
        [Range(.25f, 8f)] public float frequency = 2f;

        [Header("Escher step")]
        [Tooltip("Lattice periods gained per circuit of the axis. Whole numbers give a seamless endless staircase on the periodic surfaces; 0 = off.")]
        [Range(-4f, 4f)] public float dislocation;
        public Axis3 dislocationAxis = Axis3.Z;
        [Tooltip("Radius (in field units, the box is -1..1) inside which the shear eases off, so the axis does not tear.")]
        [Range(.01f, 1f)] public float dislocationCore = .15f;
        [Tooltip("Sphere inversion, Droste spiral and the scrolling window. Shared with the Escher rooms.")]
        public EscherSpace space = new EscherSpace();

        [Header("Fractals")]
        [Tooltip("Mandelbulb power. 8 is the classic; animating it is the most striking thing it does.")]
        [Range(2, 16)] public float power = 8f;
        [Range(2, 20)] public int iterations = 8;
        [Tooltip("Mandelbox scale. Negative values near -1.5 give the room-and-corridor interiors.")]
        [Range(-3f, 3f)] public float boxScale = -1.75f;
        [Range(.1f, 2f)] public float minRadius = .5f;
        [Range(.5f, 3f)] public float fixedRadius = 1f;
        [Tooltip("Menger and Sierpinski recursion depth.")]
        [Range(1, 6)] public int folds = 3;

        [Header("Barth sextic")]
        [Tooltip("Homogenising parameter. Animating it swells and splits the surface.")]
        [Range(.2f, 2f)] public float barthW = 1f;
    }

    public static class Implicits
    {
        const float Phi = 1.6180339887f;

        /// <summary>
        /// Field value at p. Negative inside, positive outside, zero on the surface.
        /// p is expected in roughly [-1, 1] on each axis.
        /// </summary>
        public static float Field(ImplicitSettings s, Vector3 p, float time)
        {
            // The Escher warps go first (EscherSpace: inversion, Droste, screw dislocation, scroll),
            // with the lattice's true period — the TPMS are evaluated at π·frequency·p, so they
            // repeat every 2 / frequency — which is what keeps every seam invisible.
            if (s.dislocation != 0f || (s.space != null && s.space.Active))
                p = EscherSpace.ToLattice(s.space, p, ScrewDislocation.TpmsPeriod(s.frequency), time,
                                          s.dislocationAxis, s.dislocation, s.dislocationCore);
            float v;
            switch (s.shape)
            {
                // ---- Triply periodic minimal surfaces. All tile, so they make endless interiors.
                case ImplicitShape.SchwarzP:
                {
                    Vector3 q = p * (Mathf.PI * s.frequency);
                    v = Mathf.Cos(q.x) + Mathf.Cos(q.y) + Mathf.Cos(q.z);
                    break;
                }
                case ImplicitShape.SchwarzD:
                {
                    Vector3 q = p * (Mathf.PI * s.frequency);
                    float sx = Mathf.Sin(q.x), sy = Mathf.Sin(q.y), sz = Mathf.Sin(q.z);
                    float cx = Mathf.Cos(q.x), cy = Mathf.Cos(q.y), cz = Mathf.Cos(q.z);
                    v = sx * sy * sz + sx * cy * cz + cx * sy * cz + cx * cy * sz;
                    break;
                }
                case ImplicitShape.Neovius:
                {
                    Vector3 q = p * (Mathf.PI * s.frequency);
                    float cx = Mathf.Cos(q.x), cy = Mathf.Cos(q.y), cz = Mathf.Cos(q.z);
                    v = 3f * (cx + cy + cz) + 4f * cx * cy * cz;
                    break;
                }
                case ImplicitShape.Lidinoid:
                {
                    Vector3 q = p * (Mathf.PI * s.frequency);
                    float s2x = Mathf.Sin(2f * q.x), s2y = Mathf.Sin(2f * q.y), s2z = Mathf.Sin(2f * q.z);
                    float c2x = Mathf.Cos(2f * q.x), c2y = Mathf.Cos(2f * q.y), c2z = Mathf.Cos(2f * q.z);
                    v = s2x * Mathf.Cos(q.y) * Mathf.Sin(q.z)
                      + s2y * Mathf.Cos(q.z) * Mathf.Sin(q.x)
                      + s2z * Mathf.Cos(q.x) * Mathf.Sin(q.y)
                      - c2x * c2y - c2y * c2z - c2z * c2x;
                    break;
                }
                case ImplicitShape.SplitP:
                {
                    Vector3 q = p * (Mathf.PI * s.frequency);
                    float s2x = Mathf.Sin(2f * q.x), s2y = Mathf.Sin(2f * q.y), s2z = Mathf.Sin(2f * q.z);
                    v = 1.1f * (s2x * Mathf.Sin(q.z) * Mathf.Cos(q.y)
                              + s2y * Mathf.Sin(q.x) * Mathf.Cos(q.z)
                              + s2z * Mathf.Sin(q.y) * Mathf.Cos(q.x))
                      - .2f * (Mathf.Cos(2f * q.x) * Mathf.Cos(2f * q.y)
                             + Mathf.Cos(2f * q.y) * Mathf.Cos(2f * q.z)
                             + Mathf.Cos(2f * q.z) * Mathf.Cos(2f * q.x))
                      - .4f * (Mathf.Cos(2f * q.x) + Mathf.Cos(2f * q.y) + Mathf.Cos(2f * q.z));
                    break;
                }

                // ---- The gyroid: sin x cos y + sin y cos z + sin z cos x = 0.
                default:
                case ImplicitShape.Gyroid:
                {
                    Vector3 q = p * (Mathf.PI * s.frequency);
                    v = Mathf.Sin(q.x) * Mathf.Cos(q.y)
                      + Mathf.Sin(q.y) * Mathf.Cos(q.z)
                      + Mathf.Sin(q.z) * Mathf.Cos(q.x);
                    break;
                }

                // ---- Barth sextic: degree 6, 65 ordinary double points, icosahedral symmetry.
                // 4(p^2 x^2 - y^2)(p^2 y^2 - z^2)(p^2 z^2 - x^2)
                //   - (1 + 2p)(x^2 + y^2 + z^2 - w^2)^2 w^2 = 0
                case ImplicitShape.BarthSextic:
                {
                    Vector3 q = p * 1.8f;
                    float x2 = q.x * q.x, y2 = q.y * q.y, z2 = q.z * q.z;
                    float p2 = Phi * Phi, w2 = s.barthW * s.barthW;
                    float a = p2 * x2 - y2, b = p2 * y2 - z2, c = p2 * z2 - x2;
                    float r = x2 + y2 + z2 - w2;
                    v = 4f * a * b * c - (1f + 2f * Phi) * r * r * w2;
                    // Degree 6 blows up fast; compress so the extractor sees a usable range.
                    v = Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), .25f);
                    break;
                }

                // ---- Mandelbulb, escape-time turned into a distance estimate with the standard
                // running derivative: DE = 0.5 * log(r) * r / dr.
                case ImplicitShape.Mandelbulb:
                {
                    Vector3 c = p * 1.3f;
                    Vector3 z = c;
                    float dr = 1f, r = 0f;
                    int it = Mathf.Clamp(s.iterations, 2, 20);
                    for (int i = 0; i < it; i++)
                    {
                        r = z.magnitude;
                        if (r > 2.2f) break;
                        float theta = Mathf.Acos(Mathf.Clamp(z.z / Mathf.Max(r, 1e-6f), -1f, 1f));
                        float phi = Mathf.Atan2(z.y, z.x);
                        dr = Mathf.Pow(r, s.power - 1f) * s.power * dr + 1f;
                        float zr = Mathf.Pow(r, s.power);
                        theta *= s.power; phi *= s.power;
                        z = zr * new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi),
                                             Mathf.Sin(theta) * Mathf.Sin(phi),
                                             Mathf.Cos(theta)) + c;
                    }
                    v = r < 1e-5f ? -1f : .5f * Mathf.Log(Mathf.Max(r, 1.0001f)) * r / Mathf.Max(dr, 1e-6f);
                    break;
                }

                // ---- Mandelbox: box fold then sphere fold then scale. This is the fractal that
                // reads as endless rooms and corridors; negative scale gives the interiors.
                case ImplicitShape.Mandelbox:
                {
                    Vector3 c = p * 3f;
                    Vector3 z = c;
                    float dr = 1f;
                    float minR2 = s.minRadius * s.minRadius, fixR2 = s.fixedRadius * s.fixedRadius;
                    int it = Mathf.Clamp(s.iterations, 2, 20);
                    for (int i = 0; i < it; i++)
                    {
                        z = new Vector3(Mathf.Clamp(z.x, -1f, 1f) * 2f - z.x,
                                        Mathf.Clamp(z.y, -1f, 1f) * 2f - z.y,
                                        Mathf.Clamp(z.z, -1f, 1f) * 2f - z.z);
                        float m = z.sqrMagnitude;
                        if (m < minR2) { float k = fixR2 / minR2; z *= k; dr *= k; }
                        else if (m < fixR2) { float k = fixR2 / m; z *= k; dr *= k; }
                        z = z * s.boxScale + c;
                        dr = dr * Mathf.Abs(s.boxScale) + 1f;
                    }
                    v = z.magnitude / Mathf.Max(Mathf.Abs(dr), 1e-6f);
                    break;
                }

                // ---- Menger sponge, by repeated cross folds. Genuinely architectural.
                case ImplicitShape.MengerSponge:
                {
                    Vector3 q = p * 1.1f;
                    float d = Box(q, Vector3.one);
                    float scale = 1f;
                    int depth = Mathf.Clamp(s.folds, 1, 6);
                    for (int i = 0; i < depth; i++)
                    {
                        Vector3 a = new Vector3(Mathf.Repeat(q.x * scale, 2f) - 1f,
                                                Mathf.Repeat(q.y * scale, 2f) - 1f,
                                                Mathf.Repeat(q.z * scale, 2f) - 1f);
                        scale *= 3f;
                        Vector3 r = new Vector3(1f - 3f * Mathf.Abs(a.x),
                                                1f - 3f * Mathf.Abs(a.y),
                                                1f - 3f * Mathf.Abs(a.z));
                        float cross = Cross(r) / scale;
                        d = Mathf.Max(d, cross);
                    }
                    v = d;
                    break;
                }

                // ---- Sierpinski tetrahedron, by folding onto four corners.
                case ImplicitShape.Sierpinski:
                {
                    Vector3 z = p * 1.4f;
                    float scale = 2f;
                    int depth = Mathf.Clamp(s.folds, 1, 6);
                    for (int i = 0; i < depth; i++)
                    {
                        if (z.x + z.y < 0f) z = new Vector3(-z.y, -z.x, z.z);
                        if (z.x + z.z < 0f) z = new Vector3(-z.z, z.y, -z.x);
                        if (z.y + z.z < 0f) z = new Vector3(z.x, -z.z, -z.y);
                        z = z * scale - Vector3.one * (scale - 1f);
                    }
                    v = z.magnitude * Mathf.Pow(scale, -depth) - .02f;
                    break;
                }

                // ---- Plain torus and Goursat, as sanity references for the extractor.
                case ImplicitShape.Torus:
                {
                    float rr = new Vector2(p.x, p.y).magnitude - .6f;
                    v = Mathf.Sqrt(rr * rr + p.z * p.z) - .25f;
                    break;
                }
                case ImplicitShape.Goursat:
                {
                    Vector3 q = p * 1.5f;
                    float x4 = Mathf.Pow(q.x, 4), y4 = Mathf.Pow(q.y, 4), z4 = Mathf.Pow(q.z, 4);
                    float r2 = q.sqrMagnitude;
                    v = x4 + y4 + z4 - 1.5f * r2 * r2 + 1f;
                    break;
                }
            }

            v -= s.level;
            // A thickened level set: the wall around the surface instead of the surface itself.
            if (s.thickness > 0f) v = Mathf.Abs(v) - s.thickness;
            return v;
        }

        static float Box(Vector3 p, Vector3 b)
        {
            Vector3 q = new Vector3(Mathf.Abs(p.x) - b.x, Mathf.Abs(p.y) - b.y, Mathf.Abs(p.z) - b.z);
            Vector3 m = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f));
            return m.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }

        /// <summary>Infinite cross of three square tubes: the piece a Menger fold removes.</summary>
        static float Cross(Vector3 r)
        {
            float da = Mathf.Max(r.x, r.y);
            float db = Mathf.Max(r.y, r.z);
            float dc = Mathf.Max(r.z, r.x);
            return Mathf.Min(da, Mathf.Min(db, dc));
        }

        /// <summary>
        /// True for the shapes whose field is a usable signed distance, so they can also drive the
        /// SDF particle graph rather than only the surface extractor.
        /// </summary>
        public static bool IsSignedDistance(ImplicitShape shape)
        {
            switch (shape)
            {
                case ImplicitShape.Mandelbulb:
                case ImplicitShape.Mandelbox:
                case ImplicitShape.MengerSponge:
                case ImplicitShape.Sierpinski:
                case ImplicitShape.Torus:
                    return true;
                default:
                    // The TPMS family and Barth are level sets, not distances: the gradient
                    // magnitude varies, so stick distances come out uneven without normalising.
                    return false;
            }
        }

        /// <summary>True for the surfaces that tile, so a camera can fly through them endlessly.</summary>
        public static bool IsPeriodic(ImplicitShape shape)
        {
            switch (shape)
            {
                case ImplicitShape.Gyroid:
                case ImplicitShape.SchwarzP:
                case ImplicitShape.SchwarzD:
                case ImplicitShape.Neovius:
                case ImplicitShape.Lidinoid:
                case ImplicitShape.SplitP:
                    return true;
                default:
                    return false;
            }
        }
    }
}
