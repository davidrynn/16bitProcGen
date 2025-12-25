using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS.Terrain.SDF
{
    /// <summary>
    /// Debug-focused input bridge that pushes additive/subtractive SDF edits via simple mouse/keyboard shortcuts.
    /// Left click (or Q) subtracts; right click (or E) adds.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TerrainEditInputSystem : ISystem
    {
        private EntityQuery chunkQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunk>();
            state.RequireForUpdate<SDFTerrainFieldSettings>();
            chunkQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            EnsureEditBufferExists(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
#if !UNITY_DOTSRUNTIME
            if (!TryGetBrushCommand(out var operation, out var center))
            {
                return;
            }

            var editBuffer = SystemAPI.GetSingletonBuffer<SDFEdit>();
            editBuffer.Add(SDFEdit.Create(center, BrushRadius, operation));

            var entityManager = state.EntityManager;
            using var chunks = chunkQuery.ToEntityArray(Allocator.Temp);
            if (chunks.Length > 0)
            {
                TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunks, center, BrushRadius);
            }
#endif
        }

        private static bool TryGetBrushCommand(out int operation, out float3 center)
        {
            operation = 0;
            center = float3.zero;

            var addPressed = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E);
            var subtractPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q);

            if (!addPressed && !subtractPressed)
            {
                return false;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            var origin = camera.transform.position;
            var direction = camera.transform.forward;
            var ray = new Ray(origin, direction);
            center = Physics.Raycast(ray, out var hit, BrushDistance)
                ? hit.point
                : origin + direction * BrushDistance;

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
    }
}
