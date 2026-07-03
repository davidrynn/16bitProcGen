namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Implemented by scatter placement records (trees, rocks, future families) so the
    /// shared delta utility can remove/sort records by their deterministic candidate-slot
    /// identity without knowing the concrete record type. Explicit implementations keep
    /// the struct field layout untouched (interfaces add no data).
    /// </summary>
    public interface IStableLocalIdRecord
    {
        ushort StableLocalId { get; }
    }
}
