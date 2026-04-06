using Unity.Entities;

namespace DOTS.Terrain
{
    public struct TerrainColliderSettings : IComponentData
    {
        public bool Enabled;
        /// <summary>
        /// Max terrain mesh collider builds per frame. If zero or negative, defaults to 4 (BUG-011 tuning).
        /// </summary>
        public int MaxCollidersPerFrame;
        /// <summary>
        /// Layer 3 trial (BUG-011): enable Unity Physics detailed static mesh contacts
        /// on terrain mesh colliders to reduce ghost/unstable contact behavior.
        /// </summary>
        public bool EnableDetailedStaticMeshCollision;
    }
}
