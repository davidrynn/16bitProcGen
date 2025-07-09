using Unity.Mathematics;

/// <summary>
/// Represents a single modification to the terrain
/// </summary>
public struct TerrainModification
{
    public int2 position;           // 2D position of the modification
    public float originalHeight;    // Height before modification
    public float newHeight;         // Height after modification
    public float modificationTime;  // Time when modification occurred
    public ModificationType type;   // Type of modification
} 