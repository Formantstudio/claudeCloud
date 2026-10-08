using System;
using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// The harder Escher manipulations, as warps of the space a periodic field is sampled in. Each is
    /// seam-exact by the same rule as the screw dislocation: a jump in the sampling coordinates is
    /// allowed only where it is a whole number of the lattice's own periods, so the field cannot see it.
    ///
    /// Applied window → lattice, in this order:
    ///
    /// 1. **Sphere inversion** (a Möbius map, conformal): p → c + R²(p − c)/|p − c|². The infinite
    ///    lattice outside the sphere is folded inside it, with infinity at the centre — Circle Limit
    ///    in three dimensions, every wall meeting every other at its true angle.
    /// 2. **Droste spiral** (Print Gallery / Smaller and Smaller): log-cylindrical coordinates about an
    ///    axis, X = P·(ln(ρ/ρ₀)/ln s + twist·θ/2π), Y = P·n·θ/2π, Z = P·n·z/(2πρ). The structure is
    ///    exactly invariant under scaling by s, so rooms nest inside rooms forever toward the axis;
    ///    with a whole-number twist one turn about the axis also steps one scale level, which is
    ///    Escher's spiral. Whole-number sectors n and twist keep the θ = ±π seam invisible.
    /// 3. **Screw dislocation** (<see cref="ScrewDislocation"/>): one floor per turn.
    /// 4. **Scroll**: the lattice slides through the window by <see cref="scroll"/> +
    ///    <see cref="scrollVelocity"/>·time periods, wrapped to one period. Because it is applied last,
    ///    in lattice coordinates, the field is periodic in it whatever came before, so the loop is
    ///    exact and the offset never grows: a camera can climb the staircase forever without float
    ///    drift.
    ///
    /// Shared by the engine's <see cref="Implicits"/> and the Escher rooms' fields, so both systems
    /// warp space identically.
    /// </summary>
    [Serializable]
    public sealed class EscherSpace
    {
        [Header("Sphere inversion (Möbius)")]
        [Tooltip("Folds the whole infinite lattice inside a sphere, with infinity at its centre. Conformal: walls keep their angles.")]
        public bool invert;
        [Tooltip("Inversion sphere radius, in field units (the sampled box is -1..1).")]
        [Range(.05f, 4f)] public float inversionRadius = .9f;
        public Vector3 inversionCentre;

        [Header("Droste spiral (Print Gallery)")]
        [Tooltip("Self-similar nesting: the structure repeats exactly under scaling by Droste Scale, rooms inside rooms toward the axis.")]
        public bool droste;
        public Axis3 drosteAxis = Axis3.Z;
        [Tooltip("Scale factor between one nesting level and the next.")]
        [Range(1.25f, 32f)] public float drosteScale = 4f;
        [Tooltip("Lattice periods around one turn. Whole numbers only, or the seam shows.")]
        [Range(1, 32)] public int drosteSectors = 6;
        [Tooltip("Scale levels gained per turn about the axis: Escher's spiral. 0 = concentric nesting.")]
        [Range(-4, 4)] public int drosteTwist = 1;
        [Tooltip("Radius where the reference level sits, in field units.")]
        [Range(.05f, 2f)] public float drosteRadius = .5f;
        [Tooltip("Below this radius the infinite nesting is frozen: the sampling grid cannot resolve it.")]
        [Range(.001f, .5f)] public float drosteCore = .02f;

        [Header("Window")]
        [Tooltip("Lattice offset in periods. Wrapped to one period, so 0 and 1 are the same room.")]
        public Vector3 scroll;
        [Tooltip("Periods per second the lattice slides through the window. Along the screw axis this climbs the staircase forever.")]
        public Vector3 scrollVelocity;

        public bool Active => invert || droste || scroll != Vector3.zero || scrollVelocity != Vector3.zero;

        /// <summary>A value that changes whenever any setting does, for rebuild checks.</summary>
        public int Signature()
        {
            unchecked
            {
                int h = invert ? 17 : 3;
                h = h * 31 + inversionRadius.GetHashCode();
                h = h * 31 + inversionCentre.GetHashCode();
                h = h * 31 + (droste ? 1 : 0);
                h = h * 31 + (int)drosteAxis;
                h = h * 31 + drosteScale.GetHashCode();
                h = h * 31 + drosteSectors;
                h = h * 31 + drosteTwist;
                h = h * 31 + drosteRadius.GetHashCode();
                h = h * 31 + drosteCore.GetHashCode();
                h = h * 31 + scroll.GetHashCode();
                h = h * 31 + scrollVelocity.GetHashCode();
                return h;
            }
        }

        /// <summary>
        /// Window point → lattice point, for a field whose lattice repeats every
        /// <paramref name="period"/> along each axis. <paramref name="space"/> may be null.
        /// </summary>
        public static Vector3 ToLattice(EscherSpace space, Vector3 p, float period, float time,
                                        Axis3 dislocationAxis, float dislocation, float dislocationCore)
        {
            if (space != null && space.invert) p = Invert(p, space.inversionCentre, space.inversionRadius);
            if (space != null && space.droste) p = Droste(space, p, period);
            if (dislocation != 0f) p = ScrewDislocation.Apply(p, dislocationAxis, dislocation, period, dislocationCore);
            if (space != null && (space.scroll != Vector3.zero || space.scrollVelocity != Vector3.zero))
                p += Wrapped(space.scroll + space.scrollVelocity * time) * period;
            return p;
        }

        /// <summary>Sphere inversion: an involution, conformal, fixing the sphere itself.</summary>
        public static Vector3 Invert(Vector3 p, Vector3 centre, float radius)
        {
            Vector3 d = p - centre;
            float m = d.sqrMagnitude;
            // The centre is the image of infinity; hold it at a finite distance instead.
            if (m < 1e-12f) m = 1e-12f;
            return centre + d * (radius * radius / m);
        }

        /// <summary>The Droste map alone (see the class summary for the formula).</summary>
        public static Vector3 Droste(EscherSpace s, Vector3 p, float period)
        {
            Vector3 q = ScrewDislocation.ToAxis(p, s.drosteAxis);
            float rho = Mathf.Max(Mathf.Sqrt(q.x * q.x + q.y * q.y), Mathf.Max(s.drosteCore, 1e-6f));
            float turns = Mathf.Atan2(q.y, q.x) / (Mathf.PI * 2f);              // -½ .. ½
            float levels = Mathf.Log(rho / Mathf.Max(s.drosteRadius, 1e-4f)) / Mathf.Log(Mathf.Max(s.drosteScale, 1.0001f));
            int n = Mathf.Max(s.drosteSectors, 1);
            var lattice = new Vector3(
                period * (levels + s.drosteTwist * turns),
                period * n * turns,
                period * n * q.z / (Mathf.PI * 2f * rho));
            return lattice;
        }

        static Vector3 Wrapped(Vector3 v) => new Vector3(v.x - Mathf.Floor(v.x), v.y - Mathf.Floor(v.y), v.z - Mathf.Floor(v.z));
    }
}
