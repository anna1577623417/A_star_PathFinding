Shader "Custom/GridNode_v2"
{
    Properties
    {
        _State ("State", Float) = 0
        _TerrainColor ("Terrain Color", Color) = (0.75, 0.75, 0.75, 1)
        _TerrainGlow ("Terrain Glow", Float) = 0
        _PulseSpeed ("Pulse Speed", Float) = 3.0
        _FresnelPower ("Fresnel Power", Float) = 4.0
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

            float _State;
            float4 _TerrainColor;
            float _TerrainGlow;
            float _PulseSpeed;
            float _FresnelPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalDir : TEXCOORD0;
                float3 viewDir  : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float4 baseColor;
                float emission = 0;

                if (_State < 0.5)
                {
                    // State 0: 显示地形颜色
                    baseColor = _TerrainColor;
                    emission = _TerrainGlow * pulse * 0.8;
                }
                else if (_State < 1.5)
                {
                    // State 1: Hover - 淡蓝
                    baseColor = float4(0.6, 0.85, 1.0, 1);
                    emission = pulse * 0.3;
                }
                else if (_State < 2.5)
                {
                    // State 2: Start/End - 金黄
                    baseColor = float4(1.0, 0.85, 0.0, 1);
                    emission = pulse * 0.5;
                }
                else if (_State < 3.5)
                {
                    // State 3: Path - 明绿
                    baseColor = float4(0.1, 0.9, 0.25, 1);
                    emission = pulse * 0.4;
                }
                else if (_State < 4.5)
                {
                    // State 4: Exploring - 青色
                    float fp = sin(_Time.y * _PulseSpeed * 2.0) * 0.5 + 0.5;
                    baseColor = float4(0.1, 0.75, 0.85, 1);
                    emission = fp * 0.5;
                }
                else if (_State < 5.5)
                {
                    // State 5: Explored - 深蓝紫
                    baseColor = float4(0.25, 0.25, 0.55, 1);
                    emission = 0.05;
                }
                else
                {
                    // State 7: Player - 橙黄
                    baseColor = float4(1.0, 0.5, 0.05, 1);
                    emission = pulse * 1.2;
                }

                // Fresnel 边缘光
                float fresnel = pow(1 - saturate(dot(i.normalDir, i.viewDir)), _FresnelPower);

                float3 finalColor = baseColor.rgb;
                finalColor += baseColor.rgb * emission;
                finalColor += baseColor.rgb * fresnel * 0.6;

                return float4(finalColor, 1);
            }
            ENDCG
        }
    }
}
