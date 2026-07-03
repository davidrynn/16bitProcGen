using Unity.Entities;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Blob payload holding the constraint set consumed by WFCComponent
    /// </summary>
    public struct WFCConstraintData
    {
        public BlobArray<WFCConstraint> constraints;
        public int constraintCount;
    }
}
