Shader "MyShared/IL3DN/Branch_URP"
{
    Properties
    {
        [MainTexture] _MainTex ("树干贴图", 2D) = "white" {}
        [MainColor] _Color ("树干颜色", Color) = (1,1,1,1)

        [Toggle(_WIND_ON)] _Wind ("启用风动", Float) = 1
        _WindNoiseTex ("风噪贴图", 2D) = "gray" {}
        _WindDirection ("风向（世界坐标）", Vector) = (1,0,0,0)
        _WindSpeed ("风速", Range(0,5)) = 0.5
        _WindStrength ("风力强度", Range(0,2)) = 0.35
        _WindNoiseScale ("风噪缩放", Range(0.001,2)) = 0.15
        _WindNoiseStrength ("风噪影响", Range(0,2)) = 1
        [Toggle(_USE_VERTEX_COLOR_WIND)] _UseVertexColorWind ("使用顶点色控制风动", Float) = 1
        _WindMaskStrength ("顶点色风动影响", Range(0,2)) = 1

        [Toggle(_SNOW_ON)] _Snow ("启用积雪", Float) = 0
        _SnowAmount ("积雪强度", Range(0,2)) = 0.5
        _SnowColor ("积雪颜色", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 200
        Cull Back

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "VS_indirect.cginc"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_WindNoiseTex);
        SAMPLER(sampler_WindNoiseTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            float4 _WindDirection;
            float _WindSpeed;
            float _WindStrength;
            float _WindNoiseScale;
            float _WindNoiseStrength;
            float _WindMaskStrength;
            float _SnowAmount;
            half4 _SnowColor;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            float4 shadowCoord : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 GetWindDirectionWS()
        {
            float3 direction = _WindDirection.xyz;
            direction.y = 0.0;
            float lengthSq = dot(direction, direction);
            return lengthSq > 0.0001 ? direction * rsqrt(lengthSq) : float3(1, 0, 0);
        }

        float3 ApplyBranchWind(float3 positionOS, float4 vertexColor)
        {
            #if defined(_WIND_ON)
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 directionWS = GetWindDirectionWS();

                float2 windUV = positionWS.xz * _WindNoiseScale
                    - directionWS.xz * (_Time.y * _WindSpeed);

                float noise = SAMPLE_TEXTURE2D_LOD(
                    _WindNoiseTex, sampler_WindNoiseTex, windUV, 0
                ).r;
                noise = (noise * 2.0 - 1.0) * _WindNoiseStrength;

                float vertexMask = 1.0;
                #if defined(_USE_VERTEX_COLOR_WIND)
                    // 沿用原始 Shader 的顶点色约定：G 与 A 通道共同控制受风程度。
                    vertexMask = saturate(vertexColor.g + vertexColor.a) * _WindMaskStrength;
                #endif

                float displacement = noise * _WindStrength * vertexMask;
                float3 offsetWS = directionWS * displacement;
                positionOS += TransformWorldToObjectDir(offsetWS, true);
            #endif

            return positionOS;
        }

        Varyings BranchVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 deformedPositionOS = ApplyBranchWind(input.positionOS.xyz, input.color);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(deformedPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.shadowCoord = GetShadowCoord(positionInputs);
            return output;
        }

        half3 GetBranchAlbedo(float2 uv, half3 normalWS)
        {
            half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
            half3 albedo = baseSample.rgb;

            #if defined(_SNOW_ON)
                half snowMask = saturate(normalWS.y) * saturate(_SnowAmount);
                albedo = lerp(albedo, _SnowColor.rgb, snowMask);
            #endif

            return albedo;
        }

        half4 BranchFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            half3 albedo = GetBranchAlbedo(input.uv, normalWS);

            // 主方向光，保留原有实时阴影接收。
            Light mainLight = GetMainLight(input.shadowCoord);
            half mainNdotL = saturate(dot(normalWS, mainLight.direction));
            half3 ambient = SampleSH(normalWS);
            half3 lighting = ambient
                + mainLight.color * (mainNdotL * mainLight.shadowAttenuation);

            // 逐像素附加光源：支持场景中的 Point Light / Spot Light。
            #if defined(_ADDITIONAL_LIGHTS)
                uint additionalLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalLightCount; lightIndex++)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                    half additionalNdotL = saturate(dot(normalWS, additionalLight.direction));
                    lighting += additionalLight.color
                        * additionalNdotL
                        * additionalLight.distanceAttenuation
                        * additionalLight.shadowAttenuation;
                }
            #endif

            return half4(albedo * lighting, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BranchVertex
            #pragma fragment BranchFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile GPU_FRUSTUM_ON__
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _SNOW_ON
            #pragma shader_feature_local _USE_VERTEX_COLOR_WIND
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile GPU_FRUSTUM_ON__
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _USE_VERTEX_COLOR_WIND
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVertex(Attributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = ApplyBranchWind(input.positionOS.xyz, input.color);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS)
                );

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
