//ddInvertLin shader: Daniel DeEntremont
//Only works correctly in Linear Color space
//Apply this shader to a mesh and watch all pixels behind the mesh become inverted!
Shader "ddShaders/ddInvertLin" {
	Properties
	{
		_Color ("Tint Color", Color) = (1,1,1,1)
	}

	SubShader
	{
		Tags { "Queue"="Transparent" }

		Pass
		{
			ZWrite On
			ColorMask 0
		}

		Pass
		{
			Blend OneMinusDstColor OneMinusSrcAlpha
			BlendOp Add

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			fixed4 _Color;

			struct appdata { float4 vertex : POSITION; };
			struct v2f     { float4 pos    : SV_POSITION; };

			v2f vert(appdata v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				return _Color;
			}
			ENDCG
		}

		// Gamma-correction passes for Linear color space:
		// multiply destination by itself to simulate a de-gamma.
		Pass
		{
			Blend Zero DstColor
			BlendOp Add

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata { float4 vertex : POSITION; };
			struct v2f     { float4 pos    : SV_POSITION; };

			v2f vert(appdata v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				return fixed4(1,1,1,1);
			}
			ENDCG
		}

		Pass
		{
			Blend Zero DstColor
			BlendOp Add

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata { float4 vertex : POSITION; };
			struct v2f     { float4 pos    : SV_POSITION; };

			v2f vert(appdata v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				return fixed4(1,1,1,1);
			}
			ENDCG
		}
	}
}
