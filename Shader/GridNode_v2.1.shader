Shader "Custom/GridNode_v2.1"
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

            // ✅ 核心1：开启 instancing
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            // ❌ 删除原来的全局变量（会破坏instancing）
            // float _State;
            // float4 _TerrainColor;

            float _TerrainGlow;
            float _PulseSpeed;
            float _FresnelPower;

            // ✅ 核心2：定义 instancing buffer
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _State)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;

                // ✅ 必须加
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalDir : TEXCOORD0;
                float3 viewDir  : TEXCOORD1;

                // ✅ 必须传
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;

                // ✅ 必须加
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalDir = UnityObjectToWorldNormal(v.normal);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ✅ 必须加
                UNITY_SETUP_INSTANCE_ID(i);

                // ✅ 核心3：通过 instancing 读取变量
                float _State_i = UNITY_ACCESS_INSTANCED_PROP(Props, _State);
                float4 _TerrainColor_i = UNITY_ACCESS_INSTANCED_PROP(Props, _TerrainColor);

                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float4 baseColor;
                float emission = 0;

                if (_State_i < 0.5)
                {
                    baseColor = _TerrainColor_i;
                    emission = _TerrainGlow * pulse * 0.8;
                }
                else if (_State_i < 1.5)
                {
                    baseColor = float4(0.6, 0.85, 1.0, 1);
                    emission = pulse * 0.3;
                }
                else if (_State_i < 2.5)
                {
                    baseColor = float4(1.0, 0.85, 0.0, 1);
                    emission = pulse * 0.5;
                }
                else if (_State_i < 3.5)
                {
                    baseColor = float4(0.1, 0.9, 0.25, 1);
                    emission = pulse * 0.4;
                }
                else if (_State_i < 4.5)
                {
                    float fp = sin(_Time.y * _PulseSpeed * 2.0) * 0.5 + 0.5;
                    baseColor = float4(0.1, 0.75, 0.85, 1);
                    emission = fp * 0.5;
                }
                else if (_State_i < 5.5)
                {
                    baseColor = float4(0.25, 0.25, 0.55, 1);
                    emission = 0.05;
                }
                else
                {
                    baseColor = float4(1.0, 0.5, 0.05, 1);
                    emission = pulse * 1.2;
                }

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