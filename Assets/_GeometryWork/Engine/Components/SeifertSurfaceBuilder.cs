using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// A Seifert surface for a knot or link, built by Seifert's algorithm on its closed braid
    /// (see <see cref="SeifertSurface"/>): stacked disks, one per strand, joined by a half-twisted band
    /// per crossing. One orientable, two-sided, unwelded quad mesh whose boundary is the link.
    ///
    /// The surface is static; <see cref="WireMeshComponent.animate"/> only spins it about the braid
    /// axis, which is the readable way to watch the bands twist. Rebuilds only on change.
    /// </summary>
    [AddComponentMenu("Geometry Engine/Seifert Surface")]
    public sealed class SeifertSurfaceBuilder : WireMeshComponent, IWireGeometry
    {
        [Header("Link")]
        public SeifertPreset preset = SeifertPreset.Trefoil;
        [Tooltip("TorusKnot: strands.")]
        [Range(2, 12)] public int p = 3;
        [Tooltip("TorusKnot: full turns of the braid. Negative mirrors the knot.")]
        [Range(-24, 24)] public int q = 4;
        [Tooltip("Custom: signed generators (\"1 -2 1 -2\") or letters (\"aBaB\", capital = inverse).")]
        public string braidWord = "aBaB";

        [Header("Shape")]
        public SeifertSurface.Shape shape = SeifertSurface.Shape.Default;
        [Tooltip("Spin about the braid axis, turns per second, while animating.")]
        [Range(-1f, 1f)] public float spin = .03f;

        readonly List<Vector3> scratch = new List<Vector3>();
        int[] word = new int[0];
        int strands = 1;

        protected override void OnValidate()
        {
            shape.radius = Mathf.Max(shape.radius, .01f);
            shape.spacing = Mathf.Max(shape.spacing, .001f);
            shape.diskRings = Mathf.Clamp(shape.diskRings, 1, 64);
            shape.bandColumns = Mathf.Clamp(shape.bandColumns, 1, 32);
            shape.bandRows = Mathf.Clamp(shape.bandRows, 2, 128);
            shape.arcSegments = Mathf.Clamp(shape.arcSegments, 1, 64);
            base.OnValidate();
        }

        void Reset() { shape = SeifertSurface.Shape.Default; }

        protected override void Build(WireMeshBuilder into, float time)
        {
            word = SeifertSurface.Word(preset, p, q, braidWord, out strands);
            if (shape.radius <= 0f) shape = SeifertSurface.Shape.Default;
            SeifertSurface.Build(into, word, strands, shape, scratch);

            if (spin != 0f && time != 0f)
            {
                float a = time * spin * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                var v = into.vertices; var n = into.normals;
                for (int i = 0; i < v.Count; i++)
                {
                    v[i] = new Vector3(v[i].x * c - v[i].y * s, v[i].x * s + v[i].y * c, v[i].z);
                    n[i] = new Vector3(n[i].x * c - n[i].y * s, n[i].x * s + n[i].y * c, n[i].z);
                }
            }
        }

        protected override string Describe()
        {
            int chi = SeifertSurface.EulerCharacteristic(word, strands);
            return preset + " · " + strands + " strands, " + word.Length + " crossings, χ " + chi +
                   ", genus " + SeifertSurface.Genus(word, strands) +
                   ", " + SeifertSurface.Components(word, strands) + " component(s)";
        }

        // ---- IWireGeometry: u around the disks, v up the stack -----------------

        public bool IsBuilt => builder.VertexCount > 0;
        public int GridU => Mathf.Max(word.Length, 1) * Mathf.Max(shape.bandColumns + shape.arcSegments, 1);
        public int GridV => Mathf.Max(strands, 1) * Mathf.Max(shape.diskRings, 1);

        /// <summary>Particles sit on the disks: u is the angle, v climbs radius then height.</summary>
        public Vector3 SampleGrid(float u, float v)
        {
            float level = Mathf.Clamp01(v) * Mathf.Max(strands, 1);
            int disk = Mathf.Min((int)level, Mathf.Max(strands - 1, 0));
            float r = Mathf.Lerp(.15f, 1f, level - disk) * shape.radius;
            float a = Mathf.Repeat(u, 1f) * Mathf.PI * 2f + (animate ? ElapsedTime * spin * Mathf.PI * 2f : 0f);
            return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), disk * shape.spacing);
        }

        [ContextMenu("Trefoil")] public void UseTrefoil() { preset = SeifertPreset.Trefoil; MarkDirty(); }
        [ContextMenu("Figure-eight")] public void UseFigureEight() { preset = SeifertPreset.FigureEight; MarkDirty(); }
        [ContextMenu("Borromean rings")] public void UseBorromean() { preset = SeifertPreset.Borromean; MarkDirty(); }
        [ContextMenu("Torus knot (3,4)")] public void UseT34() { preset = SeifertPreset.TorusKnot; p = 3; q = 4; MarkDirty(); }
    }
}
