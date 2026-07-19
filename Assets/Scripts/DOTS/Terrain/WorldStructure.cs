using Unity.Mathematics;

namespace DOTS.Terrain
{
    /// <summary>
    /// The <c>H</c> authority — the world macro-structure heightfield
    /// (<c>Assets/Docs/Terrain/WORLD_STRUCTURE_SPEC.md</c>, ticket H1).
    ///
    /// <para><c>H(x, z)</c> takes a world-space XZ position and returns a world-Y offset in world
    /// units, deterministic from the world seed. It is the single shape every representation of the
    /// world will sample — near-field SDF density (Phase C), the mid-field disc undulation (Phase B),
    /// and the far sky band / horizon ring (Phase B / Phase 2) — so all of them agree *by
    /// construction*, exactly as <c>GroundNoise.hlsl</c> makes the terrain↔disc color seam vanish by
    /// sharing one world-space noise. Phase A wires no consumers; it only stands the function up.</para>
    ///
    /// <para><b>This C# implementation and <c>Assets/Shaders/WorldStructure.hlsl</c> are a mirrored
    /// pair — the same math in both languages (the <c>GroundNoise</c> precedent). They MUST stay in
    /// lockstep: <c>WorldStructureParityTests</c> pins the C# side, and any edit here has a twin edit
    /// there.</b> NUnit can only execute the C# side, so — like the <c>GroundNoise</c> contract tests —
    /// the guarantee is a line-for-line structural mirror plus property/golden pins on C#, not a live
    /// GPU comparison.</para>
    ///
    /// <para><b>Determinism (spec §3):</b> pure function of <c>(worldSeed, WorldStructureSettings)</c>.
    /// No frame time, camera, or player state may enter here — persistence (Phase E) depends on it, so
    /// treat a nondeterminism leak as a correctness bug, not a style nit.</para>
    ///
    /// <para>The hash/value-noise primitives are intentionally a *private copy* of
    /// <c>GroundNoiseCore.hlsl</c>'s, not a reuse: <c>H</c> owns its noise so the save-config hash
    /// surface (§5.1) never couples to ground-patch color tuning. The ridged transform
    /// <c>(1 − |2n − 1|)²</c> is inherited from the V15 sky band (spec §5.5) so the Phase-B band swap
    /// starts from matching character.</para>
    /// </summary>
    public static class WorldStructure
    {
        /// <summary>Upper bound on the seed-derived noise-space offset (spec §4.1: ≤ ~10⁴, keeps
        /// noise inputs small for float precision — §5.9). Mirror of the HLSL constant.</summary>
        public const float MaxSeedOffset = 10000f;

        // ── Noise primitives (private copy of GroundNoiseCore.hlsl — see class remarks) ──────────

        /// <summary>2D hash → [0,1). Bit-identical to <c>GroundNoiseCore.hlsl:GroundHash21</c>;
        /// HLSL <c>frac</c> == <c>math.frac</c> (x − floor(x)) on negatives too, so C#/GPU agree.</summary>
        public static float Hash21(float2 p)
        {
            p = math.frac(p * new float2(234.34f, 435.345f));
            float d = math.dot(p, p + new float2(34.23f, 34.23f));
            p += new float2(d, d);
            return math.frac(p.x * p.y);
        }

        /// <summary>Bilinear value noise with the classic smootherstep-free <c>3f²−2f³</c> fade.</summary>
        public static float ValueNoise(float2 p)
        {
            float2 i = math.floor(p);
            float2 f = math.frac(p);
            float2 u = f * f * (3.0f - 2.0f * f);
            return math.lerp(
                math.lerp(Hash21(i), Hash21(i + new float2(1.0f, 0.0f)), u.x),
                math.lerp(Hash21(i + new float2(0.0f, 1.0f)), Hash21(i + new float2(1.0f, 1.0f)), u.x),
                u.y);
        }

        /// <summary>
        /// Ridged fractal noise, normalized to [0,1]. Each octave applies the V15 ridged transform
        /// (<c>r = 1 − |2n − 1|</c>, then squared for sharp crests / V-valleys) and accumulates at a
        /// halving amplitude. Normalizing by the summed amplitude — accumulated in-loop rather than
        /// closed-form so C# and HLSL divide by the identical value — makes the amplitude ramp
        /// <see cref="AmplitudeRamp"/> read as literal peak units regardless of octave count.
        /// </summary>
        public static float RidgedFBM(float2 p, int octaves, float lacunarity, float gain)
        {
            float sum = 0.0f;
            float amp = 0.5f;
            float freq = 1.0f;
            float norm = 0.0f;
            for (int o = 0; o < octaves; o++)
            {
                float n = ValueNoise(p * freq);
                float r = 1.0f - math.abs(2.0f * n - 1.0f);
                sum += r * r * amp;
                norm += amp;
                freq *= lacunarity;
                amp *= gain;
            }
            // norm is 0 only if octaves <= 0 (a misconfiguration); guard so H stays finite.
            return norm > 0.0f ? sum / norm : 0.0f;
        }

