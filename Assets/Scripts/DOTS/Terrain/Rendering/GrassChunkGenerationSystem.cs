using DOTS.Terrain.Core;
using DOTS.Terrain.LOD;
using DOTS.Terrain.Settings;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DOTS.Terrain.Rendering
{
    /// <summary>
    /// Builds or rebuilds the GPU blade buffer for every terrain chunk tagged with
    /// <see cref="TerrainChunkGrassSurface"/> whenever a <see cref="GrassChunkNeedsRebuild"/>
    /// tag is present on the entity.
    ///
    /// Trigger sources:
    ///   1. First time: added by this system when a surface chunk has no buffer yet.
    ///   2. Terrain edit: TerrainChunkEditUtility adds the tag after SDF modification.
    ///   3. Settings change: any code can add the tag to force a rebuild.
    ///
    /// After a successful rebuild the <see cref="GrassChunkNeedsRebuild"/> tag is removed
    /// and <see cref="GrassChunkBladeBuffer"/> is added/replaced on the entity.
    /// Chunks with <c>GrassType != 0</c> are skipped (future clump variant).
    ///
    /// UpdateBefore PresentationSystemGroup so buffers are ready before GrassChunkRenderSystem.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DOTS.Terrain.TerrainChunkColliderBuildSystem))]
    public partial struct GrassChunkGenerationSystem : ISystem
    {
        // Query: surface chunks that have a mesh and need a rebuild.
        private EntityQuery _rebuildQuery;
        // Query: surface chunks that have no buffer at all (first-time setup).
        private EntityQuery _uninitializedQuery;
        // Query: chunks currently holding grass GPU buffers.
        private EntityQuery _bufferedQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkGrassSurface>();

            _rebuildQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<TerrainChunkGrassSurface>(),
                ComponentType.ReadOnly<TerrainChunkMeshData>(),
                ComponentType.ReadOnly<GrassChunkNeedsRebuild>()
            );

            // Chunks tagged as grass surface but not yet given a rebuild tag —
            // these need first-time initialization.
            _uninitializedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TerrainChunkGrassSurface>()
                .WithAll<TerrainChunkMeshData>()
                .WithNone<GrassChunkNeedsRebuild>()
                .Build(ref state);

            _bufferedQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<TerrainChunkGrassSurface>(),
                ComponentType.ReadOnly<TerrainChunkLodState>(),
                ComponentType.ReadOnly<GrassChunkBladeBuffer>()
            );
        }

        // Not Burst: uses managed APIs (GraphicsBuffer, ScriptableObject, EntityManager structural changes).
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var hasLodPolicy = SystemAPI.TryGetSingleton<TerrainLodSettings>(out var lodPolicy);
            var grassMaxLod = hasLodPolicy ? lodPolicy.GrassMaxLod : int.MaxValue;

            if (hasLodPolicy)
            {
                RemoveBuffersOutsideLodPolicy(em, grassMaxLod);
            }

            // Tag any surface chunk that has a mesh but no rebuild request and no buffer.
            TagUninitializedChunks(em, hasLodPolicy, grassMaxLod);

            if (_rebuildQuery.IsEmpty) return;

            var settings = GrassSystemSettings.Load();
            var entities = _rebuildQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var surface = em.GetComponentData<TerrainChunkGrassSurface>(entity);

                if (hasLodPolicy && em.HasComponent<TerrainChunkLodState>(entity))
                {
                    var lodState = em.GetComponentData<TerrainChunkLodState>(entity);
                    if (lodState.CurrentLod > grassMaxLod)
                    {
                        em.RemoveComponent<GrassChunkNeedsRebuild>(entity);
                        RemoveBufferIfPresent(em, entity);
                        continue;
                    }
                }

                // Skip unimplemented grass types.
                if (surface.GrassType != 0)
                {
                    em.RemoveComponent<GrassChunkNeedsRebuild>(entity);
                    continue;
                }

                if (surface.Density <= 0f)
                {
                    em.RemoveComponent<GrassChunkNeedsRebuild>(entity);
                    RemoveBufferIfPresent(em, entity);
                    continue;
                }

                var meshData = em.GetComponentData<TerrainChunkMeshData>(entity);
                if (!meshData.HasMesh)
                {
                    // Mesh not yet built — leave the tag in place, retry next frame.
                    continue;
                }

                BuildBuffer(em, entity, surface, meshData, settings);
                em.RemoveComponent<GrassChunkNeedsRebuild>(entity);
            }

            entities.Dispose();
        }

        private void TagUninitializedChunks(EntityManager em, bool hasLodPolicy, int grassMaxLod)
        {
            if (_uninitializedQuery.IsEmpty) return;
            var uninitialized = _uninitializedQuery.ToEntityArray(Allocator.Temp);
            foreach (var e in uninitialized)
            {
                if (hasLodPolicy && em.HasComponent<TerrainChunkLodState>(e))
                {
                    var lodState = em.GetComponentData<TerrainChunkLodState>(e);
                    if (lodState.CurrentLod > grassMaxLod)
                        continue;
                }

                // Only tag if no buffer has been built yet.
                if (!em.HasComponent<GrassChunkBladeBuffer>(e))
                    em.AddComponent<GrassChunkNeedsRebuild>(e);
            }
            uninitialized.Dispose();
        }

        private void RemoveBuffersOutsideLodPolicy(EntityManager em, int grassMaxLod)
        {
            if (_bufferedQuery.IsEmpty) return;

            using var entities = _bufferedQuery.ToEntityArray(Allocator.Temp);
            using var lodStates = _bufferedQuery.ToComponentDataArray<TerrainChunkLodState>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (lodStates[i].CurrentLod <= grassMaxLod)
                    continue;

                RemoveBufferIfPresent(em, entities[i]);
                if (em.HasComponent<GrassChunkNeedsRebuild>(entities[i]))
                    em.RemoveComponent<GrassChunkNeedsRebuild>(entities[i]);
            }
        }

        private static void BuildBuffer(
            EntityManager            em,
            Entity                   entity,
            TerrainChunkGrassSurface surface,
            TerrainChunkMeshData     meshData,
            GrassSystemSettings      settings)
        {
            // Resolve settings or fall back to defaults.
            int   maxBlades       = settings != null ? settings.MaxBladesPerChunk : GrassDefaults.MaxBladesPerChunk;
            float bladesPerSqMeter= settings != null ? settings.BladesPerSqMeter  : GrassDefaults.BladesPerSqMeter;

            GrassBiomeParams biome = ResolveBiome(surface, settings);

            // SurfaceNets vertices are in chunk-local space (samplePos * voxelSize, origin at
            // chunk corner). LocalToWorld holds the chunk's world-space transform. We must
            // transform each vertex to world space before scattering so blade WorldPositions
            // are correct in the shader (which receives them as world-space coordinates).
            var ltw = em.HasComponent<LocalToWorld>(entity)
                ? em.GetComponentData<LocalToWorld>(entity).Value
                : float4x4.identity;

            // Copy BlobAsset arrays to NativeArrays for scatter logic.
            ref var blob = ref meshData.Mesh.Value;
            int vertCount = blob.Vertices.Length;
            int idxCount  = blob.Indices.Length;

            if (vertCount < 3 || idxCount < 3)
            {
                DebugSettings.LogRendering($"[GrassChunkGenerationSystem] Chunk {entity} mesh too small to scatter grass.");
                return;
            }

            var verts  = new NativeArray<float3>(vertCount, Allocator.Temp);
            var idx    = new NativeArray<int>(idxCount, Allocator.Temp);
            for (int i = 0; i < vertCount; i++)
                verts[i] = math.transform(ltw, blob.Vertices[i]);
            for (int i = 0; i < idxCount;  i++) idx[i] = blob.Indices[i];

            // Estimate surface area for blade count calculation.
            float area = EstimateSurfaceArea(verts, idx);
            int bladeCount = GrassBladeScatter.ComputeBladeCount(
                area, bladesPerSqMeter, surface.Density, biome.DensityMultiplier, maxBlades);

            var blades = new NativeList<GrassBladeData>(bladeCount + 16, Allocator.Temp);

            // Seed from chunk world position hash for determinism.
            uint seed = (uint)(entity.Index * 2654435761u ^ entity.Version * 40503u);
            GrassBladeScatter.Scatter(verts, idx, bladeCount, biome, seed, blades);

            verts.Dispose();
            idx.Dispose();

            // Dispose old buffer before replacing.
            RemoveBufferIfPresent(em, entity);

            if (blades.Length == 0)
            {
                blades.Dispose();
                return;
            }

            var bladeMesh = settings?.BladeMesh != null
                ? settings.BladeMesh
                : Resources.Load<Mesh>("GrassBladeMesh");

            if (bladeMesh == null)
            {
                DebugSettings.LogRendering("[GrassChunkGenerationSystem] Blade mesh not found. Run DOTS Terrain > Build Grass Blade Mesh.");
                blades.Dispose();
                return;
            }

            var bladeBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                blades.Length,
                System.Runtime.InteropServices.Marshal.SizeOf<GrassBladeData>());
            bladeBuffer.SetData(blades.AsArray());

            var argsData = new uint[]
            {
                (uint)bladeMesh.GetIndexCount(0),
                (uint)blades.Length,
                0u, 0u, 0u
            };
            var argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
            argsBuffer.SetData(argsData);

            int finalBladeCount = blades.Length;
            blades.Dispose();

            var bufferComponent = new GrassChunkBladeBuffer
            {
                BladeBuffer = bladeBuffer,
                ArgsBuffer  = argsBuffer,
                BladeCount  = finalBladeCount,
            };

            // RemoveBufferIfPresent was already called above, so no component exists yet.
            em.AddComponentObject(entity, bufferComponent);

            DebugSettings.LogRendering($"[GrassChunkGenerationSystem] Built {finalBladeCount} blades for chunk {entity}.");
        }

        private static void RemoveBufferIfPresent(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<GrassChunkBladeBuffer>(entity)) return;
            var existing = em.GetComponentObject<GrassChunkBladeBuffer>(entity);
            existing?.Dispose();
            em.RemoveComponent<GrassChunkBladeBuffer>(entity);
        }

        private static GrassBiomeParams ResolveBiome(TerrainChunkGrassSurface surface, GrassSystemSettings settings)
        {
            GrassBiomeSettings biomeAsset = settings?.GetBiome(surface.BiomeTypeId);
            if (biomeAsset != null)
            {
                return new GrassBiomeParams
                {
                    BaseColor         = new float3(biomeAsset.BaseColor.r, biomeAsset.BaseColor.g, biomeAsset.BaseColor.b),
                    DensityMultiplier = biomeAsset.DensityMultiplier,
                    MinBladeHeight    = biomeAsset.MinBladeHeight,
                    MaxBladeHeight    = biomeAsset.MaxBladeHeight,
                    ColorNoiseScale   = biomeAsset.ColorNoiseScale,
                };
            }

            // Default biome if no settings asset.
            return new GrassBiomeParams
            {
                BaseColor         = new float3(0.35f, 0.65f, 0.20f),
                DensityMultiplier = 1f,
                MinBladeHeight    = GrassDefaults.MinBladeHeight,
                MaxBladeHeight    = GrassDefaults.MaxBladeHeight,
                ColorNoiseScale   = 0.12f,
            };
        }

        private static float EstimateSurfaceArea(NativeArray<float3> verts, NativeArray<int> idx)
        {
            float area = 0f;
            int triCount = idx.Length / 3;
            for (int i = 0; i < triCount; i++)
            {
                float3 a = verts[idx[i * 3]];
                float3 b = verts[idx[i * 3 + 1]];
                float3 c = verts[idx[i * 3 + 2]];
                area += math.length(math.cross(b - a, c - a)) * 0.5f;
            }
            return area;
        }
    }
}
