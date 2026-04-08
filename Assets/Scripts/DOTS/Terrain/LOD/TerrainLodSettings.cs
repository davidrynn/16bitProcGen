using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.LOD
{
    public struct TerrainLodSettings : IComponentData
    {
        // Ring thresholds in chunk units (Chebyshev distance).
        // LOD0 when dist <= Lod0MaxDist, LOD1 when <= Lod1MaxDist,
        // LOD2 when <= Lod2MaxDist, else LOD3 (culled) unless mapped to streaming boundary.
        public float Lod0MaxDist;
        public float Lod1MaxDist;
        public float Lod2MaxDist;
        public float HysteresisChunks;

        // Grid settings per LOD level.
        public int3 Lod0Resolution; public float Lod0VoxelSize;
        public int3 Lod1Resolution; public float Lod1VoxelSize;
        public int3 Lod2Resolution; public float Lod2VoxelSize;

        // Policy gates.
        public int ColliderMaxLod;
        public int GrassMaxLod;
        public int ShadowMaxLod;

        // Per-frame rebuild budgets (enforced in existing pipeline systems).
        public int MaxDensityRebuildsPerFrame;
        public int MaxMeshRebuildsPerFrame;
        public int MaxColliderRebuildsPerFrame;

        // If true, chunks outside Lod2 are expected to be culled by streaming despawn.
        // Selection will clamp to LOD2 for loaded chunks.
        public bool UseStreamingAsCullBoundary;

        public static TerrainLodSettings Default => new TerrainLodSettings
        {
            // LOD ring thresholds in chunk units (Chebyshev distance).
            // With StreamingRadius=12: LOD0 covers ~4 chunks, LOD1 ~8, LOD2 ~12.
            Lod0MaxDist = 4f,
            Lod1MaxDist = 8f,
            Lod2MaxDist = 12f,
            HysteresisChunks = 0.5f,
            Lod0Resolution = new int3(16, 16, 16), Lod0VoxelSize = 1f,
            // Keep chunk world footprint invariant: (res-1) * voxel = 15 on all LODs.
            Lod1Resolution = new int3(9, 9, 9),   Lod1VoxelSize = 1.875f,
            Lod2Resolution = new int3(5, 5, 5),   Lod2VoxelSize = 3.75f,
            ColliderMaxLod = 1,
            GrassMaxLod = 0,
            ShadowMaxLod = 1,
            MaxDensityRebuildsPerFrame = 6,
            MaxMeshRebuildsPerFrame = 6,
            MaxColliderRebuildsPerFrame = 4,
            UseStreamingAsCullBoundary = true,
        };

        public int3 GetResolution(int lod)
        {
            if (lod == 0) return Lod0Resolution;
            if (lod == 1) return Lod1Resolution;
            if (lod == 2) return Lod2Resolution;
            return Lod0Resolution;
        }

        public float GetVoxelSize(int lod)
        {
            if (lod == 0) return Lod0VoxelSize;
            if (lod == 1) return Lod1VoxelSize;
            if (lod == 2) return Lod2VoxelSize;
            return Lod0VoxelSize;
        }

        public float GetFootprintX(int lod)
        {
            var resolution = GetResolution(lod);
            return math.max(0, resolution.x - 1) * GetVoxelSize(lod);
        }

        public float GetFootprintZ(int lod)
        {
            var resolution = GetResolution(lod);
            return math.max(0, resolution.z - 1) * GetVoxelSize(lod);
        }

        public bool HasInvariantFootprint(float tolerance = 0.001f)
        {
            var lod0X = GetFootprintX(0);
            var lod0Z = GetFootprintZ(0);

            return math.abs(GetFootprintX(1) - lod0X) <= tolerance
                && math.abs(GetFootprintX(2) - lod0X) <= tolerance
                && math.abs(GetFootprintZ(1) - lod0Z) <= tolerance
                && math.abs(GetFootprintZ(2) - lod0Z) <= tolerance;
        }
    }
}
