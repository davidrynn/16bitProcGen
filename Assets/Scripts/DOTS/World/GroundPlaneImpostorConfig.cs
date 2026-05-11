using Unity.Entities;
using UnityEngine;

namespace DOTS.Impostors
{
    /// <summary>
    /// Managed component (class) on the ground-plane impostor entity.
    /// Holds configuration values and the runtime material reference so
    /// <see cref="GroundPlaneImpostorSystem"/> can push per-frame shader
    /// properties without a texture or MaterialPropertyBlock indirection.
    /// </summary>
    public class GroundPlaneImpostorConfig : IComponentData
    {
        /// <summary>Fixed world Y at which the plane sits (median terrain height).</summary>
        public float WorldY;

        /// <summary>Distance from the plane centre where alpha begins rising from 0.</summary>
        public float InnerFadeStart;

        /// <summary>Distance from the plane centre where alpha reaches full opacity.</summary>
        public float InnerFadeEnd;

        /// <summary>Outer radius of the disc mesh in world units.</summary>
        public float OuterRadius;

        /// <summary>Runtime material instance. System sets <c>_PlayerXZ</c> each frame.</summary>
        public Material ImpostorMaterial;

        /// <summary>Generated disc mesh passed to <c>Graphics.RenderMeshInstanced</c> each frame.</summary>
        public Mesh ImpostorMesh;
    }
}
