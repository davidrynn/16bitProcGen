using Unity.Entities;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Records the time and frame when a terrain chunk was spawned by the streaming system.
    /// Used by TerrainColliderTimingSystem to measure collider build latency.
    /// </summary>
    public struct TerrainChunkSpawnTimestamp : IComponentData
    {
        public double SpawnElapsedTime;
        public int SpawnFrameCount;
    }

    /// <summary>
    /// Stores per-chunk collider mesh quality statistics for fall-through diagnostics (Phase 4).
    /// </summary>
    public struct TerrainChunkColliderDiagnostics : IComponentData
    {
        public float MinTriangleArea;
        public float MaxTriangleArea;
        public float AvgTriangleArea;
        public float MaxAspectRatio;
        public int DegenerateTriangleCount;
        public int TotalTriangleCount;
    }
}
