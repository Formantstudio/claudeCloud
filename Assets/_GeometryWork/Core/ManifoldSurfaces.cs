using System;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The basic shape set. Every one of these is a grid on the unit square (u,v) in [0,1]^2,
    /// which is what lets any pair of them morph by a straight Lerp — the grids correspond
    /// vertex for vertex.
    ///
    /// Cylinder is first so it can stand in for the original Curved Geometry Chamber shape.
    /// </summary>
    public enum ManifoldSurface
    {
        Cylinder,
        Torus,
        Sphere,
        MobiusStrip,
        Superellipsoid,
        TorusKnotTube,
        Helicoid,
        KleinBottle,
        Duocylinder,
        CliffordTorus,
        // Appended, never reordered: the enum's numbers are serialized in scenes.
        RomanSurface,
        BoysSurface,
        CrossCap,
        Enneper,
        Catenoid,
        DinisSurface,
        KuenSurface,
        BreatherSurface,
        Pseudosphere,
        Hyperboloid,
        HornTorus,
        TwistedTorus,
        MonkeySaddle,
        WhitneyUmbrella,
        Henneberg,
        BoursSurface,
        Supershape,
        ConicalSpiral,
        FigureEightTorus,
        TrefoilRibbon
    }

    /// <summary>How a 4-D point is brought down to 3-D. Used by Duocylinder and CliffordTorus.</summary>
    public enum Projection4D { Stereographic, Perspective, Orthographic }

    /// <summary>Which of the duocylinder's three pieces to draw.</summary>
    public enum DuocylinderCell { Ridge, CellA, CellB, BothCells }

    /// <summary>
    /// Shape parameters, as one serializable block so a variant lives on the GameObject
    /// rather than in a ScriptableObject.
    /// </summary>
    [Serializable]
    public sealed class ManifoldSettings
    {
        [Header("Common")]
        [Min(.01f)] public float radius = 3.8f;
        [Tooltip("Length for the cylinder and helicoid; minor radius for the tori.")]
        [Min(.01f)] public float extent = 28f;
        [Min(.001f)] public float minorRadius = 1.1f;

        [Header("Enneper folds")]
        [Range(.3f, 3f)] public float enneperDomain = 2f;
        [Tooltip("Associate-family phase in radians: continuously folds the Enneper surface into its conjugate.")]
        public float enneperPhase;

        [Header("Möbius strip")]
        [Tooltip("1 = Möbius, 2 = full-twist annulus, 3 = triple half-twist.")]
        [Range(0, 8)] public int halfTwists = 1;
        [Min(.01f)] public float bandWidth = 1.2f;

        [Header("Superellipsoid")]
        [Range(.1f, 6f)] public float squareness = .4f;
        [Range(.1f, 6f)] public float roundness = .4f;

        [Header("Torus knot")]
        [Range(1, 12)] public int knotP = 2;
        [Range(1, 12)] public int knotQ = 3;
        [Min(.001f)] public float tubeRadius = .35f;

        [Header("Helicoid, Dini, spiral")]
        [Range(-4, 4)] public float pitch = 1.4f;
        [Range(.5f, 6f)] public float turns = 2f;

        [Header("Supershape (Gielis)")]
        [Tooltip("Lobe counts for the longitude and latitude superformulas.")]
        public Vector2 superM = new Vector2(7f, 3f);
        [Tooltip("n1 shapes the facets (low = round, high = flat), n2 and n3 bias the lobes.")]
        public Vector3 superN = new Vector3(.3f, 1.7f, 1.7f);

        [Header("4-D")]
        [Tooltip("Use the scene's Hyperspace4DAxis for rotation and projection instead of the rates below. That is what puts every 4-D shape on one shared camera axis.")]
        public bool useSharedAxis = true;
        public Projection4D projection = Projection4D.Stereographic;
        public DuocylinderCell cell = DuocylinderCell.BothCells;
        [Tooltip("How far up the cell wall to draw. 1 = the full solid torus, 0 = just the ridge.")]
        [Range(0, 1)] public float cellFill = 1f;
        [Tooltip("Viewer distance along w. Larger is gentler; near 1 the inversion gets violent.")]
        [Range(1.05f, 6f)] public float wDistance = 2.2f;
        [Tooltip("Rotation rate in the xy and zw planes. Equal rates give the isoclinic inside-out turn.")]
        public Vector2 simpleRates = new Vector2(.12f, .12f);
        [Tooltip("Rotation rate in the mixed xz and yw planes — reads as tumbling through itself.")]
        public Vector2 mixedRates = Vector2.zero;
        [Min(.01f)] public float scale4D = 2.6f;
    }

    /// <summary>
    /// Evaluates the basic shapes. Pure functions of (u, v, time) so the same code can drive a
    /// mesh, a particle target or a position map later.
    /// </summary>
    public static class Manifolds
    {
        const float Tau = Mathf.PI * 2f;

        public static Vector3 Evaluate(ManifoldSurface surface, ManifoldSettings s, float u, float v, float time)
        {
            float a = u * Tau, b = v * Tau;
            switch (surface)
            {
                case ManifoldSurface.Torus:
                {
                    float r = s.radius + s.minorRadius * Mathf.Cos(b);
                    return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, s.minorRadius * Mathf.Sin(b));
                }

                case ManifoldSurface.Sphere:
                {
                    float theta = v * Mathf.PI;
                    float ring = Mathf.Sin(theta) * s.radius;
                    return new Vector3(Mathf.Cos(a) * ring, Mathf.Sin(a) * ring, Mathf.Cos(theta) * s.radius);
                }

                case ManifoldSurface.MobiusStrip:
                {
                    float h = s.bandWidth * (2f * v - 1f) * .5f;
                    float k = s.halfTwists * .5f;
                    float r = s.radius + h * Mathf.Cos(k * a);
                    return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, h * Mathf.Sin(k * a));
                }

                case ManifoldSurface.Superellipsoid:
                {
                    // Signed powers are required: pow() on a negative base returns NaN and would
                    // blow the mesh out to the horizon.
                    float theta = v * Mathf.PI - Mathf.PI * .5f;
                    float ct = SignedPow(Mathf.Cos(theta), s.squareness);
                    float st = SignedPow(Mathf.Sin(theta), s.squareness);
                    float ca = SignedPow(Mathf.Cos(a), s.roundness);
                    float sa = SignedPow(Mathf.Sin(a), s.roundness);
                    return new Vector3(ct * ca, ct * sa, st) * s.radius;
                }

                case ManifoldSurface.TorusKnotTube:
                    return KnotTube(s, u, v);

                case ManifoldSurface.Helicoid:
                {
                    float arm = (2f * u - 1f) * s.radius;
                    float sweep = v * Tau * s.turns;
                    return new Vector3(Mathf.Cos(sweep) * arm, Mathf.Sin(sweep) * arm, s.pitch * sweep);
                }

                case ManifoldSurface.KleinBottle:
                {
                    float half = a * .5f;
                    float bulge = Mathf.Cos(half) * Mathf.Sin(b) - Mathf.Sin(half) * Mathf.Sin(2f * b);
                    float r = s.radius + s.minorRadius * bulge;
                    float z = s.minorRadius * (Mathf.Sin(half) * Mathf.Sin(b) + Mathf.Cos(half) * Mathf.Sin(2f * b));
                    return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
                }

                case ManifoldSurface.Duocylinder:
                    return Duocylinder(s, u, v, time);

                case ManifoldSurface.CliffordTorus:
                    return Clifford(s, a, b, 1f, 1f, time);

                // ---- Steiner's Roman surface: the sphere under (X,Y,Z) -> (YZ, XZ, XY).
                // Antipodal points collapse, so this is RP^2, with 3 double lines and 6 pinch
                // points. Same topological object as Boy's surface, which is why they morph.
                case ManifoldSurface.RomanSurface:
                {
                    float theta = v * Mathf.PI, st = Mathf.Sin(theta);
                    float X = st * Mathf.Cos(a), Y = st * Mathf.Sin(a), Z = Mathf.Cos(theta);
                    return new Vector3(Y * Z, X * Z, X * Y) * (s.radius * 2f);
                }

                // ---- Boy's surface, Bryant-Kusner. An immersion of RP^2 with no singularities.
                // The denominator has zeros inside the unit disk at |w| ~ 0.7257; the map stays
                // continuous there (all three g go to infinity, so the ratios go to 0) but it
                // needs the epsilon guard or it emits NaNs.
                case ManifoldSurface.BoysSurface:
                {
                    float rho = Mathf.Clamp01(v);
                    float wr = rho * Mathf.Cos(a), wi = rho * Mathf.Sin(a);
                    return Boys(wr, wi) * (s.radius * 1.6f);
                }

                // ---- Cross-cap: the cheapest RP^2 immersion, two pinch points.
                case ManifoldSurface.CrossCap:
                {
                    float p = v * Mathf.PI, q = a;
                    float sp = Mathf.Sin(p), cp = Mathf.Cos(p), cq = Mathf.Cos(q);
                    return new Vector3(cq * Mathf.Sin(2f * p), Mathf.Sin(q) * Mathf.Sin(2f * p),
                                       cp * cp - cq * cq * sp * sp) * s.radius;
                }

                // ---- Enneper: a minimal surface, self-intersecting, four lobes.
                case ManifoldSurface.Enneper:
                {
                    float domain = Mathf.Clamp(s.enneperDomain, .3f, 3f);
                    float p = (u * 2f - 1f) * domain, q = (v * 2f - 1f) * domain;
                    var real = new Vector3(p - p * p * p / 3f + p * q * q,
                                           q - q * q * q / 3f + q * p * p, p * p - q * q);
                    var imaginary = new Vector3(q - p * p * q + q * q * q / 3f,
                        -(p + p * p * p / 3f - p * q * q), 2f * p * q);
                    return (real * Mathf.Cos(s.enneperPhase) - imaginary * Mathf.Sin(s.enneperPhase)) * (s.radius * .18f);
                }

                // ---- Catenoid: the minimal surface of revolution, and the helicoid's partner.
                case ManifoldSurface.Catenoid:
                {
                    float c = Mathf.Max(s.minorRadius, .05f);
                    float z = (v * 2f - 1f) * s.extent * .06f;
                    float rr = c * Cosh(z / c);
                    return new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, z) * (s.radius / 3f);
                }

                // ---- Dini's surface: a helicoid wound onto a pseudosphere. Constant negative
                // curvature, so it is a genuinely hyperbolic surface.
                case ManifoldSurface.DinisSurface:
                {
                    float A = s.radius * .34f, B = s.pitch * .22f;
                    float U = u * Mathf.PI * 4f, V = Mathf.Lerp(.08f, 1.6f, v);
                    return new Vector3(A * Mathf.Cos(U) * Mathf.Sin(V),
                                       A * Mathf.Sin(U) * Mathf.Sin(V),
                                       A * (Mathf.Cos(V) + Mathf.Log(Mathf.Max(Mathf.Tan(V * .5f), 1e-4f))) + B * U);
                }

                // ---- Kuen's surface: another constant-negative-curvature surface, with a
                // distinctive flared lobe.
                case ManifoldSurface.KuenSurface:
                {
                    float U = (u * 2f - 1f) * 4f, V = Mathf.Lerp(.08f, Mathf.PI - .08f, v);
                    float sv = Mathf.Sin(V);
                    float d = 1f + U * U * sv * sv;
                    return new Vector3(2f * (Mathf.Cos(U) + U * Mathf.Sin(U)) * sv / d,
                                       2f * (Mathf.Sin(U) - U * Mathf.Cos(U)) * sv / d,
                                       Mathf.Log(Mathf.Max(Mathf.Tan(V * .5f), 1e-4f)) + 2f * Mathf.Cos(V) / d)
                           * (s.radius * .45f);
                }

                // ---- Breather surface: a pseudospherical surface from the sine-Gordon equation.
                case ManifoldSurface.BreatherSurface:
                {
                    const float bb = .4f;
                    float r = 1f - bb * bb, w = Mathf.Sqrt(r);
                    float U = (u * 2f - 1f) * 13.2f, V = (v * 2f - 1f) * 37.4f;
                    float chb = Cosh(bb * U), shb = Sinh(bb * U);
                    float wv = w * V;
                    float denom = bb * ((w * chb) * (w * chb) + (bb * Mathf.Sin(wv)) * (bb * Mathf.Sin(wv)));
                    if (Mathf.Abs(denom) < 1e-5f) denom = 1e-5f;
                    return new Vector3(
                        -U + 2f * r * chb * shb / denom,
                        2f * w * chb * (-(w * Mathf.Cos(V) * Mathf.Cos(wv)) - Mathf.Sin(V) * Mathf.Sin(wv)) / denom,
                        2f * w * chb * (-(w * Mathf.Sin(V) * Mathf.Cos(wv)) + Mathf.Cos(V) * Mathf.Sin(wv)) / denom
                    ) * (s.radius * .3f);
                }

                // ---- Pseudosphere (tractricoid): constant negative curvature, a hyperbolic horn.
                case ManifoldSurface.Pseudosphere:
                {
                    float U = (v * 2f - 1f) * 3f;
                    float sech = 1f / Cosh(U);
                    return new Vector3(sech * Mathf.Cos(a), sech * Mathf.Sin(a), U - Tanh(U)) * s.radius;
                }

                // ---- One-sheet hyperboloid: the ruled saddle of revolution.
                case ManifoldSurface.Hyperboloid:
                {
                    float V = (v * 2f - 1f) * 1.5f;
                    float ch = Cosh(V);
                    return new Vector3(ch * Mathf.Cos(a), ch * Mathf.Sin(a), Sinh(V)) * (s.radius * .45f);
                }

                // ---- Horn torus: minor radius equals major, so the hole closes to a point.
                case ManifoldSurface.HornTorus:
                {
                    float rr = s.radius * (1f + Mathf.Cos(b));
                    return new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, s.radius * Mathf.Sin(b));
                }

                // ---- Twisted torus: the tube rotates as it goes round, so the cross-section
                // shears. knotQ sets how many twists per circuit.
                case ManifoldSurface.TwistedTorus:
                {
                    float twist = b + Mathf.Max(s.knotQ, 1) * a;
                    float rr = s.radius + s.minorRadius * Mathf.Cos(twist);
                    return new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, s.minorRadius * Mathf.Sin(twist));
                }

                // ---- Monkey saddle: three rising and three falling regions, z = x^3 - 3xy^2.
                case ManifoldSurface.MonkeySaddle:
                {
                    float p = (u * 2f - 1f), q = (v * 2f - 1f);
                    return new Vector3(p, q, p * p * p - 3f * p * q * q) * s.radius;
                }

                // ---- Whitney umbrella: the standard stable map singularity, z = y^2.
                case ManifoldSurface.WhitneyUmbrella:
                {
                    float p = (u * 2f - 1f), q = (v * 2f - 1f) * 1.4f;
                    return new Vector3(p * q, p, q * q) * s.radius * .8f;
                }

                // ---- Henneberg: a minimal surface that is non-orientable.
                case ManifoldSurface.Henneberg:
                {
                    float U = (v * 2f - 1f) * .9f, V = a;
                    return new Vector3(
                        2f * Sinh(U) * Mathf.Cos(V) - (2f / 3f) * Sinh(3f * U) * Mathf.Cos(3f * V),
                        2f * Sinh(U) * Mathf.Sin(V) + (2f / 3f) * Sinh(3f * U) * Mathf.Sin(3f * V),
                        2f * Cosh(2f * U) * Mathf.Cos(2f * V)) * (s.radius * .22f);
                }

                // ---- Bour's minimal surface: a spiralling sheet with a branch point.
                case ManifoldSurface.BoursSurface:
                {
                    float U = v * 2f, V = a;
                    return new Vector3(
                        U * Mathf.Cos(V) - U * U * .5f * Mathf.Cos(2f * V),
                        -U * Mathf.Sin(V) - U * U * .5f * Mathf.Sin(2f * V),
                        4f / 3f * Mathf.Pow(Mathf.Max(U, 0f), 1.5f) * Mathf.Cos(1.5f * V)) * (s.radius * .5f);
                }

                // ---- Gielis supershape: the spherical product of two superformulas. One shape
                // family that reaches stars, gears, rounded cubes and shells.
                case ManifoldSurface.Supershape:
                {
                    float lon = (u * 2f - 1f) * Mathf.PI;
                    float lat = (v - .5f) * Mathf.PI;
                    float r1 = Superformula(lon, s.superM.x, s.superN.x, s.superN.y, s.superN.z);
                    float r2 = Superformula(lat, s.superM.y, s.superN.x, s.superN.y, s.superN.z);
                    return new Vector3(r1 * Mathf.Cos(lon) * r2 * Mathf.Cos(lat),
                                       r1 * Mathf.Sin(lon) * r2 * Mathf.Cos(lat),
                                       r2 * Mathf.Sin(lat)) * s.radius;
                }

                // ---- Conical spiral shell: a circular tube swept along a conical helix.
                case ManifoldSurface.ConicalSpiral:
                {
                    float t = v * s.turns * Mathf.PI * 2f;
                    float taper = 1f - v;
                    float ring = s.minorRadius * taper;
                    float rr = s.radius * taper + ring * Mathf.Cos(a);
                    return new Vector3(Mathf.Cos(t) * rr, Mathf.Sin(t) * rr,
                                       s.pitch * v * s.extent * .08f + ring * Mathf.Sin(a));
                }

                // ---- Figure-eight torus: a tube whose cross-section is a lemniscate, so the
                // surface passes through itself once per circuit.
                case ManifoldSurface.FigureEightTorus:
                {
                    float cx = s.minorRadius * Mathf.Sin(b);
                    float cy = s.minorRadius * Mathf.Sin(b) * Mathf.Cos(b);
                    float rr = s.radius + cx;
                    return new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, cy);
                }

                // ---- Trefoil ribbon: a flat band with half-twists swept along a (p,q) torus
                // knot. halfTwists = 1 makes the band one-sided.
                case ManifoldSurface.TrefoilRibbon:
                    return KnotRibbon(s, u, v);

                default:
                {
                    // The original chamber shape, kept so the generalisation can be checked
                    // against what it replaced.
                    float r = s.radius * (1f + .055f * Mathf.Sin(v * Mathf.PI * 8f));
                    return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -4f + v * s.extent);
                }
            }
        }

        /// <summary>
        /// Signed power. Inputs within 1e-6 of zero are treated as zero: a fractional power magnifies
        /// float residue (sin 2π ≈ -1.7e-7 becomes 0.002 at exponent 0.4), which left the
        /// superellipsoid's u = 0 and u = 1 columns visibly apart.
        /// </summary>
        static float SignedPow(float x, float e) =>
            Mathf.Abs(x) < 1e-6f ? 0f : Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), e);

        static float Cosh(float x) => (float)System.Math.Cosh(x);
        static float Sinh(float x) => (float)System.Math.Sinh(x);
        static float Tanh(float x) => (float)System.Math.Tanh(x);

        /// <summary>
        /// Gielis superformula. r(theta) = (|cos(m.theta/4)|^n2 + |sin(m.theta/4)|^n3)^(-1/n1).
        /// n1 near 1 gives smooth blobs, large n1 gives flat facets, m sets the lobe count.
        /// </summary>
        static float Superformula(float theta, float m, float n1, float n2, float n3)
        {
            n1 = Mathf.Max(n1, .05f);
            float t = m * theta * .25f;
            float p1 = Mathf.Pow(Mathf.Abs(Mathf.Cos(t)), Mathf.Max(n2, .05f));
            float p2 = Mathf.Pow(Mathf.Abs(Mathf.Sin(t)), Mathf.Max(n3, .05f));
            float sum = p1 + p2;
            return sum < 1e-6f ? 0f : Mathf.Pow(sum, -1f / n1);
        }

        /// <summary>
        /// Bryant-Kusner Boy's surface for a complex w inside the unit disk.
        ///   den = w^6 + sqrt5.w^3 - 1
        ///   g1 = -1.5 Im( w(1-w^4)/den ),  g2 = -1.5 Re( w(1+w^4)/den )
        ///   g3 = Im( (1+w^6)/den ) - 0.5,  point = (g1,g2,g3)/(g1^2+g2^2+g3^2)
        /// </summary>
        static Vector3 Boys(float wr, float wi)
        {
            CMul(wr, wi, wr, wi, out float w2r, out float w2i);
            CMul(w2r, w2i, wr, wi, out float w3r, out float w3i);
            CMul(w2r, w2i, w2r, w2i, out float w4r, out float w4i);
            CMul(w3r, w3i, w3r, w3i, out float w6r, out float w6i);

            float root5 = Mathf.Sqrt(5f);
            float dr = w6r + root5 * w3r - 1f;
            float di = w6i + root5 * w3i;
            // The denominator genuinely vanishes inside the disk; the guard keeps the ratio finite.
            if (dr * dr + di * di < 1e-9f) { dr = 1e-5f; di = 0f; }

            CMul(wr, wi, 1f - w4r, -w4i, out float n1r, out float n1i);
            CDiv(n1r, n1i, dr, di, out _, out float q1i);
            float g1 = -1.5f * q1i;

            CMul(wr, wi, 1f + w4r, w4i, out float n2r, out float n2i);
            CDiv(n2r, n2i, dr, di, out float q2r, out _);
            float g2 = -1.5f * q2r;

            CDiv(1f + w6r, w6i, dr, di, out _, out float q3i);
            float g3 = q3i - .5f;

            float g = g1 * g1 + g2 * g2 + g3 * g3;
            if (g < 1e-6f) return Vector3.zero;
            return new Vector3(g1 / g, g2 / g, g3 / g);
        }

        static void CMul(float ar, float ai, float br, float bi, out float r, out float i)
        {
            r = ar * br - ai * bi;
            i = ar * bi + ai * br;
        }

        static void CDiv(float ar, float ai, float br, float bi, out float r, out float i)
        {
            float d = br * br + bi * bi;
            if (d < 1e-12f) { r = 0f; i = 0f; return; }
            r = (ar * br + ai * bi) / d;
            i = (ai * br - ar * bi) / d;
        }

        /// <summary>
        /// A flat ribbon with half-twists swept along a (p,q) torus knot. Uses the same
        /// reference-vector frame as <see cref="KnotTube"/>: a Frenet frame flips wherever
        /// curvature vanishes and turns the ribbon inside out.
        /// </summary>
        static Vector3 KnotRibbon(ManifoldSettings s, float u, float v)
        {
            float t = u * Tau;
            Vector3 centre = KnotPoint(s, t);
            const float h = 1e-3f;
            Vector3 tangent = (KnotPoint(s, t + h) - KnotPoint(s, t - h)).normalized;

            Vector3 reference = Mathf.Abs(tangent.z) < .9f ? Vector3.forward : Vector3.right;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(tangent, reference));
            Vector3 binormal = Vector3.Cross(tangent, normal);

            float across = (v * 2f - 1f) * s.bandWidth * .5f;
            float spin = s.halfTwists * .5f * t;
            return centre + (normal * Mathf.Cos(spin) + binormal * Mathf.Sin(spin)) * across;
        }

        /// <summary>
        /// Circle of radius tubeRadius swept along a (p,q) torus knot. The frame is built from the
        /// analytic tangent plus a reference-vector cross product rather than a Frenet frame,
        /// because Frenet flips wherever curvature vanishes.
        /// </summary>
        static Vector3 KnotTube(ManifoldSettings s, float u, float v)
        {
            float t = u * Tau;
            Vector3 centre = KnotPoint(s, t);
            const float h = 1e-3f;
            Vector3 tangent = (KnotPoint(s, t + h) - KnotPoint(s, t - h)).normalized;

            Vector3 reference = Mathf.Abs(tangent.z) < .9f ? Vector3.forward : Vector3.right;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(tangent, reference));
            Vector3 binormal = Vector3.Cross(tangent, normal);

            float ring = v * Tau;
            return centre + (normal * Mathf.Cos(ring) + binormal * Mathf.Sin(ring)) * s.tubeRadius;
        }

        static Vector3 KnotPoint(ManifoldSettings s, float t)
        {
            int p = Mathf.Max(s.knotP, 1), q = Mathf.Max(s.knotQ, 1);
            float r = s.radius + s.minorRadius * Mathf.Cos(q * t);
            return new Vector3(Mathf.Cos(p * t) * r, Mathf.Sin(p * t) * r, s.minorRadius * Mathf.Sin(q * t));
        }

        /// <summary>
        /// The duocylinder: D² × D², whose boundary is two solid tori glued along a ridge. That
        /// ridge is exactly the Clifford torus, so both shapes come from one function.
        /// v is split between the two cells when BothCells is selected.
        /// </summary>
        static Vector3 Duocylinder(ManifoldSettings s, float u, float v, float time)
        {
            float a = u * Tau;
            switch (s.cell)
            {
                case DuocylinderCell.Ridge:
                    return Clifford(s, a, v * Tau, 1f, 1f, time);
                case DuocylinderCell.CellA:
                    return Clifford(s, a, v * Tau, Mathf.Lerp(1f, 1f - s.cellFill, v), 1f, time);
                case DuocylinderCell.CellB:
                    return Clifford(s, a, v * Tau, 1f, Mathf.Lerp(1f, 1f - s.cellFill, v), time);
                default:
                {
                    // First half of v walks up cell A, second half walks up cell B. The two meet
                    // at the shared ridge in the middle, so the surface stays continuous.
                    if (v < .5f)
                    {
                        float k = 1f - s.cellFill * (1f - v * 2f);
                        return Clifford(s, a, v * 2f * Tau, k, 1f, time);
                    }
                    float k2 = 1f - s.cellFill * ((v - .5f) * 2f);
                    return Clifford(s, a, (v - .5f) * 2f * Tau, 1f, k2, time);
                }
            }
        }

        /// <summary>
        /// A point on the Clifford torus in S³, scaled per plane, 4-D rotated, then projected.
        /// scaleA/scaleB pull the point off the ridge and into one of the duocylinder's cells.
        /// </summary>
        static Vector3 Clifford(ManifoldSettings s, float a, float b, float scaleA, float scaleB, float time)
        {
            const float Inv = .70710678f;
            float x = Mathf.Cos(a) * Inv * scaleA;
            float y = Mathf.Sin(a) * Inv * scaleA;
            float z = Mathf.Cos(b) * Inv * scaleB;
            float w = Mathf.Sin(b) * Inv * scaleB;

            // One shared 4-D camera axis for the whole scene, when there is one.
            if (s.useSharedAxis && Hyperspace4DAxis.Current)
                return Hyperspace4DAxis.Current.RotateAndProject(new Vector4(x, y, z, w)) * s.scale4D;

            Rotate(ref x, ref y, s.simpleRates.x * time * Tau);
            Rotate(ref z, ref w, s.simpleRates.y * time * Tau);
            Rotate(ref x, ref z, s.mixedRates.x * time * Tau);
            Rotate(ref y, ref w, s.mixedRates.y * time * Tau);

            return Project(s, x, y, z, w);
        }

        static void Rotate(ref float p, ref float q, float angle)
        {
            if (angle == 0f) return;
            float c = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            float p0 = p, q0 = q;
            p = p0 * c - q0 * sn;
            q = p0 * sn + q0 * c;
        }

        static Vector3 Project(ManifoldSettings s, float x, float y, float z, float w)
        {
            switch (s.projection)
            {
                case Projection4D.Stereographic:
                {
                    // Guarded: w -> 1 sends the point to infinity.
                    float d = 1f - w;
                    float k = s.scale4D / (Mathf.Abs(d) < .06f ? Mathf.Sign(d == 0f ? 1f : d) * .06f : d);
                    return new Vector3(x, y, z) * k;
                }
                case Projection4D.Perspective:
                {
                    float d = Mathf.Max(s.wDistance, 1.05f) - w;
                    return new Vector3(x, y, z) * (s.scale4D / Mathf.Max(d, .06f));
                }
                default:
                    return new Vector3(x, y, z) * s.scale4D;
            }
        }

        /// <summary>
        /// A point on a real 2-D slice of the Fermat quintic z1^n + z2^n = 1, drawn as n^2 patches.
        /// This is the standard Calabi-Yau visualisation (Hanson). For n = 5 that is 25 patches.
        ///
        /// Patch (k1, k2), with x in [0, pi/2] and y in [-1, 1]:
        ///   z1 = exp(2i*pi*k1/n) * (cos(x + iy))^(2/n)
        ///   z2 = exp(2i*pi*k2/n) * (sin(x + iy))^(2/n)
        /// then C^2 -> R^3 with an animatable angle a:
        ///   X = Re z1,  Y = Re z2,  Z = Im z1 * cos a + Im z2 * sin a
        ///
        /// The fractional power must go through the polar form. A naive pow tears along the branch
        /// cuts, which shows up as gaps exactly where the lobes meet.
        /// </summary>
        public static Vector3 CalabiYau(int n, int patch, float u, float v, float angle, float scale)
        {
            n = Mathf.Clamp(n, 2, 8);
            int k1 = patch / n, k2 = patch % n;

            float x = u * Mathf.PI * .5f;
            float y = (v * 2f - 1f);

            ComplexCos(x, y, out float c1re, out float c1im);
            ComplexSin(x, y, out float s1re, out float s1im);

            float e = 2f / n;
            ComplexPow(c1re, c1im, e, out float z1re, out float z1im);
            ComplexPow(s1re, s1im, e, out float z2re, out float z2im);

            // Multiply by the patch's root of unity.
            float a1 = Mathf.PI * 2f * k1 / n, a2 = Mathf.PI * 2f * k2 / n;
            Rotate2(ref z1re, ref z1im, a1);
            Rotate2(ref z2re, ref z2im, a2);

            return new Vector3(z1re, z2re, z1im * Mathf.Cos(angle) + z2im * Mathf.Sin(angle)) * scale;
        }

        static void ComplexCos(float x, float y, out float re, out float im)
        {
            // cos(x + iy) = cos x cosh y - i sin x sinh y
            re = Mathf.Cos(x) * (float)System.Math.Cosh(y);
            im = -Mathf.Sin(x) * (float)System.Math.Sinh(y);
        }

        static void ComplexSin(float x, float y, out float re, out float im)
        {
            // sin(x + iy) = sin x cosh y + i cos x sinh y
            re = Mathf.Sin(x) * (float)System.Math.Cosh(y);
            im = Mathf.Cos(x) * (float)System.Math.Sinh(y);
        }

        /// <summary>w^e through the polar form, which is what keeps patch seams from tearing.</summary>
        static void ComplexPow(float re, float im, float e, out float outRe, out float outIm)
        {
            float r = Mathf.Sqrt(re * re + im * im);
            if (r < 1e-7f) { outRe = 0f; outIm = 0f; return; }
            float theta = Mathf.Atan2(im, re);
            float rp = Mathf.Pow(r, e);
            outRe = rp * Mathf.Cos(theta * e);
            outIm = rp * Mathf.Sin(theta * e);
        }

        static void Rotate2(ref float re, ref float im, float angle)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float r0 = re, i0 = im;
            re = r0 * c - i0 * s;
            im = r0 * s + i0 * c;
        }

        /// <summary>True where the surface wraps in u, so the mesh closes instead of showing a seam.</summary>
        public static bool WrapsU(ManifoldSurface surface)
        {
            switch (surface)
            {
                // These use u as a linear parameter across an open sheet, not an angle.
                // Dini winds two pitched turns and Bour carries cos(1.5V), so neither closes in u.
                case ManifoldSurface.DinisSurface:
                case ManifoldSurface.BoursSurface:
                case ManifoldSurface.Helicoid:
                case ManifoldSurface.Enneper:
                case ManifoldSurface.KuenSurface:
                case ManifoldSurface.BreatherSurface:
                case ManifoldSurface.MonkeySaddle:
                case ManifoldSurface.WhitneyUmbrella:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>True where the surface wraps in v. Open strips and sheets do not.</summary>
        public static bool WrapsV(ManifoldSurface surface)
        {
            switch (surface)
            {
                case ManifoldSurface.Cylinder:
                case ManifoldSurface.MobiusStrip:
                case ManifoldSurface.Helicoid:
                case ManifoldSurface.Sphere:
                case ManifoldSurface.Superellipsoid:
                // Open in v as well: discs, sheets, horns and bands with two free edges.
                case ManifoldSurface.BoysSurface:
                case ManifoldSurface.CrossCap:
                case ManifoldSurface.Enneper:
                case ManifoldSurface.Catenoid:
                case ManifoldSurface.DinisSurface:
                case ManifoldSurface.KuenSurface:
                case ManifoldSurface.BreatherSurface:
                case ManifoldSurface.Pseudosphere:
                case ManifoldSurface.Hyperboloid:
                case ManifoldSurface.MonkeySaddle:
                case ManifoldSurface.WhitneyUmbrella:
                case ManifoldSurface.Henneberg:
                case ManifoldSurface.BoursSurface:
                case ManifoldSurface.Supershape:
                case ManifoldSurface.ConicalSpiral:
                case ManifoldSurface.TrefoilRibbon:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// True where the u = 1 edge meets the u = 0 edge mirrored in v — (1, v) is (0, 1 − v) — so the
        /// surface is one-sided across that seam: the Klein bottle, and the Möbius strip and trefoil
        /// ribbon when their half-twist count is odd. A grid that joins its last column to its first
        /// must join row j to row (rows − 1 − j) on these, not to row j.
        /// </summary>
        public static bool FlipsAcrossUSeam(ManifoldSurface surface, ManifoldSettings s)
        {
            switch (surface)
            {
                case ManifoldSurface.KleinBottle: return true;
                case ManifoldSurface.MobiusStrip:
                case ManifoldSurface.TrefoilRibbon: return s == null ? true : (s.halfTwists & 1) == 1;
                default: return false;
            }
        }

        /// <summary><see cref="WrapsU(ManifoldSurface)"/> for these settings.</summary>
        public static bool WrapsU(ManifoldSurface surface, ManifoldSettings s) => WrapsU(surface);

        /// <summary>
        /// <see cref="WrapsV(ManifoldSurface)"/> for these settings. The duocylinder only closes in v as
        /// the bare ridge (the Clifford torus); with a cell filled, v runs from one rim to another.
        /// </summary>
        public static bool WrapsV(ManifoldSurface surface, ManifoldSettings s)
        {
            if (surface == ManifoldSurface.Duocylinder && s != null)
                return s.cell == DuocylinderCell.Ridge || s.cellFill <= 0f;
            return WrapsV(surface);
        }

        /// <summary>
        /// <see cref="HasPoles(ManifoldSurface)"/> for these settings. A fully filled duocylinder cell
        /// shrinks one rim to a single point, which is a pole like any other.
        /// </summary>
        public static bool HasPoles(ManifoldSurface surface, ManifoldSettings s)
        {
            if (surface == ManifoldSurface.Duocylinder && s != null)
                return s.cell != DuocylinderCell.Ridge && s.cellFill >= .999f;
            return HasPoles(surface);
        }

        /// <summary>
        /// Surfaces with a pole, where a whole ring of the grid collapses to one point. Zero-area
        /// triangles there make fwidth(bary) meaningless and the wire flickers, so the chamber
        /// insets v by half a cell when either morph endpoint has one.
        /// </summary>
        public static bool HasPoles(ManifoldSurface surface)
        {
            switch (surface)
            {
                case ManifoldSurface.Sphere:
                case ManifoldSurface.Superellipsoid:
                case ManifoldSurface.Supershape:
                case ManifoldSurface.RomanSurface:
                case ManifoldSurface.CrossCap:
                case ManifoldSurface.BoysSurface:
                case ManifoldSurface.BoursSurface:
                case ManifoldSurface.ConicalSpiral:
                    return true;
                default:
                    return false;
            }
        }
    }
}
