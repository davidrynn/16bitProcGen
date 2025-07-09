using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Blob asset structure for storing terrain height and type data efficiently
/// </summary>
public struct TerrainHeightData
{
    public BlobArray<float> heights;        // Height values for each terrain point
    public BlobArray<TerrainType> terrainTypes; // Terrain type for each point
    public int2 size;                       // Size of the terrain grid (width, height)
} 