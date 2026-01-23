using DOTS.Terrain;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using ChunkComponent = DOTS.Terrain.TerrainChunk;

namespace DOTS.Terrain.Bootstrap
{
    /// <summary>
    /// Simple scene bootstrap that spawns a grid of DOTS terrain chunk entities plus the required SDF settings.
    /// </summary>
    public class TerrainBootstrapAuthoring : MonoBehaviour
    {
        [Header("Chunk Layout")]
        [SerializeField] private int2 gridSize = new int2(3, 3);
        [SerializeField] private float chunkSpacing = 16f;

        [Header("Chunk Resolution")]
        [SerializeField] private int chunkResolution = 16;
        [SerializeField] private float voxelSize = 1f;

        [Header("SDF Field Settings")]
        [SerializeField] private float baseHeight = 0f;
        [SerializeField] private float amplitude = 4f;
        [SerializeField] private float frequency = 0.1f;
        [SerializeField] private float noiseValue = 0f;

        private void Start()
        {
            RunBootstrap();
        }

        public bool RunBootstrap(World worldOverride = null)
        {
            // TODO: decouple from World.DefaultGameObjectInjectionWorld once bootstrap is managed outside MonoBehaviours.
            var world = worldOverride ?? World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogWarning("No DOTS world found for TerrainBootstrapAuthoring.");
                return false;
            }

            EnsureCameraAndLight();

            var entityManager = world.EntityManager;
            EnsureFieldSettings(entityManager);
            SpawnChunkGrid(entityManager); // Seeds a basic grid so Phase 2+ systems have data to process.
            return true;
        }

        private void EnsureFieldSettings(EntityManager entityManager)
        {
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFTerrainFieldSettings>());
            if (query.CalculateEntityCount() == 0)
            {
                var entity = entityManager.CreateEntity(typeof(SDFTerrainFieldSettings));
                entityManager.SetComponentData(entity, new SDFTerrainFieldSettings
                {
                    BaseHeight = baseHeight,
                    Amplitude = amplitude,
                    Frequency = frequency,
                    NoiseValue = noiseValue
                });
            }
            query.Dispose();
        }

        private void SpawnChunkGrid(EntityManager entityManager)
        {
            var safeGrid = new int2(math.max(1, gridSize.x), math.max(1, gridSize.y));
            var safeResolution = math.max(1, chunkResolution);
            var resolution = new int3(safeResolution, safeResolution, safeResolution);

            // Chunks share border samples; the world-space span of a chunk is (resolution-1) * voxelSize.
            // If chunkSpacing is set inconsistently (e.g. 16 when resolution=16 and voxelSize=1), it will create visible gaps.
            var expectedStride = math.max(0, safeResolution - 1) * voxelSize;
            var chunkStride = expectedStride;
            if (chunkSpacing > 0f && math.abs(chunkSpacing - expectedStride) <= 1e-4f)
            {
                chunkStride = chunkSpacing;
            }
            else if (chunkSpacing > 0f && math.abs(chunkSpacing - expectedStride) > 1e-4f)
            {
                Debug.LogWarning($"[TerrainBootstrapAuthoring] chunkSpacing ({chunkSpacing}) does not match expected chunk stride ({expectedStride}) for resolution={safeResolution}, voxelSize={voxelSize}. Using {expectedStride} to avoid seams.");
            }

            // Center the chunk volume vertically around BaseHeight so the ground isosurface is
            // inside the sampled density range even when BaseHeight is near 0.
            var chunkVerticalSpan = math.max(0, safeResolution - 1) * voxelSize;
            var originY = baseHeight - (chunkVerticalSpan * 0.5f);

            for (int x = 0; x < safeGrid.x; x++)
            {
                for (int z = 0; z < safeGrid.y; z++)
                {
                    var entity = entityManager.CreateEntity(
                        typeof(ChunkComponent),
                        typeof(TerrainChunkGridInfo),
                        typeof(TerrainChunkBounds),
                        typeof(TerrainChunkNeedsDensityRebuild));

                    entityManager.SetComponentData(entity, new ChunkComponent
                    {
                        ChunkCoord = new int3(x, 0, z)
                    });

                    entityManager.SetComponentData(entity, TerrainChunkGridInfo.Create(resolution, voxelSize));

                    var origin = new float3(x * chunkStride, originY, z * chunkStride);
                    entityManager.SetComponentData(entity, new TerrainChunkBounds
                    {
                        WorldOrigin = origin
                    });

                    entityManager.AddComponentData(entity, LocalTransform.FromPosition(origin));
                }
            }
        }

        private void EnsureCameraAndLight()
        {
            if (Camera.main == null)
            {
                var cameraGO = new GameObject("Terrain Bootstrap Camera");
                var camera = cameraGO.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 30f, -30f);
                camera.transform.rotation = Quaternion.Euler(25f, 45f, 0f);
            }

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightGO = new GameObject("Terrain Bootstrap Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }
    }
}
