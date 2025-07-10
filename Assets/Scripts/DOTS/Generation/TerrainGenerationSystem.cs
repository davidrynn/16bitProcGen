using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using DOTS.Terrain;
using TerrainData = DOTS.Terrain.TerrainData;

/// <summary>
/// System responsible for generating terrain using compute shaders
/// Handles the integration between DOTS entities and compute shaders
/// </summary>
public partial class TerrainGenerationSystem : SystemBase
{
    private ComputeShaderManager computeManager;
    private EntityQuery terrainQuery;
    
    protected override void OnCreate()
    {
        Debug.Log("[DOTS] TerrainGenerationSystem: Initializing...");
        
        // Get compute shader manager
        computeManager = ComputeShaderManager.Instance;
        if (computeManager == null)
        {
            Debug.LogError("[DOTS] TerrainGenerationSystem: ComputeShaderManager not found!");
            return;
        }
        
        // Create query for terrain entities that need generation
        terrainQuery = GetEntityQuery(
            ComponentType.ReadWrite<TerrainData>()
        );
        
        RequireForUpdate<TerrainData>();
        
        Debug.Log("[DOTS] TerrainGenerationSystem: Initialization complete");
    }
    
    protected override void OnUpdate()
    {
        // TEMPORARY: Disable TerrainGenerationSystem to let HybridTerrainGenerationSystem handle entities
        bool disableTerrainGenerationSystem = true;
        
        if (disableTerrainGenerationSystem)
        {
            return; // Skip processing - let HybridTerrainGenerationSystem handle it
        }
        
        if (computeManager == null)
        {
            Debug.LogWarning("[DOTS] TerrainGenerationSystem: ComputeManager not available, skipping update");
            return;
        }
        
        // Process terrain entities that need generation
        Entities
            .WithAll<DOTS.Terrain.TerrainData>()
            .ForEach((Entity entity, ref DOTS.Terrain.TerrainData terrainData) =>
            {
                if (terrainData.needsGeneration)
                {
                    GenerateTerrainForEntity(entity, ref terrainData);
                }
            }).WithoutBurst().Run();
    }
    
    /// <summary>
    /// Generates terrain for a specific entity using compute shaders
    /// </summary>
    private void GenerateTerrainForEntity(Entity entity, ref DOTS.Terrain.TerrainData terrainData)
    {
        Debug.Log($"[DOTS] Generating terrain for entity {entity} at {terrainData.chunkPosition}");
        
        try
        {
            // Step 1: Create height data blob
            var heightData = CreateHeightDataBlob(terrainData);
            
            // Step 2: Generate terrain using compute shader
            var generatedHeights = GenerateTerrainWithComputeShader(terrainData);
            
            // Step 3: Update terrain data with generated heights
            UpdateTerrainData(ref terrainData, heightData, generatedHeights);
            
            // Step 4: Mark as generated
            terrainData.needsGeneration = false;
            
            Debug.Log($"[DOTS] Successfully generated terrain for entity {entity}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DOTS] Failed to generate terrain for entity {entity}: {e.Message}");
        }
    }
    
    /// <summary>
    /// Creates a height data blob for the terrain
    /// </summary>
    private BlobAssetReference<TerrainHeightData> CreateHeightDataBlob(DOTS.Terrain.TerrainData terrainData)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<TerrainHeightData>();
        
        // Set size
        root.size = new int2(terrainData.resolution, terrainData.resolution);
        
        // Create height array
        var heightArray = builder.Allocate(ref root.heights, terrainData.resolution * terrainData.resolution);
        
        // Create terrain type array
        var terrainTypeArray = builder.Allocate(ref root.terrainTypes, terrainData.resolution * terrainData.resolution);
        
        // Initialize with default values
        for (int i = 0; i < heightArray.Length; i++)
        {
            heightArray[i] = 0f;
            terrainTypeArray[i] = TerrainType.Grass; // Default terrain type
        }
        
        var blobAsset = builder.CreateBlobAssetReference<TerrainHeightData>(Allocator.Persistent);
        builder.Dispose();
        
