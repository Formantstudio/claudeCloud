using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Dekeract: the 10-dimensional hypercube, 1,024 vertices, 5,120 edges. Rotated in 10-D and projected to 3-D,
    /// with mesh particles on the vertices and sliding along the edges.
    ///
    /// All the controls live on <see cref="NCubeSwarmBase"/> and <see cref="NodeEdgeSwarmBase"/>:
    /// projection mode and morph, rotation planes, fractal particle clustering, swirl, scatter and
    /// reassemble. DekeractTetraSwarm is a separate, untouched component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DekeractSwarm : NCubeSwarmBase
    {
        public override int Dimensions => 10;
        protected override string SwarmName => "Dekeract swarm";
    }
}
