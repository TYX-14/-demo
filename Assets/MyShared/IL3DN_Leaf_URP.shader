Shader "MyShared/IL3DN/Leaf_URP"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _AlphaCutoff("Alpha Cutoff", Range(0,1)) = 0.5
        _MainTex("MainTex", 2D) = "white" {}
        [Toggle(_SNOW_ON)] _Snow("Snow", Float) = 1
        [Toggle(_WIND_ON)] _Wind("Wind", Float) = 1
        _WindStrenght("Wind Strenght", Range(0,1)) = 0.5
        [Toggle(_WIGGLE_ON)] _Wiggle("Wiggle", Float) = 1
        _WiggleStrenght("Wiggle Strenght", Range(0,1)) = 0.5
        _ColorVariationTexture("Color Variation Texture", 2D) = "white" {}
        _ColorVariationNoise("Color Variation Noise", Range(0,1)) = 0
        _ColorVariationScale("Color Variation Scale", Range(0,100)) = 50
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "VS_indirect.cginc"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_ColorVariationTexture); SAMPLER(sampler_ColorVariationTexture);
        TEXTURE2D(NoiseTextureFloat); SAMPLER(sampler_NoiseTextureFloat);

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _AlphaCutoff;
            float _WindStrenght;
            float _WiggleStrenght;
            float _ColorVariationNoise;
            float _ColorVariationScale;
        CBUFFER_END

        // These remain global parameters, matching the original IL3DN shader's interface.
        float3 WindDirection;
        float WindSpeedFloat;
        float WindTurbulenceFloat;
        float WindStrenghtFloat;
        float SnowLeavesFloat;
        float LeavesWiggleFloat;
        float AlphaCutoffFloat;

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
            float4 color : TEXCOORD3;
            float4 shadowCoord : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 RGBToHSV(float3 c)
        {
            float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
        }

        float3 HSVToRGB(float3 c)
        {
            float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
            return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
        }

        float4 SampleWindNoise(float3 positionWS, float vertexGreen, float vertexAlpha)
        {
            float2 windPlane = WindDirection.xz;
            float2 panner = _Time.y * (windPlane * WindSpeedFloat * 10.0) + positionWS.xy;
            float4 noise = SAMPLE_TEXTURE2D_LOD(
                NoiseTextureFloat, sampler_NoiseTextureFloat,
                (panner * WindTurbulenceFloat) / 10.0, 0);
            return noise * _WindStrenght * WindStrenghtFloat;
        }

        float3 ApplyLeafWindOS(float3 positionOS, float4 vertexColor)
        {
            #if defined(_WIND_ON)
                float3 positionWS = TransformObjectToWorld(positionOS);
                float4 noise = SampleWindNoise(positionWS, vertexColor.g, vertexColor.a);
                // The source shader multiplies WindDirection by the RGB channels of the noise.
                float3 windOffsetWS = WindDirection * noise.rgb * (vertexColor.a + vertexColor.g);
                float3 offsetOS = mul(GetWorldToObjectMatrix(), float4(windOffsetWS, 0.0)).xyz;
                positionOS += offsetOS;
            #endif
            return positionOS;
        }

        float2 AnimateLeafUV(float2 uv, float3 positionWS)
        {
            #if defined(_WIGGLE_ON)
                float4 noise = SampleWindNoise(positionWS, 0, 0);
                float angle = SAMPLE_TEXTURE2D(
                    NoiseTextureFloat, sampler_NoiseTextureFloat, noise.rg).r
                    * LeavesWiggleFloat * _WiggleStrenght;
                float s = sin(angle);
                float c = cos(angle);
                float2 centered = uv - 0.5;
                uv = float2(centered.x * c - centered.y * s,
                             centered.x * s + centered.y * c) + 0.5;
            #endif
            return uv;
        }

        float4 SampleLeafAlbedo(float2 uv, float3 positionWS, float3 normalWS)
        {
            uv = AnimateLeafUV(uv, positionWS);
            float4 leafTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            float alphaDivider = 1.0;
            #if defined(_SNOW_ON)
                alphaDivider = max(AlphaCutoffFloat, 0.0001);
            #endif
            clip(leafTex.a / alphaDivider - _AlphaCutoff);

            float3 hsv = RGBToHSV(_Color.rgb);
            float tiling = rcp(max(_ColorVariationScale, 0.0001));
            float3 weights = pow(abs(normalWS), 1.0);
            weights /= weights.x + weights.y + weights.z + 1e-5;
            float3 signs = sign(normalWS);

            float4 triX = SAMPLE_TEXTURE2D(_ColorVariationTexture, sampler_ColorVariationTexture,
                positionWS.zy * tiling * float2(signs.x, 1.0));
            float4 triY = SAMPLE_TEXTURE2D(_ColorVariationTexture, sampler_ColorVariationTexture,
                positionWS.xz * tiling * float2(signs.y, 1.0));
            float4 triZ = SAMPLE_TEXTURE2D(_ColorVariationTexture, sampler_ColorVariationTexture,
                positionWS.xy * tiling * float2(-signs.z, 1.0));
            float4 triplanar = triX * weights.x + triY * weights.y + triZ * weights.z;

            float4 normalizedVariation = normalize(triplanar * hsv.x);
            float3 variation = HSVToRGB(float3(normalizedVariation.x, hsv.y, hsv.z));
            float4 src = float4(variation, 0.0);
            float4 dest = _Color * leafTex;
            float4 softBlend = lerp(dest,
                2.0 * dest * src + dest * dest * (1.0 - 2.0 * src),
                _ColorVariationNoise);

            float3 snow = 0.0;
            #if defined(_SNOW_ON)
                snow = saturate(pow(normalWS.y, 1.0)) * SnowLeavesFloat;
            #endif
            return float4(saturate(snow + saturate(softBlend.rgb)), 1.0);
        }

        Varyings LeafVertex(Attributes IN)
        {
            Varyings OUT;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 positionOS = ApplyLeafWindOS(IN.positionOS.xyz, IN.color);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

            OUT.positionHCS = positionInputs.positionCS;
            OUT.positionWS = positionInputs.positionWS;
            OUT.normalWS = normalize(normalInputs.normalWS);
            OUT.uv = IN.uv;
            OUT.color = IN.color;
            OUT.shadowCoord = GetShadowCoord(positionInputs);
            return OUT;
        }

        half4 LeafFragment(Varyings IN) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            float3 normalWS = normalize(IN.normalWS);
            float4 albedo = SampleLeafAlbedo(IN.uv, IN.positionWS, normalWS);

            Light mainLight = GetMainLight(IN.shadowCoord);
            float ndotl = saturate(dot(normalWS, mainLight.direction));
            float3 ambient = SampleSH(normalWS);
            float3 lighting = ambient + mainLight.color * ndotl * mainLight.shadowAttenuation;
            return half4(albedo.rgb * lighting, 1.0);
        }

        struct ShadowVaryings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        ShadowVaryings LeafShadowVertex(Attributes IN)
        {
            ShadowVaryings OUT;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 positionOS = ApplyLeafWindOS(IN.positionOS.xyz, IN.color);
            float3 positionWS = TransformObjectToWorld(positionOS);
            float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _MainLightPosition.xyz;
            #endif

            positionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
            OUT.positionHCS = TransformWorldToHClip(positionWS);
            OUT.uv = IN.uv;
            OUT.positionWS = positionWS;
            OUT.normalWS = normalize(normalWS);
            return OUT;
        }

        half4 LeafShadowFragment(ShadowVaryings IN) : SV_TARGET
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            float3 normalWS = normalize(IN.normalWS);
            SampleLeafAlbedo(IN.uv, IN.positionWS, normalWS);
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
            #pragma multi_compile __ _WIND_ON
            #pragma multi_compile __ _SNOW_ON
            #pragma multi_compile __ _WIGGLE_ON
            #pragma multi_compile GPU_FRUSTUM_ON__
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
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
            #pragma multi_compile __ _WIND_ON
            #pragma multi_compile __ _SNOW_ON
            #pragma multi_compile __ _WIGGLE_ON
            #pragma multi_compile GPU_FRUSTUM_ON__
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}