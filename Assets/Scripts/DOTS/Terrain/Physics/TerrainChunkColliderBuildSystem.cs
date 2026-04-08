using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using Unity.Transforms;
using DOTS.Terrain.Core;
using System;

namespace DOTS.Terrain
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Meshing.TerrainChunkMeshUploadSystem))]
    public partial struct TerrainChunkColliderBuildSystem : ISystem
    {
        private const int DefaultMaxCollidersPerFrame = 4;
        private const int MaxDisableRemovalsPerFrame = 16;
        private EntityQuery pendingColliderQuery;
        private EntityQuery colliderLodCleanupQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunk>();
            pendingColliderQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainChunkNeedsColliderBuild>());
            colliderLodCleanupQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TerrainChunk, DOTS.Terrain.LOD.TerrainChunkLodState, PhysicsCollider>()
                .WithNone<TerrainChunkNeedsColliderBuild>()
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            // Collect stale blobs and dispose them AFTER ECB playback so that
            // PhysicsCollider components never reference disposed data.
            var staleBlobs = new NativeList<BlobAssetReference<Unity.Physics.Collider>>(Allocator.Temp);
            int builtCount = 0;
            bool collidersEnabled = true;
            var maxCollidersThisFrame = DefaultMaxCollidersPerFrame;
            var enableDetailedStaticMeshCollision = true;

            if (SystemAPI.TryGetSingleton<TerrainColliderSettings>(out var settings))
            {
                collidersEnabled = settings.Enabled;
                if (settings.MaxCollidersPerFrame > 0)
                {
                    maxCollidersThisFrame = settings.MaxCollidersPerFrame;
                }

                enableDetailedStaticMeshCollision = settings.EnableDetailedStaticMeshCollision;
            }

            var terrainMaterial = CreateTerrainColliderMaterial(enableDetailedStaticMeshCollision);

            if (!collidersEnabled)
            {
                int removedCount = 0;
                foreach (var (_, entity) in SystemAPI
                             .Query<RefRO<PhysicsCollider>>()
                             .WithAll<TerrainChunk>()
                             .WithEntityAccess())
                {
                    CollectAndRemoveCollider(entityManager, ecb, staleBlobs, entity);
                    removedCount++;
                    if (removedCount >= MaxDisableRemovalsPerFrame)
                    {
                        break;
                    }
                }

                ecb.Playback(entityManager);
                ecb.Dispose();
                DisposeStaleBlobs(staleBlobs);
                staleBlobs.Dispose();
                return;
            }

            // Read LOD policy once before the loop.
            var lodPolicyValid = SystemAPI.TryGetSingleton<DOTS.Terrain.LOD.TerrainLodSettings>(out var lodPolicy);
            var lodRemovalCount = 0;
            if (lodPolicyValid)
            {
                lodRemovalCount = RemoveCollidersOutsideLodPolicy(
                    entityManager,
                    ecb,
                    staleBlobs,
                    lodPolicy.ColliderMaxLod,
                    MaxDisableRemovalsPerFrame);
            }

            if (pendingColliderQuery.IsEmpty)
            {
                if (lodRemovalCount > 0)
                {
                    ecb.Playback(entityManager);
                    ecb.Dispose();
                    DisposeStaleBlobs(staleBlobs);
                    staleBlobs.Dispose();
                }
                else
                {
                    ecb.Dispose();
                    staleBlobs.Dispose();
                }

                return;
            }

            if (DebugSettings.EnableTerrainColliderPipelineDebug)
            {
                DebugSettings.LogTerrainColliderPipeline(
                    $"collider build config: detailedStaticMesh={enableDetailedStaticMeshCollision}, " +
                    $"maxPerFrame={maxCollidersThisFrame}");
            }

            foreach (var (chunk, meshData, entity) in SystemAPI
                         .Query<RefRO<TerrainChunk>, RefRO<TerrainChunkMeshData>>()
                         .WithAll<LocalTransform>()
                         .WithAll<TerrainChunkNeedsColliderBuild>()
                         .WithEntityAccess())
            {
                if (builtCount >= maxCollidersThisFrame)
                {
                    break;
                }

                // Skip collider builds for chunks above ColliderMaxLod — they are far from the
                // player and will be promoted before the player can reach them. This keeps the
                // 4/frame budget free for near-player LOD0/1 chunks.
                if (lodPolicyValid && entityManager.HasComponent<DOTS.Terrain.LOD.TerrainChunkLodState>(entity))
                {
                    var lodState = entityManager.GetComponentData<DOTS.Terrain.LOD.TerrainChunkLodState>(entity);
                    if (lodState.CurrentLod > lodPolicy.ColliderMaxLod)
                    {
                        CollectAndRemoveCollider(entityManager, ecb, staleBlobs, entity);
                        if (DebugSettings.EnableTerrainColliderPipelineDebug)
                            DebugSettings.LogTerrainColliderPipeline(
                                $"collider removed (LOD {lodState.CurrentLod} > max {lodPolicy.ColliderMaxLod}) chunk={entity.Index}");
                        continue;
                    }
                }

                var mesh = meshData.ValueRO.Mesh;
                var chunkCoord = chunk.ValueRO.ChunkCoord;
                if (!mesh.IsCreated || mesh.Value.Vertices.Length == 0 || mesh.Value.Indices.Length == 0)
                {
                    if (DebugSettings.EnableTerrainColliderPipelineDebug)
                    {
                        DebugSettings.LogTerrainColliderPipeline(
                            $"collider skipped (empty mesh) chunk={chunkCoord} entity={entity.Index} — preserving existing collider");
                    }

                    // Preserve any existing collider as a safety net (e.g. previous LOD's collider
                    // while a coarser LOD mesh is empty). Just clear the pending tag.
                    if (entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity))
                        ecb.RemoveComponent<TerrainChunkNeedsColliderBuild>(entity);
                    continue;
                }

                var vertexCount = mesh.Value.Vertices.Length;
                var indexCount = mesh.Value.Indices.Length;
                if (indexCount % 3 != 0 || !IndicesWithinBounds(mesh, vertexCount))
                {
                    LogInvalidMesh(chunkCoord, entity, indexCount, vertexCount);
                    CollectAndRemoveCollider(entityManager, ecb, staleBlobs, entity);
                    continue;
                }

                var filter = new CollisionFilter
                {
                    BelongsTo = 2u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                };

                var newCollider = BuildMeshCollider(mesh, filter, terrainMaterial);
                if (!newCollider.IsCreated)
                {
                    if (DebugSettings.EnableTerrainColliderPipelineDebug)
                    {
                        DebugSettings.LogTerrainColliderPipeline(
                            $"collider build failed (MeshCollider.Create) chunk={chunkCoord} entity={entity.Index}");
                    }

                    CollectAndRemoveCollider(entityManager, ecb, staleBlobs, entity);
                    continue;
                }

                ApplyCollider(entityManager, ecb, staleBlobs, entity, newCollider);
                if (DebugSettings.EnableTerrainColliderPipelineDebug)
                {
                    var triCount = indexCount / 3;
                    DebugSettings.LogTerrainColliderPipeline(
                        $"collider applied chunk={chunkCoord} entity={entity.Index} tris={triCount} verts={vertexCount}");
                }

                builtCount++;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            DisposeStaleBlobs(staleBlobs);
            staleBlobs.Dispose();

            if (DebugSettings.EnableTerrainColliderPipelineDebug && !pendingColliderQuery.IsEmpty)
            {
                var stillPending = pendingColliderQuery.CalculateEntityCount();
                if (stillPending > 0)
                {
                    DebugSettings.LogTerrainColliderPipeline(
                        $"collider backlog: {stillPending} chunk(s) still have TerrainChunkNeedsColliderBuild " +
                        $"(built={builtCount} this frame, maxPerFrame={maxCollidersThisFrame})");
                }
            }
        }

        private int RemoveCollidersOutsideLodPolicy(
            EntityManager entityManager,
            EntityCommandBuffer ecb,
            NativeList<BlobAssetReference<Unity.Physics.Collider>> staleBlobs,
            int colliderMaxLod,
            int maxRemovalsPerFrame)
        {
            if (colliderLodCleanupQuery.IsEmpty)
            {
                return 0;
            }

            var removedCount = 0;
            using var entities = colliderLodCleanupQuery.ToEntityArray(Allocator.Temp);
            using var lodStates = colliderLodCleanupQuery.ToComponentDataArray<DOTS.Terrain.LOD.TerrainChunkLodState>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (removedCount >= maxRemovalsPerFrame)
                {
                    break;
                }

                if (lodStates[i].CurrentLod <= colliderMaxLod)
                {
                    continue;
                }

                CollectAndRemoveCollider(entityManager, ecb, staleBlobs, entities[i]);
                removedCount++;

                if (DebugSettings.EnableTerrainColliderPipelineDebug)
                {
                    DebugSettings.LogTerrainColliderPipeline(
                        $"collider removed (steady-state LOD cleanup, LOD {lodStates[i].CurrentLod} > max {colliderMaxLod}) chunk={entities[i].Index}");
                }
            }

            return removedCount;
        }

        private static BlobAssetReference<Unity.Physics.Collider> BuildMeshCollider(BlobAssetReference<TerrainChunkMeshBlob> mesh, CollisionFilter filter, Unity.Physics.Material material)
        {
            var vertexCount = mesh.Value.Vertices.Length;
            var indexCount = mesh.Value.Indices.Length;
            var triangleCount = indexCount / 3;

            var vertices = new NativeArray<float3>(vertexCount, Allocator.Temp);
            var triangles = new NativeArray<int3>(triangleCount, Allocator.Temp);

            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = mesh.Value.Vertices[i];
            }

            for (int i = 0; i < triangleCount; i++)
            {
                int baseIndex = i * 3;
                triangles[i] = new int3(
                    mesh.Value.Indices[baseIndex],
                    mesh.Value.Indices[baseIndex + 1],
                    mesh.Value.Indices[baseIndex + 2]);
            }

            var collider = Unity.Physics.MeshCollider.Create(vertices, triangles, filter, material);

            vertices.Dispose();
            triangles.Dispose();

            return collider;
        }

        private static Unity.Physics.Material CreateTerrainColliderMaterial(bool enableDetailedStaticMeshCollision)
        {
            var material = Unity.Physics.Material.Default;
            material.EnableDetailedStaticMeshCollision = enableDetailedStaticMeshCollision;
            return material;
        }

        private static void ApplyCollider(EntityManager entityManager, EntityCommandBuffer ecb, NativeList<BlobAssetReference<Unity.Physics.Collider>> staleBlobs, Entity entity, BlobAssetReference<Unity.Physics.Collider> newCollider)
        {
            var hadColliderData = entityManager.HasComponent<TerrainChunkColliderData>(entity);
            if (hadColliderData)
            {
                var oldData = entityManager.GetComponentData<TerrainChunkColliderData>(entity);
                if (oldData.IsCreated)
                {
                    AddStaleBlobIfNeeded(staleBlobs, oldData.Collider);
                }
            }

            var colliderComponent = new PhysicsCollider { Value = newCollider };
            if (entityManager.HasComponent<PhysicsCollider>(entity))
            {
                ecb.SetComponent(entity, colliderComponent);
            }
            else
            {
                ecb.AddComponent(entity, colliderComponent);
            }

            // Register the entity in the default physics world so the broadphase sees the collider.
            if (!entityManager.HasComponent<PhysicsWorldIndex>(entity))
            {
                ecb.AddSharedComponent(entity, new PhysicsWorldIndex());
            }

            var newData = new TerrainChunkColliderData { Collider = newCollider };
            if (hadColliderData)
            {
                ecb.SetComponent(entity, newData);
            }
            else
            {
                ecb.AddComponent(entity, newData);
            }

            if (entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity))
            {
                ecb.RemoveComponent<TerrainChunkNeedsColliderBuild>(entity);
            }
        }

        private static void CollectAndRemoveCollider(EntityManager entityManager, EntityCommandBuffer ecb, NativeList<BlobAssetReference<Unity.Physics.Collider>> staleBlobs, Entity entity)
        {
            if (entityManager.HasComponent<PhysicsCollider>(entity))
            {
                ecb.RemoveComponent<PhysicsCollider>(entity);
            }

            if (entityManager.HasComponent<TerrainChunkColliderData>(entity))
            {
                var data = entityManager.GetComponentData<TerrainChunkColliderData>(entity);
                if (data.IsCreated)
                {
                    AddStaleBlobIfNeeded(staleBlobs, data.Collider);
                }

                // Remove data component together with PhysicsCollider so stale blob references
                // are not observed/disposed again in later frames.
                ecb.RemoveComponent<TerrainChunkColliderData>(entity);
            }

            if (entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity))
            {
                ecb.RemoveComponent<TerrainChunkNeedsColliderBuild>(entity);
            }
        }

        private static void DisposeStaleBlobs(NativeList<BlobAssetReference<Unity.Physics.Collider>> staleBlobs)
        {
            for (int i = 0; i < staleBlobs.Length; i++)
            {
                if (staleBlobs[i].IsCreated)
                {
                    try
                    {
                        staleBlobs[i].Dispose();
                    }
                    catch (InvalidOperationException)
                    {
                        // Blob was already disposed elsewhere; ignore to keep collider cleanup
                        // resilient and avoid cascading physics-step failures.
                    }
                }
            }
        }

        private static void AddStaleBlobIfNeeded(
            NativeList<BlobAssetReference<Unity.Physics.Collider>> staleBlobs,
            BlobAssetReference<Unity.Physics.Collider> collider)
        {
            if (!collider.IsCreated)
            {
                return;
            }

            for (int i = 0; i < staleBlobs.Length; i++)
            {
                if (staleBlobs[i].Equals(collider))
                {
                    return;
                }
            }

            staleBlobs.Add(collider);
        }

        private static bool IndicesWithinBounds(BlobAssetReference<TerrainChunkMeshBlob> mesh, int vertexCount)
        {
            ref var indices = ref mesh.Value.Indices;
            for (int i = 0; i < indices.Length; i++)
            {
                var index = indices[i];
                if (index < 0 || index >= vertexCount)
                {
                    return false;
                }
            }

            return true;
        }

        private static void LogInvalidMesh(int3 chunkCoord, Entity entity, int indexCount, int vertexCount)
        {
            DebugSettings.LogWarning($"TerrainCollider: Invalid mesh for chunk {chunkCoord} (entity {entity.Index}). Indices={indexCount} Vertices={vertexCount}");
        }
    }
}
