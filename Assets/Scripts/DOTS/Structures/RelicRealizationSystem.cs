using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using DOTS.Core;

namespace DOTS.Structures
{
    /// <summary>
    /// Spawns one ECS entity per accepted relic anchor so Entities Graphics
    /// renders each relic with per-entity RenderBounds. Looks up the anchor's
    /// TemplateId in the <see cref="RelicRenderConfig"/> template registry to
    /// pick the correct mesh/material/scale for each relic type.
    ///
    /// Runs after StructureAnchorPlanningSystem. On each update it diffs
    /// the anchor buffer against existing realized entities (by StableAnchorId)
    /// and spawns/destroys as needed.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StructureAnchorPlanningSystem))]
    public partial class RelicRealizationSystem : SystemBase
    {
        /// <summary>
        /// Tracks which generation version we last synced so we only
        /// re-diff when anchors may have changed.
        /// </summary>
        private uint _lastSyncedVersion;

        /// <summary>
        /// Set once after the first successful spawn pass to avoid
        /// re-running the diff every frame when nothing changes.
        /// </summary>
        private bool _initialSyncDone;

        protected override void OnCreate()
        {
            RequireForUpdate<StructurePlacementSingleton>();
            RequireForUpdate<RelicRenderConfig>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.ManagedAPI.GetSingleton<RelicRenderConfig>();
            if (config.Templates.Count == 0)
            {
                DebugSettings.LogRendering("RelicRealizationSystem: No templates registered — skipping.");
                return;
            }

            var singletonEntity = SystemAPI.GetSingletonEntity<StructurePlacementSingleton>();
            var anchors = EntityManager.GetBuffer<StructureAnchorRecord>(singletonEntity, true);

            uint currentVersion = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i].GenerationVersion > currentVersion)
                    currentVersion = anchors[i].GenerationVersion;
            }

            if (_initialSyncDone && currentVersion == _lastSyncedVersion)
                return;

            // Pass the entity, not the buffer — SyncRealizedEntities runs Entities.ForEach
            // and structural changes (DestroyEntity / CreateEntity) which invalidate any
            // DynamicBuffer reference obtained before them.
            SyncRealizedEntities(config, singletonEntity);
            _lastSyncedVersion = currentVersion;
            _initialSyncDone = true;
        }

        private void SyncRealizedEntities(RelicRenderConfig config, Entity singletonEntity)
        {
            var em = EntityManager;

            // Collect existing realized relic entities by StableAnchorId.
            var existingIds = new NativeHashMap<uint, Entity>(16, Allocator.Temp);
            Entities
                .WithAll<StructureRealizedTag>()
                .ForEach((Entity entity, in StructureRealizedTag tag) =>
                {
                    existingIds.TryAdd(tag.StableAnchorId, entity);
                })
                .WithoutBurst()
                .Run();

            // Re-fetch the buffer after Entities.ForEach.Run() — that call releases its
            // safety handles, which invalidates any DynamicBuffer reference taken before it.
            // Copy to NativeArray so subsequent DestroyEntity / CreateEntity calls (structural
            // changes) cannot invalidate it again.
            var anchorsBuffer = em.GetBuffer<StructureAnchorRecord>(singletonEntity, true);
            var anchors = anchorsBuffer.ToNativeArray(Allocator.Temp);

            // Build set of relic anchor IDs from the snapshot.
            var desiredIds = new NativeHashSet<uint>(16, Allocator.Temp);
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i].Family == StructureFamilyId.Relic)
                    desiredIds.Add(anchors[i].StableAnchorId);
            }

            // Destroy entities whose anchors were removed.
            var toDestroy = new NativeList<Entity>(4, Allocator.Temp);
            foreach (var kv in existingIds)
            {
                if (!desiredIds.Contains(kv.Key))
                    toDestroy.Add(kv.Value);
            }
            for (int i = 0; i < toDestroy.Length; i++)
            {
                DebugSettings.LogRendering(
                    $"RelicRealizationSystem: Destroying stale relic entity for anchor {toDestroy[i]}");
                em.DestroyEntity(toDestroy[i]);
            }

            // Spawn entities for new anchors.
            int spawnCount = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor.Family != StructureFamilyId.Relic)
                    continue;

                if (existingIds.ContainsKey(anchor.StableAnchorId))
                    continue;

                var template = config.GetTemplate(anchor.TemplateId);
                if (template == null || template.Mesh == null || template.Material == null)
                {
                    DebugSettings.LogRendering(
                        $"RelicRealizationSystem: No valid template for '{anchor.TemplateId}' — skipping anchor {anchor.StableAnchorId:X8}");
                    continue;
                }

                SpawnRelicEntity(em, config, template, anchor);
                spawnCount++;
            }

            if (spawnCount > 0 || toDestroy.Length > 0)
            {
                DebugSettings.LogRendering(
                    $"RelicRealizationSystem: Spawned {spawnCount}, destroyed {toDestroy.Length} " +
                    $"relic entities (desired={desiredIds.Count})",
                    forceLog: true);
            }

            anchors.Dispose();
            toDestroy.Dispose();
            desiredIds.Dispose();
            existingIds.Dispose();
        }

        /// <summary>
        /// Creates a single ECS entity with Entities Graphics render components
        /// using the template's mesh/material. Sets per-entity <see cref="RelicLodParams"/>
        /// so the LOD system can swap without knowing the template.
        /// </summary>
        private static void SpawnRelicEntity(
            EntityManager em,
            RelicRenderConfig config,
            RelicTemplateEntry template,
            StructureAnchorRecord anchor)
        {
            var entity = em.CreateEntity();

            // Lifecycle tracking
            em.AddComponentData(entity, new StructureRealizedTag
            {
                StableAnchorId = anchor.StableAnchorId,
            });

            // Transform
            float scale = math.max(0.01f, math.abs(template.UniformScale));
            var pos = anchor.WorldPosition;
            pos.y += template.YOffset;
            em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(
                pos,
                anchor.Rotation,
                scale));

