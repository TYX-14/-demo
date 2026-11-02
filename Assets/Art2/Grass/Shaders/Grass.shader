Shader "Toon/Grass"
{
    Properties
    {
		[Header(Shading)]
        _TopColor("草坪顶部颜色（Top Color）", Color) = (1,1,1,1)
		_BottomColor("草坪底部颜色（Bottom Color）", Color) = (1,1,1,1)
    	_TranslucentGain("Translucent Gain", Range(0,1)) = 0.5
		
    	_BendRotationRandom("随机弯曲程度(Bend Rotation Random)", Range(0, 1)) = 0.2
    	[Header(Blades)]
    	_BladeWidth("草根宽度（Blade Width）", Float) = 0.05
		_BladeWidthRandom("草根宽度随机种子（Blade Width Random）", Float) = 0.02
		_BladeHeight("草高度（Blade Height）", Float) = 0.5
		_BladeHeightRandom("草高度随机种子（Blade Height Random）", Float) = 0.3
    	
    	_TessellationUniform1("草坪密度（Tessellation Uniform）", Range(1, 64)) = 1
    	[Header(Wind)]
    	_WindDistortionMap("风力噪声图（Wind Distortion Map）", 2D) = "white" {}
		_WindFrequency("摆动频率（Wind Frequency）", Vector) = (0.05, 0.05, 0, 0)
    	
    	_WindStrength("风力强度（Wind Strength）", Float) = 1
    }
    
    SubShader
    {
		Cull Off

        Pass
        {
			Tags
			{
				"RenderType" = "Opaque"
				"LightMode" = "UniversalForward"
			}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry grassGeo
            #pragma hull hull
			#pragma domain domain
			#pragma target 4.6
            #pragma multi_compile_fwdbase
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #include "./lib/Grass.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 frag (grassGeometryOutput i,half facing : VFACE): SV_Target
			{
				float3 normal = facing > 0 ? i.normal : -i.normal;

				//float4 SHADOW_COORDS = TransformWorldToShadowCoord(i._ShadowCoord);//SHADOW_ATTENUATION(i);
				Light mainLight = GetMainLight(i._ShadowCoord);
				half shadow = mainLight.shadowAttenuation;
				float NdotL = saturate(saturate(dot(normal, _MainLightPosition.xyz)) + _TranslucentGain) * shadow;

				float3 ambient = SampleSH(float4(normal, 1).xyz);
				float4 lightIntensity = NdotL * _MainLightColor + float4(ambient, 1);
				float4 col = lerp(_BottomColor, _TopColor * lightIntensity, i.uv.y);

				return col;
			}
            ENDHLSL
        }
		Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
		/*Pass
		{
			Tags
			{
				"LightMode" = "ShadowCaster"
			}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma geometry grassGeo
			#pragma fragment grassShadowfrag
			#pragma hull hull
			#pragma domain domain
			#pragma target 4.6
			#pragma multi_compile_shadowcaster
			#include "Grass.hlsl"
			float4 grassShadowfrag(grassGeometryOutput i) : SV_Target
			{
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDHLSL
		}*/
    }
}