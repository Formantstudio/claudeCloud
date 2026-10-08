using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Penteract: the 5-dimensional hypercube, 32 vertices, 80 edges. Rotated in 5-D and projected to 3-D,
    /// with mesh particles on the vertices and sliding along the edges.
    ///
    /// All the controls live on <see cref="NCubeSwarmBase"/> and <see cref="NodeEdgeSwarmBase"/>:
    /// projection mode and morph, rotation planes, fractal particle clustering, swirl, scatter and
    /// reassemble. DekeractTetraSwarm is a separate, untouched component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PenteractSwarm : NCubeSwarmBase
    {
        public override int Dimensions => 5;
        protected override string SwarmName => "Penteract swarm";
    }
}
