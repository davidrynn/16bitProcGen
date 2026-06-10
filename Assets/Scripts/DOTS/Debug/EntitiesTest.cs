using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Simple test to verify Entities package is working
public class EntitiesTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Entities package test: Starting...");
        
        // Test basic Entities functionality
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            Debug.Log("✅ Entities package is working! World created successfully.");
            
            // Test creating a simple entity
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            
            Debug.Log($"✅ Entity created successfully. Entity ID: {entity.Index}");
            
            // Clean up
            entityManager.DestroyEntity(entity);
        }
        else
        {
            Debug.LogError("❌ Entities package failed to create world!");
        }
    }
} 