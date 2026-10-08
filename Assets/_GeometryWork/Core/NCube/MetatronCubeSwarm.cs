using System;
using System.Collections.Generic;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Metatron's Cube: 13 nodes with all 78 lines between them, which is the complete graph K13
    /// — C(13,2) = 78 is exactly the line count the figure is known for.
    ///
    /// The 13 nodes are not an arbitrary flat arrangement. The classic 2-D figure is the
    /// **cuboctahedron** (the Vector Equilibrium, 12 vertices) plus its centre, viewed down a
    /// 3-fold axis: that projection is what produces the familiar outer hexagon with a smaller
    /// inner hexagon rotated 30 degrees. So this builds the 3-D solid and <see cref="flatten"/>
    /// collapses it along that axis — one dial from the Vector Equilibrium to the flat glyph,
    /// rather than two unrelated point sets.
    ///
    /// <see cref="showLengthClass"/> filters the 78 lines by length. Metatron's lines fall into a
    /// handful of distinct lengths, and hiding classes is how the Platonic solids inside the figure
    /// become visible one at a time.
    /// </summary>
    public sealed class MetatronCubeSwarm : NodeEdgeSwarmBase
    {
        [Header("Metatron")]
        [Tooltip("0 = the 3-D cuboctahedron (Vector Equilibrium), 1 = the flat glyph, collapsed down its 3-fold axis.")]
        [Range(0, 1)] public float flatten = 1f;
        [Tooltip("Rotates the 3-fold axis to point at the camera, so the flat form reads as the classic figure.")]
        public bool alignToViewAxis = true;
        [Tooltip("Spin about the 3-fold axis.")]
        [Range(-2, 2)] public float spin = .05f;
        [Tooltip("Include the centre node. The 78-line count assumes it is present.")]
        public bool includeCentre = true;

        [Header("Line filter")]
        [Tooltip("Which length classes to draw, shortest first. Turning classes off reveals the solids inside the figure.")]
        public bool[] showLengthClass = new bool[] { true, true, true, true, true, true, true, true };
        [Tooltip("How close two lengths must be to count as the same class, relative to the longest line.")]
        [Range(.005f, .15f)] public float classTolerance = .03f;

        protected override string SwarmName => "Metatron's Cube swarm";
        protected override int NodeCount => includeCentre ? 13 : 12;
        protected override bool TopologyDirty => builtCentre != includeCentre;

        const int MaxClasses = 16;
        bool builtCentre;
        int[] edgeClass;
        int classCount;
        readonly float[] reps = new float[MaxClasses];
        readonly int[] order = new int[MaxClasses];
        readonly int[] rank = new int[MaxClasses];
        static readonly Vector3 ThreeFoldAxis = new Vector3(1f, 1f, 1f).normalized;

        /// <summary>The 12 cuboctahedron vertices: every permutation of (+/-1, +/-1, 0), normalised.</summary>
        static readonly Vector3[] Cuboctahedron =
        {
            new Vector3( 1,  1, 0), new Vector3( 1, -1, 0), new Vector3(-1,  1, 0), new Vector3(-1, -1, 0),
            new Vector3( 1, 0,  1), new Vector3( 1, 0, -1), new Vector3(-1, 0,  1), new Vector3(-1, 0, -1),
            new Vector3(0,  1,  1), new Vector3(0,  1, -1), new Vector3(0, -1,  1), new Vector3(0, -1, -1)
        };

        /// <summary>
        /// The 13 nodes: the cuboctahedron's 12 vertices plus the centre, flattened down the
        /// 3-fold axis so they read as the classic glyph. Shared so other systems reuse one
        /// definition rather than a second copy of the construction.
        /// </summary>
        public static Vector3[] NodePositions(float flatten = 1f)
        {
            var axis = ThreeFoldAxis;
            var align = Quaternion.FromToRotation(axis, Vector3.forward);
            var result = new Vector3[13];
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = Cuboctahedron[i].normalized;
                if (flatten > 0f)
                    p = Vector3.Lerp(p, p - axis * Vector3.Dot(p, axis), flatten);
                result[i] = align * p;
            }
            result[12] = Vector3.zero;
            return result;
        }

        protected override void BuildEdges(List<int> a, List<int> b)
        {
            builtCentre = includeCentre;
            int n = NodeCount;
            // Complete graph: 13 nodes -> 78 edges.
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++) { a.Add(i); b.Add(j); }
            edgeClass = new int[a.Count];
        }

        protected override void UpdateNodes(Vector3[] into, float time)
        {
            float angle = spin * time * Mathf.PI * 2f;
            Quaternion align = alignToViewAxis
                ? Quaternion.FromToRotation(ThreeFoldAxis, Vector3.forward)
                : Quaternion.identity;
            Quaternion turn = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.forward);

            int count = Mathf.Min(into.Length, 12);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Cuboctahedron[i].normalized;
                // Collapse along the 3-fold axis to reach the flat glyph.
                if (flatten > 0f)
                    p = Vector3.Lerp(p, p - ThreeFoldAxis * Vector3.Dot(p, ThreeFoldAxis), flatten);
                into[i] = turn * (align * p) * radius;
            }
            if (into.Length > 12) into[12] = Vector3.zero;

            ClassifyEdges();
        }

        /// <summary>
        /// Sorts the 78 line lengths into classes, shortest first. Recomputed each frame because
        /// flatten, swirl and spin all change the lengths.
        /// </summary>
        void ClassifyEdges()
        {
            if (edgeClass == null || EdgeTotal == 0) { classCount = 0; return; }
            if (edgeClass.Length != EdgeTotal) edgeClass = new int[EdgeTotal];

            float longest = 0f;
            for (int e = 0; e < EdgeTotal; e++) longest = Mathf.Max(longest, EdgeLength(e));
            if (longest <= 1e-5f) { classCount = 0; return; }
            float tolerance = longest * classTolerance;

            // Representative length per class, kept in discovery order for now.
            int found = 0;
            for (int e = 0; e < EdgeTotal; e++)
            {
                float len = EdgeLength(e);
                int match = -1;
                for (int c = 0; c < found; c++)
                    if (Mathf.Abs(reps[c] - len) <= tolerance) { match = c; break; }
                if (match < 0 && found < MaxClasses) { reps[found] = len; match = found; found++; }
                edgeClass[e] = Mathf.Max(match, 0);
            }

            // Reorder so class 0 is the shortest, which is what the inspector toggles assume.
            for (int c = 0; c < found; c++) order[c] = c;
            for (int i = 1; i < found; i++)
                for (int j = i; j > 0 && reps[order[j]] < reps[order[j - 1]]; j--)
                {
                    int swap = order[j]; order[j] = order[j - 1]; order[j - 1] = swap;
                }
            for (int c = 0; c < found; c++) rank[order[c]] = c;
            for (int e = 0; e < EdgeTotal; e++) edgeClass[e] = rank[edgeClass[e]];

            classCount = found;
        }

        protected override bool EdgeVisible(int edge)
        {
            if (edgeClass == null || showLengthClass == null || edge < 0 || edge >= edgeClass.Length) return true;
            int c = edgeClass[edge];
            return c < 0 || c >= showLengthClass.Length || showLengthClass[c];
        }

        protected override string Describe() =>
            "Metatron · " + NodeTotal + " nodes, " + EdgeTotal + " lines, " + classCount + " length classes";

        [ContextMenu("Flat glyph")] public void Flat() { flatten = 1f; alignToViewAxis = true; }
        [ContextMenu("Vector Equilibrium (3D)")] public void Solid() { flatten = 0f; }
        [ContextMenu("Show all lines")]
        public void ShowAll()
        {
            if (showLengthClass == null) showLengthClass = new bool[8];
            for (int i = 0; i < showLengthClass.Length; i++) showLengthClass[i] = true;
        }
        [ContextMenu("Shortest lines only")]
        public void ShortestOnly()
        {
            if (showLengthClass == null) showLengthClass = new bool[8];
            for (int i = 0; i < showLengthClass.Length; i++) showLengthClass[i] = i == 0;
        }
    }
}
