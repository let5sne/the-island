Shader "TheIsland/CartoonWater"
{
    Properties
    {
        _MainColor ("Water Color", Color) = (0.2, 0.5, 0.9, 0.8)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1.0
        _WaveHeight ("Wave Height", Range(0, 1)) = 0.1
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            fixed4 _MainColor;
            fixed4 _FoamColor;
            float _WaveSpeed;
            float _WaveHeight;
            float _FoamAmount;

            v2f vert (appdata v)
            {
                v2f o;
                // Simple vertex displacement wave
                float wave = sin(_Time.y * _WaveSpeed + v.vertex.x * 2.0) * _WaveHeight;
                v.vertex.y += wave;
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Moving foam texture simulation using noise
                float2 uv1 = i.uv + float2(_Time.x * 0.1, _Time.x * 0.05);
                float noise = frac(sin(dot(uv1, float2(12.9898, 78.233))) * 43758.5453);
                
                // 1. Horizon/Wave Foam (Top)
                float waveFoam = step(1.0 - _FoamAmount - (noise * 0.05), i.uv.y);
                
                // 2. Shoreline Foam (Bottom)
                // Sine wave for "tide" effect
                float tide = sin(_Time.y * 1.5) * 0.05;
                float shoreThreshold = 0.05 + tide + (noise * 0.02);
                float shoreFoam = step(i.uv.y, shoreThreshold);

                // Combine foam
                float totalFoam = max(waveFoam, shoreFoam);
                
                fixed4 col = lerp(_MainColor, _FoamColor, totalFoam);
                return col;
            }
            ENDCG
        }
    }
}
