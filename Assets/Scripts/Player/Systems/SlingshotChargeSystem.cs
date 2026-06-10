using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using DOTS.Player.Components;
using DOTS.Terrain.Core;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Manages slingshot charge accumulation while the player holds LMB+RMB.
    /// Charging is allowed when grounded OR when a chain window is active (mid-air chain entry).
    /// Adds SlingshotChargeState when charge begins, computes ChargeNormalized using
    /// a power curve, and removes the component on cancel.
    /// </summary>
    /// <remarks>
    /// Uses single-component query + manual GetComponent to work around a known Unity
    /// source-generator caching issue where multi-component queries can return empty
    /// results after struct layout changes.
    /// </remarks>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PlayerGroundingSystem))]
    [UpdateBefore(typeof(SlingshotLaunchSystem))]
    public partial struct SlingshotChargeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementState>();
            state.RequireForUpdate<SlingshotConfig>();
            state.RequireForUpdate<PlayerInputComponent>();
            state.RequireForUpdate<PlayerViewComponent>();
        }

        /// <remarks>
        /// WithoutBurst: Burst silently breaks the .WithAll filter + GetComponentRO
        /// pattern used to work around the source-generator query caching issue.
        /// Runs once per frame on a single entity so managed overhead is negligible.
        /// </remarks>
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (movementState, entity) in
                     SystemAPI.Query<RefRW<PlayerMovementState>>()
                             .WithAll<PlayerInputComponent, PlayerViewComponent, SlingshotConfig>()
                             .WithEntityAccess())
            {
                var input = SystemAPI.GetComponentRO<PlayerInputComponent>(entity);
                var view = SystemAPI.GetComponentRO<PlayerViewComponent>(entity);
                var slingshotConfig = SystemAPI.GetComponentRO<SlingshotConfig>(entity);

                bool hasChargeState = SystemAPI.HasComponent<SlingshotChargeState>(entity);
                var mode = movementState.ValueRO.Mode;

                // Gate on IsGrounded rather than Mode == Grounded. PlayerGroundingSystem keeps
                // Mode = Ballistic while speed > 2 m/s even after ground contact, so checking Mode
                // would silently block charging for several seconds after landing at high speed.
                // Landing recovery suppresses starting a new charge but not continuing one already
                // in progress (SlingshotCharging mode is already past the start gate).
                bool inLandingRecovery = movementState.ValueRO.LandingRecoveryTime > 0f;
                bool canCharge = (movementState.ValueRO.IsGrounded && !inLandingRecovery) ||
                                 mode == PlayerMovementMode.SlingshotCharging;
                bool slingshotHeld = input.ValueRO.SlingshotHeld;

                if (slingshotHeld && canCharge)
                {
                    // Begin or continue charging.
                    // Only downward mouse movement (pull-back) charges the slingshot.
                    // Mouse down = negative Y delta, so accumulated SlingshotDrag.y is negative
                    // when pulling back. We negate and clamp to get the pull-back distance.
                    float2 dragDelta = input.ValueRO.SlingshotDrag;
                    float pullBack = math.max(0f, -dragDelta.y);
                    float dragNormalized = math.saturate(pullBack / slingshotConfig.ValueRO.MaxDragDistance);
                    float charge = math.pow(dragNormalized, slingshotConfig.ValueRO.CurveExponent);

                    // Aim direction: always forward (camera yaw at charge time) with an upward
                    // arc proportional to charge power. Drag magnitude determines power only —
                    // directional steering via drag was removed because it caused the trajectory
                    // preview to swing unpredictably with small mouse movements.
                    float yawRadians = math.radians(view.ValueRO.YawDegrees);
                    float3 camForward = new float3(math.sin(yawRadians), 0f, math.cos(yawRadians));

                    // Higher charge → steeper launch arc (up to ~30° at full charge)
                    float upComponent = dragNormalized * 0.55f;
                    float3 aimDirection = math.normalizesafe(camForward + new float3(0f, upComponent, 0f));

                    var chargeState = new SlingshotChargeState
                    {
                        ChargeNormalized = charge,
                        DragDelta = dragDelta,
                        AimDirection = aimDirection,
                        ChargeStartTime = hasChargeState
                            ? SystemAPI.GetComponent<SlingshotChargeState>(entity).ChargeStartTime
                            : elapsedTime
                    };

                    if (!hasChargeState)
                    {
                        ecb.AddComponent(entity, chargeState);
                    }
                    else
                    {
                        ecb.SetComponent(entity, chargeState);
                    }

                    movementState.ValueRW.Mode = PlayerMovementMode.SlingshotCharging;
                }
                else if (hasChargeState && !input.ValueRO.SlingshotReleased)
                {
                    // Cancel: slingshot released without explicit release event, or player left ground.
                    // Chain window is intentionally not consumed on cancel.
                    ecb.RemoveComponent<SlingshotChargeState>(entity);
                    if (movementState.ValueRO.Mode == PlayerMovementMode.SlingshotCharging)
                    {
                        movementState.ValueRW.Mode = PlayerMovementMode.Grounded;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
