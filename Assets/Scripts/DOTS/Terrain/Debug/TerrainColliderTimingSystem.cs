using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using DOTS.Core;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Measures the time and frame count between chunk spawn and collider completion.
    /// Logs the latency via DebugSettings.LogFallThrough and removes the timestamp component after logging.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkColliderBuildSystem))]
    public partial struct TerrainColliderTimingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunk>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!DebugSettings.EnableFallThroughDebug)
                return;

            var elapsedTime = SystemAPI.Time.ElapsedTime;
            var frameCount = UnityEngine.Time.frameCount;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (timestamp, chunk, entity) in
                     SystemAPI.Query<RefRO<TerrainChunkSpawnTimestamp>, RefRO<TerrainChunk>>()
                         .WithAll<PhysicsCollider>()
                         .WithEntityAccess())
            {
                var elapsedSinceSpawn = elapsedTime - timestamp.ValueRO.SpawnElapsedTime;
                var framesSinceSpawn = frameCount - timestamp.ValueRO.SpawnFrameCount;
                var coord = chunk.ValueRO.ChunkCoord;

                DebugSettings.LogFallThrough(
                    $"Collider built: chunk=({coord.x},{coord.z}), " +
                    $"latency={elapsedSinceSpawn:F3}s, frames={framesSinceSpawn}");

                ecb.RemoveComponent<TerrainChunkSpawnTimestamp>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