#if UNITY_ENTITIES_GRAPHICS
            // R6 P4: every realization starts fully dithered out and fades in over
            // ~0.5s (RelicSpawnFadeSystem) — streaming pop fix for relics realizing
            // inside the view. Added unconditionally: off-screen fades finish unseen.
            em.AddComponentData(entity, new RelicSpawnFade { Value = 0f });

            var renderMeshDescription = new RenderMeshDescription(
                shadowCastingMode: ShadowCastingMode.On,
                receiveShadows: true);

            // Per-entity RenderBounds from mesh local AABB
            var fullBounds = new AABB
            {
                Center = template.Mesh.bounds.center,
                Extents = template.Mesh.bounds.extents,
            };

            if (!TemplateParticipatesInLod(template))
            {
                // DISTANCE LOD IS CURRENTLY SKIPPED FOR THIS RELIC — deliberately, not
                // as an oversight (owner decision 2026-07-09, ticket V16). No billboard
                // impostor art has ever been authored, and the old fallback used the
                // SAME full mesh as "LOD 1", rescaled to a fixed target size — saving
                // zero vertices while visibly popping the relic's world size at the
                // swap distance. With relics rare (~6 in range after the V12 retune)
                // and low-poly in a scene that is vertex-bound by trees/rocks, per-
                // entity LOD buys nothing today. So: single render entry, full mesh at
                // all distances, and no RelicLodParams/RelicLodState — which means
                // RelicLodSelectionSystem's query never matches this entity and the
                // LOD machinery lies dormant. To re-enable LOD for a template, author
                // its ImpostorMesh — the full swap path below returns for its relics
                // with no other change.
                RenderMeshUtility.AddComponents(
                    entity, em, renderMeshDescription,
                    new RenderMeshArray(new[] { template.Material }, new[] { template.Mesh }),
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
                em.SetComponentData(entity, new RenderBounds { Value = fullBounds });
                return;
            }

            // Authored impostor present → this relic participates in distance LOD.
            var impostorMaterial = template.ImpostorMaterial != null ? template.ImpostorMaterial : template.Material;
            var impostorMesh = template.ImpostorMesh;

            var renderMeshArray = new RenderMeshArray(
                new[] { template.Material, impostorMaterial },
                new[] { template.Mesh, impostorMesh });

            RenderMeshUtility.AddComponents(
                entity, em, renderMeshDescription, renderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            em.SetComponentData(entity, new RenderBounds { Value = fullBounds });

            // Compute impostor LOD scale (same derivation as the old singleton path)
            var impostorMeshBounds = impostorMesh.bounds;
            float impostorTargetHalfExtent = math.max(0.01f, math.abs(template.ImpostorScale));
            float impostorMaxHalfExtent = math.max(math.max(
                impostorMeshBounds.extents.x,
                impostorMeshBounds.extents.y),
                impostorMeshBounds.extents.z);
            float computedImpostorScale = impostorMaxHalfExtent > 0.0001f
                ? impostorTargetHalfExtent / impostorMaxHalfExtent
                : impostorTargetHalfExtent;

            var impostorBounds = new AABB
            {
                Center = impostorMeshBounds.center,
                Extents = impostorMeshBounds.extents,
            };

            // Per-entity LOD parameters — set once, read per-frame by RelicLodSelectionSystem
            em.AddComponentData(entity, new RelicLodParams
            {
                FullScale = scale,
                ImpostorScale = computedImpostorScale,
                FullBoundsLocal = fullBounds,
                ImpostorBoundsLocal = impostorBounds,
            });

            em.AddComponentData(entity, new RelicLodState { CurrentLod = 0 });
#endif
        }

#if UNITY_ENTITIES_GRAPHICS
        /// <summary>
        /// A relic participates in distance LOD only when its template has an
        /// AUTHORED impostor mesh. While none exists (ticket V16), relics render
        /// the full mesh at every distance and the LOD system idles — see the
        /// dormancy comment in <see cref="SpawnRelicEntity"/> for the rationale.
        /// Pure-static so the decision is unit-testable without a live World
        /// (same pattern as <see cref="RelicLodSelectionSystem.ResolveTargetLod"/>).
        /// </summary>
        public static bool TemplateParticipatesInLod(RelicTemplateEntry template)
        {
            return template != null && template.ImpostorMesh != null;
        }
#endif
    }
}
