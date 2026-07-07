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
        _MountainBaseHeight ("Base Height", Range(-0.1, 0.3))       = 0.02
        _MountainVariation  ("Height Variation", Range(0.0, 0.15))  = 0.04
        _MountainSoftness   ("Edge Softness", Range(0.001, 0.05))   = 0.008
        _MountainOpacity    ("Opacity", Range(0, 1))                = 1.0
        _MountainDistance   ("Fictive Distance", Float)             = 900.0
        // Tuned 2026-07-05 via Play Mode screenshots (two rounds): the palette hues are
        // lit-surface colors, so the band multiplies them by _MountainShade to read as a
        // distant shaded mass — without it the ridge washed out white against the haze.
        // Floor 0.28 keeps the silhouette visible from altitude; 0.4+ dissolved it.
        _MountainShade      ("Base Shade", Range(0, 1))             = 0.55
        _MountainHazeStrength ("Haze Strength", Range(0, 1))        = 0.7
        _MountainHazeFloor  ("Haze Floor", Range(0, 1))             = 0.28
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
                half   _MountainHazeStrength;
                half   _MountainHazeFloor;
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

                // ── Mountain silhouette ──────────────────────────────────
                // Overlapping sine harmonics produce a gentle rolling ridge that
                // wraps seamlessly at ±PI — no texture assets required.
                float az = atan2(viewDir.x, viewDir.z);
                float hills = 0.60 * sin(az * 1.00 + 0.53)
                            + 0.30 * sin(az * 2.30 + 1.21)
                            + 0.10 * sin(az * 4.70 + 0.87);
                hills = hills * 0.5 + 0.5; // remap -1..1 → 0..1

                float mtnHeight = _MountainBaseHeight + _MountainVariation * (hills - 0.5);
                float mtnAlpha  = 1.0 - smoothstep(mtnHeight - _MountainSoftness, mtnHeight + _MountainSoftness, viewDir.y);
                mtnAlpha *= _MountainOpacity;

                // V9 P4 (ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.4): base hue from the shared
                // palette, hazed toward the horizon with the height-aware term. Skyboxes never
                // receive scene fog, so the band gets its haze in-shader — this kills the dark
                // unfogged wall behind the fogged plain.
                //
                // The band is NOT cut off below the horizon: it continues to the bottom of the
                // sky as the far-field ground skirt (distant land beyond the far clip). The
                // ground disc alpha-fades out at the far clip to reveal it, so the far plane is
                // hidden by matching land rather than by a thick white haze wall. The haze ray
                // is clamped at the y=0 ground plane: from altitude a downward ray exits the
                // dense layer quickly and the skirt reads as land; at ground level only the
                // heavily-hazed near-horizon sliver of the skirt is ever visible (the opaque
                // disc occludes the rest).
                half3 mtnBase = lerp(_AtmoRock.rgb, _AtmoGround.rgb, saturate(hills)) * _MountainShade;
                float groundHit = (viewDir.y < -1e-3)
                    ? max(_WorldSpaceCameraPos.y, 1.0) / -viewDir.y
                    : _MountainDistance;
                float mtnRayLen = min(_MountainDistance, groundHit);
                // The floor is distant-air scatter — part of the same atmosphere the authority
                // broadcasts, so it must obey the zero-haze dev pin (TimeOfDayController.
                // disableAtmosphereHaze). Without this gate the skirt keeps a permanent wash
                // that fills the whole below-horizon view from altitude even with haze "off".
                float hazeGate = saturate(_AtmoHazeDensity * 1e6);
                float mtnHaze = saturate(_MountainHazeFloor * hazeGate +
                    AtmoHeightHazeAmount(_WorldSpaceCameraPos.y, viewDir, mtnRayLen) * _MountainHazeStrength);
                half3 mtnColor = (half3)ApplyAerialHaze(mtnBase, mtnHaze);

                color.rgb = lerp(color.rgb, mtnColor, saturate(mtnAlpha));

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
