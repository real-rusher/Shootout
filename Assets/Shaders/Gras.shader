// Grashalme der Grafikstufe "Ultra". Einzelne Halme als Netz (keine Bildflaechen), Farbe aus
// den Punkten (Wurzel dunkel, Spitze hell), Wind ueber die Hoehe im Halm (uv.y). Wird mit
// Graphics.DrawMeshInstanced in Mengen gezeichnet (GrasFeld) und blendet in der Ferne aus.
Shader "Shootout/Gras"
{
	Properties
	{
		_Color ("Farbe", Color) = (1, 1, 1, 1)
		_Wind ("Wind", Range(0, 2)) = 0.8
		_Weite ("Sichtweite", Float) = 70
		_Glanz ("Glanz", Range(0, 1)) = 0.2
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200
		Cull Off

		CGPROGRAM
		#pragma surface surf Standard vertex:vert addshadow
		#pragma multi_compile_instancing
		#pragma target 3.0

		fixed4 _Color;
		half _Wind;
		float _Weite;
		half _Glanz;

		struct Input
		{
			float4 color : COLOR;
		};

		void vert (inout appdata_full v)
		{
			float3 welt = mul(unity_ObjectToWorld, v.vertex).xyz;
			float3 fuss = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
			// In der Ferne schrumpfen die Halme in den Boden, statt ploetzlich zu verschwinden.
			float abstand = distance(fuss, _WorldSpaceCameraPos);
			float sichtbar = saturate((_Weite - abstand) / (_Weite * 0.25));
			v.vertex.xyz *= sichtbar;
			welt = mul(unity_ObjectToWorld, v.vertex).xyz;
			float t = _Time.y;
			float h = v.texcoord.y * v.texcoord.y;
			float wind = sin(t * 1.6 + fuss.x * 0.35 + fuss.z * 0.3) * 0.7 + sin(t * 3.3 + fuss.x * 1.3 - fuss.z) * 0.3;
			float3 versatz = float3(wind * 0.18, -abs(wind) * 0.04, wind * 0.1) * h * _Wind;
			v.vertex.xyz += mul((float3x3)unity_WorldToObject, versatz);
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = IN.color.rgb * _Color.rgb;
			o.Smoothness = _Glanz;
			o.Metallic = 0.0;
			o.Alpha = 1.0;
		}
		ENDCG
	}
	Fallback "Diffuse"
}
