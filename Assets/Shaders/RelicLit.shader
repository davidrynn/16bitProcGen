Shader "Relic/RelicLit"
{
    // Hero-relic lit shader (V9 P4b — ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.4).
    //
    // Exists because stock URP/Lit applies full-strength pipeline fog with no
    // per-material dial, which washed the giant hand to fog-white at vista
    // distance (the confirmed "white-out" symptom). This variant skips built-in
    // fog entirely and applies the shared height-aware aerial term at a REDUCED
    // strength — the hero exemption: enough haze to sit in the scene, but the
    // silhouette stays legible at 250-400u.
    //
    // Rendered through Entities Graphics (BatchRendererGroup), hence the
    // DOTS_INSTANCING_ON variant and target 4.5.

    Properties
    {
        _BaseColor      ("Base Color", Color) = (0.55, 0.52, 0.48, 1)
        _AerialStrength ("Aerial Strength (hero exemption, reduced)", Range(0, 1)) = 0.3
        _SunAttenuation ("Sun Attenuation", Range(0, 1)) = 0.5
        // R6 P4: per-instance spawn dissolve, driven by RelicSpawnFadeSystem through the
        // BRG instanced property. Material default 1 = solid, so non-ECS uses are unaffected.
        _RelicSpawnFade ("Spawn Fade (per-instance, ECS-driven)", Range(0, 1)) = 1
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
            #include "Assets/Shaders/Atmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _AerialStrength;
                half  _SunAttenuation;
                float _RelicSpawnFade;
            CBUFFER_END

            // BRG per-instance override (R6 P4): RelicSpawnFade IComponentData maps here
            // via [MaterialProperty]. WITH_DEFAULT falls back to the material value (1)
            // when an instance carries no override, so anything without the component
            // renders solid.
            #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float, _RelicSpawnFade)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define _RelicSpawnFade UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RelicSpawnFade)
            #endif

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

                // Landmark edge dissolve (R6 P3) + spawn fade-in (R6 P4) — heroes never
                // pop: not at a clip plane (edge fade) and not on realization (spawn
                // fade). min() so whichever is more dissolved wins; both share the same
                // screen-space noise so the patterns can't fight. Clipped before
                // lighting so dissolved fragments cost nothing.
                float viewDist = length(IN.positionWS - _WorldSpaceCameraPos);
                float visibility = min(AtmoLandmarkEdgeFade(viewDist), _RelicSpawnFade);
                clip(visibility - AtmoInterleavedGradientNoise(IN.positionCS.xy));

                float3 normalWS    = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight   = GetMainLight(shadowCoord);

                half3 ambient  = max(SampleSH(half3(normalWS)), half3(0.05, 0.05, 0.05));
                half  ndotl    = saturate(dot(normalWS, mainLight.direction));
                // Attenuate the sun and clamp the sum, matching the ground disc's lighting
                // convention: unclamped ambient + full sun exceeds 1 on upward-facing
                // surfaces at midday, which rendered the hands near-white from altitude
                // even with haze disabled (V9 round-4 observation).
                half3 sun      = half3(mainLight.color) * mainLight.shadowAttenuation * ndotl * (half)_SunAttenuation;
                half3 lighting = saturate(ambient + sun);

                half3 color = _BaseColor.rgb * lighting;

                // Hero exemption: reduced-strength shared aerial term instead of pipeline fog,
                // WITHOUT the far-clip concealer (R6 P3) — the concealer hides the world's clip
                // edge, but a landmark drawn beyond it must not be erased by it. Same
                // no-concealer contract as the ground disc; the dither above replaces the
                // concealer as the edge treatment. The haze ramp partially ghosts the
                // landmark before that dither starts, so the dissolve never clips a fully
                // legible object; the dither itself blends toward the true backdrop.
                float haze = AtmoLandmarkHazeRamp(
                    AtmoAerialHazeAmount(IN.positionWS, _AerialStrength), viewDist);
                color = (half3)ApplyAerialHaze(color, haze);

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
            #include "Assets/Shaders/Atmosphere.hlsl"

            // Same per-instance spawn fade as ForwardLit (R6 P4) — declared per-pass
            // because each HLSLPROGRAM compiles standalone.
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _AerialStrength;
                half  _SunAttenuation;
                float _RelicSpawnFade;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float, _RelicSpawnFade)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define _RelicSpawnFade UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RelicSpawnFade)
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                // Mirror the ForwardLit dissolve exactly (same pixel coords -> same noise,
                // same min() of edge fade and spawn fade), or the depth prepass would write
                // solid depth where the color pass has dithered holes and depth-reading
                // effects would ghost the dissolving hero.
                float viewDist = length(IN.positionWS - _WorldSpaceCameraPos);
                float visibility = min(AtmoLandmarkEdgeFade(viewDist), _RelicSpawnFade);
                clip(visibility - AtmoInterleavedGradientNoise(IN.positionCS.xy));
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
