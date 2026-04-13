using DOTS.Terrain.Core;
using DOTS.Terrain.Rocks;
using DOTS.Terrain.Rendering;
using DOTS.Terrain.Trees;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace DOTS.Terrain.LOD
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkLodSelectionSystem))]
    public partial struct TerrainChunkLodApplySystem : ISystem
    {
        private EntityQuery _chunkQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainLodSettings>();
            _chunkQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadWrite<TerrainChunkLodState>(),
                ComponentType.ReadWrite<TerrainChunkGridInfo>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_chunkQuery.IsEmpty)
                return;

            var settings = SystemAPI.GetSingleton<TerrainLodSettings>();
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            using var entities = _chunkQuery.ToEntityArray(Allocator.Temp);
            using var lodStates = _chunkQuery.ToComponentDataArray<TerrainChunkLodState>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var lodState = lodStates[i];
                if (lodState.TargetLod == lodState.CurrentLod)
                    continue;

                var entity = entities[i];
                var targetLod = lodState.TargetLod;

                // LOD3 means culled while still resident in streaming window.
                // Strip runtime mesh/density/collider payload and clear pending rebuild tags.
                if (targetLod >= 3)
                {
                    ApplyCulledLod(em, ecb, entity);
                }
                else
                {
                    // Update grid info to the new LOD's resolution and voxel size.
                    em.SetComponentData(entity, TerrainChunkGridInfo.Create(
                        settings.GetResolution(targetLod),
                        settings.GetVoxelSize(targetLod)));

                    // Trigger density rebuild with new resolution.
                    if (!em.HasComponent<TerrainChunkNeedsDensityRebuild>(entity))
                        ecb.AddComponent<TerrainChunkNeedsDensityRebuild>(entity);
                }

                // Advance state.
                lodState.CurrentLod = targetLod;
                lodState.LastSwitchFrame = (uint)UnityEngine.Time.frameCount;
                em.SetComponentData(entity, lodState);

                // Mark dirty for downstream seam/skirt systems (M2).
                if (!em.HasComponent<TerrainChunkLodDirty>(entity))
                    ecb.AddComponent<TerrainChunkLodDirty>(entity);

                DebugSettings.LogLod($"Chunk LOD applied: entity {entity.Index} → LOD {targetLod}");
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void ApplyCulledLod(
            EntityManager entityManager,
            EntityCommandBuffer ecb,
            Entity entity)
        {
            if (entityManager.HasComponent<TerrainChunkDensity>(entity))
            {
                var density = entityManager.GetComponentData<TerrainChunkDensity>(entity);
                density.Dispose();
                entityManager.RemoveComponent<TerrainChunkDensity>(entity);
            }

            if (entityManager.HasComponent<TerrainChunkMeshData>(entity))
            {
                var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
                meshData.Dispose();
                entityManager.RemoveComponent<TerrainChunkMeshData>(entity);
            }

            if (entityManager.HasComponent<Mesh>(entity))
            {
                var mesh = entityManager.GetComponentObject<Mesh>(entity);
                if (mesh != null)
                    Object.Destroy(mesh);
                entityManager.RemoveComponent<Mesh>(entity);
            }

            if (entityManager.HasComponent<GrassChunkBladeBuffer>(entity))
            {
                var grassBuffer = entityManager.GetComponentObject<GrassChunkBladeBuffer>(entity);
                grassBuffer?.Dispose();
                entityManager.RemoveComponent<GrassChunkBladeBuffer>(entity);
            }

            if (entityManager.HasBuffer<TreePlacementRecord>(entity))
                entityManager.RemoveComponent<TreePlacementRecord>(entity);

            if (entityManager.HasComponent<ChunkTreePlacementTag>(entity))
                entityManager.RemoveComponent<ChunkTreePlacementTag>(entity);

            if (entityManager.HasBuffer<RockPlacementRecord>(entity))
                entityManager.RemoveComponent<RockPlacementRecord>(entity);

            if (entityManager.HasComponent<ChunkRockPlacementTag>(entity))
                entityManager.RemoveComponent<ChunkRockPlacementTag>(entity);

            // Keep collider components until TerrainChunkColliderBuildSystem processes LOD policy.
            // That system owns deferred blob disposal timing to avoid physics-world races.

            if (entityManager.HasComponent<TerrainChunkDensityGridInfo>(entity))
                ecb.RemoveComponent<TerrainChunkDensityGridInfo>(entity);

            if (entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(entity))
                ecb.RemoveComponent<TerrainChunkNeedsDensityRebuild>(entity);

            if (entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(entity))
                ecb.RemoveComponent<TerrainChunkNeedsMeshBuild>(entity);

            if (entityManager.HasComponent<TerrainChunkNeedsRenderUpload>(entity))
                ecb.RemoveComponent<TerrainChunkNeedsRenderUpload>(entity);

            if (entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity))
                ecb.RemoveComponent<TerrainChunkNeedsColliderBuild>(entity);
        }
    }
}
