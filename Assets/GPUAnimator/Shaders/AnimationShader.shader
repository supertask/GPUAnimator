Shader "Unlit/AnimationShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_RimColor("_RimColor", Color) = (1,1,1,1)
		_SpecColor("SpecColor", Color) = (1.0, 1.0, 1.0, 1.0)
		_Shininess("Shininess", Float) = 10
		_RimPower("Rim Power", Range(0.1, 10.0)) = 3.0

		PositionAnimTexture ("Position Anim Texture", 2D) = "white" {}
		PositionAnimTexture_Next ("Position Anim Texture Next", 2D) = "white" {}
		NormalAnimTexture ("Normal Anim Texture", 2D) = "white" {}
		NormalAnimTexture_Next ("Normal Anim Texture Next", 2D) = "white" {}
	}
	SubShader
	{
        Tags { "RenderType"="Transparent" "Queue" = "Transparent" }
		LOD 100
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5
			#pragma enable_d3d11_debug_symbols


			#include "UnityCG.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"

			float _NormalizedAnimTime;
			float _NormalizedAnimTime_Next;

			float4 _TexelSize;
			float4 _TexelSize_Next;
			float _TransitionTime;

			Texture2D<float3> PositionAnimTexture;
			Texture2D<float3> PositionAnimTexture_Next;
			Texture2D<float3> NormalAnimTexture;
			Texture2D<float3> NormalAnimTexture_Next;

			
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				uint vid : SV_VertexID;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float3 normal : Normal;
				float3 posWorld : TEXCOORD1;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _RimColor;
			float _Shininess;
			float _RimPower;


			v2f vert (appdata v)
			{
				v2f o;

				int2 texCoord = int2(v.vid, _NormalizedAnimTime * _TexelSize.w);
				float3 vertex = PositionAnimTexture.Load(int3(texCoord, 0)).xyz;
				float3 normal = NormalAnimTexture.Load(int3(texCoord, 0)).xyz;

				int2 texCoordNext = int2(v.vid, _NormalizedAnimTime_Next * _TexelSize_Next.w);
				float3 nextVertex = PositionAnimTexture_Next.Load(int3(texCoordNext, 0)).xyz;
				float3 nextNormal = NormalAnimTexture_Next.Load(int3(texCoordNext, 0)).xyz;
				vertex = lerp(vertex, nextVertex, _TransitionTime);
				normal = lerp(normal, nextNormal, _TransitionTime);

				o.vertex = UnityObjectToClipPos(vertex);
				o.posWorld = mul(unity_ObjectToWorld, vertex);
				o.normal = UnityObjectToWorldNormal(normal);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{

				fixed4 color = tex2D(_MainTex, i.uv);
				float3 normalDirection = i.normal;
				float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
				float3 lightDirection = -normalize(_WorldSpaceLightPos0.xyz);
				float3 lightColor = 1;// _LightColor0.rgb;
									  /// Lighting:
				float attenuation = 1.0;// LIGHT_ATTENUATION(i);
				float3 attenColor = attenuation * lightColor;

				/// Diffuse:
				float3 diffuseReflection = attenuation * lightColor * saturate(dot(normalDirection, lightDirection));

				/// Specular:
				float3 specularReflection = attenuation * _SpecColor * lightColor * saturate(dot(normalDirection, lightDirection)) * pow(saturate(dot(reflect(-lightDirection, normalDirection), viewDirection)), _Shininess);

				/// RimLight:
				float3 rimColor = _RimColor;
				half rim = 1.0 - saturate(dot(normalize(viewDirection), normalDirection));
				rim = pow(rim, _RimPower);
				float3 rimLighting = attenuation * lightColor * saturate(dot(normalDirection, lightDirection)) * rim * rimColor.rgb;

				/// FinalLight:
				float3 lightFinal = rimLighting + diffuseReflection + specularReflection + UNITY_LIGHTMODEL_AMBIENT.xyz;

				/// Final Color:
				float3 finalColor = (1 - color) * 0.65;
				fixed4 finalRGBA = fixed4(finalColor, color.a);

				return finalRGBA;
			}
			ENDCG
		}
	}
}
