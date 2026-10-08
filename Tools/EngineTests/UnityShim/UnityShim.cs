// Minimal UnityEngine stand-in so the geometry engine compiles and its tests run under plain .NET
// (Tools/EngineTests). Behaviour matches Unity for everything the engine touches; it is not a
// general Unity replacement. Extend it when engine code starts using more of the API.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Epsilon = float.Epsilon;
        public const float Infinity = float.PositiveInfinity;
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int f) => Math.Abs(f);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Exp(float p) => (float)Math.Exp(p);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Log(float f, float b) => (float)Math.Log(f, b);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(params float[] v) { float m = v[0]; foreach (var x in v) m = Math.Min(m, x); return m; }
        public static float Max(params float[] v) { float m = v[0]; foreach (var x in v) m = Math.Max(m, x); return m; }
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float Repeat(float t, float length) => Clamp(t - Floor(t / length) * length, 0f, length);
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Abs(t - length); }
        public static float Round(float f) => (float)Math.Round(f, MidpointRounding.ToEven);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.ToEven);
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t); t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }
        public static bool Approximately(float a, float b) =>
            Abs(b - a) < Max(1e-6f * Max(Abs(a), Abs(b)), Epsilon * 8f);
        public static float DeltaAngle(float a, float b) { float d = Repeat(b - a, 360f); if (d > 180f) d -= 360f; return d; }
        public static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;
    }

    [Serializable]
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float this[int i] { get => i == 0 ? x : y; set { if (i == 0) x = value; else y = value; } }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public override string ToString() => $"({x:F3}, {y:F3})";
    }

    [Serializable]
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : z;
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() { this = normalized; }
        public static Vector3 Normalize(Vector3 v) => v.normalized;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float m = (float)Math.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (m < 1e-15f) return 0f;
            float angle = (float)(Math.Acos(Math.Max(-1.0, Math.Min(1.0, Dot(from, to) / m))) * 180.0 / Math.PI);
            return Dot(axis, Cross(from, to)) < 0f ? -angle : angle;
        }
        public static Vector3 Cross(Vector3 a, Vector3 b) =>
            new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
        {
            float s = Dot(n, n); if (s < Mathf.Epsilon) return v;
            return v - n * (Dot(v, n) / s);
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && this == v;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }

    [Serializable]
    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }

    [Serializable]
    public struct Vector3Int
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
    }

    [Serializable]
    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : i == 2 ? z : w;
            set { if (i == 0) x = value; else if (i == 1) y = value; else if (i == 2) z = value; else w = value; }
        }
        public static Vector4 zero => new Vector4(0, 0, 0, 0);
        public static Vector4 one => new Vector4(1, 1, 1, 1);
        public float sqrMagnitude => x * x + y * y + z * z + w * w;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
        public Vector4 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector4 a, Vector4 b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static float Distance(Vector4 a, Vector4 b) => (a - b).magnitude;
        public static Vector4 operator +(Vector4 a, Vector4 b) => new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        public static Vector4 operator -(Vector4 a, Vector4 b) => new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        public static Vector4 operator -(Vector4 a) => new Vector4(-a.x, -a.y, -a.z, -a.w);
        public static Vector4 operator *(Vector4 a, float d) => new Vector4(a.x * d, a.y * d, a.z * d, a.w * d);
        public static Vector4 operator *(float d, Vector4 a) => new Vector4(a.x * d, a.y * d, a.z * d, a.w * d);
        public static Vector4 operator /(Vector4 a, float d) => new Vector4(a.x / d, a.y / d, a.z / d, a.w / d);
        public static implicit operator Vector4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0);
        public static implicit operator Vector3(Vector4 v) => new Vector3(v.x, v.y, v.z);
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3}, {w:F3})";
    }

    [Serializable]
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        /// <summary>Unity's ZXY Euler order, in degrees, each in [0, 360).</summary>
        public Vector3 eulerAngles
        {
            get
            {
                double sx = 2 * (w * x - y * z);
                double ex = Math.Abs(sx) >= 1 ? Math.PI / 2 * Math.Sign(sx) : Math.Asin(sx);
                double ey = Math.Atan2(2 * (w * y + x * z), 1 - 2 * (x * x + y * y));
                double ez = Math.Atan2(2 * (w * z + x * y), 1 - 2 * (x * x + z * z));
                float D(double r) { double d = r * 180 / Math.PI % 360; return (float)(d < 0 ? d + 360 : d); }
                return new Vector3(D(ex), D(ey), D(ez));
            }
        }
        public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
        public static Quaternion AngleAxis(float deg, Vector3 axis)
        {
            axis = axis.normalized; float h = deg * Mathf.Deg2Rad * .5f, s = Mathf.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h));
        }
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);
        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            Vector3 a = from.normalized, b = to.normalized;
            float d = Vector3.Dot(a, b);
            if (d < -.999999f)
            {
                Vector3 axis = Vector3.Cross(Vector3.right, a);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, a);
                return AngleAxis(180f, axis.normalized);
            }
            Vector3 c = Vector3.Cross(a, b);
            var q = new Quaternion(c.x, c.y, c.z, 1f + d);
            float m = (float)Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
        public static Quaternion Euler(float x, float y, float z) =>
            AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var u = new Vector3(q.x, q.y, q.z);
            var t = 2f * Vector3.Cross(u, v);
            return v + q.w * t + Vector3.Cross(u, t);
        }
    }

    [Serializable]
    public struct Matrix4x4
    {
        float[] m;
        float[] M => m ?? (m = new float[16]);
        public float this[int row, int col] { get => M[row * 4 + col]; set => M[row * 4 + col] = value; }
        public static Matrix4x4 identity
        {
            get { var r = new Matrix4x4(); r[0, 0] = r[1, 1] = r[2, 2] = r[3, 3] = 1; return r; }
        }
        public static Matrix4x4 zero => new Matrix4x4();
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new Matrix4x4();
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++)
            { float s = 0; for (int k = 0; k < 4; k++) s += a[i, k] * b[k, j]; r[i, j] = s; }
            return r;
        }
        public static Vector4 operator *(Matrix4x4 a, Vector4 v)
        {
            var r = new Vector4();
            for (int i = 0; i < 4; i++) r[i] = a[i, 0] * v.x + a[i, 1] * v.y + a[i, 2] * v.z + a[i, 3] * v.w;
            return r;
        }
        public Matrix4x4 transpose
        {
            get { var r = new Matrix4x4(); for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) r[i, j] = this[j, i]; return r; }
        }
        public Vector4 GetColumn(int c) => new Vector4(this[0, c], this[1, c], this[2, c], this[3, c]);
        public void SetColumn(int c, Vector4 v) { for (int i = 0; i < 4; i++) this[i, c] = v[i]; }
        public Vector4 GetRow(int r) => new Vector4(this[r, 0], this[r, 1], this[r, 2], this[r, 3]);
        public void SetRow(int r, Vector4 v) { for (int i = 0; i < 4; i++) this[r, i] = v[i]; }
    }

    [Serializable]
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color Lerp(Color x, Color y, float t)
        { t = Mathf.Clamp01(t); return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t); }
    }

    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 c, Vector3 s) { center = c; size = s; }
        public Vector3 extents => size * .5f;
    }

    public struct Random
    {
        static System.Random rng = new System.Random(1);
        public static void InitState(int seed) { rng = new System.Random(seed); }
        public struct State { }
        public static State state { get => default; set { } }
        public static float value => (float)rng.NextDouble();
        public static float Range(float a, float b) => a + (b - a) * value;
        public static int Range(int a, int b) => rng.Next(a, b);
        public static Vector3 insideUnitSphere
        {
            get
            {
                while (true)
                {
                    var p = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                    if (p.sqrMagnitude <= 1f) return p;
                }
            }
        }
    }

    public class Object
    {
        public string name;
        public HideFlags hideFlags;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
        public static void Destroy(Object o) { }
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static void DestroyImmediate(Object o) { }
        public static T Instantiate<T>(T original) where T : Object => original;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
    }

    [Flags] public enum HideFlags { None = 0, HideAndDontSave = 61, DontSave = 52 }

    public class Mesh : Object
    {
        public Rendering.IndexFormat indexFormat;
        public Bounds bounds;
        public Vector3[] vertices = Array.Empty<Vector3>();
        public Vector3[] normals = Array.Empty<Vector3>();
        public Vector2[] uv = Array.Empty<Vector2>();
        public Vector2[] uv2 = Array.Empty<Vector2>();
        public Vector4[] tangents = Array.Empty<Vector4>();
        public int vertexCount => vertices.Length;
        public int[] triangles = Array.Empty<int>();
        public readonly Dictionary<int, List<Vector3>> uvChannels3 = new Dictionary<int, List<Vector3>>();
        public readonly Dictionary<int, List<Vector2>> uvChannels2 = new Dictionary<int, List<Vector2>>();
        public void Clear() { vertices = Array.Empty<Vector3>(); normals = Array.Empty<Vector3>(); triangles = Array.Empty<int>(); uvChannels2.Clear(); uvChannels3.Clear(); }
        public void MarkDynamic() { }
        public void SetVertices(List<Vector3> v) { vertices = v.ToArray(); }
        public void SetVertices(Vector3[] v) { vertices = (Vector3[])v.Clone(); }
        public void SetNormals(List<Vector3> v) { normals = v.ToArray(); }
        public void SetNormals(Vector3[] v) { normals = (Vector3[])v.Clone(); }
        public void SetUVs(int ch, List<Vector3> v) { uvChannels3[ch] = new List<Vector3>(v); }
        public void SetUVs(int ch, Vector3[] v) { uvChannels3[ch] = new List<Vector3>(v); }
        public void SetUVs(int ch, List<Vector2> v) { uvChannels2[ch] = new List<Vector2>(v); if (ch == 0) uv = v.ToArray(); }
        public void SetUVs(int ch, Vector2[] v) { uvChannels2[ch] = new List<Vector2>(v); if (ch == 0) uv = (Vector2[])v.Clone(); }
        public void SetTriangles(List<int> t, int sub) { triangles = t.ToArray(); }
        public void SetTriangles(int[] t, int sub) { triangles = (int[])t.Clone(); }
        public void SetIndices(int[] t, MeshTopology topo, int sub) { triangles = (int[])t.Clone(); }
        public int subMeshCount = 1;
        public void SetTangents(List<Vector4> t) { }
        public void SetTangents(Vector4[] t) { }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
    }

    public enum MeshTopology { Triangles, Quads, Lines, LineStrip, Points }

    public class Component : Object
    {
        public Transform transform;
        public GameObject gameObject;
        public T GetComponent<T>() where T : class => null;
        public T[] GetComponents<T>() where T : class => new T[0];
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public class Transform : Component
    {
        public Vector3 position, localPosition, localScale = Vector3.one;
        public Quaternion rotation = Quaternion.identity, localRotation = Quaternion.identity;
        public void SetParent(Transform p, bool keep) { }
        public Vector3 TransformPoint(Vector3 p) => position + rotation * Vector3.Scale(localScale, p);
        public Vector3 InverseTransformPoint(Vector3 p) { var q = Quaternion.Inverse(rotation) * (p - position); return new Vector3(q.x / localScale.x, q.y / localScale.y, q.z / localScale.z); }
        public Matrix4x4 localToWorldMatrix => Matrix4x4.identity;
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public Vector3 forward => rotation * Vector3.forward;
    }
    public class GameObject : Object
    {
        public GameObject(string n) { name = n; transform = new Transform(); }
        public Transform transform;
        public bool activeSelf = true;
        public void SetActive(bool a) { activeSelf = a; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class => new T[0];
        public T GetComponent<T>() where T : class => null;
        public T AddComponent<T>() where T : Component { var c = Activator.CreateInstance<T>(); c.gameObject = this; c.transform = transform; c.name = name; return c; }
    }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class Renderer : Component { public Material[] sharedMaterials = new Material[0]; public Material sharedMaterial; public Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows; }
    public class MeshRenderer : Renderer { public bool allowOcclusionWhenDynamic; }
    public class Material : Object
    {
        public Material(Material m) { }
        public bool HasProperty(string n) => false;
        public Shader shader;
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
        public void SetFloat(string n, float v) { }
        public void SetVector(string n, Vector4 v) { }
        public void SetVector(int id, Vector4 v) { }
        public void SetFloat(int id, float v) { }
        public void SetColor(string n, Color c) { }
    }
    public class Shader : Object
    {
        public static int PropertyToID(string n) => n.GetHashCode();
        public static void SetGlobalMatrix(int id, Matrix4x4 m) { }
        public static void SetGlobalVector(int id, Vector4 v) { }
        public static void SetGlobalFloat(int id, float f) { }
    }
    public static class Application { public static bool isPlaying => false; }
    public static class Time { public static float time, deltaTime, realtimeSinceStartup; public static double realtimeSinceStartupAsDouble; }
    public static class Gizmos
    {
        public static Color color; public static Matrix4x4 matrix;
        public static void DrawWireSphere(Vector3 c, float r) { } public static void DrawLine(Vector3 a, Vector3 b) { } public static void DrawWireCube(Vector3 c, Vector3 s) { }
    }
    public static class Debug
    {
        public static void Log(object o) => Console.WriteLine(o);
        public static void LogWarning(object o) => Console.WriteLine("WARN " + o);
        public static void LogWarning(object o, Object context) => Console.WriteLine("WARN " + o);
        public static void LogError(object o) => Console.WriteLine("ERROR " + o);
        public static void LogError(object o, Object context) => Console.WriteLine("ERROR " + o);
        public static void Assert(bool c, string m = "") { if (!c) throw new Exception("Assert: " + m); }
    }

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public readonly float min, max; public RangeAttribute(float a, float b) { min = a; max = b; } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public readonly float min; public MinAttribute(float a) { min = a; } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class RequireComponent : Attribute { public RequireComponent(Type a, Type b = null) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class AddComponentMenu : Attribute { public AddComponentMenu(string m) { } }
    [AttributeUsage(AttributeTargets.Method)] public sealed class ContextMenu : Attribute { public ContextMenu(string m) { } }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16, UInt32 }
    public enum ShadowCastingMode { Off, On }
}
