using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Hepteract: the 7-dimensional hypercube, 128 vertices, 448 edges. Rotated in 7-D and projected to 3-D,
    /// with mesh particles on the vertices and sliding along the edges.
    ///
    /// All the controls live on <see cref="NCubeSwarmBase"/> and <see cref="NodeEdgeSwarmBase"/>:
    /// projection mode and morph, rotation planes, fractal particle clustering, swirl, scatter and
    /// reassemble. DekeractTetraSwarm is a separate, untouched component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HepteractSwarm : NCubeSwarmBase
    {
        public override int Dimensions => 7;
        protected override string SwarmName => "Hepteract swarm";
    }
}
