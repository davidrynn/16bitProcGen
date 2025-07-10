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
        
        private World defaultWorld;
        private TerrainEntityManager entityManager;
        private Entity testEntity;
        
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

        private bool generationCompleted = false;
        
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
                        generationCompleted = true;
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