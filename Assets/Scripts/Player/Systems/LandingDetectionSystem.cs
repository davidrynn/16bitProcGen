using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Detects the frame a player transitions from airborne to grounded and fires
    /// a one-frame LandingImpactEvent with speed data. The event is disabled the next frame.
    /// </summary>
    /// <remarks>
    /// Uses the previous frame's Mode (tracked internally) vs current Mode to detect transitions.
    /// Reads velocity from PlayerMovementState.Velocity (cached by MovementStateBookkeepingSystem)
    /// so this system does not require PhysicsVelocity access.
    /// </remarks>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementStateBookkeepingSystem))]
    public partial struct LandingDetectionSystem : ISystem
    {
        /// <summary>
        /// Stored per-entity previous mode. Since we only have one player,
        /// a single field suffices. If multiple players are needed, use a component.
        /// </summary>
        private PlayerMovementMode _previousMode;
        private float3 _previousVelocity;
        private bool _eventWasFiredLastFrame;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementState>();
            _previousMode = PlayerMovementMode.Grounded;
            _previousVelocity = float3.zero;
            _eventWasFiredLastFrame = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (movementState, entity) in
                     SystemAPI.Query<RefRO<PlayerMovementState>>().WithEntityAccess())
            {
                bool hasEvent = SystemAPI.HasComponent<LandingImpactEvent>(entity);

                // Disable the event if it was fired last frame (one-frame event)
                if (_eventWasFiredLastFrame && hasEvent)
                {
                    SystemAPI.SetComponentEnabled<LandingImpactEvent>(entity, false);
                    _eventWasFiredLastFrame = false;
                }

                var currentMode = movementState.ValueRO.Mode;
                bool wasAirborne = IsAirborne(_previousMode);
                bool isGrounded = currentMode == PlayerMovementMode.Grounded;

                if (wasAirborne && isGrounded)
                {
                    // Landing transition detected — use previous frame's velocity
                    float verticalSpeed = math.abs(_previousVelocity.y);
                    float horizontalSpeed = math.length(new float2(_previousVelocity.x, _previousVelocity.z));

                    if (hasEvent)
                    {
                        SystemAPI.SetComponent(entity, new LandingImpactEvent
                        {
                            VerticalSpeed = verticalSpeed,
                            HorizontalSpeed = horizontalSpeed
                        });
                        SystemAPI.SetComponentEnabled<LandingImpactEvent>(entity, true);
                    }

                    _eventWasFiredLastFrame = true;
                }

                _previousMode = currentMode;
                _previousVelocity = movementState.ValueRO.Velocity;
            }
        }

        private static bool IsAirborne(PlayerMovementMode mode)
        {
            return mode != PlayerMovementMode.Grounded && mode != PlayerMovementMode.SlingshotCharging;
        }
    }
}
