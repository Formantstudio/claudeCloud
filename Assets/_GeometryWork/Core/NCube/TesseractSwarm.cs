using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// Tesseract: the 4-dimensional hypercube, 16 vertices, 32 edges. Rotated in 4-D and projected to 3-D,
    /// with mesh particles on the vertices and sliding along the edges.
    ///
    /// All the controls live on <see cref="NCubeSwarmBase"/> and <see cref="NodeEdgeSwarmBase"/>:
    /// projection mode and morph, rotation planes, fractal particle clustering, swirl, scatter and
    /// reassemble. DekeractTetraSwarm is a separate, untouched component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TesseractSwarm : NCubeSwarmBase
    {
        public override int Dimensions => 4;
        protected override string SwarmName => "Tesseract swarm";
    }
}
