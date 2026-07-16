#ifndef GROUND_NOISE_CORE_INCLUDED
#define GROUND_NOISE_CORE_INCLUDED

// ---------------------------------------------------------------------------
// World-space noise primitives shared by the ground mix (GroundNoise.hlsl) and
// the atmosphere's patchy-haze modulation (Atmosphere.hlsl §5.3b).
//
// Split out of GroundNoise.hlsl (V17 P4) because Atmosphere.hlsl needs the FBM
// but GroundNoise.hlsl includes Atmosphere.hlsl — a lower shared core avoids
// the circular include. Same one-definition rule as the ground mix: every
// consumer sampling world XZ through these functions stays continuous across
// the terrain↔disc seam. Do not fork per consumer.
// ---------------------------------------------------------------------------

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

#endif // GROUND_NOISE_CORE_INCLUDED
