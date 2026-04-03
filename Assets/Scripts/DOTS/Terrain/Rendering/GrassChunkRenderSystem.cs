using DOTS.Terrain.Core;
using DOTS.Terrain.Settings;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Terrain.Rendering
{
    /// <summary>
    /// Renders grass blades for all terrain surface chunks that have a
    /// <see cref="GrassChunkBladeBuffer"/> by issuing one
    /// <see cref="Graphics.DrawMeshInstancedIndirect"/> call per chunk.
    ///
    /// Uses SystemBase (class) rather than ISystem (struct) because it holds managed
    /// references (Mesh, Material, MaterialPropertyBlock) as cached fields.
    /// ISystem structs must be fully unmanaged/blittable — managed fields break the
    /// source generator and produce "cannot be constructed" errors at world init.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GrassChunkRenderSystem : SystemBase
    {
        private EntityQuery          _renderQuery;
        private Mesh                 _bladeMesh;
        private Material             _bladeMaterial;
        private MaterialPropertyBlock _mpb;

        protected override void OnCreate()
        {
            RequireForUpdate<TerrainChunkGrassSurface>();

            _renderQuery = GetEntityQuery(
                ComponentType.ReadOnly<TerrainChunkGrassSurface>(),
                ComponentType.ReadOnly<GrassChunkBladeBuffer>(),
                ComponentType.ReadOnly<LocalToWorld>()
            );
        }

        protected override void OnUpdate()
        {
            if (_renderQuery.IsEmpty) return;

            EnsureResources();
            if (_bladeMesh == null || _bladeMaterial == null) return;

            var settings  = GrassSystemSettings.Load();
            float fadeEnd = settings != null ? settings.FadeEndDistance : GrassDefaults.FadeEndDistance;
            float maxHeight = settings?.Biomes?.Length > 0
                ? settings.Biomes[0].MaxBladeHeight
                : GrassDefaults.MaxBladeHeight;

            _mpb ??= new MaterialPropertyBlock();

            var em        = EntityManager;
            var entities  = _renderQuery.ToEntityArray(Allocator.Temp);
            var cameraPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

            foreach (var entity in entities)
            {
                var buffer = em.GetComponentObject<GrassChunkBladeBuffer>(entity);
                if (buffer == null || buffer.BladeCount == 0) continue;
                if (buffer.BladeBuffer == null || !buffer.BladeBuffer.IsValid()) continue;

                var ltw  = em.GetComponentData<LocalToWorld>(entity);
                var pos  = new Vector3(ltw.Position.x, ltw.Position.y, ltw.Position.z);

                // CPU distance cull — avoids submitting the draw call entirely for far chunks.
                if (Vector3.Distance(cameraPos, pos) > fadeEnd) continue;

                var chunkBounds = new Bounds(pos, new Vector3(32f, 32f + maxHeight, 32f));

                _mpb.SetBuffer("_BladeBuffer", buffer.BladeBuffer);

                Graphics.DrawMeshInstancedIndirect(
                    mesh:           _bladeMesh,
                    submeshIndex:   0,
                    material:       _bladeMaterial,
                    bounds:         chunkBounds,
                    bufferWithArgs: buffer.ArgsBuffer,
                    argsOffset:     0,
                    properties:     _mpb,
                    castShadows:    ShadowCastingMode.Off,
                    receiveShadows: true,
                    layer:          0);
            }

            entities.Dispose();
        }

        private void EnsureResources()
        {
            if (_bladeMesh == null)
                _bladeMesh = Resources.Load<Mesh>("GrassBladeMesh");

            if (_bladeMaterial == null)
            {
                var shader = Resources.Load<Shader>("Shaders/GrassBlades");
                if (shader != null)
                    _bladeMaterial = new Material(shader);
            }

            if (_bladeMesh == null)
                DebugSettings.LogRendering("[GrassChunkRenderSystem] GrassBladeMesh not found. Run 'DOTS Terrain > Build Grass Blade Mesh'.");
            if (_bladeMaterial == null)
                DebugSettings.LogRendering("[GrassChunkRenderSystem] GrassBlades shader not found at Resources/Shaders/GrassBlades.");
        }
    }
}
