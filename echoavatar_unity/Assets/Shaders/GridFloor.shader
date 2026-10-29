Shader "Custom/BasicGrid"
{
    Properties
    {
        _GridSize ("Grid Size", Float) = 1.0
        _LineWidth ("Line Width", Range(0.01, 0.5)) = 0.05
        _GridColor ("Grid Line Color", Color) = (0, 0, 0, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _GridSize;
            float _LineWidth;
            fixed4 _GridColor;
            fixed4 _BaseColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 转换到世界坐标
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 获取基于网格大小的 UV 坐标
                float2 uv = i.worldPos.xz / _GridSize;

                // 2. 【核心】计算当前像素在世界空间中的变化率 (即像素的粗细)
                // fwidth 返回的是当前像素覆盖了多少 grid 单位
                float2 deriv = fwidth(uv);

                // 3. 计算当前点距离网格线中心的距离 (结果范围 0 ~ 0.5)
                float2 gridDist = abs(frac(uv - 0.5) - 0.5);

                // 4. 【核心】计算线条强度 (抗锯齿 + 摩尔纹消除)
                // 我们不使用硬切 (if dist < width)，而是使用 smoothstep 进行平滑
                // 当 deriv (像素大小) 变大时，边缘会自动变模糊
                // 这里的 1.5 是抗锯齿的柔和度，可以微调
                float2 lineAA = smoothstep(_LineWidth + deriv * 1.5, _LineWidth - deriv * 1.5, gridDist);

                // 5. 合并 X 和 Z 轴的线条
                float lineStrength = max(lineAA.x, lineAA.y);

                // 6. 混合颜色
                return lerp(_BaseColor, _GridColor, lineStrength);
            }
            ENDCG
        }
    }
}