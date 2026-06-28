using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Detects the frame a player transitions from airborne to grounded and fires a one-frame
    /// LandingImpactEvent with speed and contact-height data. Also writes LandingRecoveryTime
    /// and LandingRecoveryDuration to PlayerMovementState for input gating and animation blending.
    /// </summary>
    /// <remarks>
    /// Fires on the IsGrounded edge (false → true) rather than the Mode edge. The Mode edge
    /// lagged by several frames on high-speed slingshot landings because PlayerGroundingSystem
    /// holds Mode = Ballistic while speed is above 2 m/s, even after ground contact.
    /// </remarks>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementStateBookkeepingSystem))]
    public partial struct LandingDetectionSystem : ISystem
    {
        /// <summary>Upward speed (m/s) above which a grounded edge is treated as a launch, not a landing.</summary>
        private const float MaxUpwardSpeedForLanding = 0.5f;

        private bool _previousIsGrounded;
        private float3 _previousVelocity;
        private bool _eventWasFiredLastFrame;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementState>();
            _previousIsGrounded = true;
            _previousVelocity = float3.zero;
            _eventWasFiredLastFrame = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (movementState, transform, entity) in
                     SystemAPI.Query<RefRW<PlayerMovementState>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                bool hasEvent = SystemAPI.HasComponent<LandingImpactEvent>(entity);

                // Disable the one-frame event from the previous frame.
                if (_eventWasFiredLastFrame && hasEvent)
                {
                    SystemAPI.SetComponentEnabled<LandingImpactEvent>(entity, false);
                    _eventWasFiredLastFrame = false;
                }

                bool currentIsGrounded = movementState.ValueRO.IsGrounded;

                // A landing requires non-upward motion. PlayerGroundingSystem can re-ground
                // the player on the frame after a slingshot launch (the probe still hits the
                // ground before the character rises clear), and that false→true edge fired a
                // phantom hard-landing event at takeoff — yanking the animator through
                // Landing → Idle mid-flight. Small positive tolerance keeps slope/step
                // landings, where the solver can leave a slight upward component.
                //
                // Both frames must be checked: MovementStateBookkeepingSystem syncs
                // PhysicsVelocity → MovementState.Velocity at the START of each frame, so on
                // the release frame _previousVelocity captures the stale pre-launch value
                // (~0) — only the current frame's Velocity carries the launch impulse when
                // the re-ground edge lands one frame after release.
                bool isUpwardLaunch =
                    math.max(_previousVelocity.y, movementState.ValueRO.Velocity.y) > MaxUpwardSpeedForLanding;

                if (!_previousIsGrounded && currentIsGrounded && !isUpwardLaunch)
                {
                    float verticalSpeed   = math.abs(_previousVelocity.y);
                    float horizontalSpeed = math.length(new float2(_previousVelocity.x, _previousVelocity.z));
                    float groundContactY  = transform.ValueRO.Position.y;

                    // LandingRecoveryTime/Duration and LandingIsSlide are written by
                    // PlayerGroundingSystem in PhysicsSystemGroup so input gating applies on
                    // the same physics tick as touchdown. This system only fires the
                    // LandingImpactEvent for camera and VFX consumers in SimulationSystemGroup.

                    if (hasEvent)
                    {
                        SystemAPI.SetComponent(entity, new LandingImpactEvent
                        {
                            VerticalSpeed   = verticalSpeed,
                            HorizontalSpeed = horizontalSpeed,
                            GroundContactY  = groundContactY
                        });
                        SystemAPI.SetComponentEnabled<LandingImpactEvent>(entity, true);
                    }

                    _eventWasFiredLastFrame = true;
                }

                _previousIsGrounded = currentIsGrounded;
                _previousVelocity   = movementState.ValueRO.Velocity;
            }
        }
    }
}
