#ifndef ESCHER_FIELD_4D_INCLUDED
#define ESCHER_FIELD_4D_INCLUDED

// The 4-D room field, in HLSL. Mirrors EscherFields.cs so the mesh path and the raymarched
// portal path agree — if these two drift apart, a portal shows a different building to the one
// you are standing in.
//
// Why this exists at all: a portal that only looks along W does not need a camera. It needs a ray
// tilted into the fourth dimension, marched through the field. That removes one camera and one
// render texture per portal, which is the difference between 2-3 cameras and a dozen.

// ---- parameters, fed per portal ------------------------------------------------
// Packed by EscherFieldGpu.Pack (C#), which is the one place that knows this layout.
// _EscherField   : x = field id, y = level, z = thickness, w = frequency
// _EscherStep    : x = dislocation, y = axis (0 X, 1 Y, 2 Z), z = core, w = W influence
// _EscherSlice   : xyz = W-plane turns (XW, YW, ZW), w = W offset of this slice
// _EscherMarch   : x = max steps, y = max distance, z = step relaxation, w = surface epsilon
// _EscherShape   : x = Barth W, y = Mandelbulb power, z = iterations, w = folds
// _EscherBox     : x = Mandelbox scale, y = min radius, z = fixed radius, w = unused
// _EscherInvert  : xyz = inversion centre, w = radius (0 = off)
// _EscherDroste  : x = scale (0 = off), y = sectors, z = twist, w = reference radius
// _EscherDroste2 : x = axis (0 X, 1 Y, 2 Z), y = core radius, zw = unused
// _EscherScroll  : xyz = lattice offset in periods, already wrapped to [0, 1), w = unused

float4 _EscherField;
float4 _EscherStep;
float4 _EscherSlice;
float4 _EscherMarch;
float4 _EscherShape;
float4 _EscherBox;
float4 _EscherInvert;
float4 _EscherDroste;
float4 _EscherDroste2;
float4 _EscherScroll;

// ---- field ids (match RoomField) -----------------------------------------------
#define ESCHER_GYROID    0
#define ESCHER_SCHWARZP  1
#define ESCHER_SCHWARZD  2
#define ESCHER_NEOVIUS   3
#define ESCHER_LIDINOID  4
#define ESCHER_SPLITP    5
#define ESCHER_BARTH     6
#define ESCHER_GOURSAT   7
#define ESCHER_MANDELBOX 8
#define ESCHER_MANDELBULB 9
#define ESCHER_MENGER    10
#define ESCHER_SIERPINSKI 11

void EscherRot(inout float a, inout float b, float turns)
{
    float ang = turns * 6.2831853;
    float c = cos(ang), s = sin(ang);
    float a0 = a, b0 = b;
    a = a0 * c - b0 * s;
    b = a0 * s + b0 * c;
}

float3 EscherToAxis(float3 p, int axis)
{
    return axis == 0 ? float3(p.y, p.z, p.x) : axis == 1 ? float3(p.z, p.x, p.y) : p;
}

float3 EscherFromAxis(float3 q, int axis)
{
    return axis == 0 ? float3(q.z, q.x, q.y) : axis == 1 ? float3(q.y, q.z, q.x) : q;
}

// The lattice repeats every 2 / frequency: the periodic fields are evaluated at pi * frequency * p.
float EscherPeriod()
{
    return 2.0 / max(_EscherField.w, 0.001);
}

// ---- Escher space: window point -> lattice sample ------------------------------
// Mirrors EscherSpace.Map / Evaluate (C#) step for step: sphere inversion, then either the Droste
// spiral (carrying the dislocation inside its coordinates) or the screw dislocation, then the
// scroll. Every jump these make is a whole number of lattice periods, so the field cannot see it.

struct EscherLattice
{
    float3 position;   // where the field is sampled
    float3 plain;      // the unsheared sample, blended in inside the dislocation core
    float weight;      // 1 = use `position` only
};

float3 EscherInvert(float3 p)
{
    float r = _EscherInvert.w;
    if (r <= 0.0) return p;
    float3 d = p - _EscherInvert.xyz;
    float m = max(dot(d, d), 1e-12);   // the centre is the image of infinity
    return _EscherInvert.xyz + d * (r * r / m);
}

