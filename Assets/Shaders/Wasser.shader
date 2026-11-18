// Wasser fuer die Grafikstufe "Hoch": von oben dunkel und durchsichtig, flach angesehen hell
// und deckend (Fresnel), glaenzend. Surface Shader der Built-in-Pipeline, Rueckfall auf
// Transparent/Diffuse, falls eine Grafikkarte ihn nicht kann.
Shader "Shootout/Wasser"
{
	Properties
	{
		_Farbe ("Farbe flach", Color) = (0.22, 0.45, 0.52, 0.9)
		_Tiefe ("Farbe von oben", Color) = (0.03, 0.14, 0.18, 0.6)
		_Glanz ("Glanz", Range(0, 1)) = 0.95
		_Fresnel ("Fresnel-Staerke", Range(0.5, 8)) = 3
	}
	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard alpha:fade
		#pragma target 3.0

		fixed4 _Farbe;
		fixed4 _Tiefe;
		half _Glanz;
		half _Fresnel;

		struct Input
		{
			float3 worldPos;
			float3 worldNormal;
		};

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			float3 blick = normalize(_WorldSpaceCameraPos - IN.worldPos);
			float3 normale = normalize(IN.worldNormal);
			half f = pow(1.0 - saturate(dot(blick, normale)), _Fresnel);
			fixed4 farbe = lerp(_Tiefe, _Farbe, f);
			o.Albedo = farbe.rgb;
			o.Smoothness = _Glanz;
			o.Metallic = 0.0;
			o.Alpha = farbe.a;
		}
		ENDCG
	}
	Fallback "Transparent/Diffuse"
}
