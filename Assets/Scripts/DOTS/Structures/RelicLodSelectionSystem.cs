using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using DOTS.Core;

namespace DOTS.Structures
{
    /// <summary>
    /// Per-frame distance-based LOD swap for realized relics. Alternates each
    /// entity's <see cref="MaterialMeshInfo"/> between index 0 (full mesh) and
    /// index 1 (impostor) in the two-entry <see cref="RenderMeshArray"/> written
    /// by <see cref="RelicRealizationSystem"/>.
    ///
    /// Reads per-entity <see cref="RelicLodParams"/> for scale and bounds so each
    /// relic template can have different mesh sizes without the system needing to
    /// know which template an entity uses.
    ///
    /// Shared <see cref="RelicRenderConfig.LodSwapDistance"/> and
    /// <see cref="RelicRenderConfig.LodHysteresis"/> are read once per frame from
    /// the singleton config (camera/scene-level settings, same for all templates).
    ///
    /// Runs in <see cref="PresentationSystemGroup"/> so simulation-side transform
    /// writes are visible before Entities Graphics submits draw calls.
    ///
    /// Class-based <see cref="SystemBase"/> (not ISystem) because it needs
    /// managed access to <see cref="Camera.main"/>. Realized relic count is small
    /// (≤ tens) so a main-thread foreach is inexpensive.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RelicLodSelectionSystem : SystemBase
    {
        /// <summary>
        /// One-shot flag so diagnostics log the very first LOD transition per
        /// session with <c>forceLog: true</c>.
        /// </summary>
        private bool _firstTransitionLogged;

        protected override void OnCreate()
        {
            RequireForUpdate<RelicRenderConfig>();
        }

        protected override void OnUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var config = SystemAPI.ManagedAPI.GetSingleton<RelicRenderConfig>();
            if (config == null)
            {
                return;
            }

            float swap = math.max(0f, config.LodSwapDistance);
            float hyst = math.max(0f, config.LodHysteresis);
            float nearCutoff = math.max(0f, swap - hyst);
            float farCutoff = swap + hyst;
            float nearCutoffSq = nearCutoff * nearCutoff;
            float farCutoffSq = farCutoff * farCutoff;

            float3 cameraPos = camera.transform.position;

            foreach (var (stateRW, xformRW, matMeshInfoRW, renderBoundsRW, lodParams, entity) in SystemAPI
                .Query<RefRW<RelicLodState>, RefRW<LocalTransform>, RefRW<MaterialMeshInfo>, RefRW<RenderBounds>, RefRO<RelicLodParams>>()
                .WithAll<StructureRealizedTag>()
                .WithEntityAccess())
            {
                ref var state = ref stateRW.ValueRW;
                ref var xform = ref xformRW.ValueRW;
                var lod = lodParams.ValueRO;

                float3 delta = cameraPos - xform.Position;
                float dSq = math.dot(delta, delta);

                byte target = ResolveTargetLod(dSq, state.CurrentLod, nearCutoffSq, farCutoffSq);
                if (target == state.CurrentLod)
                {
                    continue;
                }

                // Atomic swap: MaterialMeshInfo → scale → bounds → state.
                matMeshInfoRW.ValueRW = MaterialMeshInfo.FromRenderMeshArrayIndices(target, target);
                xform.Scale = target == 0 ? lod.FullScale : lod.ImpostorScale;
                renderBoundsRW.ValueRW = new RenderBounds
                {
                    Value = target == 0 ? lod.FullBoundsLocal : lod.ImpostorBoundsLocal,
                };

                if (!_firstTransitionLogged)
                {
                    DebugSettings.LogRendering(
                        $"Relic {entity.Index} LOD {state.CurrentLod} → {target} at d={math.sqrt(dSq):0.0}",
                        forceLog: true);
                    _firstTransitionLogged = true;
                }

                state.CurrentLod = target;
            }
        }

        /// <summary>
        /// Pure-static helper mirroring the per-entity swap decision.
        /// Within the hysteresis band the current LOD is preserved.
        /// Factored out for unit testing without a live World.
        /// </summary>
        public static byte ResolveTargetLod(float distanceSq, byte currentLod, float nearCutoffSq, float farCutoffSq)
        {
            if (distanceSq < nearCutoffSq) return 0;
            if (distanceSq > farCutoffSq) return 1;
            return currentLod;
        }
    }
}
