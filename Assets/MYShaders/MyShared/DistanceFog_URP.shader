Shader "MyShared/DistanceFog_URP"
{
    Properties
    {
        _NearFogColor ("近景雾颜色", Color) = (0.55, 0.64, 0.64, 1)
        _FarFogColor ("远景雾颜色", Color) = (0.68, 0.74, 0.72, 1)
        _FogStartDistance ("雾开始距离", Range(0, 200)) = 15
        _FogDensity ("雾密度", Range(0.001, 0.1)) = 0.035
        _FogOpacity ("最大雾量", Range(0, 1)) = 0.85
        _ExcludeSkybox ("不影响天空盒", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "DistanceFog"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _NearFogColor;
                half4 _FarFogColor;
                float _FogStartDistance;
                float _FogDensity;
                float _FogOpacity;
                float _ExcludeSkybox;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // Full Screen Pass Feature 的 Color Requirement 会提供当前画面。
                half4 sceneColor = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv
                );

                // 读取深度。天空盒区域使用相机清屏深度值识别。
                float rawDepth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 0.00001;
                    float deviceDepth = rawDepth;
                #else
                    bool isSky = rawDepth >= 0.99999;
                    float deviceDepth = lerp(
                        UNITY_NEAR_CLIP_VALUE,
                        1.0,
                        rawDepth
                    );
                #endif

                // 将屏幕 UV + 深度还原为世界坐标，计算真实视线距离。
                float3 worldPosition = ComputeWorldSpacePosition(
                    uv,
                    deviceDepth,
                    UNITY_MATRIX_I_VP
                );

                float distanceToCamera = distance(
                    worldPosition,
                    _WorldSpaceCameraPos
                );

                float fogDistance = max(
                    distanceToCamera - _FogStartDistance,
                    0.0
                );

                // 指数型雾：距离越远，雾量增长越明显。
                float fogAmount = 1.0 - exp(
                    -fogDistance * _FogDensity
                );

                fogAmount = saturate(fogAmount) * saturate(_FogOpacity);

                // 随雾量从近景雾色过渡到远景雾色。
                half3 fogColor = lerp(
                    _NearFogColor.rgb,
                    _FarFogColor.rgb,
                    saturate(fogAmount / max(_FogOpacity, 0.0001))
                );

                half3 finalColor = lerp(
                    sceneColor.rgb,
                    fogColor,
                    fogAmount
                );

                // 可选：让天空盒保持原样，避免无穷远深度被当作浓雾。
                if (_ExcludeSkybox > 0.5 && isSky)
                {
                    finalColor = sceneColor.rgb;
                }

                return half4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
