using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
namespace DOTS.Terrain.Generation
{
    /// <summary>
    /// Hybrid system that coordinates between DOTS entities and Compute Shaders
    /// Handles terrain generation using GPU acceleration with DOTS data management
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainSystem))]
    public partial class HybridTerrainGenerationSystem : SystemBase
    {
        // Compute Shader Manager reference
        private ComputeShaderManager computeManager;
        
        // Performance monitoring
        private float lastGenerationTime;
        private int chunksProcessedThisFrame;
        private int totalChunksProcessed;
        
        // Buffer management
        private bool buffersInitialized;
        private ComputeBuffer heightBuffer;
        private ComputeBuffer biomeBuffer;
        private ComputeBuffer structureBuffer;
        
        // Settings reference
        private TerrainGenerationSettings settings;
        
        protected override void OnCreate()
        {
            // Load settings
            settings = TerrainGenerationSettings.Default;
            if (settings != null)
            {
                settings.ValidateSettings();
            }
            else
            {
                Debug.LogWarning("HybridTerrainGenerationSystem: No settings found, using defaults");
            }
            
            // Initialize performance counters
            lastGenerationTime = 0f;
            chunksProcessedThisFrame = 0;
            totalChunksProcessed = 0;
        }
        
        protected override void OnUpdate()
        {
            // Try to get ComputeShaderManager singleton if we haven't found it yet
            if (computeManager == null)
            {
                try
                {
                    computeManager = ComputeShaderManager.Instance;
                }
                catch (System.Exception e)
                {
                    DebugWarning($"Failed to get ComputeShaderManager: {e.Message}");
                }
            }
            
            if (computeManager == null)
            {
                DebugWarning("Skipping update - no ComputeShaderManager");
                return;
            }
            
            // TEMPORARY: Force regeneration with spacebar for testing
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ResetAllEntitiesToNeedGeneration();
            }
            
            // Reset frame counters
            chunksProcessedThisFrame = 0;
            
            // Process terrain generation
            ProcessTerrainGeneration();
            
            // Update performance metrics
            UpdatePerformanceMetrics();
        }
        
        /// <summary>
        /// TEMPORARY: Resets all terrain entities to need generation for testing
        /// </summary>
        private void ResetAllEntitiesToNeedGeneration()
        {
            int resetCount = 0;
            var query = GetEntityQuery(ComponentType.ReadOnly<TerrainData>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var terrain = EntityManager.GetComponentData<TerrainData>(entity);
                terrain.needsGeneration = true;
                EntityManager.SetComponentData(entity, terrain);
                resetCount++;
            }
                
            // Intentionally silent unless troubleshooting
        }
        
        /// <summary>
        /// Main processing loop for terrain generation
        /// </summary>
        private void ProcessTerrainGeneration()
        {
            float startTime = UnityEngine.Time.realtimeSinceStartup;
            
            // Count entities that need generation
            int entitiesNeedingGeneration = 0;
            int totalEntities = 0;

            var query = GetEntityQuery(ComponentType.ReadWrite<TerrainData>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            
            totalEntities = entities.Length;

            foreach (var entity in entities)
            {
                var terrain = EntityManager.GetComponentData<TerrainData>(entity);

                if (terrain.needsGeneration)
                {
                    entitiesNeedingGeneration++;
                }

                bool entityModified = false;

                if (terrain.needsGeneration && chunksProcessedThisFrame < settings?.maxChunksPerFrame)
                {
                    if (GenerateNoiseWithComputeShader(ref terrain))
                    {
                        ProcessNoiseResults(ref terrain);
                        ApplyGameLogic(ref terrain);

                        terrain.needsGeneration = false;
                        chunksProcessedThisFrame++;
                        totalChunksProcessed++;
                        entityModified = true;
                    }
                    else
                    {
                        DebugWarning($"Failed to generate terrain for entity {entity.Index}");
                    }
                }
                else if (terrain.needsMeshUpdate)
                {
                    if (terrain.heightData.IsCreated)
                    {
                        ref var heightData = ref terrain.heightData.Value;
                        var heights = new float[heightData.heights.Length];
                        for (int i = 0; i < heights.Length; i++)
                        {
                            heights[i] = heightData.heights[i];
                        }
                        GenerateTerrainMesh(terrain, heights, terrain.resolution);
                    }
                    terrain.needsMeshUpdate = false;
                    entityModified = true;
                }

                if (entityModified)
                {
                    EntityManager.SetComponentData(entity, terrain);
                }
            }
            
            lastGenerationTime = UnityEngine.Time.realtimeSinceStartup - startTime;
        }
        
        /// <summary>
        /// Generates terrain noise using Compute Shaders
        /// </summary>
        /// <param name="terrain">Reference to terrain data</param>
        /// <returns>True if generation was successful</returns>
        private bool GenerateNoiseWithComputeShader(ref TerrainData terrain)
        {
            // Use compute shader for noise generation
            if (computeManager?.NoiseShader == null)
            {
                DebugError("Noise shader not available");
                return false;
            }

            try
            {
                // Initialize buffers if needed
                InitializeBuffers();

                int resolution = terrain.resolution;
                int totalSize = resolution * resolution;

                // Create temporary buffer for this generation
                var heightBuffer = new ComputeBuffer(totalSize, sizeof(float));

                // Prepare chunk data
                var chunkData = new TerrainChunkData
                {
                    position = new float3(terrain.chunkPosition.x * terrain.worldScale, 0, terrain.chunkPosition.y * terrain.worldScale),
                    resolution = resolution,
                    worldScale = terrain.worldScale,
                    time = (float)SystemAPI.Time.ElapsedTime,
                    biomeScale = settings?.biomeScale ?? 1.0f,
                    noiseScale = settings?.noiseScale ?? 0.02f,
                    heightMultiplier = settings?.heightMultiplier ?? 100.0f,
                    noiseOffset = settings?.noiseOffset ?? new float2(123.456f, 789.012f)
                };

                // Get the correct kernel ID
                int kernelId = computeManager.NoiseKernel;
                if (kernelId == -1)
                {
                    DebugError("Failed to get noise kernel ID");
                    return false;
                }
                
                // Set compute shader parameters
                computeManager.NoiseShader.SetBuffer(kernelId, "heights", heightBuffer);
                
                // Set individual parameters
                computeManager.NoiseShader.SetVector("chunk_position", new Vector4(chunkData.position.x, chunkData.position.y, chunkData.position.z, 0));
                computeManager.NoiseShader.SetInt("chunk_resolution", chunkData.resolution);
                computeManager.NoiseShader.SetFloat("chunk_worldScale", chunkData.worldScale);
                computeManager.NoiseShader.SetFloat("chunk_time", chunkData.time);
                computeManager.NoiseShader.SetFloat("chunk_biomeScale", chunkData.biomeScale);
                computeManager.NoiseShader.SetFloat("chunk_noiseScale", chunkData.noiseScale);
                computeManager.NoiseShader.SetFloat("chunk_heightMultiplier", chunkData.heightMultiplier);
                computeManager.NoiseShader.SetVector("chunk_noiseOffset", new Vector4(chunkData.noiseOffset.x, chunkData.noiseOffset.y, 0, 0));

                // Calculate thread groups (8x8 threads per group)
                int threadGroupsX = Mathf.CeilToInt(resolution / 8f);
                int threadGroupsY = Mathf.CeilToInt(resolution / 8f);
                
                // Dispatch compute shader
                computeManager.NoiseShader.Dispatch(kernelId, threadGroupsX, threadGroupsY, 1);

                // Read back results
                var heights = new float[totalSize];
                heightBuffer.GetData(heights);

                // DEBUG: Check height values
                // Create height data blob
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<TerrainHeightData>();

                root.size = new int2(resolution, resolution);
                var heightArray = builder.Allocate(ref root.heights, totalSize);
                var terrainTypeArray = builder.Allocate(ref root.terrainTypes, totalSize);

                // Copy heights and assign terrain types
                for (int i = 0; i < totalSize; i++)
                {
                    heightArray[i] = heights[i];
                    terrainTypeArray[i] = settings?.GetTerrainTypeFromHeight(heights[i]) ?? TerrainType.Grass;
                }

                // Update terrain data
                if (terrain.heightData.IsCreated)
                {
                    terrain.heightData = BlobAssetReference<TerrainHeightData>.Null;
                }
                terrain.heightData = builder.CreateBlobAssetReference<TerrainHeightData>(Allocator.Persistent);

                // Cleanup
                builder.Dispose();
                heightBuffer.Release();

                GenerateTerrainMesh(terrain, heights, resolution);
                return true;
            }
            catch (System.Exception e)
            {
                DebugError($"Compute shader generation failed: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Generates hardcoded test heights to bypass compute shader issues
        /// </summary>
        private bool GenerateHardcodedTestHeights(ref TerrainData terrain)
        {
            int resolution = terrain.resolution;
            int totalSize = resolution * resolution;
            
            try
            {
                // Create height data blob
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<TerrainHeightData>();

                root.size = new int2(resolution, resolution);
                var heightArray = builder.Allocate(ref root.heights, totalSize);
                var terrainTypeArray = builder.Allocate(ref root.terrainTypes, totalSize);

                // Generate a simple pattern: height increases from left to right and bottom to top
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        int index = z * resolution + x;
                        
                        // Create a simple height pattern
                        float height = (x + z) * 2.0f; // Simple increasing pattern
                        
                        // Add some variation based on chunk position
                        height += terrain.chunkPosition.x * 10.0f + terrain.chunkPosition.y * 10.0f;
                        
                        heightArray[index] = height;
                        terrainTypeArray[index] = GetTerrainTypeFromHeight(height);
                    }
                }

                // Update terrain data
                if (terrain.heightData.IsCreated)
                {
                    terrain.heightData = BlobAssetReference<TerrainHeightData>.Null;
                }
                terrain.heightData = builder.CreateBlobAssetReference<TerrainHeightData>(Allocator.Persistent);

                // Cleanup
                builder.Dispose();

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"HybridTerrainGenerationSystem: Hardcoded test generation failed: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Determines terrain type based on height value (legacy method - now uses settings)
        /// </summary>
        private TerrainType GetTerrainTypeFromHeight(float height)
        {
            return settings?.GetTerrainTypeFromHeight(height) ?? TerrainType.Grass;
        }
        
        /// <summary>
        /// Processes noise results using DOTS Jobs
        /// </summary>
        /// <param name="terrain">Reference to terrain data</param>
        private void ProcessNoiseResults(ref TerrainData terrain)
        {
            // TODO: Implement in Step 4
            // Debug.Log("HybridTerrainGenerationSystem: ProcessNoiseResults - Not implemented yet");
        }
        
        /// <summary>
        /// Applies game-specific logic to the terrain
        /// </summary>
        /// <param name="terrain">Reference to terrain data</param>
        private void ApplyGameLogic(ref TerrainData terrain)
        {
            // Calculate average height from the generated terrain data
            if (terrain.heightData.IsCreated)
            {
                ref var heightData = ref terrain.heightData.Value;
                float totalHeight = 0f;
                
                for (int i = 0; i < heightData.heights.Length; i++)
                {
                    totalHeight += heightData.heights[i];
                }
                
                terrain.averageHeight = totalHeight / heightData.heights.Length;
                
                
            }
        }
        
        /// <summary>
        /// Updates performance monitoring metrics
        /// </summary>
        private void UpdatePerformanceMetrics()
        {
            // Only log performance if debug is enabled
        }
        
        /// <summary>
        /// Initializes ComputeBuffers for GPU computation
        /// </summary>
        private void InitializeBuffers()
        {
            if (buffersInitialized) return;
            
            try
            {
                // Create buffers for GPU computation
                int bufferSize = settings?.defaultBufferSize ?? 1024 * 1024;
                heightBuffer = new ComputeBuffer(bufferSize, sizeof(float));
                biomeBuffer = new ComputeBuffer(bufferSize, sizeof(float));
                structureBuffer = new ComputeBuffer(bufferSize, sizeof(float));
                
                buffersInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"HybridTerrainGenerationSystem: Failed to initialize buffers: {e.Message}");
                buffersInitialized = false;
            }
        }
        
        /// <summary>
        /// Cleans up ComputeBuffers
        /// </summary>
        private void CleanupBuffers()
        {
            if (heightBuffer != null)
            {
                heightBuffer.Release();
                heightBuffer = null;
            }
            
            if (biomeBuffer != null)
            {
                biomeBuffer.Release();
                biomeBuffer = null;
            }
            
            if (structureBuffer != null)
            {
                structureBuffer.Release();
                structureBuffer = null;
            }
            
            buffersInitialized = false;
            // Debug.Log("HybridTerrainGenerationSystem: Buffers cleaned up");
        }
        
        protected override void OnDestroy()
        {
            CleanupBuffers();
            // Debug.Log("HybridTerrainGenerationSystem: Destroyed");
        }
        
        /// <summary>
        /// Gets performance metrics for monitoring
        /// </summary>
        /// <returns>Performance metrics tuple</returns>
        public (float lastGenerationTime, int chunksThisFrame, int totalChunks) GetPerformanceMetrics()
        {
            return (lastGenerationTime, chunksProcessedThisFrame, totalChunksProcessed);
        }
        
        /// <summary>
        /// Forces generation of a specific terrain entity
        /// </summary>
        /// <param name="entity">The terrain entity to generate</param>
        public void ForceGenerateTerrain(Entity entity)
        {
            if (!SystemAPI.Exists(entity)) return;
            
            var terrainData = SystemAPI.GetComponent<TerrainData>(entity);
            terrainData.needsGeneration = true;
            SystemAPI.SetComponent(entity, terrainData);
            
            // Intentionally silent unless troubleshooting
        }

        private void GenerateTerrainMesh(TerrainData terrain, float[] heights, int resolution)
        {
            // Create a new GameObject for the mesh
            var meshGO = new GameObject($"DOTS_TerrainMesh_{terrain.chunkPosition.x}_{terrain.chunkPosition.y}");
            meshGO.transform.position = new Vector3(terrain.chunkPosition.x * terrain.worldScale, 0, terrain.chunkPosition.y * terrain.worldScale);

            var meshFilter = meshGO.AddComponent<MeshFilter>();
            var meshRenderer = meshGO.AddComponent<MeshRenderer>();
            var mesh = new Mesh();
            mesh.name = $"TerrainMesh_{terrain.chunkPosition.x}_{terrain.chunkPosition.y}";

            // Find a suitable shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { color = Color.green };
            meshRenderer.material = mat;

            // Generate vertices and triangles
            Vector3[] vertices = new Vector3[resolution * resolution];
            int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            Vector2[] uvs = new Vector2[vertices.Length];

            // Find min/max for normalization
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < heights.Length; i++)
            {
                if (heights[i] < minH) minH = heights[i];
                if (heights[i] > maxH) maxH = heights[i];
            }
            // Calculate vertex spacing to match the compute shader coordinate system
            float vertexStep = terrain.worldScale / (float)(resolution - 1);

            // Generate vertices with the same coordinate system as the compute shader
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;
                    
                    // Use raw height directly - no normalization or mesh height scaling
                    float y = heights[index];
                    
                    // Use the same vertex spacing as the compute shader
                    float vertexX = x * vertexStep;
                    float vertexZ = z * vertexStep;
                    vertices[index] = new Vector3(vertexX, y, vertexZ);
                    uvs[index] = new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1));
                }
            }

            int tri = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int i = z * resolution + x;
                    triangles[tri++] = i;
                    triangles[tri++] = i + resolution;
                    triangles[tri++] = i + 1;
                    triangles[tri++] = i + 1;
                    triangles[tri++] = i + resolution;
                    triangles[tri++] = i + resolution + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
        }

        /// <summary>
        /// Debug log wrapper that respects the debug toggle
        /// </summary>
        private void DebugError(string message)
        {
            Debug.LogError($"[HybridSystem] {message}");
        }
        
        private void DebugWarning(string message)
        {
            Debug.LogWarning($"[HybridSystem] {message}");
        }
    }
} 