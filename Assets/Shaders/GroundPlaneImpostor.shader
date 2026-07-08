Shader "Ground/GroundPlaneImpostor"
{
    // Terrain-coloured flat disc rendered beyond the SDF chunk radius via
    // Graphics.RenderMeshInstanced (NOT Entities.Graphics / BatchRendererGroup).
    // Bypassing BatchRendererGroup means no DOTS_INSTANCING_ON variant is required.
    //
    // Rendering mode: alpha-blended transparent so the outer edge fades to the
    // skybox naturally (no hard disc cutoff), and the inner edge fades in over
    // the terrain chunk boundary without clipping artefacts.

    Properties
    {
        // Grass/rock hues are NOT material properties: the disc consumes the global
        // _AtmoGround/_AtmoRock palette broadcast by the atmosphere authority (V9 P2,
        // ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.4) so it tracks biome/palette changes.
        _NoiseScale     ("Noise Scale",      Float)      = 0.004
        _RockThreshold  ("Rock Threshold",   Range(0,1)) = 0.60
        _InnerFadeStart ("Inner Fade Start", Float)      = 0.0
        _InnerFadeEnd   ("Inner Fade End",   Float)      = 0.0
        _OuterFadeStart ("Outer Fade Start", Float)      = 900.0
        _OuterFadeEnd   ("Outer Fade End",   Float)      = 1400.0
        _PlayerXZ       ("Player XZ",        Vector)     = (0, 0, 0, 0)
        _AerialStrength ("Aerial Strength",  Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.5
            #pragma multi_compile_instancing

            // Lighting.hlsl includes Core.hlsl and exposes SampleSH + GetMainLight.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // V9: the disc's haze now comes from the shared height-aware aerial term instead of
            // built-in RenderSettings fog (which is altitude-blind and greyed out the whole plain
            // from the 400u sky-drop — see ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.3a).
            #include "Assets/Shaders/Atmosphere.hlsl"

            // All Properties must appear in UnityPerMaterial for SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                float4 _PlayerXZ;
                float  _AerialStrength;
                float  _NoiseScale;
                float  _RockThreshold;
                float  _InnerFadeStart;
                float  _InnerFadeEnd;
                float  _OuterFadeStart;
                float  _OuterFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 worldPos   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ── Noise ────────────────────────────────────────────────────────────────

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                float d = dot(p, p + float2(34.23, 34.23));
                p += float2(d, d);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash21(i),                 Hash21(i + float2(1.0, 0.0)), u.x),
                    lerp(Hash21(i + float2(0.0, 1.0)), Hash21(i + float2(1.0, 1.0)), u.x),
                    u.y);
            }

            float FBM2(float2 p)
            {
                float v = ValueNoise(p) * 0.5 + ValueNoise(p * 2.0) * 0.25;
                return v * 1.3333;
            }

            // ── Vertex ───────────────────────────────────────────────────────────────

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.worldPos   = float4(posInputs.positionWS, 1.0);
                return OUT;
            }

            // ── Fragment ─────────────────────────────────────────────────────────────

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 worldXZ = IN.worldPos.xz;
                float  dist    = length(worldXZ - _PlayerXZ.xy);

                // Inner fade: fades the disc in over the terrain chunk boundary.
                // When _InnerFadeEnd <= _InnerFadeStart (both 0 = disabled), the disc
                // is fully opaque — terrain chunks (Opaque queue) depth-occlude it naturally.
                float innerAlpha = (_InnerFadeEnd > _InnerFadeStart)
                    ? smoothstep(_InnerFadeStart, _InnerFadeEnd, dist)
                    : 1.0;

                // Outer fade: disc fades to transparent (alpha → 0) so the skybox
                // shows through naturally — no hard disc edge, no haze-colour mismatch.
                float outerBlend = 1.0 - smoothstep(_OuterFadeStart, _OuterFadeEnd, dist);

                // Biome colour from world-space noise; hues come from the authoritative
                // palette globals (V9 P2). No shade factor needed unlike the sky mountain
                // band — the lighting multiply below already provides the shaded look.
                float  n     = FBM2(worldXZ * _NoiseScale);
                half3  color = lerp((half3)_AtmoGround.rgb, (half3)_AtmoRock.rgb, step(_RockThreshold, n));

                // Day/night lighting: ambient SH probes + attenuated sun.
                // The disc is a flat +Y plane; SDF terrain has varied normals whose average
                // Lambert response is ~0.5 of flat-plane maximum. Factor matches disc brightness
                // to average shaded terrain appearance.
                half3 ambient    = max(SampleSH(half3(0, 1, 0)), half3(0.05, 0.05, 0.05));
                Light mainLight  = GetMainLight();
                half3 sunContrib = half3(mainLight.color) * saturate(mainLight.direction.y) * 0.5h;
                color           *= saturate(ambient + sunContrib);

                // Aerial perspective (V9, height-aware): haze thins with camera altitude so the
                // sky-drop reads the plain below, while ground-level horizontal rays still veil.
                // Replaces the old MixFog call — the disc no longer reads RenderSettings fog.
                color = (half3)ApplyAerialHaze(color, AtmoAerialHazeAmount(IN.worldPos.xyz, _AerialStrength));

                // Far clip is hidden by ALPHA, not by fogging to white: the disc fades out over
                // the concealer band so the skybox far-field skirt (hazed distant land in the
                // sky shader) shows through — the handoff matches whatever the sky draws there,
                // at any camera altitude, instead of a thick white haze wall.
                float viewDist = length(IN.worldPos.xyz - _WorldSpaceCameraPos);
                float farClipFade = 1.0 - AtmoFarClipHaze(viewDist);

                // Combined alpha: 0 at inner boundary, 1 in mid zone, 0 at outer/far-clip edge.
                half alpha = (half)(innerAlpha * outerBlend * farClipFade);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