// Log-cylindrical coordinates about the Droste axis: exactly invariant under scaling by the Droste
// scale, and with a whole-number twist one turn also steps one scale level (Escher's spiral).
float3 EscherDroste(float3 p, float period, float dislocation)
{
    float3 q = EscherToAxis(p, (int)(_EscherDroste2.x + 0.5));
    float rho = max(length(q.xy), max(_EscherDroste2.y, 1e-6));
    float turns = atan2(q.y, q.x) / 6.2831853;
    float levels = log(rho / max(_EscherDroste.w, 1e-4)) / log(max(_EscherDroste.x, 1.0001));
    float n = max(_EscherDroste.y, 1.0);
    return float3(period * (levels + _EscherDroste.z * turns),
                  period * n * turns,
                  period * (n * q.z / (6.2831853 * rho) + dislocation * turns));
}

EscherLattice EscherMap(float3 p)
{
    float period = EscherPeriod();
    float dislocation = _EscherStep.x;
    p = EscherInvert(p);

    EscherLattice l;
    l.weight = 1.0;
    if (_EscherDroste.x > 0.0)
    {
        l.position = EscherDroste(p, period, dislocation);
        l.plain = l.position;
    }
    else if (dislocation != 0.0)
    {
        // Screw dislocation: one circuit of the axis advances the lattice by `dislocation`
        // periods. The core blends the plain and sheared fields (see EscherField) instead of
        // easing the shear, which used to leave a crack along the cut inside the core.
        int axis = (int)(_EscherStep.y + 0.5);
        float3 q = EscherToAxis(p, axis);
        float radius = length(q.xy);
        l.plain = p;
        q.z += dislocation * period * (atan2(q.y, q.x) / 6.2831853);
        l.position = EscherFromAxis(q, axis);
        l.weight = radius <= 0.0 ? 0.0
                 : smoothstep(0.0, 1.0, min(radius / max(_EscherStep.z, 1e-4), 1.0));
    }
    else
    {
        l.position = p;
        l.plain = p;
    }

    // Scroll last, in lattice coordinates, wrapped on the CPU: the loop is exact and never drifts.
    float3 offset = _EscherScroll.xyz * period;
    l.position += offset;
    l.plain += offset;
    return l;
}

