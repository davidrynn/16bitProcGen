using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTS.Terrain.Modification
{
    /// <summary>
    /// Represents a terrain glob that has been removed from the terrain
    /// This component is added to entities that represent the actual glob objects
    /// that players can interact with, collect, or that have physics behavior
    /// </summary>
    public struct TerrainGlobComponent : IComponentData
    {
        // Basic glob properties
        public float3 originalPosition;        // Position where glob was removed from terrain
        public float3 currentPosition;         // Current position of the glob entity
        public float globRadius;               // Size of the glob (1.0f to 3.0f)
        public GlobRemovalType globType;       // Type of glob (Small, Medium, Large)
        public TerrainType terrainType;        // Type of terrain this glob was made from
        
        // Physics properties
        public float3 velocity;                // Current velocity of the glob
        public float3 angularVelocity;         // Angular velocity for rotation
        public float mass;                     // Mass of the glob (affects physics)
        public float bounciness;               // How bouncy the glob is (0.0f to 1.0f)
        public float friction;                 // Friction coefficient (0.0f to 1.0f)
        
        // State flags
        public bool isGrounded;                // Whether the glob is on the ground
        public bool isCollected;               // Whether the glob has been collected by player
        public bool isDestroyed;               // Whether the glob should be destroyed
        public float lifetime;                 // How long the glob has existed
        
        // Collection properties
        public float collectionRadius;         // Radius within which player can collect
        public bool canBeCollected;            // Whether this glob can be collected
        public int resourceValue;              // Resource value when collected
        
        // Visual properties
        public float3 scale;                   // Visual scale of the glob
        public quaternion rotation;            // Current rotation of the glob
        public float visualAlpha;              // Alpha value for fading effects
    }
    
    /// <summary>
    /// Component for globs that should have physics behavior
    /// This is a separate component to allow for optional physics
    /// </summary>
    public struct TerrainGlobPhysicsComponent : IComponentData
    {
        public bool enablePhysics;             // Whether physics is enabled for this glob
        public float gravityScale;             // Gravity scale for this glob
        public float dragCoefficient;          // Air resistance coefficient
        public float maxVelocity;              // Maximum velocity limit
        public float maxAngularVelocity;       // Maximum angular velocity limit
        
        // Collision properties
        public float collisionRadius;          // Collision detection radius
        public bool collideWithTerrain;        // Whether to collide with terrain
        public bool collideWithOtherGlobs;     // Whether to collide with other globs
        public bool collideWithPlayer;         // Whether to collide with player
    }
    
    /// <summary>
    /// Component for globs that should be rendered
    /// This is a separate component to allow for optional rendering
    /// </summary>
    public struct TerrainGlobRenderComponent : IComponentData
    {
        public bool enableRendering;           // Whether rendering is enabled
        public float3 meshScale;               // Scale of the rendered mesh
        public int meshVariant;                // Which mesh variant to use
        public float4 color;                   // Tint color for the glob
        public bool useTerrainColor;           // Whether to use terrain-based coloring
    }
} 