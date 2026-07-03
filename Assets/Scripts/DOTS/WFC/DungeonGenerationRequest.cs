using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Control component for dungeon generation requests.
    /// Add this to an entity to request dungeon generation.
    /// Extracted from DungeonEntitySpawningSystem.cs (cleanup round 1, plan row R63).
    /// </summary>
    public struct DungeonGenerationRequest : IComponentData
    {
        public bool isActive;
        public float3 position;
        public int2 size;
        public float cellSize;
    }
}
