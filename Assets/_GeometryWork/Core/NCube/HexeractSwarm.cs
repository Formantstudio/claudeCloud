using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Hexeract: the 6-dimensional hypercube, 64 vertices, 192 edges. Rotated in 6-D and projected to 3-D,
    /// with mesh particles on the vertices and sliding along the edges.
    ///
    /// All the controls live on <see cref="NCubeSwarmBase"/> and <see cref="NodeEdgeSwarmBase"/>:
    /// projection mode and morph, rotation planes, fractal particle clustering, swirl, scatter and
    /// reassemble. DekeractTetraSwarm is a separate, untouched component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HexeractSwarm : NCubeSwarmBase
    {
        public override int Dimensions => 6;
        protected override string SwarmName => "Hexeract swarm";
    }
}
