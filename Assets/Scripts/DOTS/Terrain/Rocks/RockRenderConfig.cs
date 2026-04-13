using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Managed singleton holding mesh/material used to render far rock instances.
    /// Rendering stays in managed presentation systems by design.
    /// </summary>
    public class RockRenderConfig : IComponentData
    {
        public Mesh Mesh;
        public Material Material;
        public float UniformScale = 1f;
    }
}
