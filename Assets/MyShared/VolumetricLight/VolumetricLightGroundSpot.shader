Shader "MyShared/VolumetricLightGroundSpot"
{
    Properties
    {
        _SpotColor ("光斑颜色", Color) = (1, 0.86, 0.58, 1)
        _SpotIntensity ("光斑强度", Range(0, 3)) = 0.8
        _SpotSoftness ("边缘柔和", Range(0.05, 1)) = 0.75
        _NoiseScale ("噪声尺度", Range(0.01, 10)) = 1.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+5"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "VolumetricGroundSpot"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SpotColor;
                float _SpotIntensity;
                float _SpotSoftness;
                float _NoiseScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            float HashNoise(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float radialDistance = length(centeredUV);
                float edge = 1.0 - smoothstep(1.0 - _SpotSoftness, 1.0, radialDistance);
                float noise = HashNoise(input.positionWS.xz * _NoiseScale + _Time.yy * 0.03);
                float variation = lerp(0.88, 1.12, noise);
                float intensity = edge * variation * _SpotIntensity;
                return half4(_SpotColor.rgb * intensity, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
