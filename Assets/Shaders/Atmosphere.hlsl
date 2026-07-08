#ifndef ATMOSPHERE_INCLUDED
#define ATMOSPHERE_INCLUDED

// ---------------------------------------------------------------------------
// Atmosphere color authority — shared consumer include.
// Spec: Assets/Docs/Rendering/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md (§5.2/§5.3/§5.3a)
//
// The _Atmo* uniforms are GLOBAL shader values broadcast once per frame by
// AtmosphereBroadcast (driven from TimeOfDayController, the palette authority).
// They must NOT be redeclared inside a consumer's UnityPerMaterial cbuffer —
// they are intentionally not material properties, so every consumer reads the
// same authoritative values.
//
// Requires URP Core.hlsl to be included first (_WorldSpaceCameraPos).
// ---------------------------------------------------------------------------

half4 _AtmoHorizon;      // horizon/haze color — the hue everything converges to at distance (same value drives fog)
half4 _AtmoZenith;       // top-of-sky color
half4 _AtmoGround;       // base ground/grass tint for disc, mountains, terrain tint
half4 _AtmoRock;         // base rock tint for disc/mountains
float _AtmoSaturation;   // global saturation scalar (1 = full)
float _AtmoFarFade;      // reference distance where aerial perspective reaches full (typically camera far clip)
float _AtmoHazeDensity;  // ground-level (y=0) haze density d0 for the height term
float _AtmoHazeFalloff;  // 1/scale-height of the haze layer (e.g. 1/60u)
float _AtmoDistanceHaze; // small altitude-independent aerial floor at _AtmoFarFade (see ApplyAerialPerspective)
float _AtmoLandmarkFade; // landmark dissolve distance = max(_AtmoFarFade, LandmarkDrawDistance) — R6 P3

// Analytic exponential-height fog along a finite view ray (closed form, no
// marching) — the V8 Route B height term (spec §5.3a). A downward ray from
// altitude passes through thin high air and stays clear; a horizontal
// ground-level ray accumulates dense low air and veils.
float AtmoHeightHazeAmount(float rayOriginY, float3 rayDir, float rayLen)
{
    float f  = _AtmoHazeFalloff;
    float d0 = _AtmoHazeDensity;
    float dy = rayDir.y * rayLen;
    // integral of d0 * exp(-f * y) along the ray; degenerate horizontal case -> d0 * exp(-f*y0) * rayLen
    float od = d0 * exp(-f * rayOriginY) *
               ((abs(dy) > 1e-4) ? (1.0 - exp(-f * dy)) / (f * rayDir.y) : rayLen);
    return 1.0 - exp(-max(od, 0.0));   // optical depth -> opacity
}

// Infinite-ray limit for skybox surfaces (no world position to measure to).
// Converges for upward rays; horizontal/downward rays pass through an unbounded
// dense layer and saturate to full haze — which is exactly the horizon band look.
float AtmoHeightHazeToSky(float rayOriginY, float rayDirY)
{
    float od = (rayDirY > 1e-3)
        ? _AtmoHazeDensity * exp(-_AtmoHazeFalloff * rayOriginY) / (_AtmoHazeFalloff * rayDirY)
        : 1e6;
    return 1.0 - exp(-min(od, 60.0));
}

// Far-clip concealer: ramps to full haze over the last quarter before _AtmoFarFade
// so converted world-space surfaces never show a hard clip edge against the skybox.
// Deliberately distance-only — at altitude the height term goes thin, but the far
// plane still cuts geometry, so the edge must always be veiled regardless of camera Y.
float AtmoFarClipHaze(float viewDist)
{
    return smoothstep(_AtmoFarFade * 0.75, _AtmoFarFade, viewDist);
}

// Hue/desaturation pull of spec §5.3 for a precomputed haze amount t:
// distant color desaturates and converges toward the horizon hue.
float3 ApplyAerialHaze(float3 color, float t)
{
    // Rec.709 luminance inline — core Color.hlsl's Luminance() isn't reachable from
    // every consumer's include set (the skybox shader only pulls URP Core.hlsl).
    float lum = dot(color, float3(0.2126, 0.7152, 0.0722));
    float3 desat = lerp(color, lum.xxx, saturate((1.0 - _AtmoSaturation) + t * 0.5));
    return lerp(desat, _AtmoHorizon.rgb, saturate(t));
}

// Height + distance-floor haze amount for a world-space fragment, WITHOUT the far-clip
// concealer. For alpha-blended surfaces (ground disc) that hide the far clip by fading
// their alpha out to reveal the skybox far-field skirt instead of color-fogging to the
// horizon — the handoff then matches whatever the sky actually draws behind them.
float AtmoAerialHazeAmount(float3 positionWS, float strength)
{
    float3 ray    = positionWS - _WorldSpaceCameraPos;
    float  rayLen = max(length(ray), 1e-4);
    float3 rayDir = ray / rayLen;
    // Two haze sources: the height term (altitude-aware bulk of the effect) and a small
    // altitude-independent distance floor (real air scatters over distance even above the
    // ground layer — without it the clear zone at altitude ends in a hard-edged ring).
    return saturate(AtmoHeightHazeAmount(_WorldSpaceCameraPos.y, rayDir, rayLen) * strength
                    + smoothstep(0.0, _AtmoFarFade, rayLen) * _AtmoDistanceHaze);
}

// The shared aerial-perspective entry point (spec §5.3 composed with §5.3a):
// the height term decides HOW MUCH haze, the palette decides WHAT COLOR it
// converges to, strength is the per-surface dial (disc low, mountains high).
// Includes the far-clip concealer at FULL strength — correct for opaque WORLD
// surfaces, which must always be veiled at the clip edge regardless of their
// haze dial (the concealer hides geometry cut-off, not atmosphere). Not for
// landmarks: hero relics draw past _AtmoFarFade by design (R6) and use
// AtmoAerialHazeAmount + AtmoLandmarkEdgeFade instead, like the disc's
// alpha-fade path. No consumers today; V9 P3 terrain tint is the intended one.
float3 ApplyAerialPerspective(float3 color, float3 positionWS, float strength)
{
    float rayLen = length(positionWS - _WorldSpaceCameraPos);
    float t = saturate(AtmoAerialHazeAmount(positionWS, strength) + AtmoFarClipHaze(rayLen));
    return ApplyAerialHaze(color, t);
}

// Interleaved gradient noise (Jimenez, "Next Generation Post Processing in
// Call of Duty: Advanced Warfare", 2014) over pixel coordinates — the standard
// screen-door dither pattern for opaque dissolves.
float AtmoInterleavedGradientNoise(float2 pixelCoord)
{
    return frac(52.9829189 * frac(dot(pixelCoord, float2(0.06711056, 0.00583715))));
}

// Landmark edge fade (R6 P3): visibility factor for hero landmarks, 1 = solid,
// 0 = fully dissolved over the last 10% before _AtmoLandmarkFade. Consumers
// clip() against AtmoInterleavedGradientNoise so crossing the landmark draw
// distance is a dissolve, not a pop — RelicLit is opaque + ZWrite, so alpha
// blending is not an option. Returns 1 well inside the fade band, so it never
// clips ordinary-distance fragments.
float AtmoLandmarkEdgeFade(float viewDist)
{
    return 1.0 - smoothstep(_AtmoLandmarkFade * 0.9, _AtmoLandmarkFade, viewDist);
}

#endif // ATMOSPHERE_INCLUDED
