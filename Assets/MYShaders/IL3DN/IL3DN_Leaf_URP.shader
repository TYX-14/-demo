Shader "MYShaders/IL3DN/Leaf_URP"
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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "VS_indirect.cginc"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_ColorVariationTexture); SAMPLER(sampler_ColorVariationTexture);
        TEXTURE2D(NoiseTextureFloat); SAMPLER(sampler_NoiseTextureFloat);

        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float _AlphaCutoff, _WindStrenght, _WiggleStrenght;
        float _ColorVariationNoise, _ColorVariationScale;
        CBUFFER_END

        float3 WindDirection;
        float WindSpeedFloat, WindTurbulenceFloat, WindStrenghtFloat;
        float SnowLeavesFloat, LeavesWiggleFloat, AlphaCutoffFloat;

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
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 RGBToHSV(float3 c)
        {
            float4 K = float4(0.0,-1.0/3.0,2.0/3.0,-1.0);
            float4 p = lerp(float4(c.bg,K.wz),float4(c.gb,K.xy),step(c.b,c.g));
            float4 q = lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
            float d=q.x-min(q.w,q.y);
            return float3(abs(q.z+(q.w-q.y)/(6.0*d+1e-10)),d/(q.x+1e-10),q.x);
        }

        float3 HSVToRGB(float3 c)
        {
            float4 K=float4(1.0,2.0/3.0,1.0/3.0,3.0);
            float3 p=abs(frac(c.xxx+K.xyz)*6.0-K.www);
            return c.z*lerp(K.xxx,saturate(p-K.xxx),c.y);
        }

        float4 TriplanarLeaf(TEXTURE2D_PARAM(tex,samp), float3 posWS, float3 nWS, float tiling)
        {
            float3 weights=pow(abs(nWS),1.0);
            weights/=weights.x+weights.y+weights.z+1e-5;
            float3 signN=sign(nWS);
            float4 x=SAMPLE_TEXTURE2D(tex,samp,posWS.zy*tiling*float2(signN.x,1));
            float4 y=SAMPLE_TEXTURE2D(tex,samp,posWS.xz*tiling*float2(signN.y,1));
            float4 z=SAMPLE_TEXTURE2D(tex,samp,posWS.xy*tiling*float2(-signN.z,1));
            return x*weights.x+y*weights.y+z*weights.z;
        }

        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_TRANSFER_INSTANCE_ID(IN,OUT);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            float3 posOS=IN.positionOS.xyz;
            float3 posWS=TransformObjectToWorld(posOS);
            float3 windDir=WindDirection;
            float2 windPlane=windDir.xz;
            float2 panner=_Time.y*(windPlane*WindSpeedFloat*10.0)+posWS.xy;
            float4 noise=SAMPLE_TEXTURE2D_LOD(NoiseTextureFloat,sampler_NoiseTextureFloat,
                (panner*WindTurbulenceFloat)/10.0,0)*_WindStrenght*WindStrenghtFloat;

            #if defined(_WIND_ON)
            float4 offsetOS=mul(UNITY_MATRIX_I_M,float4(windDir,0.0))*
                ((IN.color.a*noise)+(noise*IN.color.g));
            posOS+=offsetOS.xyz;
            #endif

            OUT.positionHCS=TransformObjectToH clip(posOS);
            OUT.positionWS=TransformObjectToWorld(posOS);
            OUT.normalWS=TransformObjectToWorldNormal(IN.normalOS);
            OUT.uv=IN.uv;
            OUT.color=IN.color;
            return OUT;
        }

        half4 frag(Varyings IN) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            float3 nWS=normalize(IN.normalWS);
            float2 uv=IN.uv;
            float3 p=IN.positionWS;

            float2 panner=_Time.y*((WindDirection.xz)*WindSpeedFloat*10.0)+p.xy;
            float4 noise=SAMPLE_TEXTURE2D(NoiseTextureFloat,sampler_NoiseTextureFloat,
                (panner*WindTurbulenceFloat)/10.0)*_WindStrenght*WindStrenghtFloat;

            #if defined(_WIGGLE_ON)
            float angle=SAMPLE_TEXTURE2D(NoiseTextureFloat,sampler_NoiseTextureFloat,noise.rg).r*
                LeavesWiggleFloat*_WiggleStrenght;
            float s=sin(angle), c=cos(angle);
            float2 centered=uv-0.5;
            uv=float2(centered.x*c-centered.y*s,centered.x*s+centered.y*c)+0.5;
            #endif

            float4 texel=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
            float cutoff=1.0;
            #if defined(_SNOW_ON)
            cutoff=AlphaCutoffFloat;
            #endif
            clip(texel.a/cutoff-_AlphaCutoff);

            float3 hsv=RGBToHSV(_Color.rgb);
            float4 tri=TriplanarLeaf(TEXTURE2D_ARGS(_ColorVariationTexture,sampler_ColorVariationTexture),
                p,nWS,1.0/max(_ColorVariationScale,0.0001));
            float4 normalizedVariation=normalize(tri*hsv.x);
            float3 variation=HSVToRGB(float3(normalizedVariation.x,hsv.y,hsv.z));
            float4 src=float4(variation,0);
            float4 dest=_Color*texel;
            float4 softBlend=lerp(dest,2.0*dest*src+dest*dest*(1.0-2.0*src),_ColorVariationNoise);
            float3 snow=0;
            #if defined(_SNOW_ON)
            snow=saturate(pow(nWS.y,1.0))*SnowLeavesFloat;
            #endif
            return half4(saturate(snow+saturate(softBlend.rgb)),1);
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile __ _WIND_ON
            #pragma multi_compile __ _SNOW_ON
            #pragma multi_compile __ _WIGGLE_ON
            #pragma multi_compile GPU_FRUSTUM_ON__
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}