using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using DOTS.Player.Components;
using DOTS.Terrain.Core;
using Unity.Transforms;
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

        // Matches player capsule geometry from player bootstrap systems.
        private static readonly float3 PlayerCapsuleVertex0 = new float3(0f, 0.5f, 0f);
        private static readonly float3 PlayerCapsuleVertex1 = new float3(0f, 1.5f, 0f);
        private const float PlayerCapsuleRadius = 0.5f;

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

            if (!TryBuildEdit(
                entityManager,
                chunks,
                in command,
                in settings,
                out var edit,
                out var usedSnapSpace,
                out var cellSize,
                out var hasChunkCoord,
                out var chunkCoord,
                out var rejectReason))
            {
                if (rejectReason == TerrainEditRejectReason.NoOwningChunk)
                {
                    DebugSettings.LogTerrainEditWarning(
                        $"Edit blocked: no owning chunk found for chunk-local snapped edit at hitPos={command.HitPosition}.",
                        forceLog: true);
                }

                return;
            }

            if (settings.EnablePlayerOverlapGuard && edit.Operation == SDFEditOperation.Add)
            {
                var blockedByPlayer = false;
                var blockingPlayerPosition = float3.zero;

                foreach (var playerTransform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
                {
                    var playerOrigin = playerTransform.ValueRO.Position;
                    if (!IsAddEditBlockedByPlayerSafetyVolume(in edit, playerOrigin, settings.PlayerEditClearance))
                    {
                        continue;
                    }

                    blockedByPlayer = true;
                    blockingPlayerPosition = playerOrigin;
                    break;
                }

                if (blockedByPlayer)
                {
                    // Current safety policy is block-only: reject overlap edits instead of relocating them.
                    DebugSettings.LogTerrainEditWarning(
                        $"Edit blocked: add edit overlaps player safety volume. hitPos={command.HitPosition} snappedPos={edit.Center} playerPos={blockingPlayerPosition}",
                        forceLog: true);
                    return;
                }
            }

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

        private static bool TryBuildEdit(
            EntityManager entityManager,
            NativeArray<Entity> chunks,
            in TerrainBrushCommand command,
            in TerrainEditSettings settings,
            out SDFEdit edit,
            out TerrainEditSnapSpace usedSnapSpace,
            out float cellSize,
            out bool hasChunkCoord,
            out int3 chunkCoord,
            out TerrainEditRejectReason rejectReason)
        {
            edit = default;
            usedSnapSpace = settings.SnapSpace;
            cellSize = 0f;
            hasChunkCoord = false;
            chunkCoord = int3.zero;
            rejectReason = TerrainEditRejectReason.None;

            if (settings.PlacementMode == TerrainEditPlacementMode.FreeSphere)
            {
                edit = SDFEdit.Create(command.HitPosition, BrushRadius, command.Operation);
                return true;
            }

            if (!chunks.IsCreated || chunks.Length == 0)
            {
                if (settings.LockChunkLocalSnap && settings.SnapSpace == TerrainEditSnapSpace.ChunkLocal)
                {
                    rejectReason = TerrainEditRejectReason.NoOwningChunk;
                    return false;
                }

                usedSnapSpace = TerrainEditSnapSpace.Global;
                edit = SDFEdit.Create(command.HitPosition, BrushRadius, command.Operation);
                return true;
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
                var placementPoint = ResolvePlacementPoint(in command, cellSize, settings.CubeDepthCells);
                snappedCenter = TerrainChunkEditUtility.SnapToChunkLocalLattice(placementPoint, owningBounds.WorldOrigin, cellSize);
                usedSnapSpace = TerrainEditSnapSpace.ChunkLocal;
                hasChunkCoord = true;
                chunkCoord = owningChunk.ChunkCoord;
            }
            else
            {
                if (settings.LockChunkLocalSnap && settings.SnapSpace == TerrainEditSnapSpace.ChunkLocal)
                {
                    rejectReason = TerrainEditRejectReason.NoOwningChunk;
                    return false;
                }

                var placementPoint = ResolvePlacementPoint(in command, cellSize, settings.CubeDepthCells);
                snappedCenter = TerrainChunkEditUtility.SnapToGlobalLattice(placementPoint, settings.GlobalSnapAnchor, cellSize);
                usedSnapSpace = TerrainEditSnapSpace.Global;
            }

            var halfExtents = BuildBoxHalfExtents(cellSize, settings.CubeDepthCells, command.RayDirection);

            if (command.Operation == SDFEditOperation.Subtract &&
                settings.PlacementMode == TerrainEditPlacementMode.SnappedCube &&
                TryResolveEditColumn(
                    entityManager,
                    chunks,
                    command.HitPosition,
                    snappedCenter,
                    hasChunkCoord,
                    chunkCoord,
                    out var columnCoord) &&
                TryClampSubtractCenterToLoadedYLayers(
                    entityManager,
                    chunks,
                    columnCoord,
                    halfExtents,
                    cellSize,
                    ref snappedCenter,
                    out var layerCount,
                    out var minLayerOriginY))
            {
                DebugSettings.LogTerrainEdit(
                    $"Subtract Y-layer clamp applied: column=({columnCoord.x},{columnCoord.y}) layers={layerCount} minLayerY={minLayerOriginY:F3} clampedCenterY={snappedCenter.y:F3}");
            }

            edit = SDFEdit.CreateBox(snappedCenter, halfExtents, command.Operation);
            return true;
        }

        /// <summary>
        /// Returns true when an additive edit intersects the player's safety capsule.
        /// Current policy is block-only for overlap: no auto-relocation is applied.
        /// </summary>
        public static bool IsAddEditBlockedByPlayerSafetyVolume(in SDFEdit edit, float3 playerOrigin, float clearance)
        {
            if (edit.Operation != SDFEditOperation.Add)
            {
                return false;
            }

            return EditOverlapsPlayerCapsule(in edit, playerOrigin, clearance);
        }

        private static bool TryResolveEditColumn(
            EntityManager entityManager,
            NativeArray<Entity> chunks,
            float3 hitPosition,
            float3 snappedCenter,
            bool hasChunkCoord,
            int3 chunkCoord,
            out int2 columnCoord)
        {
            if (hasChunkCoord)
            {
                columnCoord = new int2(chunkCoord.x, chunkCoord.z);
                return true;
            }

            if (TerrainChunkEditUtility.TryFindOwningChunk(
                    entityManager,
                    chunks,
                    hitPosition,
                    out _,
                    out var owningChunk,
                    out _,
                    out _))
            {
                columnCoord = new int2(owningChunk.ChunkCoord.x, owningChunk.ChunkCoord.z);
                return true;
            }

            if (TerrainChunkEditUtility.TryFindOwningChunk(
                    entityManager,
                    chunks,
                    snappedCenter,
                    out _,
                    out owningChunk,
                    out _,
                    out _))
            {
                columnCoord = new int2(owningChunk.ChunkCoord.x, owningChunk.ChunkCoord.z);
                return true;
            }

            columnCoord = int2.zero;
            return false;
        }

        private static bool TryClampSubtractCenterToLoadedYLayers(
            EntityManager entityManager,
            NativeArray<Entity> chunks,
            int2 columnCoord,
            float3 halfExtents,
            float cellSize,
            ref float3 snappedCenter,
            out int layerCount,
            out float minLayerOriginY)
        {
            layerCount = 0;
            minLayerOriginY = 0f;

            if (!chunks.IsCreated || chunks.Length == 0)
            {
                return false;
            }

            var minY = float.MaxValue;
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunkEntity = chunks[i];
                if (!entityManager.HasComponent<TerrainChunk>(chunkEntity) ||
                    !entityManager.HasComponent<TerrainChunkBounds>(chunkEntity))
                {
                    continue;
                }

                var chunk = entityManager.GetComponentData<TerrainChunk>(chunkEntity);
                if (chunk.ChunkCoord.x != columnCoord.x || chunk.ChunkCoord.z != columnCoord.y)
                {
                    continue;
                }

                var bounds = entityManager.GetComponentData<TerrainChunkBounds>(chunkEntity);
                minY = math.min(minY, bounds.WorldOrigin.y);
                layerCount++;
            }

            if (layerCount == 0)
            {
                return false;
            }

            minLayerOriginY = minY;

            // Keep subtraction edits inside the lowest loaded layer in this XZ column.
            var safeCellSize = math.max(1e-5f, cellSize);
            var minCenterY = minY + math.max(1e-5f, halfExtents.y) + safeCellSize * 0.001f;
            if (snappedCenter.y >= minCenterY - 1e-6f)
            {
                return false;
            }

            var stepsUp = math.ceil((minCenterY - snappedCenter.y) / safeCellSize);
            snappedCenter.y += stepsUp * safeCellSize;
            return true;
        }

        private static float3 ResolvePlacementPoint(in TerrainBrushCommand command, float cellSize, int cubeDepthCells)
        {
            if (command.Operation != SDFEditOperation.Subtract)
            {
                return command.HitPosition;
            }

            var safeCellSize = math.max(1e-5f, cellSize);
            var depthCells = math.max(1, cubeDepthCells);
            var halfDepth = safeCellSize * 0.5f * depthCells;
            var epsilon = safeCellSize * 0.001f;
            var inwardDirection = ResolveSubtractInwardDirection(in command);
            return command.HitPosition + inwardDirection * (halfDepth + epsilon);
        }

        private static float3 ResolveSubtractInwardDirection(in TerrainBrushCommand command)
        {
            if (command.HasHitSurfaceNormal && math.lengthsq(command.HitSurfaceNormal) > 1e-6f)
            {
                // Terrain collider normals point outward from solid volume, so subtraction
                // should push opposite the normal to place the edit center inside terrain.
                return math.normalizesafe(-command.HitSurfaceNormal, new float3(0f, -1f, 0f));
            }

            return math.normalizesafe(command.RayDirection, new float3(0f, -1f, 0f));
        }

        private static bool EditOverlapsPlayerCapsule(in SDFEdit edit, float3 playerOrigin, float clearance)
        {
            var capsuleStart = playerOrigin + PlayerCapsuleVertex0;
            var capsuleEnd = playerOrigin + PlayerCapsuleVertex1;
            var capsuleRadius = PlayerCapsuleRadius + math.max(0f, clearance);

            if (edit.Shape == SDFEditShape.Box)
            {
                var boxMin = edit.Center - edit.HalfExtents;
                var boxMax = edit.Center + edit.HalfExtents;
                return CapsuleIntersectsAabb(capsuleStart, capsuleEnd, capsuleRadius, boxMin, boxMax);
            }

            var sphereRadius = math.max(1e-5f, edit.Radius);
            var distanceSq = SegmentPointDistanceSq(capsuleStart, capsuleEnd, edit.Center);
            var overlapDistance = capsuleRadius + sphereRadius;
            return distanceSq <= overlapDistance * overlapDistance;
        }

        private static bool CapsuleIntersectsAabb(float3 segmentStart, float3 segmentEnd, float radius, float3 boxMin, float3 boxMax)
        {
            var radiusVec = new float3(radius);
            var expandedMin = boxMin - radiusVec;
            var expandedMax = boxMax + radiusVec;

            if (SegmentIntersectsAabb(segmentStart, segmentEnd, expandedMin, expandedMax))
            {
                return true;
            }

            var radiusSq = radius * radius;
            return PointAabbDistanceSq(segmentStart, boxMin, boxMax) <= radiusSq
                   || PointAabbDistanceSq(segmentEnd, boxMin, boxMax) <= radiusSq;
        }

        private static float SegmentPointDistanceSq(float3 segmentStart, float3 segmentEnd, float3 point)
        {
            var segment = segmentEnd - segmentStart;
            var lengthSq = math.dot(segment, segment);
            if (lengthSq <= 1e-6f)
            {
                return math.lengthsq(point - segmentStart);
            }

            var t = math.saturate(math.dot(point - segmentStart, segment) / lengthSq);
            var closest = segmentStart + segment * t;
            return math.lengthsq(point - closest);
        }

        private static float PointAabbDistanceSq(float3 point, float3 min, float3 max)
        {
            var clamped = math.clamp(point, min, max);
            return math.lengthsq(point - clamped);
        }

        private static bool SegmentIntersectsAabb(float3 segmentStart, float3 segmentEnd, float3 min, float3 max)
        {
            var direction = segmentEnd - segmentStart;
            var tMin = 0f;
            var tMax = 1f;

            return UpdateSegmentInterval(segmentStart.x, direction.x, min.x, max.x, ref tMin, ref tMax)
                   && UpdateSegmentInterval(segmentStart.y, direction.y, min.y, max.y, ref tMin, ref tMax)
                   && UpdateSegmentInterval(segmentStart.z, direction.z, min.z, max.z, ref tMin, ref tMax);
        }

        private static bool UpdateSegmentInterval(float start, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (math.abs(direction) <= 1e-6f)
            {
                return start >= min && start <= max;
            }

            var invDirection = 1f / direction;
            var t1 = (min - start) * invDirection;
            var t2 = (max - start) * invDirection;
            if (t1 > t2)
            {
                var temp = t1;
                t1 = t2;
                t2 = temp;
            }

            tMin = math.max(tMin, t1);
            tMax = math.min(tMax, t2);
            return tMin <= tMax;
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
                command.HitSurfaceNormal = hit.SurfaceNormal;
                command.HasHitSurfaceNormal = true;
                DebugSettings.LogTerrainEdit(
                    $"Raycast hit: pos={hit.Position} fraction={hit.Fraction:F4}");
            }
            else
            {
                command.HitPosition = origin + direction * BrushDistance;
                command.HitSurfaceNormal = float3.zero;
                command.HasHitSurfaceNormal = false;
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
                if (settings.LockChunkLocalSnap)
                {
                    DebugSettings.LogTerrainEditWarning(
                        "Snap-space toggle ignored: chunk-local snap lock is enabled.",
                        forceLog: true);
                }
                else
                {
                    settings.SnapSpace = settings.SnapSpace == TerrainEditSnapSpace.ChunkLocal
                        ? TerrainEditSnapSpace.Global
                        : TerrainEditSnapSpace.ChunkLocal;
                    changed = true;
                }
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
            if (value < 0.25f - 1e-4f) return 0.25f;
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
            public float3 HitSurfaceNormal;
            public bool HasHitSurfaceNormal;
        }

        private enum TerrainEditRejectReason : byte
        {
            None = 0,
            NoOwningChunk = 1
        }
    }
}
