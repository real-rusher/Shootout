// Landschaft ausserhalb der Spielflaeche (Huegel, Duenen, Berge): flache Stellen mit der
// Flachtextur (Gras oder Sand), steile mit Fels (triplanar), ganz unten ein Uferstreifen.
// Keine Normalenkarte: Die Berge sind weit weg, die Form macht das Licht.
Shader "Shootout/Gelaende"
{
	Properties
	{
		_Flach ("Flach (Gras, Sand)", 2D) = "white" {}
		_FlachFarbe ("Farbe flach", Color) = (0.34, 0.44, 0.21, 1)
		_Fels ("Fels", 2D) = "white" {}
		_FelsFarbe ("Farbe Fels", Color) = (0.5, 0.48, 0.44, 1)
		_UferFarbe ("Farbe Ufer", Color) = (0.79, 0.72, 0.53, 1)
		_UferBis ("Ufer bis Hoehe", Float) = 1
		_Skala ("Kacheln pro Meter", Float) = 0.05
		_Steil ("Ab dieser Steilheit Fels", Range(0, 1)) = 0.78
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows
		#pragma target 3.0

		sampler2D _Flach;
		sampler2D _Fels;
		fixed4 _FlachFarbe;
		fixed4 _FelsFarbe;
		fixed4 _UferFarbe;
		float _UferBis;
		float _Skala;
		half _Steil;

		struct Input
		{
			float3 worldPos;
			float3 worldNormal;
		};

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			float3 n = normalize(IN.worldNormal);
			float3 p = IN.worldPos * _Skala;
			float3 g = pow(abs(n), 4.0);
			g /= (g.x + g.y + g.z);
			fixed3 fels = (tex2D(_Fels, p.zy) * g.x + tex2D(_Fels, p.xz) * g.y + tex2D(_Fels, p.xy) * g.z).rgb * _FelsFarbe.rgb;
			fixed3 flach = tex2D(_Flach, p.xz).rgb * _FlachFarbe.rgb;
			// Grosse Flecken, damit die Flaechen nicht gleichfoermig aussehen
			fixed3 flecken = tex2D(_Flach, p.xz * 0.07).rgb;
			flach *= 0.75 + 0.5 * dot(flecken, fixed3(0.333, 0.333, 0.333));
			float steil = smoothstep(_Steil, _Steil - 0.12, n.y);
			fixed3 farbe = lerp(flach, fels, steil);
			float ufer = smoothstep(_UferBis + 0.8, _UferBis, IN.worldPos.y);
			farbe = lerp(farbe, _UferFarbe.rgb * tex2D(_Flach, p.xz * 3.0).rgb, ufer);
			o.Albedo = farbe;
			o.Smoothness = 0.05;
			o.Metallic = 0.0;
			o.Alpha = 1.0;
		}
		ENDCG
	}
	Fallback "Diffuse"
}
