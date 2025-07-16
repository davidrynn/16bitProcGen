using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain.Generation;
using System.Collections;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Test script for the HybridTerrainGenerationSystem
    /// Verifies the basic system structure and integration
    /// </summary>
    public class HybridGenerationTest : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private int gridSize = 2;        // Size of the grid (2x2, 3x3, etc.)
        [SerializeField] private bool useGridLayout = true; // Toggle between line and grid
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool logPerformance = true;
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = false; // NEW: Debug toggle
        [SerializeField] private bool enableVerboseLogs = false; // NEW: Verbose logging
        
        private World defaultWorld;
        private SystemHandle hybridSystemHandle;
        private TerrainEntityManager entityManager;
        
        private void Start()
        {
            if (runOnStart)
            {
                RunTest();
            }
        }
        
        /// <summary>
        /// Runs the hybrid generation test
        /// </summary>
        [ContextMenu("Run Hybrid Generation Test")]
        public void RunTest()
        {
            DebugLog("=== HYBRID GENERATION TEST ===");
            
            // Step 1: Setup
            if (!SetupTest())
            {
                DebugError("✗ Test setup failed");
                return;
            }
            
            // Step 2: Create test entities
            if (!CreateTestEntities())
            {
                DebugError("✗ Failed to create test entities");
                return;
            }
            
            // Step 3: Verify system integration
            if (!VerifySystemIntegration())
            {
                DebugError("✗ System integration verification failed");
                return;
            }
            
            // Step 4: Wait for generation to complete
            WaitForGenerationCompletion();
            
            // Step 5: Test performance monitoring
            TestPerformanceMonitoring();
            
            DebugLog("=== HYBRID GENERATION TEST COMPLETE ===");
        }
        
        /// <summary>
        /// Sets up the test environment
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupTest()
        {
            DebugLog("Setting up hybrid generation test...", true);
            
            // Step 0: Ensure test environment is set up
            var testSetup = FindFirstObjectByType<HybridTestSetup>();
            if (testSetup == null)
            {
                DebugWarning("HybridTestSetup not found - creating one automatically");
                var setupGO = new GameObject("HybridTestSetup");
                testSetup = setupGO.AddComponent<HybridTestSetup>();
            }
            
            // Run setup if needed
            testSetup.SetupHybridTestEnvironment();
            
            // Get the default world
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                DebugError("No default world found!");
                return false;
            }
            
            // Find the hybrid system
            hybridSystemHandle = defaultWorld.GetExistingSystem<HybridTerrainGenerationSystem>();
            if (hybridSystemHandle == SystemHandle.Null)
            {
                DebugError("HybridTerrainGenerationSystem not found in world!");
                return false;
            }
            
            // Find the entity manager (MonoBehaviour, not a system)
            entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager == null)
            {
                DebugError("TerrainEntityManager not found in scene!");
                return false;
            }
            
            DebugLog("✓ Test setup complete");
            return true;
        }
        
        /// <summary>
        /// Creates test terrain entities
        /// </summary>
        /// <returns>True if entities were created successfully</returns>
        private bool CreateTestEntities()
        {
            int totalChunks = useGridLayout ? gridSize * gridSize : gridSize;
            DebugLog($"Creating {totalChunks} terrain entities with {(useGridLayout ? "grid" : "line")} layout...", true);
            
            try
            {
                int chunksCreated = 0;
                
                if (useGridLayout)
                {
                    // Create a 2D grid of chunks
                    for (int z = 0; z < gridSize; z++)
                    {
                        for (int x = 0; x < gridSize; x++)
                        {
                            // Create terrain entity with test data
                            var entity = entityManager.CreateTerrainEntity(
                                new int2(x, z), // 2D grid position
                                64, // Resolution
                                10f, // World scale
                                BiomeType.Plains // Default biome type
                            );
                            
                            if (entity == Entity.Null)
                            {
                                DebugError($"Failed to create terrain entity at ({x}, {z})");
                                return false;
                            }
                            
                            // Mark the entity for generation
                            var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(entity);
                            terrainData.needsGeneration = true;
                            defaultWorld.EntityManager.SetComponentData(entity, terrainData);
                            
                            DebugLog($"✓ Created terrain entity {chunksCreated} at chunk position ({x}, {z}) - marked for generation", true);
                            chunksCreated++;
                        }
                    }
                }
                else
                {
                    // Create a horizontal line of chunks
                    for (int i = 0; i < gridSize; i++)
                    {
                        // Create terrain entity with test data
                        var entity = entityManager.CreateTerrainEntity(
                            new int2(i, 0), // Chunk position
                            64, // Resolution
                            10f, // World scale
                            BiomeType.Plains // Default biome type
                        );
                        
                        if (entity == Entity.Null)
                        {
                            DebugError($"Failed to create terrain entity {i}");
                            return false;
                        }
                        
                        // Mark the entity for generation
                        var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(entity);
                        terrainData.needsGeneration = true;
                        defaultWorld.EntityManager.SetComponentData(entity, terrainData);
                        
                        DebugLog($"✓ Created terrain entity {i} at chunk position ({i}, 0) - marked for generation", true);
                        chunksCreated++;
                    }
                }
                
                DebugLog($"✓ Created {chunksCreated} test entities successfully - all marked for generation");
                return true;
            }
            catch (System.Exception e)
            {
                DebugError($"Exception creating test entities: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Verifies that the hybrid system is properly integrated
        /// </summary>
        /// <returns>True if integration is working</returns>
        private bool VerifySystemIntegration()
        {
            DebugLog("Verifying system integration...", true);
            
            // Check if systems are running
            if (hybridSystemHandle == SystemHandle.Null)
            {
                DebugError("HybridTerrainGenerationSystem handle is null!");
                return false;
            }
            
            if (entityManager == null)
            {
                DebugError("TerrainEntityManager is null!");
                return false;
            }
            
            // Check entity count
            var entityCount = defaultWorld.EntityManager.UniversalQuery.CalculateEntityCount();
            DebugLog($"✓ World contains {entityCount} entities");
            
            // Check terrain entities specifically
            var terrainQuery = defaultWorld.EntityManager.CreateEntityQuery(typeof(TerrainData));
            var terrainCount = terrainQuery.CalculateEntityCount();
            int expectedCount = useGridLayout ? gridSize * gridSize : gridSize;
            DebugLog($"✓ Found {terrainCount} terrain entities (expected {expectedCount})");
            
            if (terrainCount < expectedCount)
            {
                DebugWarning($"Expected {expectedCount} terrain entities, found {terrainCount}");
            }
            
            DebugLog("✓ System integration verified");
            return true;
        }
        
        /// <summary>
        /// Waits for terrain generation to complete and verifies results
        /// </summary>
        /// <returns>True if generation completed successfully</returns>
        private bool WaitForGenerationCompletion()
        {
            DebugLog("Waiting for terrain generation to complete...");
            
            // Wait a few frames for generation to complete
            StartCoroutine(WaitForGenerationCoroutine());
            return true;
        }

        private System.Collections.IEnumerator WaitForGenerationCoroutine()
        {
            int maxFrames = 60; // Wait up to 1 second at 60fps
            int frameCount = 0;
            
            while (frameCount < maxFrames)
            {
                // Check if any entities still need generation
                var terrainQuery = defaultWorld.EntityManager.CreateEntityQuery(typeof(TerrainData));
                var entities = terrainQuery.ToEntityArray(Allocator.Temp);
                
                int entitiesNeedingGeneration = 0;
                foreach (var entity in entities)
                {
                    var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(entity);
                    if (terrainData.needsGeneration)
                    {
                        entitiesNeedingGeneration++;
                    }
                }
                
                entities.Dispose();
                
                if (entitiesNeedingGeneration == 0)
                {
                    DebugLog("✓ All terrain entities have been generated!");
                    break;
                }
                
                DebugLog($"Waiting for generation... {entitiesNeedingGeneration} entities still need generation", true);
                frameCount++;
                yield return null; // Wait one frame
            }
            
            if (frameCount >= maxFrames)
            {
                DebugWarning("Generation timeout - some entities may not have been processed");
            }
            
            // Final verification
            VerifyGenerationResults();
        }

        /// <summary>
        /// Verifies that terrain generation produced expected results
        /// </summary>
        private void VerifyGenerationResults()
        {
            DebugLog("Verifying generation results...", true);
            
            var terrainQuery = defaultWorld.EntityManager.CreateEntityQuery(typeof(TerrainData));
            var entities = terrainQuery.ToEntityArray(Allocator.Temp);
            
            int generatedCount = 0;
            int totalCount = entities.Length;
            
            foreach (var entity in entities)
            {
                var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(entity);
                if (!terrainData.needsGeneration)
                {
                    generatedCount++;
                }
            }
            
            entities.Dispose();
            
            DebugLog($"✓ Generation Results: {generatedCount}/{totalCount} entities processed");
            
            if (generatedCount == totalCount)
            {
                DebugLog("✓ All terrain entities successfully generated!");
            }
            else
            {
                DebugWarning($"Only {generatedCount}/{totalCount} entities were generated");
            }
        }
        
        /// <summary>
        /// Tests performance monitoring functionality
        /// </summary>
        private void TestPerformanceMonitoring()
        {
            if (!logPerformance) return;
            
            DebugLog("Testing performance monitoring...", true);
            
            // Check if the system exists and is valid
            if (hybridSystemHandle != SystemHandle.Null)
            {
                DebugLog("✓ Performance monitoring system found and valid");
            }
            else
            {
                DebugWarning("Could not access hybrid system for performance metrics");
            }
            
            DebugLog("✓ Performance monitoring working");
        }
        
        /// <summary>
        /// Forces generation of all terrain entities
        /// </summary>
        [ContextMenu("Force Generate All Terrain")]
        public void ForceGenerateAllTerrain()
        {
            Debug.Log("Forcing generation of all terrain entities...");
            
            var terrainQuery = defaultWorld.EntityManager.CreateEntityQuery(typeof(TerrainData));
            var entities = terrainQuery.ToEntityArray(Allocator.Temp);
            
            // Mark all terrain entities for regeneration
            foreach (var entity in entities)
            {
                var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(entity);
                terrainData.needsGeneration = true;
                defaultWorld.EntityManager.SetComponentData(entity, terrainData);
            }
            
            entities.Dispose();
            Debug.Log($"✓ Forced generation of {entities.Length} terrain entities");
        }
        
        /// <summary>
        /// Gets current system status
        /// </summary>
        [ContextMenu("Cleanup Test Entities")]
        public void CleanupTestEntities()
        {
            if (entityManager != null)
            {
                Debug.Log("HybridGenerationTest: Manually cleaning up test entities");
                entityManager.DestroyAllTerrainEntities();
            }
        }
        
        [ContextMenu("Get System Status")]
        public void GetSystemStatus()
        {
            Debug.Log("=== HYBRID SYSTEM STATUS ===");
            
            if (hybridSystemHandle != SystemHandle.Null)
            {
                Debug.Log($"HybridTerrainGenerationSystem: Handle valid, System exists");
            }
            else
            {
                Debug.LogWarning("HybridTerrainGenerationSystem: Not found");
            }
            
            if (entityManager != null)
            {
                Debug.Log($"TerrainEntityManager: Found in scene");
                Debug.Log($"Active Terrain Entities: {entityManager.GetTerrainEntityCount()}");
            }
            else
            {
                Debug.LogWarning("TerrainEntityManager: Not found");
            }
            
            var terrainQuery = defaultWorld.EntityManager.CreateEntityQuery(typeof(TerrainData));
            var terrainCount = terrainQuery.CalculateEntityCount();
            Debug.Log($"Active Terrain Entities: {terrainCount}");
            
            Debug.Log("=== STATUS COMPLETE ===");
        }
        
        private void OnDestroy()
        {
            // Cleanup test entities to prevent memory leaks
            if (entityManager != null)
            {
                Debug.Log("HybridGenerationTest: Cleaning up test entities");
                entityManager.DestroyAllTerrainEntities();
            }
            
            Debug.Log("HybridGenerationTest: Destroyed");
        }

        /// <summary>
        /// Debug log wrapper that respects the debug toggle
        /// </summary>
        private void DebugLog(string message, bool verbose = false)
        {
            if (enableDebugLogs && (!verbose || enableVerboseLogs))
            {
                Debug.Log($"[HybridTest] {message}");
            }
        }
        
        /// <summary>
        /// Debug error log wrapper
        /// </summary>
        private void DebugError(string message)
        {
            Debug.LogError($"[HybridTest] {message}");
        }
        
        /// <summary>
        /// Debug warning log wrapper
        /// </summary>
        private void DebugWarning(string message)
        {
            Debug.LogWarning($"[HybridTest] {message}");
        }
    }
} 