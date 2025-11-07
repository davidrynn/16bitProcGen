using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Simple keyboard movement system for testing player physics.
    /// Uses WASD for movement, Space for jump.
    /// This is a basic test system - for production, use PlayerInputSystem.
    /// 
    /// DISABLED: Now using production PlayerMovementSystem with full features.
    /// This system is kept for reference but not active.
    /// </summary>
#if SIMPLE_PLAYER_MOVEMENT_ENABLED  // Disabled by default - use production PlayerMovementSystem
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    public partial struct SimplePlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        // Note: OnUpdate can't be BurstCompiled because we use UnityEngine.Input
        // In production, use Unity.InputSystem which is Burst-compatible
        public void OnUpdate(ref SystemState state)
        {
            // Get input (not Burst-compatible, but fine for testing)
            float2 moveInput = GetMovementInput2D();
            bool jumpPressed = Input.GetKey(KeyCode.Space);

            float deltaTime = SystemAPI.Time.DeltaTime;

            // Get camera yaw for camera-relative movement
            float cameraYaw = GetCameraYaw(ref state);

            foreach (var (velocity, transform) in 
                SystemAPI.Query<RefRW<PhysicsVelocity>, RefRO<LocalTransform>>()
                    .WithAll<PlayerTag>())
            {
                // Apply horizontal movement (camera-relative)
                float moveSpeed = 5f; // m/s
                float3 desiredVelocity = CalculateCameraRelativeMovement(moveInput, cameraYaw) * moveSpeed;

                // Preserve vertical velocity (gravity), replace horizontal
                velocity.ValueRW.Linear = new float3(
                    desiredVelocity.x,
                    velocity.ValueRO.Linear.y, // Keep gravity/jumping
                    desiredVelocity.z
                );

                // Jump (simple impulse)
                if (jumpPressed && IsGrounded(velocity.ValueRO))
                {
                    velocity.ValueRW.Linear.y = 7f; // Jump velocity
                }
            }
        }

        private float2 GetMovementInput2D()
        {
            float2 input = float2.zero;

            if (Input.GetKey(KeyCode.W)) input.y += 1f;  // Forward
            if (Input.GetKey(KeyCode.S)) input.y -= 1f;  // Backward
            if (Input.GetKey(KeyCode.A)) input.x -= 1f;  // Left
            if (Input.GetKey(KeyCode.D)) input.x += 1f;  // Right

            // Normalize diagonal movement
            if (math.lengthsq(input) > 1f)
            {
                input = math.normalize(input);
            }

            return input;
        }

        private float GetCameraYaw(ref SystemState state)
        {
            // Try to get yaw from PlayerViewComponent if it exists
            foreach (var view in SystemAPI.Query<RefRO<PlayerViewComponent>>().WithAll<PlayerTag>())
            {
                return view.ValueRO.YawDegrees;
            }

            // Fallback: get camera rotation from main camera
            var camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                return camera.transform.eulerAngles.y;
            }

            return 0f;
        }

        private float3 CalculateCameraRelativeMovement(float2 input, float cameraYawDegrees)
        {
            if (math.lengthsq(input) < 0.01f)
                return float3.zero;

            // Convert yaw to radians
            float yawRadians = math.radians(cameraYawDegrees);

            // Calculate camera-relative forward and right vectors (on XZ plane)
            float3 forward = new float3(math.sin(yawRadians), 0f, math.cos(yawRadians));
            float3 right = new float3(math.cos(yawRadians), 0f, -math.sin(yawRadians));

            // Combine input with camera directions
            return right * input.x + forward * input.y;
        }

        private bool IsGrounded(PhysicsVelocity velocity)
        {
            // Simple ground check: if vertical velocity is near zero or negative
            // In production, use raycasting or collision events
            return velocity.Linear.y < 0.5f && velocity.Linear.y > -2f;
        }
    }
#endif // SIMPLE_PLAYER_MOVEMENT_ENABLED
}