        /// <summary>
        /// The wilderness ramp <c>A(r)</c> (spec §4.1): amplitude envelope over distance from the
        /// playfield origin. Gentle highland bowl near spawn (<paramref name="aNear"/>), rising to a
        /// mountain rim beyond the world edge (<paramref name="aFar"/>). This is decision D3 encoded
        /// in the function: reachability (Phase 2) later relaxes the ramp with zero consumer changes.
        /// </summary>
        public static float AmplitudeRamp(float r, float aNear, float aFar, float rampStart, float rampEnd)
        {
            return math.lerp(aNear, aFar, math.smoothstep(rampStart, rampEnd, r));
        }

        /// <summary>
        /// Sample <c>H(x, z)</c> = <c>A(r) · ridgedFBM</c> at a world-XZ position. The authored
        /// flatten mask <c>M(x, z)</c> (spec §4.1) is 1 here — ticket H3 introduces the mask multiply
        /// and the vista-corridor protection; H1's field is unmasked.
        /// </summary>
        public static float Sample(float2 worldXZ, in WorldStructureConstants c)
        {
            float2 p = worldXZ * c.MacroFreq + c.SeedOffset;
            float ridged = RidgedFBM(p, c.Octaves, c.Lacunarity, c.Gain);
            float r = math.length(worldXZ);
            float a = AmplitudeRamp(r, c.ANear, c.AFar, c.RampStart, c.RampEnd);
            return a * ridged;
        }

        /// <summary>
        /// Map a world seed to the bounded noise-space offset (spec §4.1). Computed once in C# and
        /// broadcast as <c>_WorldMacroSeedOffset</c> (ticket H2) — the HLSL never re-derives it, so
        /// the seed→offset hash lives on one side only. Keep the offset small (≤ <see cref="MaxSeedOffset"/>);
        /// never offset noise coordinates by raw large integers (§5.9).
        /// </summary>
        public static float2 SeedOffset(uint worldSeed)
        {
            uint h = HashSeed(worldSeed);
            const float inv = 1.0f / 65535.0f;
            float ox = (h & 0xFFFFu) * inv * MaxSeedOffset;
            float oy = ((h >> 16) & 0xFFFFu) * inv * MaxSeedOffset;
            return new float2(ox, oy);
        }

        /// <summary>Integer avalanche hash (Wang-style) — spreads adjacent seeds far apart so
        /// worlds 12345 and 12346 look unrelated. Deterministic, no allocation.</summary>
        private static uint HashSeed(uint s)
        {
            s ^= 2747636419u; s *= 2654435769u;
            s ^= s >> 16; s *= 2654435769u;
            s ^= s >> 16; s *= 2654435769u;
            return s;
        }
    }

    /// <summary>
    /// Blittable, Burst-friendly snapshot of the <c>H</c> tunables (built from
    /// <see cref="WorldStructureSettings"/> for a given world seed). Value type, no managed
    /// references — safe to capture in jobs and pass by <c>in</c>. The full tunable surface plus the
    /// seed is exactly the save-config hash input (spec §5.1), which is why every dial lives here and
    /// not as scattered literals.
    /// </summary>
    public struct WorldStructureConstants
    {
        /// <summary>Noise-space frequency applied to world XZ (macro wavelength ≈ 1 / this).</summary>
        public float MacroFreq;
        /// <summary>Seed-derived offset in noise space (<see cref="WorldStructure.SeedOffset"/>).</summary>
        public float2 SeedOffset;
        /// <summary>Ridged-FBM octave count (~4).</summary>
        public int Octaves;
        /// <summary>Per-octave frequency multiplier (~2).</summary>
        public float Lacunarity;
        /// <summary>Per-octave amplitude falloff (~0.5).</summary>
        public float Gain;
        /// <summary>Peak amplitude near the playfield — must fit the ~16u chunk slab (spec §5.3 / Q1: ≤ ~4u).</summary>
        public float ANear;
        /// <summary>Peak amplitude at the world rim (~150–250u; only impostor reps render out there).</summary>
        public float AFar;
        /// <summary>Distance where the ramp begins rising toward <see cref="AFar"/> (~600u world edge).</summary>
        public float RampStart;
        /// <summary>Distance where the ramp reaches <see cref="AFar"/> (~2500u).</summary>
        public float RampEnd;
    }
}
