using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

namespace DOTS.Terrain
{
    /// <summary>
    /// [LEGACY] Core terrain data component for the legacy DOTS terrain system.
    /// Contains all necessary data for terrain generation, modification, and rendering.
    /// Note: ComputeBuffer fields removed - they will be managed externally.
    /// 
    /// ⚠️ LEGACY COMPONENT: This component is part of the legacy terrain system.
    /// The current active terrain system uses SDF (Signed Distance Fields) with components in DOTS.Terrain namespace:
    /// - DOTS.Terrain.TerrainChunk (replaces this component)
    /// - TerrainChunkGridInfo, TerrainChunkBounds, TerrainChunkDensity, TerrainChunkMeshData
    /// 
    /// This component is maintained for backward compatibility with existing tests and legacy code.
    /// </summary>
    public struct TerrainData : IComponentData
    {
        public int2 chunkPosition;                                    // Position of this terrain chunk
        public int resolution;                                        // Resolution of the terrain grid
        public float worldScale;                                      // Scale of the world units
        public float averageHeight;                                   // Average height of this terrain chunk
        public BlobAssetReference<TerrainHeightData> heightData;     // Height and terrain type data
        public BlobAssetReference<TerrainModificationData> modifications; // Modification history
        public bool needsGeneration;                                 // Flag indicating if terrain needs generation
        public bool needsModification;                               // Flag indicating if modifications need processing
        public bool needsMeshUpdate;                                 // Flag indicating if mesh needs to be rebuilt after modification
        
    }
} 