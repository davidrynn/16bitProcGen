using DOTS.Terrain.LOD;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainLodOrderingTests
    {
        [Test]
        public void LODApply_RunsBefore_DensitySampling_InSimulationGroup()
        {
            using var world = new World("TerrainLodOrderingTests");
            var entityManager = world.EntityManager;

            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var lodApplySystem = world.CreateSystem<TerrainChunkLodApplySystem>();
            var densitySamplingSystem = world.CreateSystem<TerrainChunkDensitySamplingSystem>();

            simGroup.AddSystemToUpdateList(lodApplySystem);
            simGroup.AddSystemToUpdateList(densitySamplingSystem);

            var lodSettingsEntity = entityManager.CreateEntity(typeof(TerrainLodSettings));
            var lodSettings = TerrainLodSettings.Default;
            lodSettings.Lod0Resolution = new int3(16, 16, 16);
            lodSettings.Lod0VoxelSize = 1f;
            lodSettings.Lod1Resolution = new int3(9, 9, 9);
            lodSettings.Lod1VoxelSize = 2f;
            entityManager.SetComponentData(lodSettingsEntity, lodSettings);

            var fieldSettingsEntity = entityManager.CreateEntity(typeof(SDFTerrainFieldSettings));
            entityManager.SetComponentData(fieldSettingsEntity, new SDFTerrainFieldSettings
            {
                BaseHeight = 0f,
                Amplitude = 2f,
                Frequency = 0.1f,
                NoiseValue = 0f
            });

            var chunk = entityManager.CreateEntity(
                typeof(TerrainChunk),
                typeof(TerrainChunkGridInfo),
                typeof(TerrainChunkBounds),
                typeof(TerrainChunkLodState),
                typeof(TerrainChunkNeedsDensityRebuild));

            entityManager.SetComponentData(chunk, new TerrainChunk { ChunkCoord = new int3(0, 0, 0) });
            entityManager.SetComponentData(chunk, TerrainChunkGridInfo.Create(new int3(16, 16, 16), 1f));
            entityManager.SetComponentData(chunk, new TerrainChunkBounds { WorldOrigin = float3.zero });
            entityManager.SetComponentData(chunk, new TerrainChunkLodState
            {
                CurrentLod = 0,
                TargetLod = 1,
                LastSwitchFrame = 0
            });

            simGroup.Update();

            var lodState = entityManager.GetComponentData<TerrainChunkLodState>(chunk);
            Assert.AreEqual(1, lodState.CurrentLod, "LOD apply should promote CurrentLod to TargetLod before density sampling.");

            Assert.IsFalse(
                entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunk),
                "Density rebuild tag should be consumed in the same update if LOD apply runs before density sampling.");

            Assert.IsTrue(entityManager.HasComponent<TerrainChunkDensityGridInfo>(chunk));
            var densityGrid = entityManager.GetComponentData<TerrainChunkDensityGridInfo>(chunk);
            Assert.AreEqual(new int3(10, 10, 10), densityGrid.Resolution,
                "Density sampling should use post-LOD-apply grid (LOD1 9^3 => density grid 10^3). ");

            var grid = entityManager.GetComponentData<TerrainChunkGridInfo>(chunk);
            Assert.AreEqual(new int3(9, 9, 9), grid.Resolution);
            Assert.AreEqual(2f, grid.VoxelSize, 1e-5f);

            if (entityManager.HasComponent<TerrainChunkDensity>(chunk))
            {
                var density = entityManager.GetComponentData<TerrainChunkDensity>(chunk);
                density.Dispose();
                entityManager.RemoveComponent<TerrainChunkDensity>(chunk);
            }
        }
    }
}
