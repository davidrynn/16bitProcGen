using Unity.Mathematics;
using UnityEngine;

namespace DOTS.Terrain
{
    /// <summary>
    /// The single serialized home for every <c>H</c> tunable (spec §4.2, ticket H1). One asset,
    /// deliberately: the persistence save-config hash (§5.1) is
    /// <c>hash(worldSeed, WorldStructureSettings, TerrainGenerationSettings)</c>, so scattering
    /// <c>H</c> literals across code would silently break save versioning. Every dial that shapes the
    /// macro field lives here and nowhere else.
    ///
    /// <para><b>Why a new asset rather than extending <c>TerrainGenerationSettings</c>:</b> that class
    /// lives in <c>DOTS.Terrain.Legacy</c>, the quarantined heightmap pipeline CLAUDE.md forbids
    /// extending. A fresh asset is the only quarantine-safe home for the hash surface — this resolves
    /// spec §12 open-question 4.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "WorldStructureSettings",
        menuName = "DOTS/Terrain/World Structure Settings")]
    public class WorldStructureSettings : ScriptableObject
    {
        // Defaults chosen for the resolved chunk budget: ANear ≤ ~4u fits the ~16u single-layer slab
        // alongside the existing ±4u surface noise (spec §5.3 / Q1). AFar/ramp put real relief beyond
        // the 600u world edge where only impostor reps render. Owner eyeballs these in Phase B.

        [Header("Macro noise")]
        [Tooltip("Noise-space frequency applied to world XZ. Macro wavelength ≈ 1 / this " +
                 "(0.0004 ≈ a ~2500u ridge spacing).")]
        [Min(1e-6f)]
        public float macroFreq = 0.0004f;

        [Tooltip("Ridged-FBM octave count (~4 — the V15 sky-band character).")]
        [Range(1, 8)]
        public int octaves = 4;

        [Tooltip("Per-octave frequency multiplier.")]
        [Range(1.5f, 3f)]
        public float lacunarity = 2.0f;

        [Tooltip("Per-octave amplitude falloff.")]
        [Range(0.1f, 0.9f)]
        public float gain = 0.5f;

        [Header("Wilderness ramp A(r)")]
        [Tooltip("Peak relief near the playfield, world units. Must fit the ~16u chunk slab with the " +
                 "existing ±4u surface noise — keep ≤ ~4u until vertical chunking lands (spec §5.3).")]
        [Range(0f, 8f)]
        public float aNear = 3.0f;

        [Tooltip("Peak relief at the world rim, world units (only impostor reps render out there).")]
        [Range(0f, 400f)]
        public float aFar = 200.0f;

        [Tooltip("Distance where the ramp begins rising toward AFar (~the 600u world edge).")]
        [Min(0f)]
        public float rampStart = 600.0f;

        [Tooltip("Distance where the ramp reaches AFar.")]
        [Min(0f)]
        public float rampEnd = 2500.0f;

        [Header("Seed")]
        [Tooltip("World seed used for editor preview and tests. The live world seed is wired by the " +
                 "broader generation pipeline; H is a pure function of it (spec §3).")]
        public uint defaultWorldSeed = 12345u;

        /// <summary>
        /// Build the Burst-friendly <see cref="WorldStructureConstants"/> snapshot for a given world
        /// seed. The seed→offset hash lives on the C# side (<see cref="WorldStructure.SeedOffset"/>)
        /// and is baked into the returned <see cref="WorldStructureConstants.SeedOffset"/>.
        /// </summary>
        public WorldStructureConstants ToConstants(uint worldSeed) => new WorldStructureConstants
        {
            MacroFreq = macroFreq,
            SeedOffset = WorldStructure.SeedOffset(worldSeed),
            Octaves = octaves,
            Lacunarity = lacunarity,
            Gain = gain,
            ANear = aNear,
            AFar = aFar,
            RampStart = rampStart,
            RampEnd = rampEnd,
        };

        /// <summary>Convenience overload using <see cref="defaultWorldSeed"/>.</summary>
        public WorldStructureConstants ToConstants() => ToConstants(defaultWorldSeed);

        /// <summary>
        /// The code-default constants (used when no <c>WorldStructureSettings.asset</c> is present) —
        /// <see cref="WorldStructureBroadcast"/>'s fallback seed so shaders never read zeroed
        /// <c>_WorldMacro*</c> globals. The literals here mirror the field initializers above; the two
        /// are kept in lockstep by <c>WorldStructureBroadcastTests.DefaultConstants_MatchFieldDefaults</c>
        /// (that test fails if either drifts).
        /// </summary>
        public static WorldStructureConstants DefaultConstants => new WorldStructureConstants
        {
            MacroFreq = 0.0004f,
            SeedOffset = WorldStructure.SeedOffset(12345u),
            Octaves = 4,
            Lacunarity = 2.0f,
            Gain = 0.5f,
            ANear = 3.0f,
            AFar = 200.0f,
            RampStart = 600.0f,
            RampEnd = 2500.0f,
        };

        /// <summary>
        /// Deterministic hash of this asset's field values folded with a world seed — the
        /// <c>WorldStructureSettings</c> contribution to the persistence save-config hash (spec §5.1).
        /// Phase E combines this with the <c>TerrainGenerationSettings</c> hash and <c>worldSeed</c>;
        /// H1 provides the per-asset piece. Any dial change here shifts the hash, which is the point —
        /// it invalidates saved edits that were replayed against the old base field.
        /// </summary>
        public uint ComputeConfigHash(uint worldSeed)
        {
            // FNV-1a over the raw field bits. Floats go in as their IEEE-754 bit pattern so the hash
            // is exact and platform-stable (no float formatting in the middle).
            uint h = 2166136261u;
            h = Fold(h, worldSeed);
            h = Fold(h, math.asuint(macroFreq));
            h = Fold(h, (uint)octaves);
            h = Fold(h, math.asuint(lacunarity));
            h = Fold(h, math.asuint(gain));
            h = Fold(h, math.asuint(aNear));
            h = Fold(h, math.asuint(aFar));
            h = Fold(h, math.asuint(rampStart));
            h = Fold(h, math.asuint(rampEnd));
            return h;
        }

        private static uint Fold(uint h, uint value)
        {
            // Mix all four bytes of value, FNV-1a style.
            for (int b = 0; b < 4; b++)
            {
                h ^= (value >> (b * 8)) & 0xFFu;
                h *= 16777619u;
            }
            return h;
        }
    }
}
