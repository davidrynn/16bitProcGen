using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Lightweight debug component for tracking chunk lifecycle stages.
    /// Stage field is for diagnostics only; does not affect pipeline logic.
    /// </summary>
    public struct TerrainChunkDebugState : IComponentData
    {
        public int2 ChunkCoord;

        /// <summary>
        /// Lifecycle stage:
        /// 0 = Spawned
        /// 1 = NeedsDensity
        /// 2 = DensityReady
        /// 3 = NeedsMesh
        /// 4 = MeshReady
        /// 5 = Uploaded
        /// </summary>
        public byte Stage;

        public const byte StageSpawned = 0;
        public const byte StageNeedsDensity = 1;
        public const byte StageDensityReady = 2;
        public const byte StageNeedsMesh = 3;
        public const byte StageMeshReady = 4;
        public const byte StageUploaded = 5;

        public static TerrainChunkDebugState Create(int2 chunkCoord)
        {
            return new TerrainChunkDebugState
            {
                ChunkCoord = chunkCoord,
                Stage = StageSpawned
            };
        }
    }
}
