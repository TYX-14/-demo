Shader "MyShared/VolumetricLightParticles"
{
    Properties
    {
        _ParticleIntensity ("粒子亮度", Range(0, 5)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "LightDust"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ParticleIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float radial = length(centeredUV);
                float softDisc = 1.0 - smoothstep(0.25, 1.0, radial);
                float core = 1.0 - smoothstep(0.0, 0.42, radial);
                float glow = lerp(softDisc, core, 0.35);
                float alpha = input.color.a * glow;
                float3 color = input.color.rgb * alpha * _ParticleIntensity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}