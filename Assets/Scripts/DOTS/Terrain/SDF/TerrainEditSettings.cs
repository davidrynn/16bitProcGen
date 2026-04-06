using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    public enum TerrainEditPlacementMode : byte
    {
        FreeSphere = 0,
        SnappedCube = 1
    }

    public enum TerrainEditSnapSpace : byte
    {
        Global = 0,
        ChunkLocal = 1
    }

    /// <summary>
    /// Runtime terrain edit settings consumed by unmanaged systems.
    /// </summary>
    public struct TerrainEditSettings : IComponentData
    {
        public TerrainEditPlacementMode PlacementMode;
        public TerrainEditSnapSpace SnapSpace;
        public float EditCellFraction;
        public float3 GlobalSnapAnchor;
        public int CubeDepthCells;

        public static TerrainEditSettings Default => new TerrainEditSettings
        {
            PlacementMode = TerrainEditPlacementMode.SnappedCube,
            SnapSpace = TerrainEditSnapSpace.Global,
            EditCellFraction = 0.25f,
            GlobalSnapAnchor = float3.zero,
            CubeDepthCells = 1
        };

        public static TerrainEditSettings Clamp(in TerrainEditSettings settings)
        {
            var result = settings;
            result.EditCellFraction = math.clamp(result.EditCellFraction, 0.25f, 1f);
            result.CubeDepthCells = math.max(1, result.CubeDepthCells);
            return result;
        }

        public static TerrainEditSettings FromValues(
            TerrainEditPlacementMode placementMode,
            TerrainEditSnapSpace snapSpace,
            float editCellFraction,
            float anchorX,
            float anchorY,
            float anchorZ,
            int cubeDepthCells)
        {
            var settings = Default;
            settings.PlacementMode = placementMode;
            settings.SnapSpace = snapSpace;
            settings.EditCellFraction = editCellFraction;
            settings.GlobalSnapAnchor = new float3(anchorX, anchorY, anchorZ);
            settings.CubeDepthCells = cubeDepthCells;
            return Clamp(in settings);
        }
    }
}
