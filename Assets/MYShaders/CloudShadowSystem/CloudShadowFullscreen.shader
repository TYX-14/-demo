Shader "Hidden/CloudShadow/Fullscreen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CloudShadow"
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
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float CloudFbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amplitude;
                    p = p * 2.03 + float2(17.1, -9.2);
                    amplitude *= 0.5;
                }

                return sum / 0.9375;
            }

            float SampleCloudMask(float2 worldXZ)
            {
                float2 direction = _CloudShadowDirection.xy;
                direction = dot(direction, direction) > 0.0001
                    ? normalize(direction)
                    : float2(1.0, 0.0);

                float2 uv = worldXZ * max(_CloudShadowScale, 0.0001);
                uv -= direction * (_Time.y * _CloudShadowSpeed);

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

                float2 projectedCloudXZ = positionWS.xz
                    + heightToCloud * sunDirection.xz / max(sunDirection.y, 0.05);

                float cloudMask = SampleCloudMask(projectedCloudXZ);
                float shadowAmount = saturate(cloudMask * _CloudShadowStrength);
                float3 multiplier = lerp(float3(1, 1, 1), _CloudShadowTint.rgb, shadowAmount);

                sceneColor.rgb *= multiplier;
                return sceneColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}