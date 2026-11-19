// Blaetter, Buesche und Palmwedel der Grafikstufe "Ultra": Textur mit ausgeschnittenen Blaettern
// (Alpha-Test), beidseitig, im Wind schwankend. Die Biegung waechst mit der Hoehe im Objekt,
// Stamm und Wurzel bleiben ruhig. Farbe der Punkte (Vertex-Farbe) dunkelt innere Blaetter ab.
Shader "Shootout/Laub"
{
	Properties
	{
		_MainTex ("Textur", 2D) = "white" {}
		_Color ("Farbe", Color) = (1, 1, 1, 1)
		_Cutoff ("Schnittkante", Range(0, 1)) = 0.45
		_Wind ("Wind", Range(0, 2)) = 0.6
		_Glanz ("Glanz", Range(0, 1)) = 0.15
	}
	SubShader
	{
		Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
		LOD 200
		Cull Off

		CGPROGRAM
		#pragma surface surf Standard vertex:vert alphatest:_Cutoff addshadow
		#pragma target 3.0

		sampler2D _MainTex;
		fixed4 _Color;
		half _Wind;
		half _Glanz;

		struct Input
		{
			float2 uv_MainTex;
			float4 color : COLOR;
		};

		void vert (inout appdata_full v)
		{
			float3 welt = mul(unity_ObjectToWorld, v.vertex).xyz;
			float t = _Time.y;
			float hoehe = saturate(v.vertex.y * 0.18);
			float schwanken = sin(t * 1.1 + welt.x * 0.21 + welt.z * 0.17) + 0.45 * sin(t * 2.7 + welt.x * 0.6 + welt.y * 0.4);
			float flattern = sin(t * 7.0 + dot(welt, float3(2.3, 1.9, 3.1))) * 0.035;
			float3 versatz = float3(schwanken * 0.12 * hoehe + flattern, flattern * 0.6, schwanken * 0.07 * hoehe - flattern) * _Wind;
			v.vertex.xyz += mul((float3x3)unity_WorldToObject, versatz);
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 farbe = tex2D(_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = farbe.rgb * IN.color.rgb;
			o.Smoothness = _Glanz;
			o.Metallic = 0.0;
			o.Alpha = farbe.a;
		}
		ENDCG
	}
	Fallback "Legacy Shaders/Transparent/Cutout/Diffuse"
}
