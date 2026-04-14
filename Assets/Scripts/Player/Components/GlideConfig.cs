using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tunable constants for glide mechanics, set once at bootstrap.
    /// </summary>
    public struct GlideConfig : IComponentData
    {
        public float GlideChargeTime;            // 0.45 seconds hold to deploy
        public float MinGlideHeight;             // 6 m — below this, glide cannot activate
        public float GlideFallSpeed;             // -5.5 — target vertical velocity during glide
        public float GlideForwardPreservation;   // 0.96 — horizontal speed multiplier per frame
        public float AirControlGlide;            // 0.35 — steering rate during glide
        public float MaxGlideDuration;           // 9 seconds — auto-cancel safety

        public static GlideConfig Default => new GlideConfig
        {
            GlideChargeTime = 0.45f,
            MinGlideHeight = 6f,
            GlideFallSpeed = -5.5f,
            GlideForwardPreservation = 0.96f,
            AirControlGlide = 0.35f,
            MaxGlideDuration = 9f
        };
    }
}
