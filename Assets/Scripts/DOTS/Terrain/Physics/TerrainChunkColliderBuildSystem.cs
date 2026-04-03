using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using Unity.Transforms;
using DOTS.Terrain.Core;

namespace DOTS.Terrain
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Meshing.TerrainChunkMeshBuildSystem))]
    public partial struct TerrainChunkColliderBuildSystem : ISystem
    {
        private const int MaxCollidersPerFrame = 4;
        private const int MaxDisableRemovalsPerFrame = 16;
        private EntityQuery pendingColliderQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunk>();
            pendingColliderQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainChunkNeedsColliderBuild>());
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

            if (SystemAPI.TryGetSingleton<TerrainColliderSettings>(out var settings))
            {
                collidersEnabled = settings.Enabled;
            }

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

            if (pendingColliderQuery.IsEmpty)
            {
                ecb.Dispose();
                staleBlobs.Dispose();
                return;
            }

            foreach (var (chunk, meshData, entity) in SystemAPI
                         .Query<RefRO<TerrainChunk>, RefRO<TerrainChunkMeshData>>()
                         .WithAll<LocalTransform>()
                         .WithAll<TerrainChunkNeedsColliderBuild>()
                         .WithEntityAccess())
            {
                if (builtCount >= MaxCollidersPerFrame)
                {
                    break;
                }

                var mesh = meshData.ValueRO.Mesh;
                if (!mesh.IsCreated || mesh.Value.Vertices.Length == 0 || mesh.Value.Indices.Length == 0)
                {
                    CollectAndRemoveCollider(entityManager, ecb, staleBlobs, entity);
                    continue;
                }

                var vertexCount = mesh.Value.Vertices.Length;
                var indexCount = mesh.Value.Indices.Length;
                if (indexCount % 3 != 0 || !IndicesWithinBounds(mesh, vertexCount))
                {
                    LogInvalidMesh(chunk.ValueRO.ChunkCoord, entity, indexCount, vertexCount);
                    CollectAndRemoveCollider(entityManager, ecb, staleBlobs, entity);
                    continue;
                }

                var filter = new CollisionFilter
                {
                    BelongsTo = 2u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                };

                var newCollider = BuildMeshCollider(mesh, filter);
                if (!newCollider.IsCreated)
                {
                    CollectAndRemoveCollider(entityManager, ecb, staleBlobs, entity);
                    continue;
                }

                ApplyCollider(entityManager, ecb, staleBlobs, entity, newCollider);
                builtCount++;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            DisposeStaleBlobs(staleBlobs);
            staleBlobs.Dispose();
        }

        private static BlobAssetReference<Unity.Physics.Collider> BuildMeshCollider(BlobAssetReference<TerrainChunkMeshBlob> mesh, CollisionFilter filter)
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

            var collider = Unity.Physics.MeshCollider.Create(vertices, triangles, filter, Unity.Physics.Material.Default);

            vertices.Dispose();
            triangles.Dispose();

            return collider;
        }

        private static void ApplyCollider(EntityManager entityManager, EntityCommandBuffer ecb, NativeList<BlobAssetReference<Unity.Physics.Collider>> staleBlobs, Entity entity, BlobAssetReference<Unity.Physics.Collider> newCollider)
        {
            var hadColliderData = entityManager.HasComponent<TerrainChunkColliderData>(entity);
            if (hadColliderData)
            {
                var oldData = entityManager.GetComponentData<TerrainChunkColliderData>(entity);
                if (oldData.IsCreated)
                {
                    staleBlobs.Add(oldData.Collider);
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
                    staleBlobs.Add(data.Collider);
                }
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
                    staleBlobs[i].Dispose();
                }
            }
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
