Shader "MyShared/IL3DN/Leaf_URP"
{
    Properties
    {
        [Header(基础外观)]
        [MainTexture] _MainTex ("叶片贴图（RGBA）", 2D) = "white" {}
        [MainColor] _Color ("叶片颜色", Color) = (1,1,1,1)
        _AlphaCutoff ("透明剪裁阈值", Range(0,1)) = 0.5

        [Header(风动效果)]
        [Toggle(_WIND_ON)] _Wind ("启用风动", Float) = 1
        _WindNoiseTex ("风噪贴图", 2D) = "gray" {}
        _WindDirection ("风向（世界坐标）", Vector) = (1,0,0,0)
        _WindSpeed ("风速", Range(0,5)) = 0.5
        _WindStrength ("风力强度", Range(0,2)) = 0.35
        _WindNoiseScale ("风噪缩放", Range(0.01,2)) = 0.15
        [Toggle(_USE_VERTEX_COLOR_WIND)] _UseVertexColorWind ("使用顶点色控制风动", Float) = 0
        _WindMaskStrength ("顶点色风动影响", Range(0,2)) = 1

        [Header(叶片摆动)]
        [Toggle(_WIGGLE_ON)] _Wiggle ("启用叶片摆动", Float) = 1
        _WiggleStrength ("摆动幅度", Range(0,2)) = 0.25
        _WiggleSpeed ("摆动速度", Range(0,5)) = 1

        [Header(颜色变化)]
        _ColorVariationTexture ("颜色变化贴图", 2D) = "white" {}
        _ColorVariationAmount ("颜色变化强度", Range(0,1)) = 0
        _ColorVariationScale ("颜色变化缩放", Range(0.01,100)) = 50

        [Header(积雪效果)]
        [Toggle(_SNOW_ON)] _Snow ("启用积雪", Float) = 0
        _SnowAmount ("积雪强度", Range(0,2)) = 0.5
        _SnowColor ("积雪颜色", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
        }

        Cull Off
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "VS_indirect.cginc"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_WindNoiseTex); SAMPLER(sampler_WindNoiseTex);
        TEXTURE2D(_ColorVariationTexture); SAMPLER(sampler_ColorVariationTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _WindDirection;
            float4 _SnowColor;
            float _AlphaCutoff;
            float _WindSpeed;
            float _WindStrength;
            float _WindNoiseScale;
            float _WindMaskStrength;
            float _WiggleStrength;
            float _WiggleSpeed;
            float _ColorVariationAmount;
            float _ColorVariationScale;
            float _SnowAmount;
        CBUFFER_END

        // Unity 的 ShadowCaster Pass 会为这两个变量提供光源方向/位置。
        // 放在公共 HLSLINCLUDE 中，避免其他 Pass 解析阴影函数时出现未声明标识符。
        float3 _LightDirection;
        float3 _LightPosition;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float2 GetWindUV(float3 positionWS)
        {
            float3 direction = _WindDirection.xyz;
            direction.y = 0;
            direction = dot(direction, direction) > 0.0001 ? normalize(direction) : float3(1,0,0);
            float2 flowDirection = direction.xz;
            return positionWS.xz * _WindNoiseScale
                 - flowDirection * (_Time.y * _WindSpeed * 0.1);
        }

        float SampleWindNoise(float3 positionWS)
        {
            return SAMPLE_TEXTURE2D_LOD(
                _WindNoiseTex,
                sampler_WindNoiseTex,
                GetWindUV(positionWS),
                0
            ).r;
        }

        float GetWindMask(float4 vertexColor)
        {
            #if defined(_USE_VERTEX_COLOR_WIND)
                return saturate(vertexColor.g + vertexColor.a) * _WindMaskStrength;
            #else
                return 1.0;
            #endif
        }

        float3 ApplyWind(float3 positionOS, float4 vertexColor)
        {
            #if defined(_WIND_ON)
                float3 positionWS = TransformObjectToWorld(positionOS);
                float noise = SampleWindNoise(positionWS);
                // 将噪声从 0..1 映射到 -1..1，使叶片围绕原位来回摆动。
                float windWave = noise * 2.0 - 1.0;
                float mask = GetWindMask(vertexColor);
                float3 direction = _WindDirection.xyz;
                direction.y = 0;
                direction = dot(direction, direction) > 0.0001 ? normalize(direction) : float3(1,0,0);
                positionWS += direction * (windWave * _WindStrength * mask);
                return TransformWorldToObject(positionWS);
            #else
                return positionOS;
            #endif
        }

        float2 ApplyLeafWiggle(float2 uv, float3 positionWS)
        {
            #if defined(_WIGGLE_ON)
                float noise = SampleWindNoise(positionWS);
                float angle = (noise * 2.0 - 1.0) * _WiggleStrength
                            * sin(_Time.y * _WiggleSpeed + positionWS.x + positionWS.z);
                float s = sin(angle);
                float c = cos(angle);
                float2 centered = uv - 0.5;
                return float2(centered.x * c - centered.y * s,
                              centered.x * s + centered.y * c) + 0.5;
            #else
                return uv;
            #endif
        }

        float4 SampleLeafTexture(float2 uv, float3 positionWS)
        {
            float2 animatedUV = ApplyLeafWiggle(uv, positionWS);
            float4 leaf = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, animatedUV);

            // Alpha 剪裁只由叶片贴图 Alpha 和材质阈值决定，不再依赖外部全局变量或积雪参数。
            clip(leaf.a - _AlphaCutoff);
            return leaf;
        }

        float3 ApplyColorVariation(float3 baseColor, float3 positionWS, float3 normalWS)
        {
            float scale = max(_ColorVariationScale, 0.0001);
            float3 weights = pow(abs(normalWS), 1.0);
            weights /= max(weights.x + weights.y + weights.z, 0.0001);

            float4 xSample = SAMPLE_TEXTURE2D(
                _ColorVariationTexture, sampler_ColorVariationTexture,
                positionWS.zy / scale * float2(sign(normalWS.x), 1));
            float4 ySample = SAMPLE_TEXTURE2D(
                _ColorVariationTexture, sampler_ColorVariationTexture,
                positionWS.xz / scale * float2(sign(normalWS.y), 1));
            float4 zSample = SAMPLE_TEXTURE2D(
                _ColorVariationTexture, sampler_ColorVariationTexture,
                positionWS.xy / scale * float2(-sign(normalWS.z), 1));

            float variation = dot(
                float3(xSample.r, ySample.r, zSample.r),
                weights
            );
            return lerp(baseColor, baseColor * (variation * 2.0), _ColorVariationAmount);
        }

        Varyings LeafVertex(Attributes IN)
        {
            Varyings OUT;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 positionOS = ApplyWind(IN.positionOS.xyz, IN.color);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

            OUT.positionHCS = positionInputs.positionCS;
            OUT.positionWS = positionInputs.positionWS;
            OUT.normalWS = normalize(normalInputs.normalWS);
            OUT.uv = IN.uv;
            return OUT;
        }

        half4 LeafFragment(Varyings IN) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN);

            float3 normalWS = normalize(IN.normalWS);
            float4 leaf = SampleLeafTexture(IN.uv, IN.positionWS);
            float3 albedo = leaf.rgb * _Color.rgb;
            albedo = ApplyColorVariation(albedo, IN.positionWS, normalWS);

            #if defined(_SNOW_ON)
                float snowMask = saturate(normalWS.y) * _SnowAmount;
                albedo = lerp(albedo, _SnowColor.rgb, snowMask);
            #endif

            Light mainLight = GetMainLight();
            float ndotl = saturate(dot(normalWS, mainLight.direction));
            float3 ambient = SampleSH(normalWS);
            float3 lighting = ambient + mainLight.color * ndotl;
            return half4(albedo * lighting, 1);
        }

        struct ShadowVaryings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        ShadowVaryings LeafShadowVertex(Attributes IN)
        {
            ShadowVaryings OUT;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 positionOS = ApplyWind(IN.positionOS.xyz, IN.color);
            float3 positionWS = TransformObjectToWorld(positionOS);
            float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

            positionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
            OUT.positionHCS = TransformWorldToHClip(positionWS);
            OUT.positionWS = positionWS;
            OUT.uv = IN.uv;
            return OUT;
        }

        half4 LeafShadowFragment(ShadowVaryings IN) : SV_TARGET
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            SampleLeafTexture(IN.uv, IN.positionWS);
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LeafVertex
            #pragma fragment LeafFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _WIGGLE_ON
            #pragma shader_feature_local _SNOW_ON
            #pragma shader_feature_local _USE_VERTEX_COLOR_WIND
            #pragma multi_compile GPU_FRUSTUM_ON__
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LeafShadowVertex
            #pragma fragment LeafShadowFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _WIGGLE_ON
            #pragma multi_compile GPU_FRUSTUM_ON__
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
