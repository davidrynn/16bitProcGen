using Unity.Entities;
using Unity.Collections;

/// <summary>
/// Blob asset structure for storing terrain modification history
/// </summary>
namespace DOTS.Terrain.Legacy
{
    public struct TerrainModificationData
    {
        public BlobArray<TerrainModification> modifications; // List of all modifications
        public float lastModificationTime;                   // Time of the most recent modification
    } 
}
