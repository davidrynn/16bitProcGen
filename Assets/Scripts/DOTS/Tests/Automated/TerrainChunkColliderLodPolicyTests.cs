using DOTS.Terrain.LOD;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainChunkColliderLodPolicyTests
    {
        [Test]
        public void ColliderBuild_RemovesOutOfRangeCollider_WithoutPendingBuildTag()
        {
            using var world = new World("TerrainChunkColliderLodPolicyTests");
            var entityManager = world.EntityManager;

            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var colliderSystem = world.CreateSystem<TerrainChunkColliderBuildSystem>();
            simGroup.AddSystemToUpdateList(colliderSystem);

            EnsureLodSettings(entityManager, colliderMaxLod: 1);
            EnsureColliderSettings(entityManager, enabled: true);

            var chunk = entityManager.CreateEntity(
                typeof(TerrainChunk),
                typeof(TerrainChunkLodState),
                typeof(LocalTransform));

            entityManager.SetComponentData(chunk, new TerrainChunk { ChunkCoord = int3.zero });
            entityManager.SetComponentData(chunk, new TerrainChunkLodState { CurrentLod = 2, TargetLod = 2, LastSwitchFrame = 0 });
            entityManager.SetComponentData(chunk, LocalTransform.FromPosition(float3.zero));

            var collider = CreateBoxCollider();
            entityManager.AddComponentData(chunk, new PhysicsCollider { Value = collider });
            entityManager.AddComponentData(chunk, new TerrainChunkColliderData { Collider = collider });

            simGroup.Update();

            Assert.IsFalse(entityManager.HasComponent<PhysicsCollider>(chunk));
            Assert.IsFalse(entityManager.HasComponent<TerrainChunkColliderData>(chunk));
        }

        [Test]
        public void ColliderBuild_KeepsCollider_WithinMaxLod()
        {
            using var world = new World("TerrainChunkColliderLodPolicyTests");
            var entityManager = world.EntityManager;

            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var colliderSystem = world.CreateSystem<TerrainChunkColliderBuildSystem>();
            simGroup.AddSystemToUpdateList(colliderSystem);

            EnsureLodSettings(entityManager, colliderMaxLod: 1);
            EnsureColliderSettings(entityManager, enabled: true);

            var chunk = entityManager.CreateEntity(
                typeof(TerrainChunk),
                typeof(TerrainChunkLodState),
                typeof(LocalTransform));

            entityManager.SetComponentData(chunk, new TerrainChunk { ChunkCoord = int3.zero });
            entityManager.SetComponentData(chunk, new TerrainChunkLodState { CurrentLod = 1, TargetLod = 1, LastSwitchFrame = 0 });
            entityManager.SetComponentData(chunk, LocalTransform.FromPosition(float3.zero));

            var collider = CreateBoxCollider();
            entityManager.AddComponentData(chunk, new PhysicsCollider { Value = collider });
            entityManager.AddComponentData(chunk, new TerrainChunkColliderData { Collider = collider });

            simGroup.Update();

            Assert.IsTrue(entityManager.HasComponent<PhysicsCollider>(chunk));
            Assert.IsTrue(entityManager.HasComponent<TerrainChunkColliderData>(chunk));

            var colliderData = entityManager.GetComponentData<TerrainChunkColliderData>(chunk);
            colliderData.Dispose();
            entityManager.SetComponentData(chunk, colliderData);
            entityManager.RemoveComponent<TerrainChunkColliderData>(chunk);
            entityManager.RemoveComponent<PhysicsCollider>(chunk);
        }

        [Test]
        public void ColliderBuild_RemovesOutOfRangeCollider_WithPendingBuildTag()
        {
            using var world = new World("TerrainChunkColliderLodPolicyTests");
            var entityManager = world.EntityManager;

            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var colliderSystem = world.CreateSystem<TerrainChunkColliderBuildSystem>();
            simGroup.AddSystemToUpdateList(colliderSystem);

            EnsureLodSettings(entityManager, colliderMaxLod: 1);
            EnsureColliderSettings(entityManager, enabled: true);

            var chunk = entityManager.CreateEntity(
                typeof(TerrainChunk),
                typeof(TerrainChunkLodState),
                typeof(LocalTransform),
                typeof(TerrainChunkMeshData),
                typeof(TerrainChunkNeedsColliderBuild));

            entityManager.SetComponentData(chunk, new TerrainChunk { ChunkCoord = int3.zero });
            entityManager.SetComponentData(chunk, new TerrainChunkLodState { CurrentLod = 2, TargetLod = 2, LastSwitchFrame = 0 });
            entityManager.SetComponentData(chunk, LocalTransform.FromPosition(float3.zero));
            entityManager.SetComponentData(chunk, new TerrainChunkMeshData());

            var collider = CreateBoxCollider();
            entityManager.AddComponentData(chunk, new PhysicsCollider { Value = collider });
            entityManager.AddComponentData(chunk, new TerrainChunkColliderData { Collider = collider });

            simGroup.Update();

            Assert.IsFalse(entityManager.HasComponent<PhysicsCollider>(chunk));
            Assert.IsFalse(entityManager.HasComponent<TerrainChunkColliderData>(chunk));
            Assert.IsFalse(entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(chunk));
        }

        [Test]
        public void LodApply_CulledLod_PreservesColliderComponents_ForDeferredCleanup()
        {
            using var world = new World("TerrainChunkColliderLodPolicyTests");
            var entityManager = world.EntityManager;

            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var lodApplySystem = world.CreateSystem<TerrainChunkLodApplySystem>();
            simGroup.AddSystemToUpdateList(lodApplySystem);

            EnsureLodSettings(entityManager, colliderMaxLod: 1);

            var chunk = entityManager.CreateEntity(
                typeof(TerrainChunk),
                typeof(TerrainChunkGridInfo),
                typeof(TerrainChunkLodState),
                typeof(TerrainChunkNeedsColliderBuild));

            entityManager.SetComponentData(chunk, new TerrainChunk { ChunkCoord = int3.zero });
            entityManager.SetComponentData(chunk, TerrainChunkGridInfo.Create(new int3(16, 16, 16), 1f));
            entityManager.SetComponentData(chunk, new TerrainChunkLodState { CurrentLod = 0, TargetLod = 3, LastSwitchFrame = 0 });

            var collider = CreateBoxCollider();
            entityManager.AddComponentData(chunk, new PhysicsCollider { Value = collider });
            entityManager.AddComponentData(chunk, new TerrainChunkColliderData { Collider = collider });

            simGroup.Update();

            var lodState = entityManager.GetComponentData<TerrainChunkLodState>(chunk);
            Assert.AreEqual(3, lodState.CurrentLod);
            Assert.IsTrue(entityManager.HasComponent<PhysicsCollider>(chunk));
            Assert.IsTrue(entityManager.HasComponent<TerrainChunkColliderData>(chunk));
            Assert.IsFalse(entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(chunk));

            var colliderData = entityManager.GetComponentData<TerrainChunkColliderData>(chunk);
            colliderData.Dispose();
            entityManager.SetComponentData(chunk, colliderData);
            entityManager.RemoveComponent<TerrainChunkColliderData>(chunk);
            entityManager.RemoveComponent<PhysicsCollider>(chunk);
        }

        private static void EnsureLodSettings(EntityManager entityManager, int colliderMaxLod)
        {
            using var query = entityManager.CreateEntityQuery(typeof(TerrainLodSettings));
            if (query.IsEmpty)
            {
                var entity = entityManager.CreateEntity(typeof(TerrainLodSettings));
                var settings = TerrainLodSettings.Default;
                settings.ColliderMaxLod = colliderMaxLod;
                entityManager.SetComponentData(entity, settings);
                return;
            }

            var singleton = query.GetSingletonEntity();
            var current = entityManager.GetComponentData<TerrainLodSettings>(singleton);
            current.ColliderMaxLod = colliderMaxLod;
            entityManager.SetComponentData(singleton, current);
        }

        private static void EnsureColliderSettings(EntityManager entityManager, bool enabled)
        {
            using var query = entityManager.CreateEntityQuery(typeof(TerrainColliderSettings));
            if (query.IsEmpty)
            {
                var entity = entityManager.CreateEntity(typeof(TerrainColliderSettings));
                entityManager.SetComponentData(entity, new TerrainColliderSettings { Enabled = enabled });
                return;
            }

            var singleton = query.GetSingletonEntity();
            var current = entityManager.GetComponentData<TerrainColliderSettings>(singleton);
            current.Enabled = enabled;
            entityManager.SetComponentData(singleton, current);
        }

        private static BlobAssetReference<Unity.Physics.Collider> CreateBoxCollider()
        {
            return Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Size = new float3(1f, 1f, 1f),
                Orientation = quaternion.identity,
                BevelRadius = 0f
            }, CollisionFilter.Default);
        }
    }
}
