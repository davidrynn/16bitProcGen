#ifndef GROUND_NOISE_INCLUDED
#define GROUND_NOISE_INCLUDED

// ---------------------------------------------------------------------------
// Shared ground patch noise + palette mix (V9 P3).
// Spec: Assets/Docs/Rendering/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md (§6a)
//
// Factored out of GroundPlaneImpostor.shader so the SDF terrain (TerrainLit)
// and the ground disc compute their grass/rock color from ONE definition.
// The disc↔terrain seam disappears by construction only if both surfaces mix
// the same palette by the same world-space noise — the noise is continuous in
// world XZ, so grass/rock patches flow uninterrupted from real terrain onto
// the disc. Do not fork these functions per consumer.
//
// Requires Atmosphere.hlsl for _AtmoGround/_AtmoRock (included here; guarded).
// ---------------------------------------------------------------------------

#include "Assets/Shaders/Atmosphere.hlsl"

float GroundHash21(float2 p)
{
    p = frac(p * float2(234.34, 435.345));
    float d = dot(p, p + float2(34.23, 34.23));
    p += float2(d, d);
    return frac(p.x * p.y);
}

float GroundValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(GroundHash21(i),                    GroundHash21(i + float2(1.0, 0.0)), u.x),
        lerp(GroundHash21(i + float2(0.0, 1.0)), GroundHash21(i + float2(1.0, 1.0)), u.x),
        u.y);
}

float GroundPatchFBM(float2 p)
{
    float v = GroundValueNoise(p) * 0.5 + GroundValueNoise(p * 2.0) * 0.25;
    return v * 1.3333;
}

// V17 P1 — macro luminance octave (GROUND_PLANE_IMPOSTOR_SPEC.md §12.2).
// One FBM octave at a much larger wavelength (~1400u at the default scale
// 0.0007) returning a scalar multiplier in [1-strength, 1+strength] — the
// "soil moisture / dappled plain" trick that breaks the mid-field band's
// uniform read. A scalar multiply on the already-dynamic palette, so
// biome/time-of-day shifts flow through untouched (§12.3).
float GroundMacroLuminance(float2 worldXZ, float macroScale, float macroStrength)
{
    float n = GroundPatchFBM(worldXZ * macroScale);
    return 1.0 + (n - 0.5) * 2.0 * macroStrength;
}

// Grass/rock hue for a world-XZ position, from the authoritative palette.
// All four dials stay per-material, but consumers must keep them equal across
// surfaces (disc + terrain) or the patches / macro tone stop lining up —
// TerrainChunkMaterialContractTests guards the parity. The macro multiply
// lives INSIDE the mix (not left to callers) so the ~180u seam stays aligned
// by construction; forking it per consumer would reintroduce the seam.
float3 GroundPaletteMix(float2 worldXZ, float noiseScale, float rockThreshold,
                        float macroScale, float macroStrength)
{
    float n = GroundPatchFBM(worldXZ * noiseScale);
    float3 hue = lerp(_AtmoGround.rgb, _AtmoRock.rgb, step(rockThreshold, n));
    return hue * GroundMacroLuminance(worldXZ, macroScale, macroStrength);
}

// V17 P2 — pseudo-normal from a finite-differenced low-frequency height FBM
// (§12.2). For flat geometry only (the ground disc): TerrainLit has real mesh
// normals and feeding it this would fight them. strength 0 returns exactly +Y,
// so the disc's flat-plane lighting is the strict fallback.
float3 GroundReliefNormal(float2 worldXZ, float reliefScale, float reliefStrength)
{
    // Step is a fixed fraction of a noise cell so the slope estimate scales
    // with the chosen wavelength instead of aliasing at high reliefScale.
    const float E = 0.35;
    float2 p  = worldXZ * reliefScale;
    float  h0 = GroundPatchFBM(p);
    float  hx = GroundPatchFBM(p + float2(E, 0.0));
    float  hz = GroundPatchFBM(p + float2(0.0, E));
    float  k  = 4.0 * reliefStrength;
    return normalize(float3((h0 - hx) * k, 1.0, (h0 - hz) * k));
}

#endif // GROUND_NOISE_INCLUDED
