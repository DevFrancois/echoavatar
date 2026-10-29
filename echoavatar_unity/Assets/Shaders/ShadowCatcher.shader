Shader "Custom/ShadowCatcher"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.5)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "ShadowCatcher"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 接收阴影关键字
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            float4 _ShadowColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 获取阴影坐标
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                
                // 获取主光源阴影衰减 (1=亮, 0=阴影)
                Light mainLight = GetMainLight(shadowCoord);
                half shadowAtten = mainLight.shadowAttenuation;

                // 反转逻辑：我们要只显示阴影
                // 如果 shadowAtten 是 1 (无阴影)，alpha 应该是 0
                // 如果 shadowAtten 是 0 (有阴影)，alpha 应该是 _ShadowColor.a
                half alpha = (1.0 - shadowAtten) * _ShadowColor.a;

                return half4(_ShadowColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}