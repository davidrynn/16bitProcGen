using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tracks the active slingshot chain window. Added to the player entity at bootstrap.
    /// WindowRemaining counts down each frame; when it reaches zero the chain sequence resets.
    /// </summary>
    public struct ChainSlingshotState : IComponentData
    {
        /// <summary>Seconds until the chain opportunity expires. 0 = no active chain.</summary>
        public float WindowRemaining;

        /// <summary>
        /// Number of slingshot launches fired in the current chain.
        /// 0 = next launch is the first (normal impulse).
        /// 1+ = additive bonus multiplier applies.
        /// </summary>
        public int ChainCount;
    }
}
