using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace DOTS.Structures
{
    /// <summary>
    /// Pure-static deterministic anchor planning logic. Separated from the system
    /// so Burst doesn't promote struct-typed parameters to C-ABI entry points
    /// (same pattern as TreePlacementAlgorithm).
    ///
    /// Planning cells are world-space squares much larger than terrain chunks.
    /// Candidates are hashed from (worldSeed, cellCoord, familyId, candidateIndex)
    /// and evaluated in row-major cell order so the tie-break rule is deterministic
    /// regardless of streaming order.
    /// </summary>
    public static class StructureAnchorPlanningAlgorithm
    {
        /// <summary>
        /// Default planning cell size in world units. Each cell can produce
        /// candidates for each structure family. Should be larger than any
        /// family's MinSpacing to avoid degenerate all-reject passes.
        /// </summary>
        public const float DefaultPlanningCellSize = 256f;

        /// <summary>
        /// Generates accepted structure anchors for all families within a region
        /// of planning cells. Evaluates cells in row-major order so earlier-accepted
        /// anchors deterministically win spacing conflicts.
        /// </summary>
        /// <param name="worldSeed">World seed from TerrainGenerationContext.</param>
        /// <param name="generationVersion">Current generation version for cache invalidation.</param>
        /// <param name="cellMin">Minimum planning cell coordinate (inclusive).</param>
        /// <param name="cellMax">Maximum planning cell coordinate (inclusive).</param>
        /// <param name="families">Array of family rule data to evaluate.</param>
        /// <param name="existingAnchors">Already-accepted anchors (for in-memory apply of locked/modified).</param>
        /// <param name="terrainSampler">Delegate-like struct for querying terrain height and slope.</param>
        /// <param name="accepted">Output list of accepted anchor records.</param>
        public static void GenerateAnchors(
            uint worldSeed,
            uint generationVersion,
            int2 cellMin,
            int2 cellMax,
            NativeArray<FamilyRuleData> families,
            NativeList<StructureAnchorRecord> existingAnchors,
            in TerrainSampleData terrainSampler,
            ref NativeList<StructureAnchorRecord> accepted)
        {
            // Preserve locked/modified anchors from previous passes
            for (int i = 0; i < existingAnchors.Length; i++)
            {
                var a = existingAnchors[i];
                if ((a.PersistenceFlags & (StructurePersistenceFlags.Locked | StructurePersistenceFlags.Modified)) != 0)
                {
                    accepted.Add(a);
                }
            }

            // Row-major evaluation for deterministic tie-break
            for (int cz = cellMin.y; cz <= cellMax.y; cz++)
            {
                for (int cx = cellMin.x; cx <= cellMax.x; cx++)
                {
                    var cell = new int2(cx, cz);

                    for (int fi = 0; fi < families.Length; fi++)
                    {
                        var family = families[fi];
                        EvaluateCandidatesForCell(
                            worldSeed, generationVersion, cell,
                            in family, in terrainSampler,
                            ref accepted);
                    }
                }
            }
        }

        private static void EvaluateCandidatesForCell(
            uint worldSeed,
            uint generationVersion,
            int2 cell,
            in FamilyRuleData family,
            in TerrainSampleData terrain,
            ref NativeList<StructureAnchorRecord> accepted)
        {
            float cellSize = DefaultPlanningCellSize;
            float2 cellOrigin = new float2(cell.x * cellSize, cell.y * cellSize);

            for (int ci = 0; ci < family.CandidatesPerCell; ci++)
            {
                uint candidateHash = CandidateHash(worldSeed, cell, (byte)family.Family, ci);

                // Jitter position within cell
                float2 jitter = HashToJitter(candidateHash, cellSize * 0.4f);
                float2 candidateXZ = cellOrigin + new float2(cellSize * 0.5f) + jitter;

                // Sample terrain
                float terrainHeight = SampleTerrainHeight(candidateXZ, in terrain);
                float slopeNormalY = SampleSlopeNormalY(candidateXZ, in terrain);

                // Hard terrain-fit constraints
                if (terrainHeight < family.MinElevation || terrainHeight > family.MaxElevation)
                    continue;
                if (slopeNormalY < family.MinSlopeNormalY)
                    continue;

                // Hard spacing check against all already-accepted anchors of same family
                float3 candidatePos = new float3(candidateXZ.x, terrainHeight, candidateXZ.y);
                if (ViolatesSpacing(candidatePos, family.Family, family.MinSpacing, ref accepted))
                    continue;

                // Generate stable ID from deterministic inputs (never from acceptance order)
                uint stableId = StableAnchorId(worldSeed, cell, (byte)family.Family, ci);

                // Skip if this ID is already accepted (from locked/modified pass)
                if (AnchorIdExists(stableId, ref accepted))
                    continue;

                accepted.Add(new StructureAnchorRecord
                {
                    Family = family.Family,
                    PlanningCell = cell,
                    WorldPosition = candidatePos,
                    Rotation = Unity.Mathematics.quaternion.RotateY(HashToYaw(candidateHash)),
                    Radius = family.FootprintRadius,
                    StableAnchorId = stableId,
                    GenerationVersion = generationVersion,
                    TemplateId = family.DefaultTemplateId,
                    Source = StructurePlacementSource.SeededAnchor,
                    PersistenceFlags = StructurePersistenceFlags.None,
                });
            }
        }

        // ── Spacing ──────────────────────────────────────────────────────────

        private static bool ViolatesSpacing(
            float3 candidatePos,
            StructureFamilyId family,
            float minSpacing,
            ref NativeList<StructureAnchorRecord> accepted)
        {
            float minDistSq = minSpacing * minSpacing;
            for (int i = 0; i < accepted.Length; i++)
            {
                if (accepted[i].Family != family) continue;
                float distSq = distancesq(candidatePos, accepted[i].WorldPosition);
                if (distSq < minDistSq) return true;
            }
            return false;
        }

        private static bool AnchorIdExists(uint id, ref NativeList<StructureAnchorRecord> accepted)
        {
            for (int i = 0; i < accepted.Length; i++)
            {
                if (accepted[i].StableAnchorId == id) return true;
            }
            return false;
        }

        // ── Terrain sampling ─────────────────────────────────────────────────

        /// <summary>
        /// Approximate terrain height at (x, z) using the SDF ground function.
        /// SdLayeredGround returns (y - height), so at y=0: height = -result.
        /// </summary>
        public static float SampleTerrainHeight(float2 xz, in TerrainSampleData t)
        {
            return -DOTS.Terrain.SDFMath.SdLayeredGround(
                new float3(xz.x, 0f, xz.y), in t.FieldSettings, t.WorldSeed);
        }

        /// <summary>
        /// Approximate slope normal Y by finite-difference gradient of terrain height.
        /// Returns dot(surfaceNormal, up) — 1.0 = perfectly flat.
        /// </summary>
        public static float SampleSlopeNormalY(float2 xz, in TerrainSampleData t)
        {
            const float eps = 2f;
            float hC = SampleTerrainHeight(xz, in t);
            float hX = SampleTerrainHeight(xz + new float2(eps, 0f), in t);
            float hZ = SampleTerrainHeight(xz + new float2(0f, eps), in t);

            float3 dx = new float3(eps, hX - hC, 0f);
            float3 dz = new float3(0f, hZ - hC, eps);
            float3 normal = normalize(cross(dz, dx));
            return normal.y;
        }

        // ── Hashing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Deterministic hash for a structure candidate. Uses the same Fibonacci
        /// hashing approach as TreePlacementAlgorithm.CandidateHash.
        /// </summary>
        public static uint CandidateHash(uint worldSeed, int2 cell, byte familyId, int candidateIndex)
        {
            uint hash = worldSeed;
            hash ^= (uint)cell.x * 2654435761u;
            hash ^= (uint)cell.y * 1013904223u;
            hash ^= (uint)familyId * 374761393u;
            hash ^= (uint)candidateIndex * 668265263u;
            hash *= 0x9e3779b9u;
            // Extra mixing round for better distribution at region scale
            hash ^= hash >> 16;
            hash *= 0x85ebca6bu;
            hash ^= hash >> 13;
            return hash;
        }

        /// <summary>
        /// StableAnchorId is a separate hash from the candidate hash so it remains
        /// stable even if the candidate evaluation changes. Derived purely from
        /// (worldSeed, cell, family, candidateIndex).
        /// </summary>
        public static uint StableAnchorId(uint worldSeed, int2 cell, byte familyId, int candidateIndex)
        {
            uint hash = worldSeed ^ 0xDEADBEEFu; // different seed lane than CandidateHash
            hash ^= (uint)cell.x * 2246822519u;
            hash ^= (uint)cell.y * 3266489917u;
            hash ^= (uint)familyId * 668265263u;
            hash ^= (uint)candidateIndex * 374761393u;
            hash *= 0x9e3779b9u;
            hash ^= hash >> 16;
            hash *= 0x85ebca6bu;
            hash ^= hash >> 13;
            return hash;
        }

        private static float2 HashToJitter(uint hash, float maxRadius)
        {
            float jx = ((hash >> 8) & 0xFFFFu) * (1f / 65535f) * 2f - 1f;
            float jz = ((hash >> 20) & 0xFFFu) * (1f / 4095f) * 2f - 1f;
            return new float2(jx, jz) * maxRadius;
        }

        private static float HashToYaw(uint hash)
        {
            return ((hash >> 4) & 0xFFFFu) * (1f / 65535f) * PI * 2f;
        }
    }

    // ── Data carriers ────────────────────────────────────────────────────────

    /// <summary>
    /// Unmanaged mirror of StructureFamilyRuleset fields for Burst-safe planning.
    /// Populated from the ScriptableObject at system startup.
    /// </summary>
    public struct FamilyRuleData
    {
        public StructureFamilyId Family;
        public float MinSpacing;
        public float MaxSpacing;
        public int CandidatesPerCell;
        public float MinSlopeNormalY;
        public float MinElevation;
        public float MaxElevation;
        public float FootprintRadius;
        public FixedString64Bytes DefaultTemplateId;
        public float RealizationScale;
    }

    /// <summary>
    /// Carries terrain sampling context into the planning algorithm
    /// without referencing managed types.
    /// </summary>
    public struct TerrainSampleData
    {
        public DOTS.Terrain.TerrainFieldSettings FieldSettings;
        public uint WorldSeed;
    }
}
