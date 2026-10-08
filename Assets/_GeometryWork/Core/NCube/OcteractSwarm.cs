using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Octeract: the 8-dimensional hypercube, 256 vertices, 1,024 edges. Rotated in 8-D and projected to 3-D,
    /// with mesh particles on the vertices and sliding along the edges.
    ///
    /// All the controls live on <see cref="NCubeSwarmBase"/> and <see cref="NodeEdgeSwarmBase"/>:
    /// projection mode and morph, rotation planes, fractal particle clustering, swirl, scatter and
    /// reassemble. DekeractTetraSwarm is a separate, untouched component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OcteractSwarm : NCubeSwarmBase
    {
        public override int Dimensions => 8;
        protected override string SwarmName => "Octeract swarm";
    }
}
