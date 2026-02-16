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
        private EntityQuery _chunkQuery;
        private double _lastEditTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunk>();
            state.RequireForUpdate<SDFTerrainFieldSettings>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _chunkQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            _lastEditTime = 0;
            EnsureEditBufferExists(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var currentTime = SystemAPI.Time.ElapsedTime;
            if (currentTime - _lastEditTime < EditCooldown)
                return;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            if (!TryGetBrushCommand(in physicsWorld, out var operation, out var center))
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

        private static bool TryGetBrushCommand(in PhysicsWorld physicsWorld, out int operation, out float3 center)
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

            var camera = Camera.main;
            if (camera == null)
                return false;

            var origin = (float3)camera.transform.position;
            var direction = (float3)camera.transform.forward;

            // Use DOTS physics raycast to hit terrain colliders
            var rayInput = new RaycastInput
            {
                Start = origin,
                End = origin + direction * BrushDistance,
                Filter = CollisionFilter.Default
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

        private const float BrushRadius = 3f;
        private const float BrushDistance = 8f;
        private const double EditCooldown = 0.15;
    }
}