float EscherBox(float3 p, float3 b)
{
    float3 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float EscherCross(float3 r)
{
    return min(max(r.x, r.y), min(max(r.y, r.z), max(r.z, r.x)));
}

// The raw field on a 4-D point. The periodic family is genuinely four-dimensional: W shifts the
// phase of the 4-D gyroid's closing term, sin z cos(x + w), so every 3-D slice is gyroid-like but no
// two slices are the same geometry — and W = 0 is exactly the gyroid. That is what a portal looks
// through. Must match EscherFields.Raw on the CPU term for term.
float EscherRaw(float3 p, float w)
{
    int id = (int)(_EscherField.x + 0.5);
    float k = 3.14159265 * _EscherField.w;
    float3 q = p * k;
    float qw = w * k;

    if (id == ESCHER_SCHWARZP)
        return cos(q.x) + cos(q.y) + cos(q.z) + (cos(qw) - 1.0);   // exactly Schwarz P at W = 0

    if (id == ESCHER_SCHWARZD)
    {
        float3 sn = sin(q), cs = cos(q);
        float sw = sin(qw), cw = cos(qw);
        return sn.x * sn.y * sn.z * cw + sn.x * cs.y * cs.z
             + cs.x * sn.y * cs.z + cs.x * cs.y * sn.z * cw
             + sw * sn.x * sn.y * 0.5;
    }

    if (id == ESCHER_NEOVIUS)
    {
        float3 cs = cos(q);
        float cw = cos(qw);
        return 3.0 * (cs.x + cs.y + cs.z + cw) + 4.0 * cs.x * cs.y * cs.z * cw - 3.0;   // Neovius at W = 0
    }

    if (id == ESCHER_LIDINOID)
    {
        q.z += qw * 0.5;
        float3 s2 = sin(2.0 * q), c2 = cos(2.0 * q);
        float3 sn = sin(q), cs = cos(q);
        return s2.x * cs.y * sn.z + s2.y * cs.z * sn.x + s2.z * cs.x * sn.y
             - c2.x * c2.y - c2.y * c2.z - c2.z * c2.x;
    }

    if (id == ESCHER_SPLITP)
    {
        q.x += qw * 0.5;
        float3 s2 = sin(2.0 * q), c2 = cos(2.0 * q);
        float3 sn = sin(q), cs = cos(q);
        return 1.1 * (s2.x * sn.z * cs.y + s2.y * sn.x * cs.z + s2.z * sn.y * cs.x)
             - 0.2 * (c2.x * c2.y + c2.y * c2.z + c2.z * c2.x)
             - 0.4 * (c2.x + c2.y + c2.z);
    }

    if (id == ESCHER_BARTH)
    {
        const float phi = 1.6180339887;
        float3 r = p * 1.8;
        float x2 = r.x * r.x, y2 = r.y * r.y, z2 = r.z * r.z;
        float p2 = phi * phi, w2 = _EscherShape.x * _EscherShape.x;
        float a = p2 * x2 - y2, b = p2 * y2 - z2, c = p2 * z2 - x2;
        float rr = x2 + y2 + z2 - w2;
        float v = 4.0 * a * b * c - (1.0 + 2.0 * phi) * rr * rr * w2;
        return sign(v) * pow(abs(v), 0.25);
    }

    if (id == ESCHER_GOURSAT)
    {
        // x^4 by multiplication: HLSL pow(x, y) is exp2(y * log2(x)), NaN for negative x on most
        // GPUs, which used to drop every point with a negative coordinate.
        float3 r = p * 1.5;
        float3 sq = r * r;
        float r2 = sq.x + sq.y + sq.z;
        return dot(sq, sq) - 1.5 * r2 * r2 + 1.0;
    }

    if (id == ESCHER_MANDELBOX)
    {
        float3 c = p * 3.0, z = c;
        float dr = 1.0;
        float scale = _EscherBox.x, minR2 = _EscherBox.y * _EscherBox.y, fixR2 = _EscherBox.z * _EscherBox.z;
        int iterations = (int)clamp(_EscherShape.z, 2.0, 20.0);
        [loop]
        for (int i = 0; i < iterations; i++)
        {
            z = clamp(z, -1.0, 1.0) * 2.0 - z;
            float m = dot(z, z);
            if (m < minR2) { float t = fixR2 / minR2; z *= t; dr *= t; }
            else if (m < fixR2) { float t = fixR2 / m; z *= t; dr *= t; }
            z = z * scale + c;
            dr = dr * abs(scale) + 1.0;
        }
        return length(z) / max(abs(dr), 1e-6);
    }

    // Escape time turned into a distance estimate by the running derivative: 0.5 log(r) r / dr.
    if (id == ESCHER_MANDELBULB)
    {
        float3 c = p * 1.3, z = c;
        float dr = 1.0, r = 0.0, power = _EscherShape.y;
        int iterations = (int)clamp(_EscherShape.z, 2.0, 20.0);
        [loop]
        for (int i = 0; i < iterations; i++)
        {
            r = length(z);
            if (r > 2.2) break;
            float theta = acos(clamp(z.z / max(r, 1e-6), -1.0, 1.0));
            float phi = atan2(z.y, z.x);
            dr = pow(r, power - 1.0) * power * dr + 1.0;
            float zr = pow(r, power);
            theta *= power; phi *= power;
            z = zr * float3(sin(theta) * cos(phi), sin(theta) * sin(phi), cos(theta)) + c;
        }
        return r < 1e-5 ? -1.0 : 0.5 * log(max(r, 1.0001)) * r / max(dr, 1e-6);
    }

    if (id == ESCHER_MENGER)
    {
        float3 r = p * 1.1;
        float d = EscherBox(r, 1.0);
        float scale = 1.0;
        int depth = (int)clamp(_EscherShape.w, 1.0, 6.0);
        [loop]
        for (int i = 0; i < depth; i++)
        {
            // fmod of |x| rather than a floor-based repeat: the two differ only by the sign of a,
            // and only |a| is used.
            float3 a = fmod(abs(r * scale), 2.0) - 1.0;
            scale *= 3.0;
            float3 c = 1.0 - 3.0 * abs(a);
            d = max(d, EscherCross(c) / scale);
        }
        return d;
    }

    if (id == ESCHER_SIERPINSKI)
    {
        float3 z = p * 1.4;
        const float scale = 2.0;
        int depth = (int)clamp(_EscherShape.w, 1.0, 6.0);
        [loop]
        for (int i = 0; i < depth; i++)
        {
            if (z.x + z.y < 0.0) z.xy = -z.yx;
            if (z.x + z.z < 0.0) z.xz = -z.zx;
            if (z.y + z.z < 0.0) z.yz = -z.zy;
            z = z * scale - (scale - 1.0);
        }
        return length(z) * pow(scale, -(float)depth) - 0.02;
    }

    // Gyroid, 4-D: W is a phase on the closing term, so W = 0 is exactly the gyroid.
    return sin(q.x) * cos(q.y) + sin(q.y) * cos(q.z) + sin(q.z) * cos(q.x + qw);
}

// The raw field at a lattice point on this portal's slice. The slice turn is applied to the 4-D
// point before evaluating, which is what makes one portal show a different room from the next.
// W participates scaled by its influence; at 0 the field is its plain 3-D form and the slice turn
// is skipped, as on the CPU.
float EscherSliced(float3 p, float w)
{
    float wEff = w * _EscherStep.w;
    if (_EscherStep.w > 0.0)
    {
        float3 turns = _EscherSlice.xyz;
        EscherRot(p.x, wEff, turns.x);
        EscherRot(p.y, wEff, turns.y);
        EscherRot(p.z, wEff, turns.z);
    }
    return EscherRaw(p, wEff);
}

// Field value at a window point on slice `w`: the Escher space map, the field (blended with the
// plain field inside the dislocation core, so the cut never cracks), then level and thickness.
// Mirrors EscherFields.Sample.
float EscherField(float3 p, float w)
{
    EscherLattice l = EscherMap(p);
    float v = EscherSliced(l.position, w);
    if (l.weight < 1.0) v = lerp(EscherSliced(l.plain, w), v, l.weight);
    v -= _EscherField.y;
    float thickness = _EscherField.z;
    if (thickness > 0.0) v = abs(v) - thickness;
    return v;
}

float3 EscherNormal(float3 p, float w, float h)
{
    float2 e = float2(h, 0.0);
    return normalize(float3(
        EscherField(p + e.xyy, w) - EscherField(p - e.xyy, w),
        EscherField(p + e.yxy, w) - EscherField(p - e.yxy, w),
        EscherField(p + e.yyx, w) - EscherField(p - e.yyx, w)));
}

// Marches a ray that is tilted into W. `wTilt` is the fourth component of the direction: at 0 the
// ray stays in the 3-D hyperplane and this is an ordinary raymarch, and as it grows the ray leans
// further out of 3-space, so the surface it finds is the fourth-dimensional angle seen from this
// 3-D viewing angle.
//
// The periodic fields are level sets, not true distances, so the step is relaxed rather than taken
// at face value: |f| / |grad f| is the first-order distance estimate, and the relaxation keeps it
// conservative where the gradient is small.
bool EscherMarch(float3 origin, float3 direction, float wStart, float wTilt,
                 out float3 hit, out float hitW, out float travelled)
{
    int maxSteps = (int)_EscherMarch.x;
    float maxDistance = _EscherMarch.y;
    float relax = _EscherMarch.z;
    float epsilon = _EscherMarch.w;

    travelled = 0.0;
    hit = origin;
    hitW = wStart;

    [loop]
    for (int i = 0; i < maxSteps; i++)
    {
        float3 p = origin + direction * travelled;
        float w = wStart + wTilt * travelled;
        float v = EscherField(p, w);

        if (abs(v) < epsilon)
        {
            hit = p; hitW = w;
            return true;
        }

        // First-order distance from a level set: value over gradient magnitude.
        float3 g = float3(
            EscherField(p + float3(epsilon, 0, 0), w) - v,
            EscherField(p + float3(0, epsilon, 0), w) - v,
            EscherField(p + float3(0, 0, epsilon), w) - v) / epsilon;
        float grad = max(length(g), 1e-4);

        float step = max(abs(v) / grad * relax, epsilon);
        travelled += step;
        if (travelled > maxDistance) break;
    }
    return false;
}

#endif // ESCHER_FIELD_4D_INCLUDED
