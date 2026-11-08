Shader "Custom/CloudShadowOverlay"
{
    Properties
    {
        _ShadowColor ("阴影颜色", Color) = (0.16, 0.18, 0.22, 1)
        _ShadowStrength ("阴影强度", Range(0, 1)) = 0.45
        _CloudScale ("云影大小", Range(0.001, 0.1)) = 0.018
        _CloudCoverage ("云层覆盖率", Range(0, 1)) = 0.52
        _CloudSoftness ("边缘柔和度", Range(0.01, 0.5)) = 0.16
        _CloudSpeed ("流动速度", Range(0, 2)) = 0.12
        _CloudDirection ("流动方向", Vector) = (1, 0.25, 0, 0)
        _DetailStrength ("细节变化", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Name "CloudShadowOverlay"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _ShadowStrength;
                float _CloudScale;
                half _CloudCoverage;
                half _CloudSoftness;
                float _CloudSpeed;
                float4 _CloudDirection;
                half _DetailStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float noise2d(float2 p)
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
            float cloudNoise(float2 p)
            {
                float n = noise2d(p);
                n += noise2d(p * 2.03 + 17.1) * 0.42;
                n += noise2d(p * 4.11 - 8.7) * 0.18;
                return n / 1.60;
            }
            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 direction = normalize(_CloudDirection.xy + float2(0.0001, 0.0001));
                float2 worldUV = input.positionWS.xz * max(_CloudScale, 0.0001);
                worldUV += direction * (_Time.y * _CloudSpeed);
                float broad = cloudNoise(worldUV);
                float detail = cloudNoise(worldUV * 1.65 + float2(13.7, 5.2));
                float shape = lerp(broad, broad * 0.72 + detail * 0.28, _DetailStrength);
                float edge = max(_CloudSoftness, 0.01);
                float mask = 1.0 - smoothstep(_CloudCoverage - edge, _CloudCoverage + edge, shape);
                half alpha = saturate(mask * _ShadowStrength);
                return half4(_ShadowColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}