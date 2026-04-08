using Unity.Entities;

namespace DOTS.Terrain.LOD
{
    public struct TerrainChunkLodState : IComponentData
    {
        public int CurrentLod;
        public int TargetLod;
        public uint LastSwitchFrame;
    }
}
