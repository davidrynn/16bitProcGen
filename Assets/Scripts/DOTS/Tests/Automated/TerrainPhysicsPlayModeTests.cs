using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainPhysicsPlayModeTests
    {
        private World previousWorld;
        private World testWorld;
        private EntityManager entityManager;
        private InitializationSystemGroup initGroup;
        private SimulationSystemGroup simulationGroup;
        private FixedStepSimulationSystemGroup fixedStepGroup;
        private PhysicsSystemGroup physicsGroup;
        private BuildPhysicsWorld buildPhysicsWorld;
        private StepPhysicsWorld stepPhysicsWorld;
        private ExportPhysicsWorld exportPhysicsWorld;
        private double elapsedTime;
        private const float FixedDeltaTime = 1f / 60f;

        [SetUp]
        public void SetUp()
        {
            previousWorld = World.DefaultGameObjectInjectionWorld;
            testWorld = new World("Terrain Physics PlayMode Tests");
            World.DefaultGameObjectInjectionWorld = testWorld;
            entityManager = testWorld.EntityManager;

            initGroup = testWorld.GetOrCreateSystemManaged<InitializationSystemGroup>();
            simulationGroup = testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            fixedStepGroup = testWorld.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            fixedStepGroup.Timestep = FixedDeltaTime;

            physicsGroup = testWorld.GetOrCreateSystemManaged<PhysicsSystemGroup>();
            fixedStepGroup.AddSystemToUpdateList(physicsGroup);

            buildPhysicsWorld = testWorld.GetOrCreateSystemManaged<BuildPhysicsWorld>();
            stepPhysicsWorld = testWorld.GetOrCreateSystemManaged<StepPhysicsWorld>();
            exportPhysicsWorld = testWorld.GetOrCreateSystemManaged<ExportPhysicsWorld>();

            initGroup.Enabled = true;
            simulationGroup.Enabled = true;
            fixedStepGroup.Enabled = true;
            physicsGroup.Enabled = true;
            buildPhysicsWorld.Enabled = true;
            stepPhysicsWorld.Enabled = true;
            exportPhysicsWorld.Enabled = true;

            physicsGroup.AddSystemToUpdateList(buildPhysicsWorld);
            physicsGroup.AddSystemToUpdateList(stepPhysicsWorld);
            physicsGroup.AddSystemToUpdateList(exportPhysicsWorld);

            TrySortSystems(physicsGroup);
            TrySortSystems(fixedStepGroup);

            using (var physicsStepQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsStep>()))
            {
                if (physicsStepQuery.IsEmpty)
                {
                    var physicsStepEntity = entityManager.CreateEntity(typeof(PhysicsStep));
                    entityManager.SetComponentData(physicsStepEntity, PhysicsStep.Default);
                }
            }

            elapsedTime = 0d;
        }

        [TearDown]
        public void TearDown()
        {
            if (testWorld != null && testWorld.IsCreated)
            {
                testWorld.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = previousWorld;
        }

        [UnityTest]
        public IEnumerator RaycastHitsStaticBoxCollider()
        {
            var collider = BlobAssetReference<Collider>.Null;
            Entity colliderEntity = Entity.Null;

            try
            {
                collider = BoxCollider.Create(new BoxGeometry
                {
                    Center = float3.zero,
                    Size = new float3(1f, 1f, 1f),
                    Orientation = quaternion.identity,
                    BevelRadius = 0f
                }, CollisionFilter.Default);

                colliderEntity = entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsWorldIndex));
                entityManager.SetComponentData(colliderEntity, LocalTransform.FromPosition(float3.zero));
                entityManager.SetComponentData(colliderEntity, new PhysicsCollider { Value = collider });

                bool hasPhysicsWorld = false;
                using var query = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
                for (int i = 0; i < 3; i++)
                {
                    TickWorldOnce();
                    if (!query.IsEmpty)
                    {
                        hasPhysicsWorld = true;
                        break;
                    }
                }

                if (!hasPhysicsWorld)
                {
                    LogMissingPhysicsWorldDiagnostics();
                }

                Assert.IsTrue(hasPhysicsWorld, "PhysicsWorldSingleton should exist after fixed-step tick");

                var physicsWorldSingleton = query.GetSingleton<PhysicsWorldSingleton>();
                var rayInput = new RaycastInput
                {
                    Start = new float3(0f, 10f, 0f),
                    End = new float3(0f, -10f, 0f),
                    Filter = CollisionFilter.Default
                };

                bool hit = physicsWorldSingleton.PhysicsWorld.CastRay(rayInput);

                Assert.IsTrue(hit, "Expected raycast to hit the static box collider.");
            }
            finally
            {
                if (colliderEntity != Entity.Null && entityManager.Exists(colliderEntity))
                {
                    entityManager.DestroyEntity(colliderEntity);
                }

                if (collider.IsCreated)
                {
                    collider.Dispose();
                }
            }

            yield return null;
        }

        [UnityTest]
        [Ignore("TODO: Wire up terrain collider pipeline (SDF -> mesh -> collider) and assert raycast hits.")]
        public IEnumerator TerrainChunkColliderPipeline_CreatesColliderAndRaycastHits()
        {
            EnsureTerrainPipelineSystems();
            EnsureSdfFieldSettings();
            EnsureTerrainColliderSettings();

            var chunkEntity = CreateTerrainChunkEntity(new int3(16, 16, 16), 1f, float3.zero);

            try
            {
                bool ready = TickUntil(120, () =>
                {
                    if (!entityManager.Exists(chunkEntity))
                    {
                        return false;
                    }

                    var hasColliderData = entityManager.HasComponent<TerrainChunkColliderData>(chunkEntity);
                    var colliderCreated = hasColliderData && entityManager.GetComponentData<TerrainChunkColliderData>(chunkEntity).IsCreated;

                    return HasPhysicsWorldSingleton()
                           && entityManager.HasComponent<TerrainChunkDensity>(chunkEntity)
                           && entityManager.HasComponent<TerrainChunkMeshData>(chunkEntity)
                           && entityManager.HasComponent<PhysicsCollider>(chunkEntity)
                           && colliderCreated;
                });

                if (!ready)
                {
                    LogTerrainPipelineDiagnostics(chunkEntity, "Timed out waiting for terrain collider + PhysicsWorldSingleton.");
                    Assert.Fail("Terrain collider pipeline did not complete within timeout.");
                }

                TickWorldOnce();

                using var physicsWorldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
                var physicsWorldSingleton = physicsWorldQuery.GetSingleton<PhysicsWorldSingleton>();
                var rayInput = new RaycastInput
                {
                    Start = new float3(0f, 10f, 0f),
                    End = new float3(0f, -10f, 0f),
                    Filter = CollisionFilter.Default
                };

                bool hit = physicsWorldSingleton.PhysicsWorld.CastRay(rayInput);

                Assert.IsTrue(hit, "Expected raycast to hit the generated terrain collider.");
            }
            finally
            {
                if (chunkEntity != Entity.Null && entityManager.Exists(chunkEntity))
                {
                    if (entityManager.HasComponent<TerrainChunkColliderData>(chunkEntity))
                    {
                        var colliderData = entityManager.GetComponentData<TerrainChunkColliderData>(chunkEntity);
                        if (colliderData.IsCreated)
                        {
                            colliderData.Dispose();
                        }
                        entityManager.SetComponentData(chunkEntity, colliderData);
                        entityManager.RemoveComponent<TerrainChunkColliderData>(chunkEntity);
                    }

                    if (entityManager.HasComponent<PhysicsCollider>(chunkEntity))
                    {
                        entityManager.RemoveComponent<PhysicsCollider>(chunkEntity);
                    }
                }
            }
            yield return null;
        }

        private void TickWorldOnce()
        {
            AdvanceTime();
            initGroup.Update();
            simulationGroup.Update();
            fixedStepGroup.Update();
        }

        private void AdvanceTime()
        {
            elapsedTime += FixedDeltaTime;
            testWorld.SetTime(new TimeData(elapsedTime, FixedDeltaTime));
        }

        private void EnsureTerrainPipelineSystems()
        {
            var densitySystem = testWorld.GetOrCreateSystemManaged<TerrainChunkDensitySamplingSystem>();
            var meshSystem = testWorld.GetOrCreateSystemManaged<DOTS.Terrain.Meshing.TerrainChunkMeshBuildSystem>();
            var colliderSystem = testWorld.GetOrCreateSystemManaged<TerrainChunkColliderBuildSystem>();

            simulationGroup.AddSystemToUpdateList(densitySystem);
            simulationGroup.AddSystemToUpdateList(meshSystem);
            simulationGroup.AddSystemToUpdateList(colliderSystem);

            TrySortSystems(simulationGroup);
        }

        private void EnsureSdfFieldSettings()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFTerrainFieldSettings>());
            if (!query.IsEmpty)
            {
                return;
            }

            var entity = entityManager.CreateEntity(typeof(SDFTerrainFieldSettings));
            entityManager.SetComponentData(entity, new SDFTerrainFieldSettings
            {
                BaseHeight = 4f,
                Amplitude = 0f,
                Frequency = 0f,
                NoiseValue = 0f
            });
        }

        private void EnsureTerrainColliderSettings()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainColliderSettings>());
            if (query.IsEmpty)
            {
                var entity = entityManager.CreateEntity(typeof(TerrainColliderSettings));
                entityManager.SetComponentData(entity, new TerrainColliderSettings { Enabled = true });
            }
            else
            {
                var entity = query.GetSingletonEntity();
                entityManager.SetComponentData(entity, new TerrainColliderSettings { Enabled = true });
            }
        }

        private Entity CreateTerrainChunkEntity(int3 resolution, float voxelSize, float3 worldOrigin)
        {
            var entity = entityManager.CreateEntity(
                typeof(TerrainChunk),
                typeof(TerrainChunkGridInfo),
                typeof(TerrainChunkBounds),
                typeof(TerrainChunkNeedsDensityRebuild),
                typeof(LocalTransform),
                typeof(PhysicsWorldIndex));

            entityManager.SetComponentData(entity, new TerrainChunk { ChunkCoord = int3.zero });
            entityManager.SetComponentData(entity, TerrainChunkGridInfo.Create(resolution, voxelSize));
            entityManager.SetComponentData(entity, new TerrainChunkBounds { WorldOrigin = worldOrigin });
            entityManager.SetComponentData(entity, LocalTransform.FromPosition(worldOrigin));

            return entity;
        }

        private bool HasPhysicsWorldSingleton()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            return !query.IsEmpty;
        }

        private bool TickUntil(int maxFrames, System.Func<bool> condition)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                TickWorldOnce();
                if (condition())
                {
                    return true;
                }
            }

            return false;
        }

        private void LogTerrainPipelineDiagnostics(Entity chunkEntity, string reason)
        {
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Terrain pipeline diagnostics: {reason}");
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Singleton: PhysicsWorldSingleton={HasPhysicsWorldSingleton()}");
            using var physicsStepQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsStep>());
            using var colliderSettingsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainColliderSettings>());
            using var sdfSettingsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFTerrainFieldSettings>());
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Singleton: PhysicsStep={physicsStepQuery.IsEmpty == false}");
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Singleton: TerrainColliderSettings={colliderSettingsQuery.IsEmpty == false}");
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Singleton: SDFTerrainFieldSettings={sdfSettingsQuery.IsEmpty == false}");

            if (chunkEntity != Entity.Null && entityManager.Exists(chunkEntity))
            {
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Chunk components: NeedsDensityRebuild={entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkEntity)}");
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Chunk components: Density={entityManager.HasComponent<TerrainChunkDensity>(chunkEntity)}");
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Chunk components: NeedsMeshBuild={entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(chunkEntity)}");
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Chunk components: MeshData={entityManager.HasComponent<TerrainChunkMeshData>(chunkEntity)}");
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Chunk components: NeedsColliderBuild={entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(chunkEntity)}");
                var hasColliderData = entityManager.HasComponent<TerrainChunkColliderData>(chunkEntity);
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Chunk components: ColliderData={hasColliderData}");
                if (hasColliderData)
                {
                    Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Chunk components: ColliderData.IsCreated={entityManager.GetComponentData<TerrainChunkColliderData>(chunkEntity).IsCreated}");
                }
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Chunk components: PhysicsCollider={entityManager.HasComponent<PhysicsCollider>(chunkEntity)}");
            }
            else
            {
                Debug.LogWarning("[TerrainPhysicsPlayModeTests] Chunk entity missing or destroyed.");
            }

            var systemList = testWorld.Systems;
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Systems in world: {systemList.Count}");
            foreach (var system in systemList)
            {
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] System: {system.GetType().Name}");
            }
        }

        private void LogMissingPhysicsWorldDiagnostics()
        {
            var systemList = testWorld.Systems;
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Diagnostics: fixedStepGroup created={fixedStepGroup != null && fixedStepGroup.World == testWorld} enabled={fixedStepGroup?.Enabled ?? false} timestep={fixedStepGroup?.Timestep ?? 0f} deltaTime={testWorld.Time.DeltaTime}");
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Diagnostics: physicsGroup created={physicsGroup != null && physicsGroup.World == testWorld} enabled={physicsGroup?.Enabled ?? false}");
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Diagnostics: buildPhysicsWorld created={buildPhysicsWorld != null && buildPhysicsWorld.World == testWorld} enabled={buildPhysicsWorld?.Enabled ?? false}");
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Diagnostics: stepPhysicsWorld created={stepPhysicsWorld != null && stepPhysicsWorld.World == testWorld} enabled={stepPhysicsWorld?.Enabled ?? false}");
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Diagnostics: exportPhysicsWorld created={exportPhysicsWorld != null && exportPhysicsWorld.World == testWorld} enabled={exportPhysicsWorld?.Enabled ?? false}");
            LogUpdateListDiagnostics();
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] PhysicsWorldSingleton missing after tick. Systems count: {systemList.Count}");
            foreach (var system in systemList)
            {
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] System: {system.GetType().Name}");
            }
        }

        private static void TrySortSystems(ComponentSystemGroup group)
        {
            if (group == null)
            {
                return;
            }

            var sortMethod = group.GetType().GetMethod("SortSystems", System.Type.EmptyTypes);
            sortMethod?.Invoke(group, null);
        }

        private void LogUpdateListDiagnostics()
        {
            var fixedStepContainsPhysics = TryContainsSystem(fixedStepGroup, physicsGroup);
            var physicsContainsBuild = TryContainsSystem(physicsGroup, buildPhysicsWorld);
            var physicsContainsStep = TryContainsSystem(physicsGroup, stepPhysicsWorld);
            var physicsContainsExport = TryContainsSystem(physicsGroup, exportPhysicsWorld);

            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Diagnostics: fixedStepGroup contains PhysicsSystemGroup={fixedStepContainsPhysics}");
            Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Diagnostics: physicsGroup contains Build/Step/Export={physicsContainsBuild}/{physicsContainsStep}/{physicsContainsExport}");
        }

        private static string TryContainsSystem(ComponentSystemGroup group, ComponentSystemBase system)
        {
            if (group == null || system == null)
            {
                return "unknown";
            }

            var getUpdateListMethod = group.GetType().GetMethod("GetUpdateList", System.Type.EmptyTypes);
            if (getUpdateListMethod == null)
            {
                return "unavailable";
            }

            try
            {
                var updateList = getUpdateListMethod.Invoke(group, null);
                if (updateList == null)
                {
                    return "unknown";
                }

                var listType = updateList.GetType();
                var containsMethod = listType.GetMethod("Contains");
                if (containsMethod == null)
                {
                    return "unknown";
                }

                var contains = containsMethod.Invoke(updateList, new object[] { system });
                return contains is bool value ? value.ToString() : "unknown";
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[TerrainPhysicsPlayModeTests] Diagnostics: unable to inspect update list ({exception.GetType().Name})");
                return "error";
            }
        }
    }
}
