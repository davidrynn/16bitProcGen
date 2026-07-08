Shader "Terrain/TerrainLit"
{
    // SDF terrain chunk shader (V9 P3 — ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §6a).
    //
    // Replaces the Synty Generic_Basic shadergraph, whose albedo/normal samples
    // were dead weight: Surface Nets meshes upload position-only vertex buffers
    // (no UVs, no tangents), so the texture sampled one texel and the terrain
    // rendered as a single flat color. This shader makes the terrain a
    // first-class palette consumer instead — same _AtmoGround/_AtmoRock FBM mix
    // as the ground disc (shared GroundNoise.hlsl, so the ~180u seam matches by
    // construction) plus the shared height-aware aerial term for the haze axis.
    //
    // Rendered through Entities Graphics (BatchRendererGroup), hence the
    // DOTS_INSTANCING_ON variant and target 4.5 — same contract as RelicLit.

    Properties
    {
        // Defaults must match GroundPlaneImpostor.shader or the seam's grass/rock
        // patches stop lining up (world-space noise is shared; scale/threshold
        // are the only way the two mixes can drift).
        _NoiseScale     ("Noise Scale",      Float)      = 0.004
        _RockThreshold  ("Rock Threshold",   Range(0,1)) = 0.60
        _SunAttenuation ("Sun Attenuation",  Range(0,1)) = 0.5
        _AerialStrength ("Aerial Strength",  Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/GroundNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _NoiseScale;
                float _RockThreshold;
                half  _SunAttenuation;
                half  _AerialStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half3 color = (half3)GroundPaletteMix(IN.positionWS.xz, _NoiseScale, _RockThreshold);

                float3 normalWS    = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight   = GetMainLight(shadowCoord);

                // Sun attenuated + sum clamped, the disc/RelicLit lighting convention:
                // unclamped ambient + full sun exceeds 1 on upward faces at midday
                // (the V9 round-4 white-out). Real N·L here vs the disc's flat-plane
                // 0.5 factor — on near-flat plains ground they agree at the seam.
                half3 ambient  = max(SampleSH(half3(normalWS)), half3(0.05, 0.05, 0.05));
                half  ndotl    = saturate(dot(normalWS, mainLight.direction));
                half3 sun      = half3(mainLight.color) * mainLight.shadowAttenuation * ndotl * _SunAttenuation;
                color         *= saturate(ambient + sun);

                // No far-clip concealer (2026-07-08): terrain exists only ≤180u
                // horizontally, so its slant distance (~520u max even from the sky-drop)
                // never reaches either possible far plane — but it DID reach the old
                // 450-600u slant concealer band from the air, whitening the streamed
                // window's corners and drawing it as a square. Same no-concealer
                // contract as the disc and heroes.
                color = (half3)ApplyAerialHaze(color, AtmoAerialHazeAmount(IN.positionWS, _AerialStrength));

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma target   4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by URP's shadow pass setup.
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma target   4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
