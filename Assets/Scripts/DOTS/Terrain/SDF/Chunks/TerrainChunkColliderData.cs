using Unity.Entities;
using Unity.Physics;

namespace DOTS.Terrain
{
    public struct TerrainChunkColliderData : IComponentData
    {
        public BlobAssetReference<Collider> Collider;

        public bool IsCreated => Collider.IsCreated;

        public void Dispose()
        {
            if (Collider.IsCreated)
            {
                Collider.Dispose();
                Collider = default;
            }
        }
    }
}
