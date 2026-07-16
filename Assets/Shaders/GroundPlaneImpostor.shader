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
        // V17 mid-field variation (GROUND_PLANE_IMPOSTOR_SPEC.md §12). Macro dials must
        // stay equal to TerrainLit.shader (parity-guarded by TerrainChunkMaterialContractTests);
        // relief dials are disc-only — terrain has real normals.
        _MacroNoiseScale ("Macro Noise Scale", Float)      = 0.0007
        _MacroStrength   ("Macro Strength",    Range(0,1)) = 0.08
        _ReliefScale     ("Relief Scale",      Float)      = 0.002
        _ReliefStrength  ("Relief Strength",   Range(0,1)) = 0.35
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
            // GroundNoise.hlsl pulls in Atmosphere.hlsl and the grass/rock mix shared with the
            // SDF terrain (V9 P3) — one noise definition so seam patches line up by construction.
            #include "Assets/Shaders/GroundNoise.hlsl"

            // All Properties must appear in UnityPerMaterial for SRP Batcher.
            CBUFFER_START(UnityPerMaterial)
                float4 _PlayerXZ;
                float  _AerialStrength;
                float  _NoiseScale;
                float  _RockThreshold;
                float  _MacroNoiseScale;
                float  _MacroStrength;
                float  _ReliefScale;
                float  _ReliefStrength;
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
                // V17 P1: the mix includes the macro luminance octave (shared with terrain).
                half3 color = (half3)GroundPaletteMix(worldXZ, _NoiseScale, _RockThreshold,
                                                      _MacroNoiseScale, _MacroStrength);

                // Day/night lighting: ambient SH probes + attenuated sun.
                // V17 P2: Lambert against a fake relief normal instead of the flat +Y —
                // the plane's single normal gave one constant lighting value across the
                // whole band (§12.1 root cause 2). At _ReliefStrength 0 this reduces
                // exactly to the old flat-plane term. Lit by the live GetMainLight()
                // sun, so low sun rakes the relief automatically once time-of-day runs.
                // SDF terrain has varied normals whose average Lambert response is ~0.5
                // of flat-plane maximum; the 0.5 factor matches disc brightness to
                // average shaded terrain appearance at the seam.
                float3 reliefN   = GroundReliefNormal(worldXZ, _ReliefScale, _ReliefStrength);
                half3 ambient    = max(SampleSH((half3)reliefN), half3(0.05, 0.05, 0.05));
                Light mainLight  = GetMainLight();
                half  ndotl      = (half)saturate(dot(reliefN, mainLight.direction));
                half3 sunContrib = half3(mainLight.color) * ndotl * 0.5h;
                color           *= saturate(ambient + sunContrib);

                // Aerial perspective (V9, height-aware): haze thins with camera altitude so the
                // sky-drop reads the plain below, while ground-level horizontal rays still veil.
                // Replaces the old MixFog call — the disc no longer reads RenderSettings fog.
                color = (half3)ApplyAerialHaze(color, AtmoAerialHazeAmount(IN.worldPos.xyz, _AerialStrength));

                // The disc→skirt handoff is hidden by ALPHA, not by fogging to white: the disc
                // fades out approaching the world edge so the skybox far-field skirt (hazed
                // distant land in the sky shader) shows through. Measured HORIZONTALLY
                // (AtmoWorldEdgeHaze, 2026-07-08): a slant-based fade shrank the visible world
                // to a ~450u circle from drop altitude, drawing the terrain window as a square
                // hole in fog. AtmoLandmarkEdgeFade separately covers the REAL camera clip
                // (landmark plane, or the world edge with the feature off) — dormant in
                // normal play since the outer fade ends well inside it.
                float worldEdgeFade = 1.0 - AtmoWorldEdgeHaze(IN.worldPos.xyz);
                float viewDist = length(IN.worldPos.xyz - _WorldSpaceCameraPos);
                float clipEdgeFade = AtmoLandmarkEdgeFade(viewDist);

                // Combined alpha: 0 at inner boundary, 1 in mid zone, 0 at world/clip edge.
                half alpha = (half)(innerAlpha * outerBlend * worldEdgeFade * clipEdgeFade);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
