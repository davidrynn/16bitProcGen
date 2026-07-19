#ifndef WORLD_STRUCTURE_INCLUDED
#define WORLD_STRUCTURE_INCLUDED

// ---------------------------------------------------------------------------
// The H authority — world macro-structure heightfield (HLSL side).
// Spec: Assets/Docs/Terrain/WORLD_STRUCTURE_SPEC.md (ticket H1).
//
// MIRROR CONTRACT: this file is the twin of Assets/Scripts/DOTS/Terrain/
// WorldStructure.cs. Identical math, both languages (the GroundNoise.hlsl
// precedent) so every representation of the world — near SDF, mid disc, far
// sky band — samples ONE shape and agrees by construction. Any edit here has a
// twin edit there; WorldStructureParityTests pins the C# side. NUnit cannot run
// HLSL, so the guarantee is a line-for-line structural mirror, not a live GPU
// diff — keep them lockstep by discipline (same as GroundNoise).
//
// The hash/value-noise below is intentionally a PRIVATE COPY of
// GroundNoiseCore.hlsl's, not an include: H owns its noise so the save-config
// hash surface (spec §5.1) never couples to ground-patch color tuning. The
// ridged transform (1 - |2n-1|)^2 is inherited from the V15 sky band (§5.5).
//
// H1 defines the pure functions taking explicit params. Ticket H2 seeds the
// _WorldMacro* globals and consumers pass them in — H is static per world, so
// there is NO per-frame broadcast (§4.2 / §6.6).
// ---------------------------------------------------------------------------

// Mirror of WorldStructure.MaxSeedOffset (spec §4.1: bounded noise-space offset).
#define WORLD_MACRO_MAX_SEED_OFFSET 10000.0

// 2D hash -> [0,1). Bit-identical to GroundNoiseCore.hlsl:GroundHash21 and to
// WorldStructure.Hash21.
float WorldMacroHash21(float2 p)
{
    p = frac(p * float2(234.34, 435.345));
    float d = dot(p, p + float2(34.23, 34.23));
    p += float2(d, d);
    return frac(p.x * p.y);
}

float WorldMacroValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(WorldMacroHash21(i),                    WorldMacroHash21(i + float2(1.0, 0.0)), u.x),
        lerp(WorldMacroHash21(i + float2(0.0, 1.0)), WorldMacroHash21(i + float2(1.0, 1.0)), u.x),
        u.y);
}

// Ridged fractal, normalized to [0,1]. norm is accumulated in-loop (not
// closed-form) so C# and HLSL divide by the identical value — see the C# twin.
float WorldMacroRidgedFBM(float2 p, int octaves, float lacunarity, float gain)
{
    float sum  = 0.0;
    float amp  = 0.5;
    float freq = 1.0;
    float norm = 0.0;
    for (int o = 0; o < octaves; o++)
    {
        float n = WorldMacroValueNoise(p * freq);
        float r = 1.0 - abs(2.0 * n - 1.0);
        sum  += r * r * amp;
        norm += amp;
        freq *= lacunarity;
        amp  *= gain;
    }
    return norm > 0.0 ? sum / norm : 0.0;
}

// The wilderness ramp A(r) (spec §4.1): gentle near the playfield, mountainous
// at the rim. Decision D3 encoded in the function.
float WorldMacroAmplitudeRamp(float r, float aNear, float aFar, float rampStart, float rampEnd)
{
    return lerp(aNear, aFar, smoothstep(rampStart, rampEnd, r));
}

// H(x,z) = A(r) * ridgedFBM. The authored flatten mask M(x,z) is 1 here; ticket
// H3 adds the mask multiply and vista-corridor protection.
float SampleWorldMacroHeight(float2 worldXZ, float macroFreq, float2 seedOffset,
                             int octaves, float lacunarity, float gain,
                             float aNear, float aFar, float rampStart, float rampEnd)
{
    float2 p     = worldXZ * macroFreq + seedOffset;
    float ridged = WorldMacroRidgedFBM(p, octaves, lacunarity, gain);
    float r      = length(worldXZ);
    float a      = WorldMacroAmplitudeRamp(r, aNear, aFar, rampStart, rampEnd);
    return a * ridged;
}

#endif // WORLD_STRUCTURE_INCLUDED
