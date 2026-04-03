using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Rendering
{
    /// <summary>
    /// Managed component that owns the GPU-side resources for one chunk's grass blades.
    ///
    /// Stored as a class-based IComponentData (managed component) because
    /// <see cref="GraphicsBuffer"/> is a GPU resource that cannot live in a blittable struct.
    ///
    /// Lifecycle:
    ///   Created/replaced by <see cref="GrassChunkGenerationSystem"/> after a rebuild.
    ///   Disposed in the same system when the buffer is replaced or the entity is destroyed.
    /// </summary>
    public class GrassChunkBladeBuffer : IComponentData, System.IDisposable
    {
        /// <summary>
        /// Structured buffer of <see cref="GrassBladeData"/> instances, stride 32 bytes.
        /// Bound to the GrassBlades shader as <c>StructuredBuffer&lt;GrassBladeData&gt; _BladeBuffer</c>.
        /// </summary>
        public GraphicsBuffer BladeBuffer;

        /// <summary>
        /// Indirect draw arguments buffer (5 × uint):
        ///   [0] indexCountPerInstance, [1] instanceCount, [2] startIndex, [3] baseVertex, [4] startInstance
        /// </summary>
        public GraphicsBuffer ArgsBuffer;

        /// <summary>Number of blade instances in <see cref="BladeBuffer"/>.</summary>
        public int BladeCount;

        public void Dispose()
        {
            BladeBuffer?.Dispose();
            BladeBuffer = null;
            ArgsBuffer?.Dispose();
            ArgsBuffer = null;
            BladeCount = 0;
        }
    }
}
