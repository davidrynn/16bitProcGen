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
        /// <summary>Radius of the sphere used for legacy free-sphere edits.</summary>
        private const float BrushRadius = 1f;

        /// <summary>Maximum raycast distance from camera to detect terrain for editing.</summary>
        private const float BrushDistance = 8f;

        /// <summary>Minimum time (seconds) between edit operations to prevent spam and optimize performance.</summary>
        private const double EditCooldown = 0.15;

        private EntityQuery _chunkQuery;
        private double _lastEditTime;
        private bool _hasLoggedAimDiag;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunk>();
            state.RequireForUpdate<SDFTerrainFieldSettings>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _chunkQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkBounds>(),
                ComponentType.ReadOnly<TerrainChunkGridInfo>());
            _lastEditTime = 0;
            _hasLoggedAimDiag = false;
            EnsureEditBufferExists(ref state);
            EnsureEditSettingsExists(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonRW<TerrainEditSettings>(out var settingsRW))
            {
                return;
            }

            ref var settings = ref settingsRW.ValueRW;
            settings = TerrainEditSettings.Clamp(in settings);

            if (HandleEditSettingsHotkeys(ref settings))
            {
                DebugSettings.LogTerrainEdit(
                    $"Edit settings changed: mode={settings.PlacementMode} snapSpace={settings.SnapSpace} fraction={settings.EditCellFraction:F2} depthCells={settings.CubeDepthCells}");
            }

            var currentTime = SystemAPI.Time.ElapsedTime;
            if (currentTime - _lastEditTime < EditCooldown)
            {
                return;
            }

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            if (!TryGetBrushCommand(in physicsWorld, ref _hasLoggedAimDiag, out var command))
            {
                return;
            }

            _lastEditTime = currentTime;

            var entityManager = state.EntityManager;
            using var chunks = _chunkQuery.ToEntityArray(Allocator.Temp);

            var edit = BuildEdit(
                entityManager,
                chunks,
                in command,
                in settings,
                out var usedSnapSpace,
                out var cellSize,
                out var hasChunkCoord,
                out var chunkCoord);

            var editBuffer = SystemAPI.GetSingletonBuffer<SDFEdit>();
            editBuffer.Add(edit);

            var opLabel = edit.Operation == SDFEditOperation.Add ? "Add" : "Subtract";
            var chunkLabel = hasChunkCoord ? $"({chunkCoord.x},{chunkCoord.y},{chunkCoord.z})" : "n/a";

            DebugSettings.LogTerrainEdit(
                $"Edit op={opLabel} mode={settings.PlacementMode} shape={edit.Shape} snapSpace={usedSnapSpace} cellSize={cellSize:F3} hitPos={command.HitPosition} snappedPos={edit.Center} chunkCoord={chunkLabel}");

            if (chunks.Length > 0)
            {
                TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunks, in edit);
            }
        }

        private static SDFEdit BuildEdit(
            EntityManager entityManager,
            NativeArray<Entity> chunks,
            in TerrainBrushCommand command,
            in TerrainEditSettings settings,
            out TerrainEditSnapSpace usedSnapSpace,
            out float cellSize,
            out bool hasChunkCoord,
            out int3 chunkCoord)
        {
            usedSnapSpace = settings.SnapSpace;
            cellSize = 0f;
            hasChunkCoord = false;
            chunkCoord = int3.zero;

            if (settings.PlacementMode == TerrainEditPlacementMode.FreeSphere)
            {
                return SDFEdit.Create(command.HitPosition, BrushRadius, command.Operation);
            }

            if (!chunks.IsCreated || chunks.Length == 0)
            {
                usedSnapSpace = TerrainEditSnapSpace.Global;
                return SDFEdit.Create(command.HitPosition, BrushRadius, command.Operation);
            }

            var referenceGrid = entityManager.GetComponentData<TerrainChunkGridInfo>(chunks[0]);
            cellSize = TerrainChunkEditUtility.ComputeQuantizedCellSize(in referenceGrid, settings.EditCellFraction);

            float3 snappedCenter;
            if (settings.SnapSpace == TerrainEditSnapSpace.ChunkLocal &&
                TerrainChunkEditUtility.TryFindOwningChunk(
                    entityManager,
                    chunks,
                    command.HitPosition,
                    out _,
                    out var owningChunk,
                    out var owningBounds,
                    out var owningGrid))
            {
                cellSize = TerrainChunkEditUtility.ComputeQuantizedCellSize(in owningGrid, settings.EditCellFraction);
                snappedCenter = TerrainChunkEditUtility.SnapToChunkLocalLattice(command.HitPosition, owningBounds.WorldOrigin, cellSize);
                usedSnapSpace = TerrainEditSnapSpace.ChunkLocal;
                hasChunkCoord = true;
                chunkCoord = owningChunk.ChunkCoord;
            }
            else
            {
                snappedCenter = TerrainChunkEditUtility.SnapToGlobalLattice(command.HitPosition, settings.GlobalSnapAnchor, cellSize);
                usedSnapSpace = TerrainEditSnapSpace.Global;
            }

            var halfExtents = BuildBoxHalfExtents(cellSize, settings.CubeDepthCells, command.RayDirection);
            return SDFEdit.CreateBox(snappedCenter, halfExtents, command.Operation);
        }

        private static float3 BuildBoxHalfExtents(float cellSize, int cubeDepthCells, float3 rayDirection)
        {
            var half = new float3(math.max(1e-5f, cellSize) * 0.5f);
            var depthCells = math.max(1, cubeDepthCells);
            if (depthCells <= 1)
            {
                return half;
            }

            var direction = math.abs(rayDirection);
            if (direction.x >= direction.y && direction.x >= direction.z)
            {
                half.x *= depthCells;
                return half;
            }

            if (direction.y >= direction.x && direction.y >= direction.z)
            {
                half.y *= depthCells;
                return half;
            }

            half.z *= depthCells;
            return half;
        }

        private static bool TryGetBrushCommand(in PhysicsWorld physicsWorld, ref bool hasLoggedAimDiag, out TerrainBrushCommand command)
        {
            command = default;

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

            command.RayDirection = direction;

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
                command.HitPosition = hit.Position;
                DebugSettings.LogTerrainEdit(
                    $"Raycast hit: pos={hit.Position} fraction={hit.Fraction:F4}");
            }
            else
            {
                command.HitPosition = origin + direction * BrushDistance;
                DebugSettings.LogTerrainEdit(
                    $"Raycast miss, using fallback at {command.HitPosition}");
            }

            command.Operation = addPressed ? SDFEditOperation.Add : SDFEditOperation.Subtract;
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

        private void EnsureEditSettingsExists(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TerrainEditSettings>(out _))
            {
                var entity = state.EntityManager.CreateEntity(typeof(TerrainEditSettings));
                state.EntityManager.SetComponentData(entity, TerrainEditSettings.Default);
            }
        }

        private static bool HandleEditSettingsHotkeys(ref TerrainEditSettings settings)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            var changed = false;
            if (keyboard.tKey.wasPressedThisFrame)
            {
                settings.PlacementMode = settings.PlacementMode == TerrainEditPlacementMode.SnappedCube
                    ? TerrainEditPlacementMode.FreeSphere
                    : TerrainEditPlacementMode.SnappedCube;
                changed = true;
            }

            if (keyboard.gKey.wasPressedThisFrame)
            {
                settings.SnapSpace = settings.SnapSpace == TerrainEditSnapSpace.ChunkLocal
                    ? TerrainEditSnapSpace.Global
                    : TerrainEditSnapSpace.ChunkLocal;
                changed = true;
            }

            if (keyboard[Key.LeftBracket].wasPressedThisFrame)
            {
                settings.EditCellFraction = GetPreviousFraction(settings.EditCellFraction);
                changed = true;
            }

            if (keyboard[Key.RightBracket].wasPressedThisFrame)
            {
                settings.EditCellFraction = GetNextFraction(settings.EditCellFraction);
                changed = true;
            }

            settings = TerrainEditSettings.Clamp(in settings);
            return changed;
        }

        private static float GetNextFraction(float current)
        {
            var value = math.clamp(current, 0.25f, 1f);
            if (value < 0.5f - 1e-4f) return 0.5f;
            if (value < 0.75f - 1e-4f) return 0.75f;
            if (value < 1f - 1e-4f) return 1f;
            return 0.25f;
        }

        private static float GetPreviousFraction(float current)
        {
            var value = math.clamp(current, 0.25f, 1f);
            if (value > 0.75f + 1e-4f) return 0.75f;
            if (value > 0.5f + 1e-4f) return 0.5f;
            if (value > 0.25f + 1e-4f) return 0.25f;
            return 1f;
        }

        private struct TerrainBrushCommand
        {
            public int Operation;
            public float3 HitPosition;
            public float3 RayDirection;
        }
    }
}
