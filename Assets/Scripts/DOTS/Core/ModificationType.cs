using System;

/// <summary>
/// Defines the different types of terrain modifications that can occur
/// </summary>
public enum ModificationType
{
    PlayerDig,           // Player-initiated terrain destruction
    WeatherErosion,      // Weather-based terrain erosion
    StructurePlacement,  // Placement of structures that modify terrain
    NaturalProcess       // Natural geological processes
} 