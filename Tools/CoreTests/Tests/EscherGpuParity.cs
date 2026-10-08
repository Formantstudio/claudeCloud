using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using PsychedelicLab.EscherWorld;
using PsychedelicLab.GeometryFX;
using U = PsychedelicLab.EscherWorld.EscherFieldGpu.Uniforms;

/// <summary>
/// Plan P7: the portal shader's field is the room's field. There is no HLSL runtime here, so
/// <see cref="Mirror"/> is EscherField4D.hlsl transcribed line for line, reading only the packed
/// uniforms; it is checked against <see cref="EscherFields.Sample"/> for every field with every
/// warp, which tests the packing and the shader's logic together. The HLSL itself is compiled with
/// glslang when it is installed.
/// </summary>
public class EscherGpuParity
{
    static class Mirror
    {
        static Vector3 ToAxis(Vector3 p, int axis) => axis == 0 ? new Vector3(p.y, p.z, p.x) : axis == 1 ? new Vector3(p.z, p.x, p.y) : p;
        static Vector3 FromAxis(Vector3 q, int axis) => axis == 0 ? new Vector3(q.z, q.x, q.y) : axis == 1 ? new Vector3(q.y, q.z, q.x) : q;
        static float Period(in U u) => 2f / Mathf.Max(u.field.w, .001f);
        static float Smoothstep(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        static float Fmod(float x, float y) => x - y * (float)Math.Truncate(x / y);

        static Vector3 Invert(in U u, Vector3 p)
        {
            float r = u.invert.w;
            if (r <= 0f) return p;
            Vector3 c = u.invert, d = p - c;
            float m = Mathf.Max(Vector3.Dot(d, d), 1e-12f);
            return c + d * (r * r / m);
        }

        static Vector3 Droste(in U u, Vector3 p, float period, float dislocation)
        {
            Vector3 q = ToAxis(p, (int)(u.droste2.x + .5f));
            float rho = Mathf.Max(new Vector2(q.x, q.y).magnitude, Mathf.Max(u.droste2.y, 1e-6f));
            float turns = Mathf.Atan2(q.y, q.x) / 6.2831853f;
            float levels = Mathf.Log(rho / Mathf.Max(u.droste.w, 1e-4f)) / Mathf.Log(Mathf.Max(u.droste.x, 1.0001f));
            float n = Mathf.Max(u.droste.y, 1f);
            return new Vector3(period * (levels + u.droste.z * turns), period * n * turns,
                               period * (n * q.z / (6.2831853f * rho) + dislocation * turns));
        }

        static void Map(in U u, Vector3 p, out Vector3 position, out Vector3 plain, out float weight)
        {
            float period = Period(u), dislocation = u.step.x;
            p = Invert(u, p);
            weight = 1f;
            if (u.droste.x > 0f) { position = Droste(u, p, period, dislocation); plain = position; }
            else if (dislocation != 0f)
            {
                int axis = (int)(u.step.y + .5f);
                Vector3 q = ToAxis(p, axis);
                float radius = new Vector2(q.x, q.y).magnitude;
                plain = p;
                q.z += dislocation * period * (Mathf.Atan2(q.y, q.x) / 6.2831853f);
                position = FromAxis(q, axis);
                weight = radius <= 0f ? 0f : Smoothstep(0f, 1f, Mathf.Min(radius / Mathf.Max(u.step.z, 1e-4f), 1f));
            }
            else { position = p; plain = p; }
            Vector3 offset = (Vector3)u.scroll * period;
            position += offset;
            plain += offset;
        }

        static void Rot(ref float a, ref float b, float turns)
        {
            float ang = turns * 6.2831853f, c = Mathf.Cos(ang), s = Mathf.Sin(ang), a0 = a, b0 = b;
            a = a0 * c - b0 * s;
            b = a0 * s + b0 * c;
        }

        static float Box(Vector3 p, Vector3 b)
        {
            Vector3 q = new Vector3(Mathf.Abs(p.x) - b.x, Mathf.Abs(p.y) - b.y, Mathf.Abs(p.z) - b.z);
            return new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }

        static float Cross(Vector3 r) => Mathf.Min(Mathf.Max(r.x, r.y), Mathf.Min(Mathf.Max(r.y, r.z), Mathf.Max(r.z, r.x)));

        static float Raw(in U u, Vector3 p, float w)
        {
            int id = (int)(u.field.x + .5f);
            float k = 3.14159265f * u.field.w;
            Vector3 q = p * k;
            float qw = w * k;
            switch (id)
            {
                case 1: return Mathf.Cos(q.x) + Mathf.Cos(q.y) + Mathf.Cos(q.z) + (Mathf.Cos(qw) - 1f);
                case 2:
                {
                    float sx = Mathf.Sin(q.x), sy = Mathf.Sin(q.y), sz = Mathf.Sin(q.z), cx = Mathf.Cos(q.x), cy = Mathf.Cos(q.y), cz = Mathf.Cos(q.z);
                    float sw = Mathf.Sin(qw), cw = Mathf.Cos(qw);
                    return sx * sy * sz * cw + sx * cy * cz + cx * sy * cz + cx * cy * sz * cw + sw * sx * sy * .5f;
                }
                case 3:
                {
                    float cw = Mathf.Cos(qw);
                    return 3f * (Mathf.Cos(q.x) + Mathf.Cos(q.y) + Mathf.Cos(q.z) + cw) + 4f * Mathf.Cos(q.x) * Mathf.Cos(q.y) * Mathf.Cos(q.z) * cw - 3f;
                }
                case 4:
                {
                    q.z += qw * .5f;
                    return Mathf.Sin(2 * q.x) * Mathf.Cos(q.y) * Mathf.Sin(q.z) + Mathf.Sin(2 * q.y) * Mathf.Cos(q.z) * Mathf.Sin(q.x)
                         + Mathf.Sin(2 * q.z) * Mathf.Cos(q.x) * Mathf.Sin(q.y)
                         - Mathf.Cos(2 * q.x) * Mathf.Cos(2 * q.y) - Mathf.Cos(2 * q.y) * Mathf.Cos(2 * q.z) - Mathf.Cos(2 * q.z) * Mathf.Cos(2 * q.x);
                }
                case 5:
                {
                    q.x += qw * .5f;
                    float c2x = Mathf.Cos(2 * q.x), c2y = Mathf.Cos(2 * q.y), c2z = Mathf.Cos(2 * q.z);
                    return 1.1f * (Mathf.Sin(2 * q.x) * Mathf.Sin(q.z) * Mathf.Cos(q.y) + Mathf.Sin(2 * q.y) * Mathf.Sin(q.x) * Mathf.Cos(q.z)
                                 + Mathf.Sin(2 * q.z) * Mathf.Sin(q.y) * Mathf.Cos(q.x))
                         - .2f * (c2x * c2y + c2y * c2z + c2z * c2x) - .4f * (c2x + c2y + c2z);
                }
                case 6:
                {
                    const float phi = 1.6180339887f;
                    Vector3 r = p * 1.8f;
                    float x2 = r.x * r.x, y2 = r.y * r.y, z2 = r.z * r.z, p2 = phi * phi, w2 = u.shape.x * u.shape.x;
                    float a = p2 * x2 - y2, b = p2 * y2 - z2, c = p2 * z2 - x2, rr = x2 + y2 + z2 - w2;
                    float v = 4f * a * b * c - (1f + 2f * phi) * rr * rr * w2;
                    return Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), .25f);
                }
                case 7:
                {
                    Vector3 r = p * 1.5f, sq = Vector3.Scale(r, r);
                    float r2 = sq.x + sq.y + sq.z;
                    return Vector3.Dot(sq, sq) - 1.5f * r2 * r2 + 1f;
                }
                case 8:
                {
                    Vector3 c = p * 3f, z = c;
                    float dr = 1f, scale = u.box.x, minR2 = u.box.y * u.box.y, fixR2 = u.box.z * u.box.z;
                    int iterations = (int)Mathf.Clamp(u.shape.z, 2f, 20f);
                    for (int i = 0; i < iterations; i++)
                    {
                        z = new Vector3(Mathf.Clamp(z.x, -1f, 1f) * 2f - z.x, Mathf.Clamp(z.y, -1f, 1f) * 2f - z.y, Mathf.Clamp(z.z, -1f, 1f) * 2f - z.z);
                        float m = Vector3.Dot(z, z);
                        if (m < minR2) { float t = fixR2 / minR2; z *= t; dr *= t; }
                        else if (m < fixR2) { float t = fixR2 / m; z *= t; dr *= t; }
                        z = z * scale + c;
                        dr = dr * Mathf.Abs(scale) + 1f;
                    }
                    return z.magnitude / Mathf.Max(Mathf.Abs(dr), 1e-6f);
                }
                case 9:
                {
                    Vector3 c = p * 1.3f, z = c;
                    float dr = 1f, r = 0f, power = u.shape.y;
                    int iterations = (int)Mathf.Clamp(u.shape.z, 2f, 20f);
                    for (int i = 0; i < iterations; i++)
                    {
                        r = z.magnitude;
                        if (r > 2.2f) break;
                        float theta = Mathf.Acos(Mathf.Clamp(z.z / Mathf.Max(r, 1e-6f), -1f, 1f)), ph = Mathf.Atan2(z.y, z.x);
                        dr = Mathf.Pow(r, power - 1f) * power * dr + 1f;
                        float zr = Mathf.Pow(r, power);
                        theta *= power; ph *= power;
                        z = zr * new Vector3(Mathf.Sin(theta) * Mathf.Cos(ph), Mathf.Sin(theta) * Mathf.Sin(ph), Mathf.Cos(theta)) + c;
                    }
                    return r < 1e-5f ? -1f : .5f * Mathf.Log(Mathf.Max(r, 1.0001f)) * r / Mathf.Max(dr, 1e-6f);
                }
                case 10:
                {
                    Vector3 r = p * 1.1f;
                    float d = Box(r, Vector3.one), scale = 1f;
                    int depth = (int)Mathf.Clamp(u.shape.w, 1f, 6f);
                    for (int i = 0; i < depth; i++)
                    {
                        Vector3 a = new Vector3(Fmod(Mathf.Abs(r.x * scale), 2f) - 1f, Fmod(Mathf.Abs(r.y * scale), 2f) - 1f, Fmod(Mathf.Abs(r.z * scale), 2f) - 1f);
                        scale *= 3f;
                        Vector3 cc = new Vector3(1f - 3f * Mathf.Abs(a.x), 1f - 3f * Mathf.Abs(a.y), 1f - 3f * Mathf.Abs(a.z));
                        d = Mathf.Max(d, Cross(cc) / scale);
                    }
                    return d;
                }
                case 11:
                {
                    Vector3 z = p * 1.4f;
                    const float scale = 2f;
                    int depth = (int)Mathf.Clamp(u.shape.w, 1f, 6f);
                    for (int i = 0; i < depth; i++)
                    {
                        if (z.x + z.y < 0f) z = new Vector3(-z.y, -z.x, z.z);
                        if (z.x + z.z < 0f) z = new Vector3(-z.z, z.y, -z.x);
                        if (z.y + z.z < 0f) z = new Vector3(z.x, -z.z, -z.y);
                        z = z * scale - Vector3.one * (scale - 1f);
                    }
                    return z.magnitude * Mathf.Pow(scale, -(float)depth) - .02f;
                }
                default:
                    return Mathf.Sin(q.x) * Mathf.Cos(q.y) + Mathf.Sin(q.y) * Mathf.Cos(q.z) + Mathf.Sin(q.z) * Mathf.Cos(q.x + qw);
            }
        }

