namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Minimal persistent rock lifecycle stages for seeded rocks.
    /// </summary>
    public enum RockStateStage : byte
    {
        Intact = 0,
        Cracked = 1,
        Depleted = 2,
    }
}
