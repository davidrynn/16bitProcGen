#ifndef RELIC_SURFACE_INCLUDED
#define RELIC_SURFACE_INCLUDED

#include "Assets/Shaders/GroundNoiseCore.hlsl"

// ---------------------------------------------------------------------------
// Procedural weathered-stone surfacing for hero relics (RelicLit).
//
// World-space and UV-free by necessity: RelicLit's Attributes carry POSITION and
// NORMAL only, and the relic meshes ship without usable UVs (the mound/rubble are
// script-generated and have no UV layer at all). Deriving everything from world
// position means no vertex attribute, no unwrap, and no re-export — and it keeps
// the grain continuous across the seam where the hand intersects its mound, so the
// two read as one carved mass rather than two assets.
//
// The noise primitives are CALLED, never copied: GroundNoiseCore.hlsl declares a
// one-definition rule (guarded by TerrainChunkMaterialContractTests) so every
// consumer stays continuous with the terrain. This file only adds a triplanar
// wrapper around them.
//
// Deliberately NOT reusing GroundReliefNormal: its header marks it flat-geometry-
// only (ground disc), and it fights real mesh normals — it would break on a
// vertical palm.
// ---------------------------------------------------------------------------

// Triplanar sample of the shared ground FBM. The terrain samples world XZ directly,
// which smears into stripes on anything vertical; blending the three world planes by
// the normal keeps the grain the right size on the back of a hand and on top of a
// mound alike.
float RelicTriplanarFBM(float3 positionWS, float3 normalWS, float scale)
{
    float3 w = abs(normalWS);
    w = w * w * w * w;                       // sharpen, or the three samples average into mush
    w /= max(w.x + w.y + w.z, 1e-4);
    return GroundPatchFBM(positionWS.zy * scale) * w.x
         + GroundPatchFBM(positionWS.xz * scale) * w.y
         + GroundPatchFBM(positionWS.xy * scale) * w.z;
}

// Weathered stone over a flat albedo.
//
// `stain`/`bleach` carry their blend amount in ALPHA, so a single Color property
// controls both the hue and how far it is pushed.
half3 RelicWeatheredStone(half3 albedo, float3 positionWS, float3 normalWS,
                          half strength, half grainScale,
                          half strataScale, half strataStrength,
                          half4 stain, half4 bleach)
{
    // Exact passthrough when disabled, so relics that never opt in (relic_head,
    // stone_outcrop) render bit-for-bit as before and cost nothing extra.
    if (strength <= 0.0h)
        return albedo;

    // Two octaves at very different world scales. The macro term is what actually
    // resolves at vista range (~250-400u); the grain only reads up close, so it is
    // weighted down to stop it boiling into noise at distance.
    float macro  = RelicTriplanarFBM(positionWS, normalWS, grainScale * 0.18);
    float grain  = RelicTriplanarFBM(positionWS, normalWS, grainScale);
    float mottle = lerp(macro, grain, 0.35) * 2.0 - 1.0;                  // -1..1

    // Sedimentary banding along world Y, warped by the macro noise so the strata
    // undulate instead of ringing the silhouette as perfect horizontal stripes.
    // This is the strongest single "cut from ancient bedrock" cue at silhouette scale.
    float band   = positionWS.y * strataScale + macro * 1.6;
    float strata = smoothstep(0.25, 0.75, sin(band * 6.2831853) * 0.5 + 0.5) * 2.0 - 1.0;

    // Slope response: up-facing surfaces bleach in the sun and catch dust, steep and
    // downward faces stay dark and carry runoff. This reads as *weathering* rather
    // than as noise, which is what sells age at a distance.
    float up         = saturate(normalWS.y);
    float bleachMask = smoothstep(0.35, 0.95, up);
    float stainMask  = 1.0 - smoothstep(0.0, 0.55, up);

    // Runoff streaks: the FBM stretched hard along Y so it draws downward on vertical
    // faces instead of tiling isotropically.
    float streak = GroundPatchFBM(float2((positionWS.x + positionWS.z) * grainScale * 2.0,
                                          positionWS.y * grainScale * 0.15));
    stainMask *= lerp(0.55, 1.0, streak);

    half3 col = albedo * (1.0h + (half)(mottle * 0.18 + strata * 0.16 * strataStrength));
    col = lerp(col, stain.rgb,  (half)stainMask  * stain.a);
    col = lerp(col, bleach.rgb, (half)bleachMask * bleach.a);

    return lerp(albedo, col, strength);
}

#endif // RELIC_SURFACE_INCLUDED
