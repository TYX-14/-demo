Shader "Hidden/CloudShadow/Fullscreen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CloudShadowFullscreen"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _CloudShadowEnabled;
            float _CloudShadowStrength;
            float _CloudShadowScale;
            float _CloudShadowCoverage;
            float _CloudShadowSoftness;
            float _CloudShadowDetail;
            float _CloudShadowSpeed;
            float4 _CloudShadowDirection;
            float _CloudShadowHeight;
            float4 _CloudShadowSunDirection;
            float4 _CloudShadowTint;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(
                    lerp(a, b, local.x),
                    lerp(c, d, local.x),
                    local.y
                );
            }

            float CloudFbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int octave = 0; octave < 4; octave++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = p * 2.03 + float2(17.1, -9.2);
                    amplitude *= 0.5;
                }

                return value / 0.9375;
            }

            float SampleCloudMask(float2 worldXZ)
            {
                float2 direction = _CloudShadowDirection.xy;
                direction = dot(direction, direction) > 0.0001
                    ? normalize(direction)
                    : float2(1.0, 0.0);

                float2 uv = worldXZ * max(_CloudShadowScale, 0.0001);
                uv -= direction * (_Time.y * _CloudShadowSpeed);

                // 域扭曲打散规则噪声，让云块边缘更自然。
                float2 warp;
                warp.x = CloudFbm(uv + float2(3.7, 11.2));
                warp.y = CloudFbm(uv + float2(17.1, 5.3));
                warp = (warp - 0.5) * 1.8;

                float broad = CloudFbm(uv + warp);
                float detail = CloudFbm(uv * 2.15 + warp * 0.65 + float2(13.7, 5.2));
                float shape = lerp(
                    broad,
                    broad * 0.72 + detail * 0.28,
                    saturate(_CloudShadowDetail)
                );

                float softness = max(_CloudShadowSoftness, 0.001);
                return 1.0 - smoothstep(
                    _CloudShadowCoverage - softness,
                    _CloudShadowCoverage + softness,
                    shape
                );
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 sceneColor = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    input.texcoord
                );

                if (_CloudShadowEnabled < 0.5 || _CloudShadowStrength <= 0.001)
                    return sceneColor;

                float rawDepth = SampleSceneDepth(input.texcoord);

                // 天空没有有效几何深度，不参与云影计算。
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.000001)
                        return sceneColor;
                #else
                    if (rawDepth >= 0.999999)
                        return sceneColor;
                #endif

                float3 positionWS = ComputeWorldSpacePosition(
                    input.texcoord,
                    rawDepth,
                    UNITY_MATRIX_I_VP
                );

                float3 sunDirection = normalize(_CloudShadowSunDirection.xyz);
                if (sunDirection.y <= 0.05)
                    return sceneColor;

                float heightToCloud = _CloudShadowHeight - positionWS.y;
                if (heightToCloud <= 0.0)
                    return sceneColor;

                // 沿太阳方向把表面位置投影到云层高度。
                // 这样高处的树冠与地面会产生符合太阳方向的阴影偏移。
                float2 projectedCloudXZ = positionWS.xz
                    + heightToCloud * sunDirection.xz / max(sunDirection.y, 0.05);

                float cloudMask = SampleCloudMask(projectedCloudXZ);
                float shadowAmount = saturate(cloudMask * _CloudShadowStrength);

                // 云影以乘法压暗为主，保留原材质颜色与色彩关系。
                float3 shadowMultiplier = lerp(
                    float3(1.0, 1.0, 1.0),
                    _CloudShadowTint.rgb,
                    shadowAmount
                );

                sceneColor.rgb *= shadowMultiplier;
                return sceneColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
