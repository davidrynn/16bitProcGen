using Unity.Collections;
using Unity.Entities;

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

    public struct TerrainChunkDensityBlob
    {
        public BlobArray<float> Values;
    }
}
