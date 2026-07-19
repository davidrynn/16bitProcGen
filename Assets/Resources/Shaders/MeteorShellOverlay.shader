// V14 meteor-interior loading shell (METEOR_ARRIVAL_SEQUENCE_SPEC.md).
// Canvas overlay shader: dark rock interior + pulsing crack glow, dissolving open along the
// cracks from screen center outward when _OpenProgress rises. Fed a packed texture generated
// by MeteorShellOverlay (R = rock FBM, G = crack mask, B = radial distance from center).
// Plain CG UI shader on purpose — ScreenSpaceOverlay canvases bypass URP scene rendering.
Shader "UI/MeteorShellOverlay"
{
    Properties
    {
        _MainTex ("Packed (R rock, G crack, B radial)", 2D) = "white" {}
        _RockColor ("Rock Color", Color) = (0.17, 0.135, 0.11, 1)
        _RockDeepColor ("Rock Deep Color", Color) = (0.028, 0.022, 0.018, 1)
        _CrackColor ("Crack Glow Color", Color) = (1.0, 0.45, 0.12, 1)
        _CrackGlow ("Crack Glow Intensity", Range(0, 4)) = 1.1
        _PulseSpeed ("Pulse Speed", Float) = 2.2
        _OpenProgress ("Open Progress", Range(0, 1)) = 0
        _Flare ("Flare", Range(0, 1)) = 0
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

            sampler2D _MainTex;
            fixed4 _RockColor;
            fixed4 _RockDeepColor;
            fixed4 _CrackColor;
            float _CrackGlow;
            float _PulseSpeed;
            float _OpenProgress;
            float _Flare;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 shellTex = tex2D(_MainTex, i.uv);
                float rockL = shellTex.r;
                float crack = shellTex.g;
                float radial = shellTex.b;

                // Interior read: rock closing in around the view — darkest at the screen edges.
                float3 rock = lerp(_RockDeepColor.rgb, _RockColor.rgb, rockL);
                rock *= lerp(1.0, 0.35, smoothstep(0.35, 1.0, radial));

                // Cracks pulse slowly while holding; the flare beat slams them bright at release.
                float pulse = 0.55 + 0.45 * sin(_Time.y * _PulseSpeed + rockL * 6.2831);
                float glowAmt = crack * (_CrackGlow * pulse + _Flare * 3.0);
                float3 col = rock + _CrackColor.rgb * glowAmt;

                // Dissolve field: low on cracks and near center → those open first, the plate
                // interiors and screen edges burn away last ("shatters away from the edges").
                float field = saturate(radial * 0.72 + (1.0 - crack) * 0.24 + rockL * 0.08);
                float p = _OpenProgress * 1.35 - 0.12; // < 0 at start → fully opaque everywhere
                float alpha = smoothstep(p, p + 0.08, field);

                // Burning rim on the still-solid band just ahead of the dissolve front.
                float rim = alpha * (1.0 - smoothstep(p + 0.08, p + 0.24, field)) * saturate(_OpenProgress * 8.0);
                col += _CrackColor.rgb * rim * 2.0;

                return fixed4(col, alpha) * i.color;
            }
            ENDCG
        }
    }
}
