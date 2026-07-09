Shader "ProceduralGradientSky"
{
    Properties
    {
        [Header(Gradient)]
        _HorizonColor     ("Horizon Color", Color)  = (0.85, 0.75, 0.55, 1.0)
        _ZenithColor      ("Zenith Color", Color)   = (0.30, 0.50, 0.80, 1.0)
        _GradientExponent ("Gradient Exponent", Range(0.01, 10.0)) = 1.0
        _HorizonHeight    ("Horizon Height", Range(-0.5, 0.5)) = 0.0

        [Header(Clouds)]
        _CloudColor         ("Cloud Color", Color)          = (1, 1, 1, 1)
        _CloudShadowColor   ("Cloud Shadow Color", Color)   = (0.6, 0.6, 0.7, 1)
        _ScrollSpeed        ("Scroll Speed", Vector)        = (0.01, 0.005, 0, 0)
        _NoiseScale         ("Noise Scale", Float)          = 3.0
        _CoverageThreshold  ("Coverage Threshold", Range(0, 1)) = 0.45
        _EdgeSoftness       ("Edge Softness", Range(0.01, 1))  = 0.15
        _Opacity            ("Opacity", Range(0, 1))        = 0.6

        [Header(Mountains)]
        // Base hue comes from the global _AtmoGround/_AtmoRock palette (V9 authority) —
        // the old flat _MountainColor literal is gone as a color source. These dials
        // control the silhouette shape and how hard the band hazes into the horizon.
        // Band composition spec: Assets/Docs/Rendering/SKY_MOUNTAIN_BAND_SPEC.md (V15).
        _MountainBaseHeight ("Base Height", Range(-0.1, 0.3))       = 0.02
        _MountainVariation  ("Height Variation", Range(0.0, 0.2))   = 0.06
        _MountainSoftness   ("Edge Softness", Range(0.001, 0.05))   = 0.008
        _MountainOpacity    ("Opacity", Range(0, 1))                = 1.0
        _MountainDistance   ("Fictive Distance", Float)             = 900.0
        _MountainRidgeFreq  ("Ridge Base Frequency", Range(2, 16))  = 6.0
        // Tuned 2026-07-05 via Play Mode screenshots (two rounds): the palette hues are
        // lit-surface colors, so the band multiplies them by _MountainShade to read as a
        // distant shaded mass — without it the ridge washed out white against the haze.
        // Floor 0.28 keeps the silhouette visible from altitude; 0.4+ dissolved it.
        // _MountainShade now only shades the below-horizon ground skirt; the visible
        // range above the horizon line uses _MountainRangeShade (V15).
        _MountainShade      ("Skirt Shade", Range(0, 1))            = 0.55
        _MountainHazeStrength ("Haze Strength", Range(0, 1))        = 0.7
        _MountainHazeFloor  ("Haze Floor", Range(0, 1))             = 0.28

        [Header(Back Ridge)]
        // Second, fictively-farther ridge line: same mountain type as the front but
        // FINER — distance compresses apparent wavelength and height, so the back
        // ridge runs a higher noise frequency with smaller variation and only a
        // slightly higher base (enough to peek through the front saddles). Round 2
        // (owner feedback 2026-07-09): v1 had it broader + much taller, which read
        // as a bigger range behind foothills — backwards.
        _Mountain2BaseHeight ("Back Base Height", Range(-0.1, 0.3)) = 0.03
        _Mountain2Variation  ("Back Height Variation", Range(0, 0.2)) = 0.09
        _Mountain2Distance   ("Back Fictive Distance", Float)       = 1500.0

        [Header(Horizon Line and Range Color)]
        // Above viewDir.y = 0 the band is the mountain RANGE: darker and pulled
        // toward the horizon hue so it separates from the terrain below (owner
        // call 2026-07-09 — the old single ground-palette band blended too
        // seamlessly). Below the line stays ground palette: that part is the
        // far-field ground skirt the disc alpha-fades into (do NOT recolor it).
        _MountainRangeShade  ("Range Shade", Range(0, 1))           = 0.45
        _MountainHueShift    ("Range Hue Shift", Range(0, 1))       = 0.30
        _MountainLineSoftness ("Horizon Line Softness", Range(0.0005, 0.05)) = 0.0015

        [Header(Snow)]
        // OFF by default (owner call 2026-07-09: keep as a toggle, not a priority) —
        // raise _SnowOpacity to enable. One global snow-line elevation angle: only
        // peaks rising above it get caps, so coverage follows ridge height for free.
        // Snow hue derives from the sky horizon color (never a white literal) so it
        // dims/warms with the time-of-day palette.
        _SnowLineHeight     ("Snow Line Elevation", Range(0, 0.3))  = 0.045
        _SnowSoftness       ("Snow Line Softness", Range(0.001, 0.05)) = 0.012
        _SnowOpacity        ("Snow Opacity (0 = off)", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off

        Pass
        {
            Name "ProceduralGradientSky"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _CLOUDS_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/Atmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                half  _GradientExponent;
                half  _HorizonHeight;

                half4  _CloudColor;
                half4  _CloudShadowColor;
                float4 _ScrollSpeed;
                float  _NoiseScale;
                half   _CoverageThreshold;
                half   _EdgeSoftness;
                half   _Opacity;

                half   _MountainBaseHeight;
                half   _MountainVariation;
                half   _MountainSoftness;
                half   _MountainOpacity;
                half   _MountainShade;
                float  _MountainDistance;
                float  _MountainRidgeFreq;
                half   _MountainHazeStrength;
                half   _MountainHazeFloor;

                half   _Mountain2BaseHeight;
                half   _Mountain2Variation;
                float  _Mountain2Distance;

                half   _MountainRangeShade;
                half   _MountainHueShift;
                half   _MountainLineSoftness;

                half   _SnowLineHeight;
                half   _SnowSoftness;
                half   _SnowOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
            };

            // ── Cloud noise (only compiled when _CLOUDS_ON) ──────────────
            #if defined(_CLOUDS_ON)
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                value += valueNoise(p) * amplitude;
                p *= 2.0; amplitude *= 0.5;
                value += valueNoise(p) * amplitude;
                p *= 2.0; amplitude *= 0.5;
                value += valueNoise(p) * amplitude;
                p *= 2.0; amplitude *= 0.5;
                value += valueNoise(p) * amplitude;

                return value;
            }
            #endif

            // ── Mountain ridge noise (always compiled — the band is not keyword-gated) ──
            // 1D periodic value noise over the azimuth lattice: hashing on fmod(i, period)
            // makes the last cell interpolate back into cell 0, so the ridge wraps
            // seamlessly at ±PI with no seam behind the player. The ridged transform
            // (1 - |2n - 1|)² gives sharp crests and V-shaped valleys — the rugged read
            // the old three smooth sine harmonics could never produce (V15).
            float MountainHash(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float MountainPeriodicNoise(float x, float period)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(MountainHash(fmod(i, period)),
                            MountainHash(fmod(i + 1.0, period)),
                            f);
            }

            // t01 = azimuth remapped to [0,1). Each octave's lattice count is an exact
            // multiple of its period, so every octave wraps; phase decorrelates the two
            // ridge lines without breaking periodicity (the noise has period 1 in t01).
            float MountainRidgedFBM(float t01, float baseFreq, float phase)
            {
                float sum  = 0.0;
                float amp  = 0.5;
                // The wrap only holds when the lattice count is an integer (fmod(i, period)
                // must land exactly back on cell 0) — round so any dial value stays seamless.
                float freq = max(2.0, round(baseFreq));
                [unroll]
                for (int o = 0; o < 4; o++)
                {
                    float n = MountainPeriodicNoise(frac(t01 + phase) * freq, freq);
                    float r = 1.0 - abs(2.0 * n - 1.0);
                    sum += r * r * amp;
                    freq *= 2.0;
                    amp  *= 0.5;
                }
                return sum;   // ≈ [0, 0.94]
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                float4 clipPos = output.positionCS;
                float4 worldPos = mul(UNITY_MATRIX_I_VP, float4(clipPos.xy / clipPos.w, 1.0, 1.0));
                worldPos.xyz /= worldPos.w;
                output.viewDirWS = worldPos.xyz - _WorldSpaceCameraPos.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDirWS);
                float y = viewDir.y;

                // ── Gradient ─────────────────────────────────────────────
                float h0 = _HorizonHeight;
                float h1 = 1.0;

                float t = saturate((y - h0) / (h1 - h0));
                t = pow(t, _GradientExponent);

                half4 color = lerp(_HorizonColor, _ZenithColor, t);

                // ── Clouds (keyword-gated) ───────────────────────────────
                #if defined(_CLOUDS_ON)
                if (viewDir.y > 0.01)
                {
                    float2 uv = viewDir.xz / (viewDir.y + 0.5);
                    uv *= _NoiseScale;
                    uv += _ScrollSpeed.xy * _Time.y;

                    float noise = fbm(uv);
                    float coverage = smoothstep(_CoverageThreshold, _CoverageThreshold + _EdgeSoftness, noise);
                    float horizonFade = saturate((viewDir.y - 0.01) / 0.15);

                    float shadow = fbm(uv + float2(0.3, 0.1));
                    half3 cloudColor = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, saturate(shadow + 0.3));

                    half cloudAlpha = coverage * _Opacity * horizonFade;
                    color.rgb = lerp(color.rgb, cloudColor, cloudAlpha);
                }
                #endif

                // ── Mountain silhouette (V15 — SKY_MOUNTAIN_BAND_SPEC.md) ────────────
                // Two ridged-FBM silhouette layers (front + back), a horizon
                // demarcation line, and palette-derived snow caps.
                float az  = atan2(viewDir.x, viewDir.z);
                float t01 = az / TWO_PI + 0.5;

                // Back ridge runs at a higher base frequency (finer apparent wavelength —
                // it's farther away) with a phase offset so its peaks don't shadow the
                // front ridge's. 1.6× keeps the two patterns clearly siblings.
                float hills  = MountainRidgedFBM(t01, _MountainRidgeFreq, 0.0);
                float hills2 = MountainRidgedFBM(t01, _MountainRidgeFreq * 1.6, 0.37);

                float mtnHeight  = _MountainBaseHeight  + _MountainVariation  * (hills  - 0.5);
                float mtnHeight2 = _Mountain2BaseHeight + _Mountain2Variation * (hills2 - 0.5);

                float mtnAlpha  = (1.0 - smoothstep(mtnHeight  - _MountainSoftness, mtnHeight  + _MountainSoftness, y)) * _MountainOpacity;
                float mtnAlpha2 = (1.0 - smoothstep(mtnHeight2 - _MountainSoftness, mtnHeight2 + _MountainSoftness, y)) * _MountainOpacity;

                // V9 P4 (ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.4): all hues from the shared
                // palette, hazed toward the horizon with the height-aware term. Skyboxes never
                // receive scene fog, so the band gets its haze in-shader.
                //
                // Horizon line (V15): below viewDir.y = 0 the front layer is the far-field
                // ground SKIRT (distant land beyond the far clip — the ground disc alpha-fades
                // out to reveal it) and must keep the ground palette, or the clip edge shows
                // again. Above the line the band is the mountain RANGE: darker, pulled toward
                // the horizon hue, so it reads as a distinct distant mass instead of blending
                // seamlessly into the terrain. The gate itself is the demarcation line.
                float aboveHorizon = smoothstep(0.0, _MountainLineSoftness, y);

                half3 skirtBase = lerp(_AtmoRock.rgb, _AtmoGround.rgb, saturate(hills)) * _MountainShade;
                half3 rangeHue  = (half3)lerp(_AtmoRock.rgb, _AtmoHorizon.rgb, _MountainHueShift);
                half3 rangeFront = rangeHue * _MountainRangeShade;
                // Depth stacking: nearest ridge darkest. The back ridge sits deeper in the
                // atmosphere — pushed a step further toward the horizon hue and lighter
                // (fixed constants; the dials already give enough tuning surface). Kept
                // subtle: the ranges should read as siblings, one atmospheric step apart
                // (owner feedback 2026-07-09, round 2 — v1's 0.5/0.4 was too separated).
                half3 rangeBack  = (half3)lerp(rangeHue, _AtmoHorizon.rgb, 0.35) * lerp(_MountainRangeShade, 1.0, 0.25);

                // Snow caps (dormant while _SnowOpacity = 0): applied before haze so
                // distant snow recedes like everything else.
                float snowMask  = smoothstep(_SnowLineHeight, _SnowLineHeight + _SnowSoftness, y) * _SnowOpacity;
                half3 snowColor = (half3)lerp(_AtmoHorizon.rgb, float3(1.0, 1.0, 1.0), 0.65);
                rangeFront = lerp(rangeFront, snowColor, snowMask);
                rangeBack  = lerp(rangeBack,  snowColor, snowMask);

                half3 frontBase = lerp(skirtBase, rangeFront, aboveHorizon);

                // The haze ray is clamped at the y=0 ground plane: from altitude a downward
                // ray exits the dense layer quickly and the skirt reads as land; at ground
                // level only the heavily-hazed near-horizon sliver is ever visible.
                float groundHit = (y < -1e-3)
                    ? max(_WorldSpaceCameraPos.y, 1.0) / -y
                    : _MountainDistance;
                // The floor is distant-air scatter — part of the same atmosphere the authority
                // broadcasts, so it must obey the zero-haze dev pin (TimeOfDayController.
                // disableAtmosphereHaze). Without this gate the skirt keeps a permanent wash
                // that fills the whole below-horizon view from altitude even with haze "off".
                float hazeGate = saturate(_AtmoHazeDensity * 1e6);
                float mtnHaze = saturate(_MountainHazeFloor * hazeGate +
                    AtmoHeightHazeAmount(_WorldSpaceCameraPos.y, viewDir, min(_MountainDistance, groundHit)) * _MountainHazeStrength);
                float mtnHaze2 = saturate(_MountainHazeFloor * hazeGate +
                    AtmoHeightHazeAmount(_WorldSpaceCameraPos.y, viewDir, min(_Mountain2Distance, groundHit)) * _MountainHazeStrength);

                half3 mtnColor  = (half3)ApplyAerialHaze(frontBase, mtnHaze);
                half3 mtnColor2 = (half3)ApplyAerialHaze(rangeBack, mtnHaze2);

                // Composite back-to-front; below the horizon the front layer's alpha is 1,
                // so the skirt fully occludes the back ridge there.
                color.rgb = lerp(color.rgb, mtnColor2, saturate(mtnAlpha2));
                color.rgb = lerp(color.rgb, mtnColor,  saturate(mtnAlpha));

                // Anti-banding: interleaved gradient noise (Jimenez 2014)
                float dither = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
                color.rgb += (dither - 0.5) / 255.0;

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
