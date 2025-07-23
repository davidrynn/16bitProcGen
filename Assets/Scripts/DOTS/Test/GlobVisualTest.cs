using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Terrain.Modification;

/// <summary>
/// Simple visual test component that creates GameObjects to represent glob entities
/// This is a temporary solution for testing - in production you'd use proper DOTS rendering
/// </summary>
public class GlobVisualTest : MonoBehaviour
{
    [Header("Visual Settings")]
    public bool enableVisuals = true;
    public Material globMaterial;
    public float updateInterval = 0.1f; // How often to update visuals
    
    [Header("Debug")]
    public bool logVisualUpdates = false;
    
    private GameObject[] globVisuals = new GameObject[100]; // Support up to 100 globs
    private Entity[] trackedGlobs = new Entity[100];
    private int globCount = 0;
    private float lastUpdateTime = 0f;
    
    void Start()
    {
        if (enableVisuals && globMaterial == null)
        {
            // Create a default material if none is assigned
            globMaterial = new Material(Shader.Find("Standard"));
            globMaterial.color = Color.red;
        }
    }
    
    void Update()
    {
        if (!enableVisuals) return;
        
        // Update visuals periodically
        if (Time.time - lastUpdateTime > updateInterval)
        {
            UpdateGlobVisuals();
            lastUpdateTime = Time.time;
        }
    }
    
    private void UpdateGlobVisuals()
    {
        if (!DOTSWorldSetup.IsWorldReady()) return;
        
        var world = DOTSWorldSetup.GetWorld();
        var entityManager = world.EntityManager;
        
        // Find all glob entities
        var globQuery = entityManager.CreateEntityQuery(typeof(TerrainGlobComponent), typeof(LocalTransform));
        var globEntities = globQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        
        // Update existing visuals
        for (int i = 0; i < globCount; i++)
        {
            if (trackedGlobs[i] != Entity.Null && entityManager.Exists(trackedGlobs[i]))
            {
                var globComponent = entityManager.GetComponentData<TerrainGlobComponent>(trackedGlobs[i]);
                var transform = entityManager.GetComponentData<LocalTransform>(trackedGlobs[i]);
                
                if (globVisuals[i] != null)
                {
                    globVisuals[i].transform.position = transform.Position;
                    globVisuals[i].transform.rotation = transform.Rotation;
                    globVisuals[i].transform.localScale = Vector3.one * globComponent.globRadius;
                    
                    if (logVisualUpdates)
                    {
                        Debug.Log($"Updated glob visual {i} at position {transform.Position}");
                    }
                }
            }
            else
            {
                // Glob was destroyed, remove visual
                if (globVisuals[i] != null)
                {
                    DestroyImmediate(globVisuals[i]);
                    globVisuals[i] = null;
                }
                trackedGlobs[i] = Entity.Null;
            }
        }
        
        // Add new globs
        for (int i = 0; i < globEntities.Length; i++)
        {
            var entity = globEntities[i];
            bool alreadyTracked = false;
            
            // Check if we're already tracking this entity
            for (int j = 0; j < globCount; j++)
            {
                if (trackedGlobs[j] == entity)
                {
                    alreadyTracked = true;
                    break;
                }
            }
            
            if (!alreadyTracked && globCount < globVisuals.Length)
            {
                var globComponent = entityManager.GetComponentData<TerrainGlobComponent>(entity);
                var transform = entityManager.GetComponentData<LocalTransform>(entity);
                
                // Create visual GameObject
                var visualGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visualGO.name = $"GlobVisual_{entity.Index}";
                visualGO.transform.position = transform.Position;
                visualGO.transform.localScale = Vector3.one * globComponent.globRadius;
                
                // Set material and color based on terrain type
                var renderer = visualGO.GetComponent<Renderer>();
                if (renderer != null && globMaterial != null)
                {
                    renderer.material = globMaterial;
                    renderer.material.color = GetTerrainColor(globComponent.terrainType);
                }
                
                // Store reference
                globVisuals[globCount] = visualGO;
                trackedGlobs[globCount] = entity;
                globCount++;
                
                Debug.Log($"Created visual for glob {entity.Index} at {transform.Position} with radius {globComponent.globRadius}");
            }
        }
        
        globEntities.Dispose();
    }
    
    private Color GetTerrainColor(TerrainType terrainType)
    {
        return terrainType switch
        {
            TerrainType.Grass => Color.green,
            TerrainType.Sand => new Color(0.9f, 0.8f, 0.6f),
            TerrainType.Rock => Color.gray,
            TerrainType.Snow => Color.white,
            TerrainType.Water => Color.blue,
            _ => Color.red
        };
    }
    
    [ContextMenu("Clear All Visuals")]
    public void ClearAllVisuals()
    {
        for (int i = 0; i < globCount; i++)
        {
            if (globVisuals[i] != null)
            {
                DestroyImmediate(globVisuals[i]);
                globVisuals[i] = null;
            }
        }
        globCount = 0;
        
        Debug.Log("Cleared all glob visuals");
    }
    
    [ContextMenu("Update Visuals Now")]
    public void UpdateVisualsNow()
    {
        UpdateGlobVisuals();
    }
    
    void OnDestroy()
    {
        ClearAllVisuals();
    }
} 