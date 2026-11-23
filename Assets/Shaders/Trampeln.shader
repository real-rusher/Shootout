// Trampelkarte fuer das Gras (GrasFeld): Draufsicht auf die Wiese. r = wie stark das Gras hier
// niedergedrueckt ist, gb = Richtung, in die es liegt (0..1 fuer -1..1). Jedes Bild: alter Stand
// erholt sich ein Stueck, wo Figuren stehen oder laufen, wird das Gras flachgedrueckt.
Shader "Hidden/Shootout/Trampeln"
{
	Properties
	{
		_MainTex ("Vorher", 2D) = "black" {}
	}
	SubShader
	{
		ZTest Always
		Cull Off
		ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			// xy = Welt-Ecke (x, z), zw = 1 / Groesse
			float4 _Bereich;
			float _Erholung;
			// xyz = Fuss, w = Radius
			float4 _Figuren[8];
			// xz = Laufrichtung (Laenge = wie schnell, 0..1)
			float4 _Richtungen[8];
			float _Anzahl;

			float4 frag (v2f_img i) : SV_Target
			{
				float4 alt = tex2D(_MainTex, i.uv);
				float staerke = max(0.0, alt.r - _Erholung);
				float2 richtung = alt.gb * 2.0 - 1.0;
				float2 welt = _Bereich.xy + i.uv / _Bereich.zw;
				for (int k = 0; k < 8; k++)
				{
					float aktiv = step((float)k + 0.5, _Anzahl);
					float2 weg = welt - _Figuren[k].xz;
					float abstand = length(weg);
					float s = smoothstep(0.0, 1.0, saturate(1.0 - abstand / max(_Figuren[k].w, 0.01))) * aktiv;
					float2 nachAussen = weg / max(abstand, 0.01);
					float2 d = nachAussen * 0.6 + _Richtungen[k].xz;
					d /= max(length(d), 0.001);
					richtung = lerp(richtung, d, s);
					staerke = max(staerke, s);
				}
				return float4(staerke, richtung * 0.5 + 0.5, 1.0);
			}
			ENDCG
		}
	}
}
