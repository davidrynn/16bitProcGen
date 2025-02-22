Shader "Custom/CelShading"
{
    // Implements cel shading with 16bit-like rendering
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorSteps ("Color Steps", Range(2, 32)) = 16
        _ShadeSteps ("Shading Steps", Range(1, 5)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float _ColorSteps;
            float _ShadeSteps;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                col.rgb = round(col.rgb * _ColorSteps) / _ColorSteps; // Apply color quantization

                // Cel shading calculation
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float diff = max(dot(normal, lightDir), 0.0);

                // Quantize the shading into steps
                diff = floor(diff * _ShadeSteps) / _ShadeSteps;

                col.rgb *= diff; // Apply stepped shading
                return col;
            }
            ENDCG
        }
    }
}
