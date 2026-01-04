using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using Unity.Transforms;

namespace DOTS.Terrain
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Meshing.TerrainChunkMeshBuildSystem))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
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
            int builtCount = 0;
            bool collidersEnabled = true;

            if (SystemAPI.TryGetSingleton<TerrainColliderSettings>(out var settings))
            {
                collidersEnabled = settings.Enabled;
            }

            if (!collidersEnabled)
            {
                int removedCount = 0;
                bool budgetReached = false;
                foreach (var (_, entity) in SystemAPI
                             .Query<RefRO<PhysicsCollider>>()
                             .WithAll<TerrainChunk>()
                             .WithEntityAccess())
                {
                    RemoveCollider(entityManager, entity);
                    removedCount++;
                    if (removedCount >= MaxDisableRemovalsPerFrame)
                    {
                        budgetReached = true;
                        break;
                    }
                }

                return;
            }

            if (pendingColliderQuery.IsEmpty)
            {
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
                    RemoveCollider(entityManager, entity);
                    continue;
                }

                var vertexCount = mesh.Value.Vertices.Length;
                var indexCount = mesh.Value.Indices.Length;
                if (indexCount % 3 != 0 || !IndicesWithinBounds(mesh, vertexCount))
                {
                    LogInvalidMesh(chunk.ValueRO.ChunkCoord, entity, indexCount, vertexCount);
                    RemoveCollider(entityManager, entity);
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
                    RemoveCollider(entityManager, entity);
                    continue;
                }

                ApplyCollider(entityManager, entity, newCollider);
                builtCount++;
            }
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

        private static void ApplyCollider(EntityManager entityManager, Entity entity, BlobAssetReference<Unity.Physics.Collider> newCollider)
        {
            var hadColliderData = entityManager.HasComponent<TerrainChunkColliderData>(entity);
            TerrainChunkColliderData oldData = default;
            if (hadColliderData)
            {
                oldData = entityManager.GetComponentData<TerrainChunkColliderData>(entity);
            }

            var colliderComponent = new PhysicsCollider { Value = newCollider };
            if (entityManager.HasComponent<PhysicsCollider>(entity))
            {
                entityManager.SetComponentData(entity, colliderComponent);
            }
            else
            {
                entityManager.AddComponentData(entity, colliderComponent);
            }

            var newData = new TerrainChunkColliderData { Collider = newCollider };
            if (hadColliderData)
            {
                entityManager.SetComponentData(entity, newData);
            }
            else
            {
                entityManager.AddComponentData(entity, newData);
            }

            if (oldData.IsCreated)
            {
                oldData.Dispose();
                entityManager.SetComponentData(entity, oldData);
            }

            if (entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity))
            {
                entityManager.RemoveComponent<TerrainChunkNeedsColliderBuild>(entity);
            }
        }

        private static void RemoveCollider(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<PhysicsCollider>(entity))
            {
                entityManager.RemoveComponent<PhysicsCollider>(entity);
            }

            if (entityManager.HasComponent<TerrainChunkColliderData>(entity))
            {
                var data = entityManager.GetComponentData<TerrainChunkColliderData>(entity);
                if (data.IsCreated)
                {
                    data.Dispose();
                }
                entityManager.SetComponentData(entity, data);
            }

            if (entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity))
            {
                entityManager.RemoveComponent<TerrainChunkNeedsColliderBuild>(entity);
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
            Debug.LogWarning($"[TerrainCollider] Invalid mesh for chunk {chunkCoord} (entity {entity.Index}). Indices={indexCount} Vertices={vertexCount}");
        }
    }
}
