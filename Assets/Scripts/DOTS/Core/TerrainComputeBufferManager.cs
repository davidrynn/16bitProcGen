using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages ComputeBuffer instances for terrain data
/// This is separate from DOTS components because ComputeBuffer is not blittable
/// </summary>
public class TerrainComputeBufferManager : MonoBehaviour
{
    private Dictionary<int2, ComputeBuffer> heightBuffers = new Dictionary<int2, ComputeBuffer>();
    private Dictionary<int2, ComputeBuffer> biomeBuffers = new Dictionary<int2, ComputeBuffer>();
    private Dictionary<int2, ComputeBuffer> terrainTypeBuffers = new Dictionary<int2, ComputeBuffer>();
    
    /// <summary>
    /// Gets or creates a height buffer for a terrain chunk
    /// </summary>
    /// <param name="chunkPosition">Position of the terrain chunk</param>
    /// <param name="resolution">Resolution of the terrain grid</param>
    /// <returns>ComputeBuffer for height data</returns>
    public ComputeBuffer GetHeightBuffer(int2 chunkPosition, int resolution)
    {
        if (heightBuffers.TryGetValue(chunkPosition, out ComputeBuffer buffer))
        {
            return buffer;
        }
        
        // Create new buffer
        int bufferSize = resolution * resolution;
        buffer = new ComputeBuffer(bufferSize, sizeof(float));
        heightBuffers[chunkPosition] = buffer;
        
        return buffer;
    }
    
    /// <summary>
    /// Gets or creates a biome buffer for a terrain chunk
    /// </summary>
    /// <param name="chunkPosition">Position of the terrain chunk</param>
    /// <param name="resolution">Resolution of the terrain grid</param>
    /// <returns>ComputeBuffer for biome data</returns>
    public ComputeBuffer GetBiomeBuffer(int2 chunkPosition, int resolution)
    {
        if (biomeBuffers.TryGetValue(chunkPosition, out ComputeBuffer buffer))
        {
            return buffer;
        }
        
        // Create new buffer
        int bufferSize = resolution * resolution;
        buffer = new ComputeBuffer(bufferSize, sizeof(float));
        biomeBuffers[chunkPosition] = buffer;
        
        return buffer;
    }
    
    /// <summary>
    /// Gets or creates a terrain type buffer for a terrain chunk
    /// </summary>
    /// <param name="chunkPosition">Position of the terrain chunk</param>
    /// <param name="resolution">Resolution of the terrain grid</param>
    /// <returns>ComputeBuffer for terrain type data (int)</returns>
    public ComputeBuffer GetTerrainTypeBuffer(int2 chunkPosition, int resolution)
    {
        if (terrainTypeBuffers.TryGetValue(chunkPosition, out ComputeBuffer buffer))
        {
            return buffer;
        }
        
        // Create new buffer
        int bufferSize = resolution * resolution;
        buffer = new ComputeBuffer(bufferSize, sizeof(int));
        terrainTypeBuffers[chunkPosition] = buffer;
        
        return buffer;
    }
    
    /// <summary>
    /// Releases a height buffer for a terrain chunk
    /// </summary>
    /// <param name="chunkPosition">Position of the terrain chunk</param>
    public void ReleaseHeightBuffer(int2 chunkPosition)
    {
        if (heightBuffers.TryGetValue(chunkPosition, out ComputeBuffer buffer))
        {
            buffer.Release();
            heightBuffers.Remove(chunkPosition);
        }
    }
    
    /// <summary>
    /// Releases a biome buffer for a terrain chunk
    /// </summary>
    /// <param name="chunkPosition">Position of the terrain chunk</param>
    public void ReleaseBiomeBuffer(int2 chunkPosition)
    {
        if (biomeBuffers.TryGetValue(chunkPosition, out ComputeBuffer buffer))
        {
            buffer.Release();
            biomeBuffers.Remove(chunkPosition);
        }
    }
    
    /// <summary>
    /// Releases a terrain type buffer for a terrain chunk
    /// </summary>
    /// <param name="chunkPosition">Position of the terrain chunk</param>
    public void ReleaseTerrainTypeBuffer(int2 chunkPosition)
    {
        if (terrainTypeBuffers.TryGetValue(chunkPosition, out ComputeBuffer buffer))
        {
            buffer.Release();
            terrainTypeBuffers.Remove(chunkPosition);
        }
    }
    
    /// <summary>
    /// Releases all buffers for a terrain chunk
    /// </summary>
    /// <param name="chunkPosition">Position of the terrain chunk</param>
    public void ReleaseChunkBuffers(int2 chunkPosition)
    {
        ReleaseHeightBuffer(chunkPosition);
        ReleaseBiomeBuffer(chunkPosition);
        ReleaseTerrainTypeBuffer(chunkPosition);
    }
    
    /// <summary>
    /// Releases all buffers
    /// </summary>
    public void ReleaseAllBuffers()
    {
        foreach (var buffer in heightBuffers.Values)
        {
            buffer.Release();
        }
        heightBuffers.Clear();
        
        foreach (var buffer in biomeBuffers.Values)
        {
            buffer.Release();
        }
        biomeBuffers.Clear();
        
        foreach (var buffer in terrainTypeBuffers.Values)
        {
            buffer.Release();
        }
        terrainTypeBuffers.Clear();
    }
    
    private void OnDestroy()
    {
        ReleaseAllBuffers();
    }
} 