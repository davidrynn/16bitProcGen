using Unity.Entities;
using Unity.Mathematics;

// Enum for different glob removal types
public enum GlobRemovalType
{
    Small = 0,   // 1x1 size
    Medium = 1,  // 2x2 size
    Large = 2    // 3x3 size
}

public struct PlayerModificationComponent : IComponentData
{
    // Basic modification parameters
    public float3 position;
    public float radius;
    public float strength;
    public int resolution;
    
    // Glob-specific parameters
    public GlobRemovalType removalType;
    public float maxDepth;
    public bool allowUnderground;
    
    // Tool-specific parameters
    public float toolEfficiency; // How effective the tool is (0.0 to 1.0)
    public bool isMiningTool;    // Whether this is a mining tool (can dig underground)
} 