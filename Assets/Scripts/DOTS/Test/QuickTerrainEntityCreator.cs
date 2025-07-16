using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Quick script to create terrain entities for testing
/// This will immediately create entities so the HybridTerrainGenerationSystem has something to process
/// Now includes seamless terrain verification
/// </summary>
public class QuickTerrainEntityCreator : MonoBehaviour
{
    [Header("Creation Settings")]
    public bool createOnStart = true;
    public int numberOfEntities = 5;
    public int resolution = 32;
    public float worldScale = 10f;
    
    [Header("Seamless Testing")]
    public bool enableSeamlessTesting = true;
    public bool showBoundaryHeights = false;
    public float seamlessThreshold = 0.1f; // Maximum height difference for "seamless"
    
    private TerrainEntityManager entityManager;
    private Entity[] createdEntities;
    
    private void Start()
    {
        if (createOnStart)
        {
            CreateTerrainEntities();
        }
    }
    
    /// <summary>
    /// Creates terrain entities for testing
    /// </summary>
    [ContextMenu("Create Terrain Entities")]
    public void CreateTerrainEntities()
    {
        Debug.Log("=== QUICK TERRAIN ENTITY CREATOR ===");
        
        // Find or create TerrainEntityManager
        entityManager = FindFirstObjectByType<TerrainEntityManager>();
        if (entityManager == null)
        {
            Debug.Log("Creating TerrainEntityManager...");
            var go = new GameObject("TerrainEntityManager");
            entityManager = go.AddComponent<TerrainEntityManager>();
        }
        
        Debug.Log($"Creating {numberOfEntities} terrain entities...");
        
        // Create entities in a grid pattern
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(numberOfEntities));
        createdEntities = new Entity[numberOfEntities];
        
