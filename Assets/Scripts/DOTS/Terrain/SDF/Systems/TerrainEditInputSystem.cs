using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using DOTS.Terrain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DOTS.Terrain
{
    /// <summary>
    /// Debug-focused input bridge that pushes additive/subtractive SDF edits via mouse clicks.
    /// Left click (or Q) subtracts; right click (or E) adds.
    /// Uses DOTS PhysicsWorldSingleton for raycasting against terrain colliders.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TerrainEditInputSystem : ISystem
    {
        /// <summary>Radius of the sphere used for terrain edits (increase for larger modifications)</summary>
        private const float BrushRadius = 1f;
        
        /// <summary>Maximum raycast distance from camera to detect terrain for editing</summary>
        private const float BrushDistance = 8f;
        
        /// <summary>Minimum time (seconds) between edit operations to prevent spam and optimize performance</summary>
        private const double EditCooldown = 0.15;

        private EntityQuery _chunkQuery;
        private double _lastEditTime;
        private bool _hasLoggedAimDiag;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunk>();
            state.RequireForUpdate<SDFTerrainFieldSettings>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _chunkQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            _lastEditTime = 0;
            _hasLoggedAimDiag = false;
            EnsureEditBufferExists(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var currentTime = SystemAPI.Time.ElapsedTime;
            if (currentTime - _lastEditTime < EditCooldown)
                return;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            if (!TryGetBrushCommand(in physicsWorld, ref _hasLoggedAimDiag, out var operation, out var center))
                return;

            _lastEditTime = currentTime;

            var editBuffer = SystemAPI.GetSingletonBuffer<SDFEdit>();
            editBuffer.Add(SDFEdit.Create(center, BrushRadius, operation));

            DebugSettings.LogTerrainEdit(
                $"Edit op={operation} center={center} radius={BrushRadius}");

            var entityManager = state.EntityManager;
            using var chunks = _chunkQuery.ToEntityArray(Allocator.Temp);
            if (chunks.Length > 0)
            {
                TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunks, center, BrushRadius);
            }
        }

        private static bool TryGetBrushCommand(in PhysicsWorld physicsWorld, ref bool hasLoggedAimDiag, out int operation, out float3 center)
        {
            operation = 0;
            center = float3.zero;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return false;

            var addPressed = mouse.rightButton.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame;
            var subtractPressed = mouse.leftButton.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame;

            if (!addPressed && !subtractPressed)
                return false;

            if (!CenterAimRayUtility.TryGetCenterRay(BrushDistance, out var camera, out var origin, out var direction, out var end))
            {
                DebugSettings.LogTerrainEditWarning("DIAG: Camera.main is null — no edit possible.", forceLog: true);
                return false;
            }

            if (!hasLoggedAimDiag)
            {
                DebugSettings.LogTerrainEdit(
                    $"DIAG: Center aim camera='{camera.name}' (instanceID={camera.GetInstanceID()}) origin={origin} dir={direction}",
                    forceLog: true);
                hasLoggedAimDiag = true;
            }

            // Use DOTS physics raycast to hit terrain colliders only.
            // BelongsTo = ~0u (ray is "everything"), CollidesWith = 2u (terrain layer only).
            // This prevents hitting the player capsule (layer 1) which occupies the same
            // space as the camera origin and would produce a hit at the player's position.
            var rayInput = new RaycastInput
            {
                Start = origin,
                End = end,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = 2u,  // Terrain layer only (see TerrainChunkColliderBuildSystem)
                    GroupIndex = 0
                }
            };

            if (physicsWorld.CastRay(rayInput, out var hit))
            {
                center = hit.Position;
                DebugSettings.LogTerrainEdit(
                    $"Raycast hit: pos={hit.Position} fraction={hit.Fraction:F4}");
            }
            else
            {
                center = origin + direction * BrushDistance;
                DebugSettings.LogTerrainEdit(
                    $"Raycast miss, using fallback at {center}");
            }

            operation = addPressed ? SDFEditOperation.Add : SDFEditOperation.Subtract;
            return true;
        }

        private void EnsureEditBufferExists(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonBuffer<SDFEdit>(out _))
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddBuffer<SDFEdit>(entity);
            }
        }
    }
}
