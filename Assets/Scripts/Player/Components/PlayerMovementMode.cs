namespace DOTS.Player.Components
{
    public enum PlayerMovementMode : byte
    {
        Grounded = 0,
        SlingshotCharging = 1,
        Ballistic = 2,
        GlideCharging = 3,
        Gliding = 4,
        ThermalBoost = 5,
        Grappling = 6        // Layer 2, post-MVP
    }
}
