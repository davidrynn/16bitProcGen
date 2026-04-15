using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Manages glide conversion: space hold during Ballistic above MinGlideHeight
    /// transitions through GlideCharging → Gliding. During Gliding, vertical velocity
    /// is clamped toward GlideFallSpeed, horizontal speed decays per frame, and air
    /// control uses the glide-specific rate.
    /// </summary>
    /// <remarks>
    /// Tracks charge hold time internally rather than adding input fields, per spec:
    /// "GlideSystem tracks hold duration internally using JumpPressed state."
    /// Runs after PlayerMovementSystem so velocity writes don't conflict.
    /// </remarks>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    public partial struct GlideSystem : ISystem
    {
        /// <summary>
        /// Accumulated time space has been held during Ballistic. Reset on release or
        /// mode change. Single-player assumption — same as other movement systems.
        /// </summary>
        private float _chargeHoldTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GlideConfig>();
            state.RequireForUpdate<PlayerMovementState>();
            _chargeHoldTime = 0f;
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (movementState, input, glideConfig, velocity, transform, entity) in
                     SystemAPI.Query<RefRW<PlayerMovementState>, RefRO<PlayerInputComponent>,
                                     RefRO<GlideConfig>, RefRW<PhysicsVelocity>,
                                     RefRO<LocalTransform>>()
                             .WithEntityAccess())
            {
                var mode = movementState.ValueRO.Mode;
                bool hasGlideState = SystemAPI.HasComponent<GlideState>(entity);
                bool spaceHeld = input.ValueRO.JumpHeld;
                float height = transform.ValueRO.Position.y;
                var cfg = glideConfig.ValueRO;

                switch (mode)
                {
                    case PlayerMovementMode.Ballistic:
                        HandleBallistic(ref movementState.ValueRW, spaceHeld, height, cfg, dt);
                        break;

                    case PlayerMovementMode.GlideCharging:
                        HandleGlideCharging(ref movementState.ValueRW, spaceHeld, height, cfg, dt, entity, ecb);
                        break;

                    case PlayerMovementMode.Gliding:
                        HandleGliding(ref state, ref movementState.ValueRW, ref velocity.ValueRW, cfg, dt, entity, hasGlideState, ecb);
                        break;

                    default:
                        // Not in a glide-relevant mode — reset charge timer and clean up
                        _chargeHoldTime = 0f;
                        if (hasGlideState)
                        {
                            ecb.RemoveComponent<GlideState>(entity);
                        }
                        break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void HandleBallistic(
            ref PlayerMovementState movementState,
            bool spaceHeld,
            float height,
            in GlideConfig cfg,
            float dt)
        {
            if (spaceHeld && height >= cfg.MinGlideHeight)
            {
                _chargeHoldTime += dt;
                if (_chargeHoldTime >= cfg.GlideChargeTime)
                {
                    // Charge complete — transition to GlideCharging as a brief vulnerability
                    // window before full glide deploys. For simplicity, transition directly
                    // through GlideCharging into Gliding on the next frame.
                    movementState.Mode = PlayerMovementMode.GlideCharging;
                }
            }
            else
            {
                _chargeHoldTime = 0f;
            }
        }

        private void HandleGlideCharging(
            ref PlayerMovementState movementState,
            bool spaceHeld,
            float height,
            in GlideConfig cfg,
            float dt,
            Entity entity,
            EntityCommandBuffer ecb)
        {
            if (!spaceHeld)
            {
                // Space released during charge window — cancel back to Ballistic
                movementState.Mode = PlayerMovementMode.Ballistic;
                _chargeHoldTime = 0f;
                return;
            }

            if (height < cfg.MinGlideHeight)
            {
                // Dropped below minimum height during charge — cancel
                movementState.Mode = PlayerMovementMode.Ballistic;
                _chargeHoldTime = 0f;
                return;
            }

            // Deploy glide
            movementState.Mode = PlayerMovementMode.Gliding;
            _chargeHoldTime = 0f;
            ecb.AddComponent(entity, new GlideState
            {
                GlideElapsed = 0f,
                HorizonBlendProgress = 0f
            });
        }

        private void HandleGliding(
            ref SystemState state,
            ref PlayerMovementState movementState,
            ref PhysicsVelocity velocity,
            in GlideConfig cfg,
            float dt,
            Entity entity,
            bool hasGlideState,
            EntityCommandBuffer ecb)
        {
            // Track glide duration for auto-cancel
            float elapsed = 0f;
            if (hasGlideState)
            {
                var gs = SystemAPI.GetComponent<GlideState>(entity);
                elapsed = gs.GlideElapsed + dt;
                ecb.SetComponent(entity, new GlideState
                {
                    GlideElapsed = elapsed,
                    HorizonBlendProgress = math.saturate(elapsed / 2f) // blend over 2 seconds
                });
            }

            // Auto-cancel after max duration
            if (elapsed >= cfg.MaxGlideDuration)
            {
                movementState.Mode = PlayerMovementMode.Ballistic;
                if (hasGlideState)
                    ecb.RemoveComponent<GlideState>(entity);
                return;
            }

            // Clamp vertical velocity toward GlideFallSpeed (gentle descent, not instant snap)
            float targetVY = cfg.GlideFallSpeed;
            float verticalLerp = math.saturate(3f * dt); // smooth transition to glide fall rate
            velocity.Linear.y = math.lerp(velocity.Linear.y, targetVY, verticalLerp);

            // Apply horizontal decay each frame
            velocity.Linear.x *= cfg.GlideForwardPreservation;
            velocity.Linear.z *= cfg.GlideForwardPreservation;
        }
    }
}
