using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Enneract: the 9-dimensional hypercube, 512 vertices, 2,304 edges. Rotated in 9-D and projected to 3-D,
    /// with mesh particles on the vertices and sliding along the edges.
    ///
    /// All the controls live on <see cref="NCubeSwarmBase"/> and <see cref="NodeEdgeSwarmBase"/>:
    /// projection mode and morph, rotation planes, fractal particle clustering, swirl, scatter and
    /// reassemble. DekeractTetraSwarm is a separate, untouched component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnneractSwarm : NCubeSwarmBase
    {
        public override int Dimensions => 9;
        protected override string SwarmName => "Enneract swarm";
    }
}
