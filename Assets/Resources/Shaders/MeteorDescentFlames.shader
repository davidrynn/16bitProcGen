// V13 burning-descent VFX (METEOR_ARRIVAL_SEQUENCE_SPEC.md Phase 3).
// One procedural screen-space layer: flame tongues licking inward from the screen edges,
// ember sparks streaming past, and a faint warm vignette — all scaled by _Intensity, which the
// controller drives from the ignition ramp x altitude-band envelope. Fully procedural (FBM +
// hash), no textures. Plain CG UI shader — ScreenSpaceOverlay canvases bypass URP rendering.
Shader "UI/MeteorDescentFlames"
{
    Properties
    {
        _MainTex ("Unused (RawImage contract)", 2D) = "white" {}
        _Intensity ("Burn Intensity", Range(0, 1)) = 0
        _FlameInner ("Flame Inner Color", Color) = (1.0, 0.85, 0.35, 1)
        _FlameOuter ("Flame Outer Color", Color) = (0.9, 0.25, 0.05, 1)
        _FlameReach ("Flame Reach", Range(0, 0.5)) = 0.22
        _FlameSpeed ("Flame Speed", Float) = 1.6
        _EmberDensity ("Ember Density", Range(0, 1)) = 0.45
        _EmberSize ("Ember Size", Range(0.001, 0.03)) = 0.006
        _EmberSpeed ("Ember Speed", Range(0, 80)) = 34
        _EmberJitter ("Ember Jitter", Range(0, 2)) = 1.3
        _VignetteStrength ("Warm Vignette", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float _Intensity;
            fixed4 _FlameInner;
            fixed4 _FlameOuter;
            float _FlameReach;
            float _FlameSpeed;
            float _EmberDensity;
            float _EmberSize;
            float _EmberSpeed;
            float _EmberJitter;
            float _VignetteStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += amp * vnoise(p);
                    p = p * 2.03 + 17.17;
                    amp *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float t = _Time.y;

                // Distance in from the nearest screen edge; the flames live in this border band.
                float edgeDist = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));

                // Continuous around-the-frame coordinate (cos/sin domain avoids the atan2 seam);
                // second channel scrolls inward over time so tongues lick and flicker.
                float ang = atan2(p.y, p.x);
                float2 flameDomain = float2(cos(ang), sin(ang)) * 3.2;
                float n = fbm(flameDomain * 2.1 + float2(0.0, edgeDist * 9.0 - t * _FlameSpeed));

                // Noisy flame boundary: solid at the very edge, ragged tongues reaching inward.
                float reach = _FlameReach * (0.55 + 0.45 * _Intensity);
                float flame = saturate((reach - edgeDist - (n - 0.5) * reach * 1.4) / reach);
                flame = flame * flame; // hotter core, wispier tips

                float3 flameCol = lerp(_FlameOuter.rgb, _FlameInner.rgb, flame);
                float flameAlpha = flame * (0.55 + 0.45 * n) * _Intensity;

                // Embers: small ROUND sparks born near the screen center and streaming outward
                // (owner: round + small + less dense; the outward direction is right). Placement and
                // motion live in a polar angle×log-radius lattice that scrolls outward over time, but
                // each spark's SHAPE is measured in SCREEN space so every dot stays round and the
                // same size at any radius. Measuring the shape in lattice space is what smeared them
                // into radial warp-streaks (the bug); a cell is tiny-tangential near the center and
                // huge far out, so a "round" lattice dot is anything but round on screen.
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 pc = float2(p.x * aspect, p.y);
                float rad = length(pc);
                float ang01 = atan2(pc.y, pc.x) * (1.0 / 6.2831853) + 0.5; // 0..1, wraps at ±PI
                float logR = log(rad + 0.03);

                float ember = 0.0;
                for (int l = 0; l < 2; l++)
                {
                    // _EmberSpeed scales BOTH layers (the small +l term is only parallax between
                    // them). The earlier `0.7 * l` form left layer 0 at the base speed, so multiplying
                    // that term sped up only the second layer — the "only some embers moved" you saw.
                    float speed   = _EmberSpeed * (1.0 + 0.25 * l);
                    float angBins = 26.0 + 10.0 * l;
                    float radBins = 5.0 + 2.0 * l;
                    // Log-radius coordinate scrolls so a fixed spark travels outward with velocity
                    // proportional to its radius — it crawls near center and WHIPS toward the edge
                    // (radius grows exponentially in time). Minus sign = outward (cf. the flame's
                    // inward lick above); raise `_EmberSpeed` for a faster field, lower `radBins` for
                    // a steeper whip.
                    float2 g = float2(ang01 * angBins, logR * radBins - t * speed);
                    float2 cell = floor(g);
                    float h = hash21(cell + l * 61.7);
                    // Jitter the spark's home inside its cell so the lattice never reads as rings/rays.
                    float2 jitter = float2(hash21(cell + 7.3), hash21(cell + 3.1)) - 0.5;
                    float2 f = (frac(g) - 0.5) - jitter * _EmberJitter;
                    // Lattice offset -> screen-space offset (arc lengths) so the dot is round on screen:
                    //   radial  ~ (f/radBins)*rad ,  tangential ~ (f/angBins)*2PI*rad
                    float dR = (f.y / radBins) * rad;
                    float dT = (f.x / angBins) * 6.2831853 * rad;
                    float dScreen = length(float2(dR, dT));
                    // Small round dot; per-spark size variation keeps them non-uniform.
                    float size = _EmberSize * (0.55 + 0.9 * hash21(cell + 19.1));
                    float spark = saturate(1.0 - dScreen / max(size, 1e-4));
                    spark *= spark; // crisp core, quick falloff
                    // Fade out the singular hot-spot at the exact center; sparks are born just off it.
                    float radialFade = smoothstep(0.04, 0.22, rad);
                    ember += spark * step(1.0 - _EmberDensity * 0.5, h) * radialFade;
                }
                float3 emberCol = _FlameInner.rgb * 1.4;
                float emberAlpha = saturate(ember) * _Intensity;

                // Warm vignette — a faint heat tint creeping in from the frame.
                float vig = smoothstep(0.5, 0.0, edgeDist) * _VignetteStrength * _Intensity;

                float3 col = flameCol * flameAlpha + emberCol * emberAlpha + _FlameOuter.rgb * vig * 0.5;
                float alpha = saturate(flameAlpha + emberAlpha * 0.8 + vig * 0.35);

                // Normalize color back to un-premultiplied for standard alpha blending.
                col = alpha > 1e-4 ? col / max(alpha, 1e-4) : col;

                return fixed4(col, alpha) * i.color;
            }
            ENDCG
        }
    }
}
