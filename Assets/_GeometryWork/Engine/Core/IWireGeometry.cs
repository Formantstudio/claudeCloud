using UnityEngine;

namespace PsychedelicLab.GeometryFX
{
    /// <summary>
    /// What a wire chamber has to expose for a particle swarm to ride it. <see cref="CurvedGeometryChamber"/> implements this in all
    /// three of its modes, so one swarm serves every shape and every shape added later gets
    /// particles for free.
    ///
    /// Everything is in the implementing component's local space, and samplers read the same
    /// per-frame data the mesh was built from, so particles and wire can never disagree.
    /// </summary>
    public interface IWireGeometry
    {
        /// <summary>False until a mesh exists and this frame's geometry is valid.</summary>
        bool IsBuilt { get; }

        /// <summary>Grid divisions across the surface, for choosing a sensible particle density.</summary>
        int GridU { get; }
        int GridV { get; }

        /// <summary>A point on the surface at normalised (u, v) in [0,1]^2.</summary>
        Vector3 SampleGrid(float u, float v);
    }
}
