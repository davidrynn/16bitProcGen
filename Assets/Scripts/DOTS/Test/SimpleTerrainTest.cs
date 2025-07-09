using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Simple test to verify compute shader terrain generation
    /// </summary>
    public class SimpleTerrainTest : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private int testResolution = 32;
        [SerializeField] private float testWorldScale = 10f;
        
        private World defaultWorld;
        private TerrainEntityManager entityManager;
        private Entity testEntity;
        
        private void Start()
        {
            // Wait a frame for DOTS to initialize
            StartCoroutine(RunTestAfterDelay());
        }
        
        private System.Collections.IEnumerator RunTestAfterDelay()
        {
            yield return new WaitForSeconds(0.5f);
            RunTest();
        }
        
        [ContextMenu("Run Simple Terrain Test")]
        public void RunTest()
        {
            Debug.Log("=== SIMPLE TERRAIN TEST ===");
            
            if (!SetupTest())
            {
                Debug.LogError("Test setup failed!");
                return;
            }
            
            if (!CreateTestEntity())
            {
                Debug.LogError("Failed to create test entity!");
                return;
            }
            
            // Wait a few frames for generation
            StartCoroutine(WaitAndVerify());
        }
        
        private bool SetupTest()
        {
            Debug.Log("Setting up simple terrain test...");
            
            // Get default world
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null)
            {
                Debug.LogError("Default world not found!");
                return false;
            }
            
            // Get TerrainEntityManager
            entityManager = FindObjectOfType<TerrainEntityManager>();
            if (entityManager == null)
            {
                Debug.LogError("TerrainEntityManager not found!");
                return false;
            }
            
            Debug.Log("✓ Simple test setup complete");
            return true;
        }
        
        private bool CreateTestEntity()
        {
            Debug.Log("Creating simple test terrain entity...");
            
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
        
        private System.Collections.IEnumerator WaitAndVerify()
        {
            Debug.Log("Waiting for terrain generation...");
            
            // Wait for generation to complete
            int maxFrames = 60; // Wait up to 1 second
            int frameCount = 0;
            
            while (frameCount < maxFrames)
            {
                if (defaultWorld.EntityManager.Exists(testEntity))
                {
                    var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(testEntity);
                    
                    if (!terrainData.needsGeneration && terrainData.heightData.IsCreated)
                    {
                        Debug.Log("✓ Terrain generation completed!");
                        VerifyResults();
                        yield break;
                    }
                }
                
                frameCount++;
                yield return null;
            }
            
            Debug.LogError("Generation timeout!");
        }
        
        private void VerifyResults()
        {
            Debug.Log("Verifying terrain generation results...");
            
            if (!defaultWorld.EntityManager.Exists(testEntity))
            {
                Debug.LogError("Test entity no longer exists!");
                return;
            }
            
            var terrainData = defaultWorld.EntityManager.GetComponentData<TerrainData>(testEntity);
            
            if (!terrainData.heightData.IsCreated)
            {
                Debug.LogError("Height data was not generated!");
                return;
            }
            
            ref var heightData = ref terrainData.heightData.Value;
            
            Debug.Log($"✓ Height data: {heightData.size.x}x{heightData.size.y} = {heightData.heights.Length} values");
            
            // Check for height variation
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            
            for (int i = 0; i < heightData.heights.Length; i++)
            {
                float height = heightData.heights[i];
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }
            
            Debug.Log($"✓ Height range: {minHeight:F3} to {maxHeight:F3} (range: {maxHeight - minHeight:F3})");
            
            if (maxHeight > minHeight)
            {
                Debug.Log("✓ SUCCESS: Terrain generation is working!");
            }
            else
            {
                Debug.LogError("✗ FAILED: No height variation detected!");
            }
        }
        
        private void OnDestroy()
        {
            Debug.Log("SimpleTerrainTest: Destroyed");
        }
    }
} 