        static float Sliced(in U u, Vector3 p, float w)
        {
            float wEff = w * u.step.w;
            if (u.step.w > 0f)
            {
                Rot(ref p.x, ref wEff, u.slice.x);
                Rot(ref p.y, ref wEff, u.slice.y);
                Rot(ref p.z, ref wEff, u.slice.z);
            }
            return Raw(u, p, wEff);
        }

        public static float Field(in U u, Vector3 p, float w)
        {
            Map(u, p, out var position, out var plain, out float weight);
            float v = Sliced(u, position, w);
            if (weight < 1f) v = Mathf.Lerp(Sliced(u, plain, w), v, weight);
            v -= u.field.y;
            if (u.field.z > 0f) v = Mathf.Abs(v) - u.field.z;
            return v;
        }
    }

    static EscherFieldSettings Settings(RoomField field, int variant)
    {
        var s = new EscherFieldSettings
        {
            field = field, level = .1f, thickness = variant % 2 == 0 ? .12f : 0f, frequency = 1.7f,
            w = .4f, wInfluence = variant == 3 ? 0f : .8f, wRotation = new Vector3(.1f, -.05f, .2f),
            dislocation = variant == 1 || variant == 5 ? 1f : 0f, dislocationAxis = (RoomAxis)(variant % 3), dislocationCore = .35f,
            barthW = 1.3f, power = 7f, iterations = 9, folds = 4, boxScale = -1.9f, minRadius = .4f, fixedRadius = 1.2f,
        };
        s.space = new EscherSpace
        {
            invert = variant == 2 || variant == 5,
            inversionRadius = .8f, inversionCentre = new Vector3(.1f, .2f, -.1f),
            droste = variant == 4 || variant == 5,
            drosteAxis = Axis3.Y, drosteScale = 3f, drosteSectors = 5, drosteTwist = 1, drosteRadius = .6f, drosteCore = .03f,
            scroll = variant >= 3 ? new Vector3(.3f, 1.7f, -.4f) : Vector3.zero,
            scrollVelocity = variant >= 3 ? new Vector3(0f, .25f, 0f) : Vector3.zero,
        };
        return s;
    }

    [Test]
    public void ShaderFieldIsTheRoomField()
    {
        var rng = new System.Random(77);
        float time = 13.7f;
        foreach (RoomField field in Enum.GetValues(typeof(RoomField)))
        for (int variant = 0; variant < 6; variant++)
        {
            var s = Settings(field, variant);
            var u = EscherFieldGpu.Pack(s, time);
            float worst = 0f;
            for (int i = 0; i < 400; i++)
            {
                var p = new Vector3((float)rng.NextDouble() * 4f - 2f, (float)rng.NextDouble() * 4f - 2f, (float)rng.NextDouble() * 4f - 2f);
                float w = s.w + (float)rng.NextDouble() - .5f;
                float cpu = EscherFields.Sample(s, p, w, time), gpu = Mirror.Field(u, p, w);
                worst = Mathf.Max(worst, Mathf.Abs(cpu - gpu) / (1f + Mathf.Abs(cpu)));
            }
            Assert.Less(worst, 1e-4f, $"{field} variant {variant}");
        }
    }

    [Test]
    public void ScrollIsPackedWrapped()
    {
        var s = Settings(RoomField.Gyroid, 3);
        s.space.scrollVelocity = new Vector3(0f, 3.1f, 0f);
        var u = EscherFieldGpu.Pack(s, 1e5f);
        Assert.GreaterOrEqual(u.scroll.y, 0f);
        Assert.Less(u.scroll.y, 1f);
    }

    [Test]
    public void HlslCompiles()
    {
        const string glslang = "/usr/bin/glslangValidator";
        if (!File.Exists(glslang)) Assert.Inconclusive("glslangValidator not installed (apt install glslang-tools)");
        string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../"));
        string rooms = Path.Combine(root, "Assets/_EscherWorldManagement/Rooms");
        Assert.IsTrue(File.Exists(Path.Combine(rooms, "EscherField4D.hlsl")), rooms);
        string dir = Path.Combine(Path.GetTempPath(), "escher-hlsl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string src = Path.Combine(dir, "check.hlsl");
        File.WriteAllText(src, "#include \"EscherField4D.hlsl\"\n" +
            "float4 main(float4 pos : SV_Position) : SV_Target {\n" +
            "  float3 hit; float hitW; float t;\n" +
            "  bool ok = EscherMarch(float3(0, 0, -3), normalize(float3(pos.xy * 0.001, 1)), 0.0, 0.1, hit, hitW, t);\n" +
            "  return ok ? float4(EscherNormal(hit, hitW, 0.001) * 0.5 + 0.5, 1) : float4(0, 0, 0, 1);\n}\n");
        var psi = new ProcessStartInfo(glslang, $"-D -e main -S frag -V -I{rooms} {src} -o {Path.Combine(dir, "check.spv")}")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var proc = Process.Start(psi);
        string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        Assert.AreEqual(0, proc.ExitCode, output);
    }
}
