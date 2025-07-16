using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using System.Linq; // ADD THIS LINE

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
            Debug.Log("HybridTerrainGenerationSystem: Initializing...");
            
            // Load settings
            settings = TerrainGenerationSettings.Default;
            if (settings != null)
            {
                settings.ValidateSettings();
                Debug.Log("HybridTerrainGenerationSystem: Settings loaded successfully");
            }
            else
            {
                Debug.LogWarning("HybridTerrainGenerationSystem: No settings found, using defaults");
            }
            
            // Initialize performance counters
            lastGenerationTime = 0f;
            chunksProcessedThisFrame = 0;
            totalChunksProcessed = 0;
            
            Debug.Log("HybridTerrainGenerationSystem: Initialization complete");
        }
        
        protected override void OnUpdate()
        {
            // Try to get ComputeShaderManager singleton if we haven't found it yet
            if (computeManager == null)
            {
                try
                {
                    computeManager = ComputeShaderManager.Instance;
                    DebugLog("Found ComputeShaderManager singleton");
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
                DebugLog("Spacebar pressed - forcing regeneration of all entities");
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
            
            Entities
                .WithAll<TerrainData>()
                .ForEach((Entity entity, ref TerrainData terrain) =>
                {
                    terrain.needsGeneration = true;
                    resetCount++;
                }).WithoutBurst().Run();
                
            if (resetCount > 0)
            {
                Debug.Log($"HybridTerrainGenerationSystem: Reset {resetCount} entities to need generation");
            }
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
            
            Entities
                .WithAll<TerrainData>()
                .ForEach((Entity entity, ref TerrainData terrain) =>
                {
                    totalEntities++;
                    if (terrain.needsGeneration)
                    {
                        entitiesNeedingGeneration++;
                    }
                }).WithoutBurst().Run();
            
            if (totalEntities > 0 && settings?.enableDebugLogs == true)
            {
                DebugLog($"Found {totalEntities} terrain entities, {entitiesNeedingGeneration} need generation", true);
            }
            
            Entities
                .WithAll<TerrainData>()
                .ForEach((Entity entity, ref TerrainData terrain) =>
                {
                    // Check if this terrain needs generation
                    if (terrain.needsGeneration && chunksProcessedThisFrame < settings?.maxChunksPerFrame)
                    {
                        DebugLog($"Processing entity {entity.Index} - resolution: {terrain.resolution}, position: {terrain.chunkPosition}", true);
                        
                        // Step 1: Generate noise with Compute Shader
                        if (GenerateNoiseWithComputeShader(ref terrain))
                        {
                            // Step 2: Process results with DOTS Jobs
                            ProcessNoiseResults(ref terrain);
                            
                            // Step 3: Apply game logic
                            ApplyGameLogic(ref terrain);
                            
                            // Mark as processed
                            terrain.needsGeneration = false;
                            chunksProcessedThisFrame++;
                            totalChunksProcessed++;
                            
                            DebugLog($"Generated terrain for entity {entity.Index}");
                        }
                        else
                        {
                            DebugWarning($"Failed to generate terrain for entity {entity.Index}");
                        }
                    }
                }).WithoutBurst().Run();
            
            lastGenerationTime = UnityEngine.Time.realtimeSinceStartup - startTime;
        }
        
        /// <summary>
        /// Generates terrain noise using Compute Shaders
        /// </summary>
        /// <param name="terrain">Reference to terrain data</param>
        /// <returns>True if generation was successful</returns>
        private bool GenerateNoiseWithComputeShader(ref TerrainData terrain)
        {
            DebugLog($"GenerateNoiseWithComputeShader called for terrain at {terrain.chunkPosition}", true);
            
            // Use compute shader for noise generation
            DebugLog("Using compute shader for noise generation", true);
            
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

                DebugLog($"Dispatching compute shader with kernel {kernelId}, thread groups: {threadGroupsX}x{threadGroupsY}", true);
                
                // Dispatch compute shader
                computeManager.NoiseShader.Dispatch(kernelId, threadGroupsX, threadGroupsY, 1);

                // Read back results
                var heights = new float[totalSize];
                heightBuffer.GetData(heights);

                // DEBUG: Check height values
                if (settings?.logHeightValues == true)
                {
                    DebugLog($"Height values - First: {heights[0]:F2}, Last: {heights[totalSize-1]:F2}, Sample: {heights[totalSize/2]:F2}");
                }

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

                DebugLog($"Generated {totalSize} height values for terrain at {terrain.chunkPosition}");
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
            
            Debug.Log($"HybridTerrainGenerationSystem: Generating {totalSize} hardcoded test heights");
            
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

                // Debug: Check first few values
                Debug.Log($"HybridTerrainGenerationSystem: Hardcoded test - First 5 height values: {heightArray[0]}, {heightArray[1]}, {heightArray[2]}, {heightArray[3]}, {heightArray[4]}");
                Debug.Log($"HybridTerrainGenerationSystem: Hardcoded test - Last 5 height values: {heightArray[totalSize-5]}, {heightArray[totalSize-4]}, {heightArray[totalSize-3]}, {heightArray[totalSize-2]}, {heightArray[totalSize-1]}");
                
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
            // TODO: Implement in Step 5
            // Debug.Log("HybridTerrainGenerationSystem: ApplyGameLogic - Not implemented yet");
        }
        
        /// <summary>
        /// Updates performance monitoring metrics
        /// </summary>
        private void UpdatePerformanceMetrics()
        {
            // Only log performance if debug is enabled
            if (settings?.enableDebugLogs == true)
            {
                DebugLog($"Performance: Last Gen: {lastGenerationTime:F3}s, Chunks This Frame: {chunksProcessedThisFrame}, Total Chunks: {totalChunksProcessed}", true);
            }
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
                Debug.Log("HybridTerrainGenerationSystem: Buffers initialized successfully");
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
            
            Debug.Log($"HybridTerrainGenerationSystem: Forced generation for entity {entity.Index}");
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
            float range = Mathf.Max(0.0001f, maxH - minH);
            DebugLog($"[MeshGen] Min height: {minH}, Max height: {maxH}, Range: {range}");

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
        private void DebugLog(string message, bool verbose = false)
        {
            if (settings?.enableDebugLogs == true && (!verbose || settings?.enableVerboseLogs == true))
            {
                Debug.Log($"[HybridSystem] {message}");
            }
        }
        
        /// <summary>
        /// Debug error log wrapper
        /// </summary>
        private void DebugError(string message)
        {
            Debug.LogError($"[HybridSystem] {message}");
        }
        
        /// <summary>
        /// Debug warning log wrapper
        /// </summary>
        private void DebugWarning(string message)
        {
            Debug.LogWarning($"[HybridSystem] {message}");
        }
    }
} 