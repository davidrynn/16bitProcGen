using Unity.Physics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

// Test to verify Unity Physics package is working
public class PhysicsTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Unity Physics package test: Starting...");

        // Test basic physics components
        TestPhysicsComponents();

        // Test collision detection
        TestCollisionDetection();

        // Test physics materials
        TestPhysicsMaterials();
    }

    private void TestPhysicsComponents()
    {
        Debug.Log("Testing physics components...");

        // Test creating physics components
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            var entityManager = world.EntityManager;

            // Create an entity with physics components
            var entity = entityManager.CreateEntity();

            // Add physics components
            entityManager.AddComponent<PhysicsWorldIndex>(entity);
            entityManager.AddComponent<LocalTransform>(entity);

            // Add collision filter as a shared component
            var collisionFilter = new CollisionFilterSharedComponent
            {
                Filter = CollisionFilter.Default
            };
            entityManager.AddSharedComponent(entity, collisionFilter);

            Debug.Log("✅ Physics components added successfully");

            // Clean up
            entityManager.DestroyEntity(entity);
        }
        else
        {
            Debug.LogError("❌ World not available for physics test");
        }
    }

    private void TestCollisionDetection()
    {
        Debug.Log("Testing collision detection...");

        // Test collision filter creation
        var filter1 = new CollisionFilter
        {
            BelongsTo = 1u << 0,  // Layer 0
            CollidesWith = 1u << 1,  // Collides with layer 1
            GroupIndex = 0
        };

        var filter2 = new CollisionFilter
        {
            BelongsTo = 1u << 1,  // Layer 1
            CollidesWith = 1u << 0,  // Collides with layer 0
            GroupIndex = 0
        };

        // Test if filters can collide
        bool canCollide = CollisionFilter.IsCollisionEnabled(filter1, filter2);
        Debug.Log($"✅ Collision enabled between filters: {canCollide}");

        // Test collision filter with itself
        bool selfCollide = CollisionFilter.IsCollisionEnabled(filter1, filter1);
        Debug.Log($"✅ Self-collision enabled: {selfCollide}");
    }

    private void TestPhysicsMaterials()
    {
        Debug.Log("Testing physics materials...");

        // Test material creation with explicit enum values
        var material = new Unity.Physics.Material
        {
            Friction = 0.5f,
            Restitution = 0.3f,
            FrictionCombinePolicy = Unity.Physics.Material.CombinePolicy.ArithmeticMean,
            RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.ArithmeticMean
        };

        Debug.Log($"✅ Physics material created - Friction: {material.Friction}, Restitution: {material.Restitution}");

        // Test different combine policies
        var material1 = new Unity.Physics.Material
        {
            Friction = 0.2f,
            Restitution = 0.8f,
            FrictionCombinePolicy = Unity.Physics.Material.CombinePolicy.Minimum,
            RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum
        };

        Debug.Log($"✅ Physics material with different policies created");
    }
}

// Define a shared component wrapper for CollisionFilter
public struct CollisionFilterSharedComponent : ISharedComponentData
{
    public CollisionFilter Filter;
}

// Extension method to help with physics testing
public static class PhysicsTestExtensions
{
    public static void AddPhysicsComponents(this EntityManager entityManager, Entity entity)
    {
        // Add basic physics components
        entityManager.AddComponent<PhysicsWorldIndex>(entity);
        entityManager.AddComponent<LocalTransform>(entity);

        // Add collision filter as a shared component
        var collisionFilter = new CollisionFilterSharedComponent
        {
            Filter = CollisionFilter.Default
        };
        entityManager.AddSharedComponent(entity, collisionFilter);
    }
}
