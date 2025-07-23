using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Test for the TerrainGenerationSystem
    /// Verifies that terrain generation using compute shaders works correctly
    /// </summary>
    public class TerrainGenerationTest : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private int testResolution = 32;
        [SerializeField] private float testWorldScale = 10f;
        
        [Header("Seamless Testing")]
        [SerializeField] private bool testSeamlessGeneration = true;
        [SerializeField] private int seamlessTestChunks = 4; // 2x2 grid for seamless testing
        [SerializeField] private float seamlessThreshold = 0.1f;
        
        private World defaultWorld;
        private TerrainEntityManager entityManager;
        private Entity testEntity;
        private Entity[] seamlessTestEntities;
        
        private void Start()
        {
            if (runOnStart)
            {
                RunTest();
            }
        }
        
        [ContextMenu("Run Terrain Generation Test")]
        public void RunTest()
        {
            Debug.Log("=== TERRAIN GENERATION TEST ===");
            
            if (!SetupTest())
            {
                Debug.LogError("Test setup failed!");
                return;
            }
            
            if (!CreateTestEntity())
            {
                Debug.LogError("Test entity creation failed!");
                return;
            }
            
            if (!WaitForGeneration())
            {
                Debug.LogError("Terrain generation failed!");
                return;
            }
            
            VerifyGenerationResults();
            
            // Test seamless generation if enabled
            if (testSeamlessGeneration)
            {
                TestSeamlessGeneration();
            }
            
            Debug.Log("=== TERRAIN GENERATION TEST COMPLETE ===");
        }
        
        private bool SetupTest()
        {
            Debug.Log("Setting up terrain generation test...");
            
            // Get DOTS world
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                Debug.LogError("DOTS World not found!");
                return false;
            }
            
            // Get TerrainEntityManager
            entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager == null)
            {
                Debug.LogError("TerrainEntityManager not found!");
                return false;
            }
            
            Debug.Log("✓ Test setup complete");
            return true;
        }
        
        private bool CreateTestEntity()
        {
            Debug.Log("Creating test terrain entity...");
            
            // Create a single test entity
            testEntity = entityManager.CreateTerrainEntity(
                new int2(0, 0), 
                testResolution, 
                testWorldScale, 
                BiomeType.Plains
            );
            
            if (testEntity == Entity.Null)
            {
                Debug.LogError("Failed to create test entity!");
                return false;
            }
            
            Debug.Log($"✓ Created test entity {testEntity} at (0,0) with resolution {testResolution}");
            return true;
        }
        
        private bool WaitForGeneration()
        {
            Debug.Log("Waiting for terrain generation to complete...");
            
            // Wait for the TerrainGenerationSystem to process the entity
            StartCoroutine(WaitForGenerationCoroutine());
            return true;
        }

        private System.Collections.IEnumerator WaitForGenerationCoroutine()
        {
            int maxFrames = 120; // Wait up to 2 seconds at 60fps
            int frameCount = 0;
            
            while (frameCount < maxFrames)
            {
                // Check if the entity still needs generation
                if (defaultWorld.EntityManager.Exists(testEntity))
                {
                    var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(testEntity);
                    
                    if (!terrainData.needsGeneration)
                    {
                        Debug.Log("✓ Terrain generation completed!");
                        break;
                    }
                }
                else
                {
                    Debug.LogError("Test entity was destroyed during generation!");
                    yield break;
                }
                
                frameCount++;
                yield return null; // Wait one frame
            }
            
            if (frameCount >= maxFrames)
            {
                Debug.LogError("Generation timeout - terrain generation did not complete");
            }
            
            // Wait a few more frames to ensure the generation is fully processed
            yield return new WaitForSeconds(0.1f);
        }
        
        private void VerifyGenerationResults()
        {
            Debug.Log("Verifying generation results...");
            
            if (!defaultWorld.EntityManager.Exists(testEntity))
            {
                Debug.LogError("Test entity no longer exists!");
                return;
            }
            
            var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(testEntity);
            
            // Check if height data was generated
            if (!terrainData.heightData.IsCreated)
            {
                Debug.LogError("Height data was not generated!");
                return;
            }
            
            // Access the height data
            ref var heightData = ref terrainData.heightData.Value;
            
            Debug.Log($"✓ Height data generated: {heightData.size.x}x{heightData.size.y} = {heightData.heights.Length} values");
            
            // Check some sample values
            int sampleCount = Mathf.Min(5, heightData.heights.Length);
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            float avgHeight = 0f;
            
            for (int i = 0; i < heightData.heights.Length; i++)
            {
                float height = heightData.heights[i];
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
                avgHeight += height;
            }
            
            avgHeight /= heightData.heights.Length;
            
            Debug.Log($"✓ Height Statistics:");
            Debug.Log($"  - Min Height: {minHeight:F2}");
            Debug.Log($"  - Max Height: {maxHeight:F2}");
            Debug.Log($"  - Avg Height: {avgHeight:F2}");
            Debug.Log($"  - Height Range: {maxHeight - minHeight:F2}");
            
            // Check terrain types
            int grassCount = 0, waterCount = 0, sandCount = 0, floraCount = 0, rockCount = 0;
            
            for (int i = 0; i < heightData.terrainTypes.Length; i++)
            {
                switch (heightData.terrainTypes[i])
                {
                    case TerrainType.Grass: grassCount++; break;
                    case TerrainType.Water: waterCount++; break;
                    case TerrainType.Sand: sandCount++; break;
                    case TerrainType.Flora: floraCount++; break;
                    case TerrainType.Rock: rockCount++; break;
                }
            }
            
            Debug.Log($"✓ Terrain Type Distribution:");
            Debug.Log($"  - Water: {waterCount} ({waterCount * 100f / heightData.terrainTypes.Length:F1}%)");
            Debug.Log($"  - Sand: {sandCount} ({sandCount * 100f / heightData.terrainTypes.Length:F1}%)");
            Debug.Log($"  - Grass: {grassCount} ({grassCount * 100f / heightData.terrainTypes.Length:F1}%)");
            Debug.Log($"  - Flora: {floraCount} ({floraCount * 100f / heightData.terrainTypes.Length:F1}%)");
            Debug.Log($"  - Rock: {rockCount} ({rockCount * 100f / heightData.terrainTypes.Length:F1}%)");
            
            // Verify the generation was successful
            if (maxHeight > minHeight && heightData.heights.Length > 0)
            {
                Debug.Log("✓ Terrain generation verification successful!");
            }
            else
            {
                Debug.LogError("Terrain generation verification failed - no height variation detected!");
            }
        }
        
        /// <summary>
        /// Tests seamless generation by creating multiple adjacent chunks
        /// </summary>
        private void TestSeamlessGeneration()
        {
            Debug.Log("=== TESTING SEAMLESS GENERATION ===");
            
            if (!CreateSeamlessTestEntities())
            {
                Debug.LogError("Failed to create seamless test entities!");
                return;
            }
            
            // Wait for generation and then test
            StartCoroutine(TestSeamlessAfterGeneration());
        }
        
        /// <summary>
        /// Creates multiple test entities for seamless testing
        /// </summary>
        private bool CreateSeamlessTestEntities()
        {
            Debug.Log($"Creating {seamlessTestChunks} entities for seamless testing...");
            
            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(seamlessTestChunks));
            seamlessTestEntities = new Entity[seamlessTestChunks];
            
            for (int i = 0; i < seamlessTestChunks; i++)
            {
                int x = i % gridSize;
                int z = i / gridSize;
                var chunkPosition = new int2(x, z);
                
                var entity = entityManager.CreateTerrainEntity(
                    chunkPosition,
                    testResolution,
                    testWorldScale,
                    BiomeType.Plains
                );
                
                if (entity == Entity.Null)
                {
                    Debug.LogError($"Failed to create seamless test entity at {chunkPosition}");
                    return false;
                }
                
                seamlessTestEntities[i] = entity;
                
                // Mark for generation
                var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(entity);
                terrainData.needsGeneration = true;
                defaultWorld.EntityManager.SetComponentData(entity, terrainData);
                
                Debug.Log($"✓ Created seamless test entity {i} at {chunkPosition}");
            }
            
            Debug.Log($"✓ Created {seamlessTestChunks} seamless test entities");
            return true;
        }
        
        /// <summary>
        /// Coroutine to test seamless generation after chunks are created
        /// </summary>
        private System.Collections.IEnumerator TestSeamlessAfterGeneration()
        {
            Debug.Log("Waiting for seamless test entities to generate...");
            
            // Wait for all entities to be generated
            int maxFrames = 180; // Wait up to 3 seconds
            int frameCount = 0;
            
            while (frameCount < maxFrames)
            {
                bool allGenerated = true;
                
                for (int i = 0; i < seamlessTestChunks; i++)
                {
                    if (defaultWorld.EntityManager.Exists(seamlessTestEntities[i]))
                    {
                        var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(seamlessTestEntities[i]);
                        if (terrainData.needsGeneration)
                        {
                            allGenerated = false;
                            break;
                        }
                    }
                    else
                    {
                        allGenerated = false;
                        break;
                    }
                }
                
                if (allGenerated)
                {
                    Debug.Log("✓ All seamless test entities generated!");
                    break;
                }
                
                frameCount++;
                yield return null;
            }
            
            if (frameCount >= maxFrames)
            {
                Debug.LogError("Seamless generation timeout!");
                yield break;
            }
            
            // Wait a bit more to ensure full processing
            yield return new WaitForSeconds(0.5f);
            
            // Test seamless boundaries
            VerifySeamlessBoundaries();
        }
        
        /// <summary>
        /// Verifies seamless boundaries between adjacent chunks
        /// </summary>
        private void VerifySeamlessBoundaries()
        {
            Debug.Log("Verifying seamless boundaries...");
            
            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(seamlessTestChunks));
            int totalBoundaries = 0;
            int seamlessBoundaries = 0;
            
            // Test horizontal boundaries
            for (int z = 0; z < gridSize; z++)
            {
                for (int x = 0; x < gridSize - 1; x++)
                {
                    int leftIndex = z * gridSize + x;
                    int rightIndex = z * gridSize + (x + 1);
                    
                    if (leftIndex < seamlessTestChunks && rightIndex < seamlessTestChunks)
                    {
                        bool isSeamless = CheckBoundary(leftIndex, rightIndex, true);
                        totalBoundaries++;
                        if (isSeamless) seamlessBoundaries++;
                    }
                }
            }
            
            // Test vertical boundaries
            for (int z = 0; z < gridSize - 1; z++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int bottomIndex = z * gridSize + x;
                    int topIndex = (z + 1) * gridSize + x;
                    
                    if (bottomIndex < seamlessTestChunks && topIndex < seamlessTestChunks)
                    {
                        bool isSeamless = CheckBoundary(bottomIndex, topIndex, false);
                        totalBoundaries++;
                        if (isSeamless) seamlessBoundaries++;
                    }
                }
            }
            
            // Report results
            float seamlessPercentage = totalBoundaries > 0 ? (float)seamlessBoundaries / totalBoundaries * 100f : 0f;
            
            Debug.Log($"=== SEAMLESS TEST RESULTS ===");
            Debug.Log($"Total boundaries tested: {totalBoundaries}");
            Debug.Log($"Seamless boundaries: {seamlessBoundaries}");
            Debug.Log($"Seamless percentage: {seamlessPercentage:F1}%");
            
            if (seamlessPercentage >= 95f)
            {
                Debug.Log("✓ EXCELLENT: Terrain is highly seamless!");
            }
            else if (seamlessPercentage >= 80f)
            {
                Debug.Log("✓ GOOD: Terrain is mostly seamless");
            }
            else if (seamlessPercentage >= 60f)
            {
                Debug.Log("⚠ FAIR: Some seamless issues detected");
            }
            else
            {
                Debug.Log("✗ POOR: Significant seamless issues detected");
            }
            
            Debug.Log("===============================");
        }
        
        /// <summary>
        /// Checks if a boundary between two chunks is seamless
        /// </summary>
        private bool CheckBoundary(int chunk1Index, int chunk2Index, bool isHorizontal)
        {
            var entity1 = seamlessTestEntities[chunk1Index];
            var entity2 = seamlessTestEntities[chunk2Index];
            
            if (!defaultWorld.EntityManager.Exists(entity1) || !defaultWorld.EntityManager.Exists(entity2))
                return false;
            
            var terrain1 = defaultWorld.EntityManager.GetComponentData<TerrainData>(entity1);
            var terrain2 = defaultWorld.EntityManager.GetComponentData<TerrainData>(entity2);
            
            if (!terrain1.heightData.IsCreated || !terrain2.heightData.IsCreated)
                return false;
            
            ref var heights1 = ref terrain1.heightData.Value.heights;
            ref var heights2 = ref terrain2.heightData.Value.heights;
            
            int differences = 0;
            int totalChecks = 0;
            
            if (isHorizontal)
            {
                // Check horizontal boundary (right edge of chunk1 vs left edge of chunk2)
                for (int y = 0; y < testResolution; y++)
                {
                    int index1 = y * testResolution + (testResolution - 1); // Right edge of chunk1
                    int index2 = y * testResolution + 0; // Left edge of chunk2
                    
                    if (index1 < heights1.Length && index2 < heights2.Length)
                    {
                        float height1 = heights1[index1];
                        float height2 = heights2[index2];
                        float difference = Mathf.Abs(height1 - height2);
                        
                        totalChecks++;
                        if (difference > seamlessThreshold)
                            differences++;
                    }
                }
            }
            else
            {
                // Check vertical boundary (top edge of chunk1 vs bottom edge of chunk2)
                for (int x = 0; x < testResolution; x++)
                {
                    int index1 = (testResolution - 1) * testResolution + x; // Top edge of chunk1
                    int index2 = 0 * testResolution + x; // Bottom edge of chunk2
                    
                    if (index1 < heights1.Length && index2 < heights2.Length)
                    {
                        float height1 = heights1[index1];
                        float height2 = heights2[index2];
                        float difference = Mathf.Abs(height1 - height2);
                        
                        totalChecks++;
                        if (difference > seamlessThreshold)
                            differences++;
                    }
                }
            }
            
            bool isSeamless = totalChecks > 0 && (float)differences / totalChecks < 0.1f;
            
            if (totalChecks > 0)
            {
                string boundaryType = isHorizontal ? "Horizontal" : "Vertical";
                Debug.Log($"{boundaryType} boundary {chunk1Index}->{chunk2Index}: {differences}/{totalChecks} differences ({(float)differences/totalChecks*100:F1}%) - {(isSeamless ? "SEAMLESS" : "NOT SEAMLESS")}");
            }
            
            return isSeamless;
        }
        
        [ContextMenu("Get Test Status")]
        public void GetTestStatus()
        {
            Debug.Log("=== TERRAIN GENERATION TEST STATUS ===");
            
            if (defaultWorld != null)
            {
                Debug.Log($"DOTS World: Active");
                
                if (testEntity != Entity.Null && defaultWorld.EntityManager.Exists(testEntity))
                {
                    var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(testEntity);
                    Debug.Log($"Test Entity: {testEntity}");
                    Debug.Log($"Needs Generation: {terrainData.needsGeneration}");
                    Debug.Log($"Has Height Data: {terrainData.heightData.IsCreated}");
                    
                    if (terrainData.heightData.IsCreated)
                    {
                        ref var heightData = ref terrainData.heightData.Value;
                        Debug.Log($"Height Data Size: {heightData.size.x}x{heightData.size.y}");
                        Debug.Log($"Height Values: {heightData.heights.Length}");
                    }
                }
                else
                {
                    Debug.Log("Test Entity: Not found or destroyed");
                }
            }
            else
            {
                Debug.Log("DOTS World: Not available");
            }
            
            Debug.Log("=== STATUS COMPLETE ===");
        }
        
        private void OnDestroy()
        {
            Debug.Log("TerrainGenerationTest: Destroyed");
        }
    }
} 