        for (int i = 0; i < numberOfEntities; i++)
        {
            int x = i % gridSize;
            int z = i / gridSize;
            var chunkPosition = new int2(x, z);
            
            // Create terrain entity
            var entity = entityManager.CreateTerrainEntity(
                chunkPosition,
                resolution,
                worldScale,
                BiomeType.Plains
            );
            
            if (entity == Entity.Null)
            {
                Debug.LogError($"Failed to create terrain entity at {chunkPosition}");
                continue;
            }
            
            createdEntities[i] = entity;
            
            // Mark for generation
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                var terrainData = world.EntityManager.GetComponentData<DOTS.Terrain.TerrainData>(entity);
                terrainData.needsGeneration = true;
                world.EntityManager.SetComponentData(entity, terrainData);
                
                Debug.Log($"✓ Created terrain entity {i} at {chunkPosition} - marked for generation");
            }
        }
        
        Debug.Log($"✓ Created {numberOfEntities} terrain entities successfully");
        Debug.Log("The HybridTerrainGenerationSystem should now process these entities!");
        
        // Start seamless testing if enabled
        if (enableSeamlessTesting)
        {
            StartCoroutine(TestSeamlessAfterGeneration());
        }
    }
    
    /// <summary>
    /// Coroutine to test seamless generation after chunks are created
    /// </summary>
    private System.Collections.IEnumerator TestSeamlessAfterGeneration()
    {
        Debug.Log("Waiting for terrain generation to complete before testing seamless...");
        
        // Wait for generation to complete
        yield return new WaitForSeconds(3f);
        
        // Test seamless generation
        TestSeamlessGeneration();
    }
    
    /// <summary>
    /// Tests if terrain is seamless between adjacent chunks
    /// </summary>
    [ContextMenu("Test Seamless Generation")]
    public void TestSeamlessGeneration()
    {
        if (createdEntities == null || createdEntities.Length < 2)
        {
            Debug.LogWarning("Not enough entities to test seamless generation");
            return;
        }
        
        Debug.Log("=== TESTING SEAMLESS GENERATION ===");
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("No DOTS world found for seamless testing");
            return;
        }
        
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(numberOfEntities));
        int totalSeamlessChecks = 0;
        int successfulSeamlessChecks = 0;
        
        // Test horizontal boundaries
        for (int z = 0; z < gridSize; z++)
        {
            for (int x = 0; x < gridSize - 1; x++)
            {
                int leftIndex = z * gridSize + x;
                int rightIndex = z * gridSize + (x + 1);
                
                if (leftIndex < createdEntities.Length && rightIndex < createdEntities.Length)
                {
                    bool isSeamless = CheckHorizontalBoundary(leftIndex, rightIndex, world);
                    totalSeamlessChecks++;
                    if (isSeamless) successfulSeamlessChecks++;
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
                
                if (bottomIndex < createdEntities.Length && topIndex < createdEntities.Length)
                {
                    bool isSeamless = CheckVerticalBoundary(bottomIndex, topIndex, world);
                    totalSeamlessChecks++;
                    if (isSeamless) successfulSeamlessChecks++;
                }
            }
        }
        
        // Report results
        float seamlessPercentage = totalSeamlessChecks > 0 ? (float)successfulSeamlessChecks / totalSeamlessChecks * 100f : 0f;
        Debug.Log($"=== SEAMLESS TEST RESULTS ===");
        Debug.Log($"Total boundary checks: {totalSeamlessChecks}");
        Debug.Log($"Successful seamless checks: {successfulSeamlessChecks}");
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
    /// Checks the boundary between two horizontally adjacent chunks
    /// </summary>
    private bool CheckHorizontalBoundary(int leftIndex, int rightIndex, World world)
    {
        var leftEntity = createdEntities[leftIndex];
        var rightEntity = createdEntities[rightIndex];
        
        if (!world.EntityManager.Exists(leftEntity) || !world.EntityManager.Exists(rightEntity))
            return false;
        
        var leftTerrain = world.EntityManager.GetComponentData<DOTS.Terrain.TerrainData>(leftEntity);
        var rightTerrain = world.EntityManager.GetComponentData<DOTS.Terrain.TerrainData>(rightEntity);
        
        if (!leftTerrain.heightData.IsCreated || !rightTerrain.heightData.IsCreated)
            return false;
        
        ref var leftHeights = ref leftTerrain.heightData.Value.heights;
        ref var rightHeights = ref rightTerrain.heightData.Value.heights;
        
        int differences = 0;
        int totalChecks = 0;
        
        // Check boundary heights (right edge of left chunk vs left edge of right chunk)
        for (int y = 0; y < resolution; y++)
        {
            int leftIndex_pos = y * resolution + (resolution - 1); // Right edge of left chunk
            int rightIndex_pos = y * resolution + 0; // Left edge of right chunk
            
            if (leftIndex_pos < leftHeights.Length && rightIndex_pos < rightHeights.Length)
            {
                float leftHeight = leftHeights[leftIndex_pos];
                float rightHeight = rightHeights[rightIndex_pos];
                float difference = Mathf.Abs(leftHeight - rightHeight);
                
                totalChecks++;
                if (difference > seamlessThreshold)
                    differences++;
                
                if (showBoundaryHeights)
                {
                    Debug.Log($"H-Boundary {leftIndex}->{rightIndex} at Y={y}: Left={leftHeight:F2}, Right={rightHeight:F2}, Diff={difference:F2}");
                }
            }
        }
        
        bool isSeamless = totalChecks > 0 && (float)differences / totalChecks < 0.1f; // Less than 10% differences
        
        if (totalChecks > 0)
        {
            Debug.Log($"Horizontal boundary {leftIndex}->{rightIndex}: {differences}/{totalChecks} differences ({(float)differences/totalChecks*100:F1}%) - {(isSeamless ? "SEAMLESS" : "NOT SEAMLESS")}");
        }
        
        return isSeamless;
    }
    
    /// <summary>
    /// Checks the boundary between two vertically adjacent chunks
    /// </summary>
    private bool CheckVerticalBoundary(int bottomIndex, int topIndex, World world)
    {
        var bottomEntity = createdEntities[bottomIndex];
        var topEntity = createdEntities[topIndex];
        
        if (!world.EntityManager.Exists(bottomEntity) || !world.EntityManager.Exists(topEntity))
            return false;
        
        var bottomTerrain = world.EntityManager.GetComponentData<DOTS.Terrain.TerrainData>(bottomEntity);
        var topTerrain = world.EntityManager.GetComponentData<DOTS.Terrain.TerrainData>(topEntity);
        
        if (!bottomTerrain.heightData.IsCreated || !topTerrain.heightData.IsCreated)
            return false;
        
        ref var bottomHeights = ref bottomTerrain.heightData.Value.heights;
        ref var topHeights = ref topTerrain.heightData.Value.heights;
        
        int differences = 0;
        int totalChecks = 0;
        
        // Check boundary heights (top edge of bottom chunk vs bottom edge of top chunk)
        for (int x = 0; x < resolution; x++)
        {
            int bottomIndex_pos = (resolution - 1) * resolution + x; // Top edge of bottom chunk
            int topIndex_pos = 0 * resolution + x; // Bottom edge of top chunk
            
            if (bottomIndex_pos < bottomHeights.Length && topIndex_pos < topHeights.Length)
            {
                float bottomHeight = bottomHeights[bottomIndex_pos];
                float topHeight = topHeights[topIndex_pos];
                float difference = Mathf.Abs(bottomHeight - topHeight);
                
                totalChecks++;
                if (difference > seamlessThreshold)
                    differences++;
                
                if (showBoundaryHeights)
                {
                    Debug.Log($"V-Boundary {bottomIndex}->{topIndex} at X={x}: Bottom={bottomHeight:F2}, Top={topHeight:F2}, Diff={difference:F2}");
                }
            }
        }
        
        bool isSeamless = totalChecks > 0 && (float)differences / totalChecks < 0.1f; // Less than 10% differences
        
        if (totalChecks > 0)
        {
            Debug.Log($"Vertical boundary {bottomIndex}->{topIndex}: {differences}/{totalChecks} differences ({(float)differences/totalChecks*100:F1}%) - {(isSeamless ? "SEAMLESS" : "NOT SEAMLESS")}");
        }
        
        return isSeamless;
    }
    
    /// <summary>
    /// Gets the current entity count
    /// </summary>
    [ContextMenu("Get Entity Count")]
    public void GetEntityCount()
    {
        if (entityManager != null)
        {
            var count = entityManager.GetTerrainEntityCount();
            Debug.Log($"Current terrain entity count: {count}");
        }
        else
        {
            Debug.LogWarning("TerrainEntityManager not found");
        }
    }
    
    /// <summary>
    /// Destroys all terrain entities
    /// </summary>
    [ContextMenu("Destroy All Entities")]
    public void DestroyAllEntities()
    {
        if (entityManager != null)
        {
            entityManager.DestroyAllTerrainEntities();
            Debug.Log("Destroyed all terrain entities");
        }
        else
        {
            Debug.LogWarning("TerrainEntityManager not found");
        }
    }
} 