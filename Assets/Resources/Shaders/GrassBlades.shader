// GrassBlades.shader
// GPU-instanced grass blade rendering for DOTS terrain chunks.
//
// Uses Graphics.DrawMeshInstancedIndirect — each instance reads its world position,
// height, colour tint, and facing angle from a StructuredBuffer<GrassBladeData> set
// via MaterialPropertyBlock._BladeBuffer by GrassChunkRenderSystem.
//
// NO geometry shader: vertex shader only. Y-axis billboard so blades stand upright
// and face the camera horizontally. Alpha cutout from _MainTex.a.

Shader "DOTS/GrassBlades"
{
    Properties
    {
        _MainTex         ("Blade Texture (RGBA, alpha = blade shape)", 2D) = "white" {}
        _AlphaCutoff     ("Alpha Cutoff",    Range(0.01, 1.0)) = 0.35
        _WindFrequency   ("Wind Frequency",  Float) = 1.4
        _WindScale       ("Wind XZ Scale",   Float) = 0.25
        _WindStrength    ("Wind Strength",   Float) = 0.25
        _FadeStart       ("Fade Start Dist", Float) = 60.0
        _FadeEnd         ("Fade End Dist",   Float) = 120.0
    }

    SubShader
    {
        // AlphaTest queue so cutout blades sort correctly against opaque terrain.
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "AlphaTest"
        }

        Pass
        {
            Name "GrassBladesForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Both sides visible — blades are thin quads with no interior.
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // No multi_compile_instancing — we read instance data directly from
            // _BladeBuffer via SV_InstanceID, bypassing Unity's instancing macros.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Per-material cbuffer (SRP Batcher compatible section) ────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _AlphaCutoff;
                float  _WindFrequency;
                float  _WindScale;
                float  _WindStrength;
                float  _FadeStart;
                float  _FadeEnd;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ── Per-instance blade data (set via MaterialPropertyBlock each frame) ───
            // Must match GrassBladeData C# struct exactly (32-byte stride):
            //   float3 WorldPosition  (offset  0)
            //   float  Height         (offset 12)
            //   float3 ColorTint      (offset 16)
            //   float  FacingAngle    (offset 28)
            struct GrassBladeData
            {
                float3 WorldPosition;
                float  Height;
                float3 ColorTint;
                float  FacingAngle;
            };
            StructuredBuffer<GrassBladeData> _BladeBuffer;

            // ── Vertex input / output ────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float2 uv            : TEXCOORD0;
                float3 color         : TEXCOORD1;
                float  distanceFade  : TEXCOORD2;
            };

            // ── Vertex shader ────────────────────────────────────────────────────────
            Varyings vert(Attributes input)
            {
                GrassBladeData blade = _BladeBuffer[input.instanceID];

                // --- Y-axis billboard ---
                // Build right vector as camera-facing in XZ plane so blades stand upright
                // and rotate to face the viewer regardless of camera yaw.
                float3 toCamera  = _WorldSpaceCameraPos - blade.WorldPosition;
                toCamera.y = 0.0;
                float lenSq = dot(toCamera, toCamera);
                float3 right = lenSq > 0.0001 ? normalize(toCamera) : float3(1, 0, 0);

                // Rotate right vector by FacingAngle to add per-blade variety.
                float s, c;
                sincos(blade.FacingAngle, s, c);
                right = float3(right.x * c - right.z * s,
                               0.0,
                               right.x * s + right.z * c);

                // --- World position assembly ---
                // positionOS.x  : signed horizontal offset within blade quad (-0.5..0.5)
                // positionOS.y  : normalised height within blade (0 = base, 1 = tip)
                float heightFraction = input.positionOS.y;

                // Wind: tips sway more than the base (multiply by heightFraction).
                float windPhase = _Time.y * _WindFrequency
                                + blade.WorldPosition.x * _WindScale
                                + blade.WorldPosition.z * _WindScale * 0.7;
                float windAmt = sin(windPhase) * _WindStrength * heightFraction;

                float3 worldPos = blade.WorldPosition
                    + right  * input.positionOS.x
                    + float3(0, 1, 0) * (heightFraction * blade.Height)
                    + float3(windAmt, 0.0, windAmt * 0.5);

                // --- Distance fade (alpha fade near _FadeEnd) ---
                float dist = distance(_WorldSpaceCameraPos, blade.WorldPosition);
                float fade = 1.0 - saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));

                // Simple diffuse lighting from main light direction.
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(float3(0,1,0), mainLight.direction) * 0.5 + 0.5);

                Varyings output;
                output.positionCS   = TransformWorldToHClip(worldPos);
                output.uv           = TRANSFORM_TEX(input.uv, _MainTex);
                output.color        = blade.ColorTint * mainLight.color.rgb * NdotL;
                output.distanceFade = fade;
                return output;
            }

            // ── Fragment shader ──────────────────────────────────────────────────────
            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Alpha cutout: discard transparent parts of blade texture, and fade at distance.
                clip(tex.a * input.distanceFade - _AlphaCutoff);

                return half4(tex.rgb * input.color, 1.0);
            }

            ENDHLSL
        }

    }

    FallBack Off
}
