Shader "MyShared/VolumetricLightBeam"
{
    Properties
    {
        _BeamColor ("光柱颜色", Color) = (1, 0.86, 0.58, 1)
        _Intensity ("散射强度", Range(0, 5)) = 1.4
        _Density ("体积密度", Range(0.01, 1)) = 0.35
        _EdgeSoftness ("边缘柔和", Range(0.05, 1)) = 0.65
        _NoiseStrength ("噪声强度", Range(0, 1)) = 0.12
        _NoiseScale ("噪声尺度", Range(0.01, 10)) = 1.8
        _StepCount ("采样步数", Range(4, 48)) = 24
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "VolumetricBeam"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
                float _Intensity;
                float _Density;
                float _EdgeSoftness;
                float _NoiseStrength;
                float _NoiseScale;
                float _StepCount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            float HashNoise(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // URP 的视线函数会同时处理透视与正交相机。
                float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                // 从代理立方体朝向摄像机的一侧开始，沿摄像机视线进入场景的方向积分。
                // 这样代理面的硬件深度测试可以先挡住位于光束前方的场景物体。
                float3 rayIntoSceneOS = normalize(TransformWorldToObjectDir(-viewDirectionWS));

                float3 origin = input.positionOS;
                float3 direction = rayIntoSceneOS;

                // 单位圆柱：轴向 Y，长度 1，半径 0.5。
                float a = direction.x * direction.x + direction.z * direction.z;
                float b = 2.0 * (origin.x * direction.x + origin.z * direction.z);
                float c = origin.x * origin.x + origin.z * origin.z - 0.25;
                float sideNear = -1e6;
                float sideFar = 1e6;

                if (a > 1e-6)
                {
                    float discriminant = b * b - 4.0 * a * c;
                    if (discriminant <= 0.0) discard;
                    float root = sqrt(discriminant);
                    sideNear = (-b - root) / (2.0 * a);
                    sideFar = (-b + root) / (2.0 * a);
                }
                else if (c > 0.0)
                {
                    discard;
                }

                float capNear = -1e6;
                float capFar = 1e6;
                if (abs(direction.y) > 1e-6)
                {
                    float capA = (-0.5 - origin.y) / direction.y;
                    float capB = ( 0.5 - origin.y) / direction.y;
                    capNear = min(capA, capB);
                    capFar = max(capA, capB);
                }
                else if (abs(origin.y) > 0.5)
                {
                    discard;
                }

                float rayStart = max(max(sideNear, capNear), 0.0);
                float rayEnd = min(sideFar, capFar);
                if (rayEnd <= rayStart) discard;

                // 用场景深度截断光柱，避免光束穿过不透明建筑、地形以及写入深度的 Sprite。
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 0.00001;
                    float deviceDepth = rawDepth;
                #else
                    bool isSky = rawDepth >= 0.99999;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                if (!isSky)
                {
                    float3 sceneWS = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
                    float3 sceneOS = TransformWorldToObject(sceneWS);
                    float sceneDistance = dot(sceneOS - origin, direction);
                    if (sceneDistance > 0.0)
                        rayEnd = min(rayEnd, sceneDistance);
                }

                if (rayEnd <= rayStart) discard;

                int steps = clamp((int)_StepCount, 4, 48);
                float stepLength = (rayEnd - rayStart) / steps;
                float3 accumulated = 0.0;

                [loop]
                for (int i = 0; i < 48; i++)
                {
                    if (i >= steps) break;

                    float t = rayStart + (i + 0.5) * stepLength;
                    float3 sampleOS = origin + direction * t;
                    float radialDistance = length(sampleOS.xz) * 2.0;
                    float radialFade = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, radialDistance);
                    float endFade = smoothstep(-0.5, -0.35, sampleOS.y) *
                                    (1.0 - smoothstep(0.35, 0.5, sampleOS.y));

                    float3 sampleWS = TransformObjectToWorld(sampleOS);
                    float noise = HashNoise(sampleWS * _NoiseScale + _Time.y * 0.025);
                    float noiseFactor = lerp(1.0, lerp(0.65, 1.25, noise), _NoiseStrength);

                    float3 nextWS = TransformObjectToWorld(origin + direction * (t + stepLength));
                    float worldStep = distance(sampleWS, nextWS);
                    float scattering = _Density * radialFade * endFade * noiseFactor * worldStep;
                    accumulated += _BeamColor.rgb * scattering;
                }

                accumulated *= _Intensity;
                return half4(accumulated, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
