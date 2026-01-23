using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    public struct TerrainChunkDensity : IComponentData
    {
        public BlobAssetReference<TerrainChunkDensityBlob> Data;

        public bool IsCreated => Data.IsCreated;
        public int Length => Data.IsCreated ? Data.Value.Values.Length : 0;

        public static TerrainChunkDensity FromBlob(BlobAssetReference<TerrainChunkDensityBlob> data)
        {
            return new TerrainChunkDensity { Data = data };
        }

        public void Dispose()
        {
            if (Data.IsCreated)
            {
                Data.Dispose();
            }
        }
    }

    /// <summary>
    /// Blob asset containing density values and metadata for world-space mapping.
    /// </summary>
    public struct TerrainChunkDensityBlob
    {
        /// <summary>
        /// Flat array of density values in ZYX order.
        /// Index = z * (Resolution.x * Resolution.y) + y * Resolution.x + x
        /// </summary>
        public BlobArray<float> Values;

        /// <summary>
        /// Grid resolution (including boundary overlap).
        /// </summary>
        public int3 Resolution;

        /// <summary>
        /// World-space origin of the chunk (corner position).
        /// </summary>
        public float3 WorldOrigin;

        /// <summary>
        /// Voxel size in world units.
        /// </summary>
        public float VoxelSize;

        /// <summary>
        /// Map grid indices to world position.
        /// </summary>
        public float3 GetWorldPosition(int x, int y, int z)
        {
            return WorldOrigin + new float3(x, y, z) * VoxelSize;
        }

        /// <summary>
        /// Get density value at grid index.
        /// </summary>
        public float GetDensity(int x, int y, int z)
        {
            var idx = z * (Resolution.x * Resolution.y) + y * Resolution.x + x;
            return Values[idx];
        }
    }
}
