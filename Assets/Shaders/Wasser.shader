// Wasser: Wellen und Kraeuselung pro Pixel (Summe mehrerer laufender Wellen in Weltkoordinaten),
// dadurch zerfaellt die Spiegelung von Himmel und Sonne in Glitzern wie bei echtem Wasser.
// Von oben dunkel und durchsichtig, flach angesehen hell (Fresnel), helle Kaemme mit etwas Gischt.
// Die grossen Wellen bewegt Wasser.cs ueber die Punkte des Netzes. Das Netz braucht Tangenten
// entlang x (setzt das Editor-Skript). Rueckfall auf Transparent/Diffuse fuer alte Grafikkarten.
Shader "Shootout/Wasser"
{
	Properties
	{
		_Farbe ("Farbe flach", Color) = (0.22, 0.45, 0.52, 0.9)
		_Tiefe ("Farbe von oben", Color) = (0.03, 0.14, 0.18, 0.6)
		_Glanz ("Glanz", Range(0, 1)) = 0.95
		_Fresnel ("Fresnel-Staerke", Range(0.5, 8)) = 3
		_Wellen ("Wellenstaerke", Range(0, 3)) = 1
		_Tempo ("Tempo", Range(0, 3)) = 1
		_Gischt ("Gischt auf Kaemmen", Range(0, 1)) = 0.35
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
		half _Wellen;
		half _Tempo;
		half _Gischt;

		struct Input
		{
			float3 worldPos;
			float3 viewDir;
		};

		// Eine laufende Welle: liefert Steigung (xy) und Hoehe (z). Tempo nach der Formel fuer
		// tiefes Wasser, lange Wellen laufen schneller.
		float3 welle(float2 p, float t, float2 richtung, float laenge, float hoehe)
		{
			float k = 6.2831853 / laenge;
			float w = sqrt(9.81 * k);
			float phase = dot(richtung, p) * k - w * t;
			return float3(richtung * (hoehe * k * cos(phase)), hoehe * sin(phase));
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			float2 p = IN.worldPos.xz;
			float t = _Time.y * _Tempo;
			float3 summe = 0;
			// Dunung
			summe += welle(p, t, normalize(float2(0.3, 1.0)), 13.0, 0.12);
			summe += welle(p, t, normalize(float2(-0.5, 0.85)), 7.5, 0.07);
			summe += welle(p, t, normalize(float2(0.8, 0.6)), 4.1, 0.035);
			// Kraeuselung durch Wind
			summe += welle(p, t, normalize(float2(-0.9, 0.4)), 1.9, 0.014);
			summe += welle(p, t, normalize(float2(0.2, -1.0)), 1.1, 0.008);
			summe += welle(p, t, normalize(float2(0.95, -0.3)), 0.63, 0.004);
			summe += welle(p, t, normalize(float2(-0.35, -0.94)), 0.37, 0.0025);
			float2 steigung = summe.xy * _Wellen;
			// Tangentenraum: x = Tangente, y = z der Welt (Bitangente), z = oben
			float3 normale = normalize(float3(-steigung.x, -steigung.y, 1.0));
			o.Normal = normale;

			float f = pow(1.0 - saturate(dot(normalize(IN.viewDir), normale)), _Fresnel);
			fixed4 farbe = lerp(_Tiefe, _Farbe, f);
			// Kaemme etwas heller, ein Hauch Gischt
			float kamm = saturate((summe.z - 0.1) * 6.0) * _Gischt;
			o.Albedo = lerp(farbe.rgb, fixed3(0.85, 0.9, 0.92), kamm);
			o.Smoothness = lerp(_Glanz, 0.4, kamm);
			o.Metallic = 0.0;
			o.Alpha = saturate(farbe.a + kamm * 0.5);
		}
		ENDCG
	}
	Fallback "Transparent/Diffuse"
}
