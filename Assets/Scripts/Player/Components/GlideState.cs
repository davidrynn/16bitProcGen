using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tracks active glide. Added when glide deploys, removed on landing.
    /// </summary>
    public struct GlideState : IComponentData
    {
        public float GlideElapsed;          // seconds since glide activated
        public float HorizonBlendProgress;  // 0..1, camera horizon stabilization progress
    }
}
