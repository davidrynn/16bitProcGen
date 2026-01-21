using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Authoring component to enable terrain debug mode in a scene.
    /// Creates TerrainDebugConfig singleton at runtime.
    /// </summary>
    public class TerrainDebugConfigAuthoring : MonoBehaviour
    {
        [Header("Debug Control")]
        [Tooltip("Master switch for debug behavior")]
        public bool Enabled = false;

        [Header("Streaming Control")]
        [Tooltip("If true, freeze streaming at FixedCenterChunk instead of following player")]
        public bool FreezeStreaming = false;

        [Tooltip("Center chunk coordinate when FreezeStreaming is true")]
        public Vector2Int FixedCenterChunk = Vector2Int.zero;

        [Tooltip("Radius in chunks around center when FreezeStreaming is true")]
        public int StreamingRadiusInChunks = 2;

        [Header("Seam Validation")]
        [Tooltip("Maximum allowed density difference at chunk borders")]
        public float SeamEpsilon = 0.001f;

        [Tooltip("Log seam mismatches to Console")]
        public bool EnableSeamLogging = true;

        class Baker : Baker<TerrainDebugConfigAuthoring>
        {
            public override void Bake(TerrainDebugConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new TerrainDebugConfig
                {
                    Enabled = authoring.Enabled,
                    FreezeStreaming = authoring.FreezeStreaming,
                    FixedCenterChunk = new int2(authoring.FixedCenterChunk.x, authoring.FixedCenterChunk.y),
                    StreamingRadiusInChunks = authoring.StreamingRadiusInChunks,
                    SeamEpsilon = authoring.SeamEpsilon,
                    EnableSeamLogging = authoring.EnableSeamLogging
                });
            }
        }
    }
}
