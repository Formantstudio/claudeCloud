using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// A polyhedron from Conway notation (<see cref="Conway"/>): type "tI" for the soccer ball, "sD"
    /// for the snub dodecahedron, "gaD", "k5A7", "cccC" … Every face is drawn as one polygon with its
    /// interior fan hidden, so the wire shows exactly the polyhedron's edges.
    ///
    /// The polyhedron is rebuilt only when the notation or the canonical settings change; animation is
    /// a spin, applied as the mesh is written. Particles ride the edges: u picks the edge, v runs along it.
    /// </summary>
    [AddComponentMenu("Geometry Engine/Conway Polyhedron")]
    public sealed class ConwayPolyhedronEngine : WireMeshComponent, IWireGeometry
    {
        [Header("Polyhedron")]
        [Tooltip("Conway notation, applied right to left. Seeds: T C O D I, Pn An Yn. Operators: d a k g c w q r, and t j e o s b m n. kn / tn act on n-sided faces / degree-n vertices only.")]
        public string notation = "tI";
        [Tooltip("Hart's canonical form: edges tangent to the unit sphere, planar faces. Archimedean results come out uniform.")]
        public bool canonical = true;
        [Tooltip("Relaxation steps per operator when canonical.")]
        [Range(50, 3000)] public int canonicalIterations = 600;
        [Tooltip("Refuses notations whose result would pass this many faces.")]
        [Range(100, 200000)] public int maxFaces = 50000;

        [Header("Shape")]
        [Min(.001f)] public float radius = 1f;
        [Tooltip("Turns per second about the vertical while animating.")]
        [Range(-1f, 1f)] public float spin = .02f;

        Polyhedron polyhedron;
        List<Vector2Int> edges = new List<Vector2Int>();
        string builtKey;
        string error;
        float canonicalError;

        /// <summary>The current polyhedron, or null if the notation did not parse.</summary>
        public Polyhedron Current { get { Ensure(); return polyhedron; } }

        protected override void OnValidate()
        {
            canonicalIterations = Mathf.Clamp(canonicalIterations, 50, 3000);
            maxFaces = Mathf.Clamp(maxFaces, 100, 200000);
            radius = Mathf.Max(radius, .001f);
            base.OnValidate();
        }

        void Ensure()
        {
            string key = notation + "|" + canonical + "|" + canonicalIterations + "|" + maxFaces;
            if (key == builtKey) return;
            builtKey = key;
            error = null;
            try
            {
                polyhedron = Conway.Parse(notation, out canonicalError, maxFaces, canonical ? canonicalIterations : 0);
                // Canonical forms have unit midsphere; plain ones unit mean radius. Show both at
                // unit mean radius so switching canonical on does not change the size.
                polyhedron.Normalize();
                edges = polyhedron.Edges();
            }
            catch (Exception e) when (e is FormatException || e is InvalidOperationException)
            {
                polyhedron = null;
                edges.Clear();
                error = e.Message;
            }
        }

        Quaternion Spin(float time) =>
            Quaternion.AngleAxis(animate ? time * spin * 360f : 0f, Vector3.up);

        protected override void Build(WireMeshBuilder into, float time)
        {
            Ensure();
            polyhedron?.Emit(into, Spin(time), radius);
        }

        protected override string Describe()
        {
            Ensure();
            if (polyhedron == null) return "Notation error: " + error;
            string common = Conway.CommonName(notation);
            return (common.Length > 0 ? common + " · " : "") + polyhedron.ToString() +
                   (canonical ? " · canonical error " + canonicalError.ToString("E1") : "");
        }

        // ---- IWireGeometry: u picks an edge, v runs along it -------------------

        public bool IsBuilt => builder.VertexCount > 0 && polyhedron != null;
        public int GridU => Mathf.Max(edges.Count, 1);
        public int GridV => 8;

        public Vector3 SampleGrid(float u, float v)
        {
            Ensure();
            if (polyhedron == null || edges.Count == 0) return Vector3.zero;
            int e = Mathf.Clamp((int)(Mathf.Repeat(u, 1f) * edges.Count), 0, edges.Count - 1);
            Vector3 a = polyhedron.vertices[edges[e].x], b = polyhedron.vertices[edges[e].y];
            return Spin(ElapsedTime) * (Vector3.Lerp(a, b, Mathf.Clamp01(v)) * radius);
        }

        [ContextMenu("Truncated icosahedron (tI)")] public void UseSoccerBall() { notation = "tI"; MarkDirty(); }
        [ContextMenu("Snub dodecahedron (sD)")] public void UseSnubDodecahedron() { notation = "sD"; MarkDirty(); }
        [ContextMenu("Goldberg (cccD)")] public void UseGoldberg() { notation = "cccD"; MarkDirty(); }
        [ContextMenu("Whirled icosahedron (wI)")] public void UseWhirl() { notation = "wI"; MarkDirty(); }
    }
}
