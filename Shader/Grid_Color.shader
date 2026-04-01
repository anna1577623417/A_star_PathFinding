Shader "Custom/GridNode_Advanced"
{
    Properties
    {
        _State ("State", Float) = 0
        // 0=Default  1=Hover  2=Start/End  3=Path
        // 4=Exploring(OpenSet)  5=Explored(ClosedSet)  6=Wall  7=Player

        _EmissionStrength ("Emission Strength", Float) = 1.5
        _PulseSpeed ("Pulse Speed", Float) = 3
        _FresnelPower ("Fresnel Power", Float) = 4
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
            float _EmissionStrength;
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

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // ===== 根据 State 选颜色 =====
                float4 baseColor;
                float emission = 0;
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;

                if (_State < 0.5)
                {
                    // 0 = Default - 浅灰
                    baseColor = float4(0.75, 0.75, 0.75, 1);
                }
                else if (_State < 1.5)
                {
                    // 1 = Hover - 淡蓝高亮 + 微弱呼吸
                    baseColor = float4(0.6, 0.85, 1.0, 1);
                    emission = pulse * 0.4;
                }
                else if (_State < 2.5)
                {
                    // 2 = Start/End 标记 - 金黄 + 呼吸
                    baseColor = float4(1.0, 0.3, 0.02, 1);
                    emission = pulse * _EmissionStrength * 0.6;
                }
                else if (_State < 3.5)
                {
                    // 3 = Path 最终路径 - 明亮绿 + 微发光
                    baseColor = float4(0.1, 0.9, 0.25, 1);
                    emission = pulse * 0.5;
                }
                else if (_State < 4.5)
                {
                    // 4 = Exploring 开放列表 -黄色 + (无)快速脉冲
                    //float fastPulse = sin(_Time.y * _PulseSpeed * 2.0) * 0.5 + 0.5;
                    baseColor = float4(1.0, 0.65, 0.15, 1);
                    //emission = fastPulse * 0.6;
                    emission =0.02;
                }
                else if (_State < 5.5)
                {
                    // 5 = Explored 关闭列表 - 淡蓝色 + 完全静态（明确“已处理”）
                     baseColor = float4(0.55, 0.75, 1.0, 1);
                    emission = 0.02;
                }
                else if (_State < 6.5)
                {
                    // 6 = Wall 障碍物 - 深棕（无发光）
                    baseColor = float4(0.22, 0.16, 0.12, 1);
                    emission = 0;
                }
                else
                {
                    // 7 = Player 玩家 - 明亮橙 + 强呼吸
                    baseColor = float4(1.0, 0.5, 0.05, 1);
                    emission = pulse * _EmissionStrength;
                }

                // ===== Fresnel 边缘光 =====
                float fresnel = pow(1 - saturate(dot(i.normalDir, i.viewDir)), _FresnelPower);

                // ===== 合成 =====
                float3 finalColor = baseColor.rgb;
                finalColor += baseColor.rgb * emission;
                finalColor += baseColor.rgb * fresnel * 0.8;

                return float4(finalColor, 1);
            }
            ENDCG
        }
    }
}