        return blobAsset;
    }
    
    /// <summary>
    /// Generates terrain heights using the compute shader
    /// </summary>
    private NativeArray<float> GenerateTerrainWithComputeShader(DOTS.Terrain.TerrainData terrainData)
    {
        // Remove the hardcoded test entirely - use compute shader directly
        var computeShader = computeManager.NoiseShader;
        if (computeShader == null)
        {
            Debug.LogError("[DOTS] TerrainNoise compute shader not found!");
            return new NativeArray<float>(0, Allocator.Temp);
        }
        
        int resolution = terrainData.resolution;
        int totalSize = resolution * resolution;
        
        // Create output buffer - must match RWStructuredBuffer<float> in compute shader
        var heightBuffer = new ComputeBuffer(totalSize, sizeof(float), ComputeBufferType.Structured);

        // Initialize buffer to zero
        var zeroArray = new float[totalSize];
        heightBuffer.SetData(zeroArray);
        
        Debug.Log($"[DOTS] Created buffer with size: {totalSize}, stride: {sizeof(float)}");
        
        var tempArray = new float[totalSize];

        // Debug: Check initial buffer contents
        var initialHeights = new float[totalSize];
        heightBuffer.GetData(initialHeights);
        Debug.Log($"[DOTS] Initial buffer contents - First 5 values: {initialHeights[0]}, {initialHeights[1]}, {initialHeights[2]}, {initialHeights[3]}, {initialHeights[4]}");
        
        try
        {
            // Set compute shader parameters
            var chunkData = new TerrainChunkData
            {
                position = new float3(terrainData.chunkPosition.x * terrainData.worldScale, 0, terrainData.chunkPosition.y * terrainData.worldScale),
                resolution = resolution,
                worldScale = terrainData.worldScale,
                time = (float)SystemAPI.Time.ElapsedTime,
                biomeScale = 1.0f,
                noiseScale = 0.01f,
                heightMultiplier = 1.0f,
                noiseOffset = float2.zero
            };
            
            // Get the correct kernel index
            int kernelIndex = computeManager.NoiseKernel;
            if (kernelIndex == -1)
            {
                Debug.LogError("[DOTS] Noise kernel not found in compute shader!");
                return new NativeArray<float>(0, Allocator.Temp);
            }
            
            Debug.Log($"[DOTS] Found kernel index: {kernelIndex}");
            
            // Set buffers FIRST (order matters in Unity compute shaders)
            computeShader.SetBuffer(kernelIndex, "heights", heightBuffer);
            
            // Debug: Verify buffer binding
            Debug.Log($"[DOTS] Buffer bound to kernel {kernelIndex}: {heightBuffer != null}");
            
            // Check if compute shader is valid
            if (!computeShader.HasKernel("GenerateNoise"))
            {
                Debug.LogError("[DOTS] GenerateNoise kernel not found in compute shader!");
                return new NativeArray<float>(0, Allocator.Temp);
            }
            
            Debug.Log($"[DOTS] GenerateNoise kernel found and valid");
            
            // Set compute shader data
            computeShader.SetVector("chunk_position", new Vector4(chunkData.position.x, chunkData.position.y, chunkData.position.z, 0));
            computeShader.SetInt("chunk_resolution", chunkData.resolution);
            computeShader.SetFloat("chunk_worldScale", chunkData.worldScale);
            computeShader.SetFloat("chunk_time", chunkData.time);
            computeShader.SetFloat("chunk_biomeScale", chunkData.biomeScale);
            computeShader.SetFloat("chunk_noiseScale", chunkData.noiseScale);
            computeShader.SetFloat("chunk_heightMultiplier", chunkData.heightMultiplier);
            computeShader.SetVector("chunk_noiseOffset", new Vector4(chunkData.noiseOffset.x, chunkData.noiseOffset.y, 0, 0));
            
            // Debug: Log the parameters being sent to compute shader
            Debug.Log($"[DOTS] Compute shader params - Position: {chunkData.position}, Resolution: {chunkData.resolution}, " +
                     $"WorldScale: {chunkData.worldScale}, Time: {chunkData.time}, BiomeScale: {chunkData.biomeScale}, " +
                     $"HeightMultiplier: {chunkData.heightMultiplier}");
            
            // Calculate thread groups
            int threadGroupsX = Mathf.CeilToInt(resolution / 8f);
            int threadGroupsY = Mathf.CeilToInt(resolution / 8f);
            
            Debug.Log($"[DOTS] Dispatching compute shader with kernel {kernelIndex}, thread groups: {threadGroupsX}x{threadGroupsY}");
            
            // Dispatch compute shader
            computeShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
            
            Debug.Log($"[DOTS] Compute shader dispatch completed");
            
            // Read back results directly
            heightBuffer.GetData(tempArray);
            
            // Debug: Check if buffer was actually written to
            Debug.Log($"[DOTS] Buffer size: {heightBuffer.count}, Stride: {heightBuffer.stride}");
            
            // Debug: Check if the buffer was actually modified
            bool hasNonZeroValues = false;
            for (int i = 0; i < Mathf.Min(10, tempArray.Length); i++)
            {
                if (tempArray[i] != 0)
                {
                    hasNonZeroValues = true;
                    break;
                }
            }
            Debug.Log($"[DOTS] Buffer has non-zero values: {hasNonZeroValues}"); // Add this line to use the variable
              
            // Cleanup
            heightBuffer.Release();
            
            Debug.Log($"[DOTS] Generated {totalSize} height values for terrain at {terrainData.chunkPosition}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DOTS] Compute shader generation failed: {e.Message}");
            // Remove the tempArray.Dispose() call - float[] doesn't need disposal
            heightBuffer.Release(); // Make sure to release the buffer even on error
            return new NativeArray<float>(0, Allocator.Temp);
        }
        
        return new NativeArray<float>(tempArray, Allocator.Temp);
    }
    
    /// <summary>
    /// Generates hardcoded test heights to bypass compute shader issues
    /// </summary>
    private NativeArray<float> GenerateHardcodedTestHeights(TerrainData terrainData)
    {
        int resolution = terrainData.resolution;
        int totalSize = resolution * resolution;
        
        var heights = new NativeArray<float>(totalSize, Allocator.Temp);
        
        Debug.Log($"[DOTS] Generating {totalSize} hardcoded test heights");
        
        // Generate a simple pattern: height increases from left to right and bottom to top
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;
                
                // Create a simple height pattern
                float height = (x + z) * 2.0f; // Simple increasing pattern
                
                // Add some variation based on chunk position
                height += terrainData.chunkPosition.x * 10.0f + terrainData.chunkPosition.y * 10.0f;
                
                heights[index] = height;
            }
        }
        
        // Debug: Check first few values
        Debug.Log($"[DOTS] Hardcoded test - First 5 height values: {heights[0]}, {heights[1]}, {heights[2]}, {heights[3]}, {heights[4]}");
        Debug.Log($"[DOTS] Hardcoded test - Last 5 height values: {heights[totalSize-5]}, {heights[totalSize-4]}, {heights[totalSize-3]}, {heights[totalSize-2]}, {heights[totalSize-1]}");
        
        return heights;
    }
    
    /// <summary>
    /// Updates terrain data with generated heights
    /// </summary>
    private void UpdateTerrainData(ref DOTS.Terrain.TerrainData terrainData, BlobAssetReference<TerrainHeightData> heightData, NativeArray<float> generatedHeights)
    {
        // Dispose old height data if it exists
        if (terrainData.heightData.IsCreated)
        {
            terrainData.heightData = BlobAssetReference<TerrainHeightData>.Null;
        }
        
        // Create new height data with generated values
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<TerrainHeightData>();
        
        root.size = new int2(terrainData.resolution, terrainData.resolution);
        
        var heightArray = builder.Allocate(ref root.heights, generatedHeights.Length);
        var terrainTypeArray = builder.Allocate(ref root.terrainTypes, generatedHeights.Length);
        
        // Copy generated heights and assign terrain types based on height
        for (int i = 0; i < generatedHeights.Length; i++)
        {
            heightArray[i] = generatedHeights[i];
            terrainTypeArray[i] = GetTerrainTypeFromHeight(generatedHeights[i]);
        }
        
        terrainData.heightData = builder.CreateBlobAssetReference<TerrainHeightData>(Allocator.Persistent);
        builder.Dispose();
        
        // Dispose the generated heights array
        generatedHeights.Dispose();
    }
    
    /// <summary>
    /// Determines terrain type based on height value
    /// </summary>
    private TerrainType GetTerrainTypeFromHeight(float height)
    {
        if (height < 10f) return TerrainType.Water;
        if (height < 30f) return TerrainType.Sand;
        if (height < 60f) return TerrainType.Grass;
        if (height < 100f) return TerrainType.Flora; // Trees/forest
        return TerrainType.Rock; // Mountain/rock
    }
    
    protected override void OnDestroy()
    {
        Debug.Log("[DOTS] TerrainGenerationSystem: Destroyed");
    }
}