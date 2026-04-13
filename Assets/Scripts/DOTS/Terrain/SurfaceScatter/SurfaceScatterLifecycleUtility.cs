using Unity.Entities;

namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Shared lifecycle helpers for chunk-scoped surface scatter families.
    /// These methods intentionally cover only common tag/buffer bookkeeping.
    /// </summary>
    public static class SurfaceScatterLifecycleUtility
    {
        public static DynamicBuffer<TRecord> SetOrAddPlacementBuffer<TRecord>(
            Entity entity,
            ref BufferLookup<TRecord> placementLookup,
            ref EntityCommandBuffer ecb)
            where TRecord : unmanaged, IBufferElementData
        {
            return placementLookup.HasBuffer(entity)
                ? ecb.SetBuffer<TRecord>(entity)
                : ecb.AddBuffer<TRecord>(entity);
        }

        public static void SetOrAddGenerationTag<TTag>(
            Entity entity,
            in TTag value,
            ref ComponentLookup<TTag> tagLookup,
            ref EntityCommandBuffer ecb)
            where TTag : unmanaged, IComponentData
        {
            if (tagLookup.HasComponent(entity))
            {
                ecb.SetComponent(entity, value);
            }
            else
            {
                ecb.AddComponent(entity, value);
            }
        }

        public static void RemovePlacementStateIfPresent<TRecord, TTag>(
            Entity entity,
            ref BufferLookup<TRecord> placementLookup,
            ref ComponentLookup<TTag> tagLookup,
            ref EntityCommandBuffer ecb)
            where TRecord : unmanaged, IBufferElementData
            where TTag : unmanaged, IComponentData
        {
            if (placementLookup.HasBuffer(entity))
            {
                ecb.RemoveComponent<TRecord>(entity);
            }

            if (tagLookup.HasComponent(entity))
            {
                ecb.RemoveComponent<TTag>(entity);
            }
        }
    }
}
