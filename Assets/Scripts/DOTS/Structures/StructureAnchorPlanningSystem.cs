using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;
using DOTS.Core;

namespace DOTS.Structures
{
    /// <summary>
    /// Region-scale deterministic anchor planning for structure placement.
    /// Runs once when TerrainGenerationContext appears, then re-runs only when
    /// generation version changes. Evaluates planning cells around the world
    /// origin (MVP) or around the player (streaming extension).
    ///
    /// Reads family rulesets from Resources, converts to Burst-safe FamilyRuleData,
    /// delegates to StructureAnchorPlanningAlgorithm for deterministic evaluation.
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DOTS.Terrain.Trees.TreePlacementGenerationSystem))]
    public partial struct StructureAnchorPlanningSystem : ISystem
    {
        /// <summary>
        /// Tracks which generation version we last planned for so we only
        /// re-plan when the terrain algorithm changes.
        /// </summary>
        private uint _lastPlannedVersion;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainGenerationContext>();
            state.RequireForUpdate<TerrainFieldSettings>();
            _lastPlannedVersion = 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            var context = SystemAPI.GetSingleton<TerrainGenerationContext>();

            // Skip if already planned for this version
            if (_lastPlannedVersion == context.GenerationVersion && context.GenerationVersion > 0)
                return;

            var fieldSettings = SystemAPI.GetSingleton<TerrainFieldSettings>();
            var em = state.EntityManager;

            // Load family rulesets from Resources (managed call — not Burstable)
            var rulesets = Resources.LoadAll<StructureFamilyRuleset>("");
            if (rulesets == null || rulesets.Length == 0)
            {
                DebugSettings.LogTerrain("StructureAnchorPlanningSystem: No StructureFamilyRuleset assets found in Resources.");
                _lastPlannedVersion = context.GenerationVersion;
                return;
            }

            // Convert managed SO data to unmanaged array
            var families = new NativeArray<FamilyRuleData>(rulesets.Length, Allocator.Temp);
            for (int i = 0; i < rulesets.Length; i++)
            {
                var r = rulesets[i];
                families[i] = new FamilyRuleData
                {
                    Family = r.Family,
                    MinSpacing = r.MinSpacing,
                    MaxSpacing = r.MaxSpacing,
                    CandidatesPerCell = r.CandidatesPerCell,
                    MinSlopeNormalY = r.MinSlopeNormalY,
                    MinElevation = r.MinElevation,
                    MaxElevation = r.MaxElevation,
                    FootprintRadius = math.max(r.FootprintExtents.x, r.FootprintExtents.z),
                    DefaultTemplateId = new FixedString64Bytes(r.DefaultTemplateId ?? ""),
                    RealizationScale = r.RealizationScale,
                };
            }

            var terrainSampler = new TerrainSampleData
            {
                FieldSettings = fieldSettings,
                WorldSeed = context.WorldSeed,
            };

            // Find or create the singleton entity
            Entity singletonEntity;
            var existingAnchors = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);

            if (SystemAPI.HasSingleton<StructurePlacementSingleton>())
            {
                singletonEntity = SystemAPI.GetSingletonEntity<StructurePlacementSingleton>();
                // Preserve existing locked/modified anchors for in-memory apply
                var existingBuffer = em.GetBuffer<StructureAnchorRecord>(singletonEntity);
                for (int i = 0; i < existingBuffer.Length; i++)
                    existingAnchors.Add(existingBuffer[i]);
            }
            else
            {
                singletonEntity = em.CreateEntity();
                em.AddComponent<StructurePlacementSingleton>(singletonEntity);
                em.AddBuffer<StructureAnchorRecord>(singletonEntity);
                em.AddBuffer<StructureFootprintReservation>(singletonEntity);
            }

            // Authored anchor inputs (STRUCTURE_PLACEMENT_SPEC.md §9.5) — the
            // AuthoredAnchorBootstrap creates this buffer in Start, which runs
            // before the first SimulationSystemGroup update of the same frame,
            // so planning never races ahead of authoring.
            NativeArray<AuthoredAnchorInput> authored;
            if (SystemAPI.TryGetSingletonBuffer<AuthoredAnchorInput>(out var authoredBuffer, true))
                authored = authoredBuffer.ToNativeArray(Allocator.Temp);
            else
                authored = new NativeArray<AuthoredAnchorInput>(0, Allocator.Temp);

            // MVP: plan a fixed region around world origin.
            // Planning cell grid covers -PlanRadius to +PlanRadius in each axis.
            const int planRadiusCells = 4;
            int2 cellMin = new int2(-planRadiusCells, -planRadiusCells);
            int2 cellMax = new int2(planRadiusCells - 1, planRadiusCells - 1);

            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);

            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                context.WorldSeed,
                context.GenerationVersion,
                cellMin, cellMax,
                families,
                existingAnchors,
                authored,
                in terrainSampler,
                ref accepted);

            WarnOnAuthoredConstraintViolations(accepted, families, in terrainSampler);

            // Assign per-anchor TemplateId from the family's available templates.
            // Uses StableAnchorId for deterministic selection so the same world
            // seed always produces the same relic type at the same location.
            for (int i = 0; i < accepted.Length; i++)
            {
                var anchor = accepted[i];
                // Skip locked/modified anchors — their TemplateId is already set
                if ((anchor.PersistenceFlags & (StructurePersistenceFlags.Locked | StructurePersistenceFlags.Modified)) != 0)
                    continue;
                // Skip authored anchors — their TemplateId is explicit authoring data
                if (anchor.Source == StructurePlacementSource.Authored)
                    continue;

                for (int ri = 0; ri < rulesets.Length; ri++)
                {
                    if (rulesets[ri].Family != anchor.Family) continue;
                    var templateIds = rulesets[ri].AvailableTemplateIds;
                    if (templateIds != null && templateIds.Length > 0)
                    {
                        int templateIndex = (int)(anchor.StableAnchorId % (uint)templateIds.Length);
                        anchor.TemplateId = new FixedString64Bytes(templateIds[templateIndex]);
                        accepted[i] = anchor;
                    }
                    break;
                }
            }

            // Write results to singleton buffer
            var anchorBuffer = em.GetBuffer<StructureAnchorRecord>(singletonEntity);
            anchorBuffer.Clear();
            for (int i = 0; i < accepted.Length; i++)
                anchorBuffer.Add(accepted[i]);

            // Write footprint reservations
            var footprintBuffer = em.GetBuffer<StructureFootprintReservation>(singletonEntity);
            footprintBuffer.Clear();
            for (int i = 0; i < accepted.Length; i++)
            {
                var a = accepted[i];
                // Find the family rule to get proper extents
                float radius = a.Radius;
                footprintBuffer.Add(new StructureFootprintReservation
                {
                    StableAnchorId = a.StableAnchorId,
                    Center = a.WorldPosition,
                    Extents = new float3(radius, radius * 0.5f, radius),
                });
            }

            int relicCount = 0, dungeonCount = 0;
            for (int i = 0; i < accepted.Length; i++)
            {
                var a = accepted[i];
                if (a.Family == StructureFamilyId.Relic) relicCount++;
                else if (a.Family == StructureFamilyId.Dungeon) dungeonCount++;

                // Log first 5 anchors with positions for debugging
                if (i < 5)
                {
                    DebugSettings.LogTerrain(
                        $"  Anchor[{i}]: {a.Family} id={a.StableAnchorId:X8} " +
                        $"pos=({a.WorldPosition.x:F0}, {a.WorldPosition.y:F0}, {a.WorldPosition.z:F0}) " +
                        $"cell=({a.PlanningCell.x},{a.PlanningCell.y})",
                        forceLog: true);
                }
            }

            DebugSettings.LogTerrain(
                $"StructureAnchorPlanningSystem: Planned {accepted.Length} anchors " +
                $"({relicCount} relics, {dungeonCount} dungeons) " +
                $"across {(cellMax.x - cellMin.x + 1) * (cellMax.y - cellMin.y + 1)} cells " +
                $"(version {context.GenerationVersion})",
                forceLog: true);

            _lastPlannedVersion = context.GenerationVersion;

            // Cleanup
            accepted.Dispose();
            existingAnchors.Dispose();
            families.Dispose();
            authored.Dispose();
        }

        /// <summary>
        /// Authored anchors bypass hard constraints by design (guaranteed placement),
        /// so authoring mistakes surface as warnings, not rejections: terrain-fit
        /// violations against the family ruleset, and same-family spacing conflicts.
        /// Cheap — runs once per plan over a handful of authored anchors.
        /// </summary>
        private static void WarnOnAuthoredConstraintViolations(
            NativeList<StructureAnchorRecord> accepted,
            NativeArray<FamilyRuleData> families,
            in TerrainSampleData terrainSampler)
        {
            for (int i = 0; i < accepted.Length; i++)
            {
                var a = accepted[i];
                if (a.Source != StructurePlacementSource.Authored)
                    continue;

                // Radius 0 means no family ruleset matched and no explicit radius
                // was authored — the footprint reservation is zero-extent, so
                // scatter exclusion (§8.3) silently won't apply.
                if (a.Radius <= 0f)
                    DebugSettings.LogWarning(
                        $"Authored anchor {a.TemplateId} ({a.StableAnchorId:X8}) has zero footprint radius " +
                        "(no family ruleset matched, no explicit radius) — scatter exclusion will not apply.");

                for (int fi = 0; fi < families.Length; fi++)
                {
                    if (families[fi].Family != a.Family) continue;
                    var family = families[fi];

                    var xz = new float2(a.WorldPosition.x, a.WorldPosition.z);
                    float height = StructureAnchorPlanningAlgorithm.SampleTerrainHeight(xz, in terrainSampler);
                    float slopeY = StructureAnchorPlanningAlgorithm.SampleSlopeNormalY(xz, in terrainSampler);

                    if (height < family.MinElevation || height > family.MaxElevation)
                        DebugSettings.LogWarning(
                            $"Authored anchor {a.TemplateId} ({a.StableAnchorId:X8}) elevation {height:F1} " +
                            $"outside family range [{family.MinElevation}, {family.MaxElevation}] — placed anyway.");
                    if (slopeY < family.MinSlopeNormalY)
                        DebugSettings.LogWarning(
                            $"Authored anchor {a.TemplateId} ({a.StableAnchorId:X8}) slope normalY {slopeY:F2} " +
                            $"below family minimum {family.MinSlopeNormalY:F2} — placed anyway.");

                    for (int j = 0; j < accepted.Length; j++)
                    {
                        if (j == i || accepted[j].Family != a.Family) continue;
                        float dist = math.distance(a.WorldPosition, accepted[j].WorldPosition);
                        if (dist < family.MinSpacing)
                            DebugSettings.LogWarning(
                                $"Authored anchor {a.TemplateId} ({a.StableAnchorId:X8}) is {dist:F0}u from " +
                                $"anchor {accepted[j].StableAnchorId:X8}, under family MinSpacing {family.MinSpacing:F0} — placed anyway.");
                    }
                    break;
                }
            }
        }
    }
}
