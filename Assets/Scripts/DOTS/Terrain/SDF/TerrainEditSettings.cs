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
        public bool EnablePlayerOverlapGuard;
        public float PlayerEditClearance;
        public bool LockChunkLocalSnap;

        public static TerrainEditSettings Default => new TerrainEditSettings
        {
            PlacementMode = TerrainEditPlacementMode.SnappedCube,
            SnapSpace = TerrainEditSnapSpace.ChunkLocal,
            EditCellFraction = 0.25f,
            GlobalSnapAnchor = float3.zero,
            CubeDepthCells = 1,
            EnablePlayerOverlapGuard = true,
            PlayerEditClearance = 0.15f,
            LockChunkLocalSnap = true
        };

        public static TerrainEditSettings Clamp(in TerrainEditSettings settings)
        {
            var result = settings;
            result.EditCellFraction = math.clamp(result.EditCellFraction, 0.25f, 1f);
            result.CubeDepthCells = math.max(1, result.CubeDepthCells);
            result.PlayerEditClearance = math.max(0f, result.PlayerEditClearance);
            if (result.LockChunkLocalSnap)
            {
                result.SnapSpace = TerrainEditSnapSpace.ChunkLocal;
            }
            return result;
        }

        public static TerrainEditSettings FromValues(
            TerrainEditPlacementMode placementMode,
            TerrainEditSnapSpace snapSpace,
            float editCellFraction,
            float anchorX,
            float anchorY,
            float anchorZ,
            int cubeDepthCells,
            bool enablePlayerOverlapGuard,
            float playerEditClearance,
            bool lockChunkLocalSnap)
        {
            var settings = Default;
            settings.PlacementMode = placementMode;
            settings.SnapSpace = snapSpace;
            settings.EditCellFraction = editCellFraction;
            settings.GlobalSnapAnchor = new float3(anchorX, anchorY, anchorZ);
            settings.CubeDepthCells = cubeDepthCells;
            settings.EnablePlayerOverlapGuard = enablePlayerOverlapGuard;
            settings.PlayerEditClearance = playerEditClearance;
            settings.LockChunkLocalSnap = lockChunkLocalSnap;
            return Clamp(in settings);
        }
    }
}
