using DOTS.Player.Components;
using DOTS.Terrain.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using System;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Holds player physics at startup until nearby terrain colliders are ready.
    /// This prevents early-frame free-fall through yet-to-be-built terrain chunks.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PhysicsSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PlayerTerrainSafetySystem))]
    public partial struct PlayerStartupReadinessSystem : ISystem
    {
        private const uint TerrainLayerBit = 2u;
        private static readonly float3 ProbeStartOffset = new float3(0f, 1.0f, 0f);
        private bool _hasLoggedInvalidColliderRaycast;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PlayerStartupReadinessGate>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var elapsedTime = SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (gate, transform, velocity, gravity, input, movementState, entity) in
                     SystemAPI.Query<RefRW<PlayerStartupReadinessGate>, RefRO<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PhysicsGravityFactor>, RefRW<PlayerInputComponent>, RefRW<PlayerMovementState>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                var gateData = gate.ValueRO;
                if (gateData.StartTime < 0d)
                {
                    gateData.StartTime = elapsedTime;
                    gate.ValueRW = gateData;
                }

                var timedOut = (elapsedTime - gateData.StartTime) >= gateData.TimeoutSeconds;
                var terrainReady = false;

                if (!timedOut)
                {
                    var ray = new RaycastInput
                    {
                        Start = transform.ValueRO.Position + ProbeStartOffset,
                        End = transform.ValueRO.Position - math.up() * gateData.ProbeDistance,
                        Filter = new CollisionFilter
                        {
                            BelongsTo = ~0u,
                            CollidesWith = TerrainLayerBit,
                            GroupIndex = 0
                        }
                    };

                    terrainReady = TryCastRaySafe(in physicsWorld, in ray, ref _hasLoggedInvalidColliderRaycast, out _);
                }

                if (terrainReady || timedOut)
                {
                    gravity.ValueRW.Value = gateData.ReleasedGravityFactor;
                    movementState.ValueRW.PreviousPosition = transform.ValueRO.Position;
                    ecb.RemoveComponent<PlayerStartupReadinessGate>(entity);

                    DebugSettings.LogPlayer(
                        terrainReady
                            ? "Player startup readiness gate released: nearby terrain collider detected."
                            : "Player startup readiness gate released: timeout reached before collider detection.");

                    continue;
                }

                // Hold physics and input while terrain collider readiness is pending.
                gravity.ValueRW.Value = 0f;
                velocity.ValueRW.Linear = float3.zero;
                velocity.ValueRW.Angular = float3.zero;
                input.ValueRW.Move = float2.zero;
                input.ValueRW.Look = float2.zero;
                input.ValueRW.JumpPressed = false;
                movementState.ValueRW.IsGrounded = false;
                movementState.ValueRW.FallTime = 0f;
                movementState.ValueRW.PreviousPosition = transform.ValueRO.Position;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        private static bool TryCastRaySafe(in PhysicsWorld physicsWorld, in RaycastInput input, ref bool hasLoggedInvalidColliderRaycast, out RaycastHit hit)
        {
            hit = default;
            try
            {
                return physicsWorld.CastRay(input, out hit);
            }
            catch (InvalidOperationException ex)
            {
                if (!hasLoggedInvalidColliderRaycast)
                {
                    DebugSettings.LogPlayerWarning(
                        $"Startup readiness raycast skipped due to invalid collider blob reference. " +
                        $"Likely terrain collider disposal race during streaming/rebuild. Details: {ex.Message}");
                    hasLoggedInvalidColliderRaycast = true;
                }

                return false;
            }
        }
    }
}
