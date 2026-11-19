// Textur nach Weltkoordinaten statt nach UV (triplanar): Grosse und kleine Kloetze bekommen
// dieselbe Kachelgroesse, ohne gestreckte Muster. Fuer die selbst gebauten Maps.
Shader "Shootout/Weltraster"
{
	Properties
	{
		_MainTex ("Textur", 2D) = "white" {}
		_Color ("Farbe", Color) = (1, 1, 1, 1)
		_Skala ("Kacheln pro Meter", Float) = 0.25
		_Glanz ("Glanz", Range(0, 1)) = 0.1
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0

		sampler2D _MainTex;
		fixed4 _Color;
		half _Skala;
		half _Glanz;
		// Global (Grafik.cs): 1 in der Stufe "Ultra". Dann eine zweite, feinere Lage derselben
		// Textur fuer Details aus der Naehe.
		float _ShootoutUltra;

		struct Input
		{
			float3 worldPos;
			float3 worldNormal;
		};

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			float3 gewicht = abs(normalize(IN.worldNormal));
			gewicht = pow(gewicht, 4.0);
			gewicht /= (gewicht.x + gewicht.y + gewicht.z);
			float3 p = IN.worldPos * _Skala;
			fixed4 farbe = tex2D(_MainTex, p.zy) * gewicht.x + tex2D(_MainTex, p.xz) * gewicht.y + tex2D(_MainTex, p.xy) * gewicht.z;
			float3 f = p * 5.3;
			fixed3 fein = (tex2D(_MainTex, f.zy) * gewicht.x + tex2D(_MainTex, f.xz) * gewicht.y + tex2D(_MainTex, f.xy) * gewicht.z).rgb;
			half detail = lerp(1.0, 0.6 + 0.8 * dot(fein, half3(0.333, 0.333, 0.333)), _ShootoutUltra);
			o.Albedo = farbe.rgb * _Color.rgb * detail;
			o.Smoothness = _Glanz * detail;
			o.Metallic = 0.0;
			o.Alpha = 1.0;
		}
		ENDCG
	}
	Fallback "Diffuse"
}
