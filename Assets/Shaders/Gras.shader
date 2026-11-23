// Grashalme der Grafikstufe "Ultra": dichter Teppich aus duennen Halmen. Alle Halme zeigen mit
// der Normale nach oben, so wird die Wiese wie ein Ganzes beleuchtet (keine einzelnen Bueschel).
// Farbe aus den Punkten (Wurzel dunkel, Spitze hell), dazu grosse, sanfte Farbflaechen.
// Wind: Boeen laufen als Wellen ueber die Wiese, gebogene Halme glaenzen heller.
// Trampelpfade: _ShootoutTrampel (GrasFeld) sagt, wo und in welche Richtung das Gras liegt.
// Gezeichnet mit Graphics.DrawMeshInstanced, in der Ferne schrumpfen die Halme in den Boden.
Shader "Shootout/Gras"
{
	Properties
	{
		_Color ("Farbe", Color) = (1, 1, 1, 1)
		_Wind ("Wind", Range(0, 2)) = 1
		_Weite ("Sichtweite", Float) = 45
		_Glanz ("Glanz", Range(0, 1)) = 0.12
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

		// Global, von GrasFeld
		float4 _ShootoutFiguren[8];
		float _ShootoutFigurenAnzahl;
		sampler2D _ShootoutTrampel;
		float4 _ShootoutTrampelBereich;
		float _ShootoutTrampelAktiv;

		struct Input
		{
			float4 color : COLOR;
		};

		void vert (inout appdata_full v)
		{
			float3 fuss = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
			float abstand = distance(fuss, _WorldSpaceCameraPos);
			float sichtbar = saturate((_Weite - abstand) / (_Weite * 0.3));
			v.vertex.xyz *= sichtbar;
			float3 welt = mul(unity_ObjectToWorld, v.vertex).xyz;
			float t = _Time.y;
			float h = v.texcoord.y;
			float h2 = h * h;

			// Wind aus einer Hauptrichtung, Boeen als laufende Wellen (zwei Lagen), dazu Flattern.
			float2 windRichtung = normalize(float2(1.0, 0.35));
			float2 quer = float2(-windRichtung.y, windRichtung.x);
			float welle1 = sin(dot(fuss.xz, windRichtung) * 0.16 - t * 1.9);
			float welle2 = sin(dot(fuss.xz, normalize(float2(0.8, -0.6))) * 0.33 - t * 2.7 + 1.3);
			float boe = saturate(0.5 + 0.35 * welle1 + 0.2 * welle2);
			float flattern = sin(t * 7.0 + dot(fuss.xz, float2(3.1, 2.3)) + h * 3.0) * 0.04;
			float neigung = (0.15 + 0.85 * boe) * _Wind;
			float3 versatz = float3(windRichtung.x, 0, windRichtung.y) * (neigung * 0.32 * h2) + float3(quer.x, 0, quer.y) * flattern * h;
			versatz.y -= neigung * 0.1 * h2;

			// Trampelpfade: niedergedruecktes Gras liegt in Laufrichtung.
			float2 uv = (welt.xz - _ShootoutTrampelBereich.xy) * _ShootoutTrampelBereich.zw;
			float innen = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0) * _ShootoutTrampelAktiv;
			float4 tritt = tex2Dlod(_ShootoutTrampel, float4(saturate(uv), 0, 0));
			float platt = smoothstep(0.0, 1.0, tritt.r) * innen;
			float2 liegt = tritt.gb * 2.0 - 1.0;
			// Hoehe des Punkts ueber dem Boden in Metern: flachgedrueckt liegt der Halm fast am Boden.
			float hoeheWelt = max(welt.y - fuss.y, 0.0);
			versatz.xz = lerp(versatz.xz, liegt * 0.8 * hoeheWelt, platt);
			versatz.y -= platt * 0.72 * hoeheWelt;

			// Figuren direkt daneben druecken zusaetzlich zur Seite (auch in der Luft knapp darueber).
			float3 druck = 0;
			for (int i = 0; i < 8; i++)
			{
				float aktiv = step((float)i + 0.5, _ShootoutFigurenAnzahl);
				float4 figur = _ShootoutFiguren[i];
				float2 weg = welt.xz - figur.xz;
				float laenge = length(weg);
				float naehe = saturate(1.0 - laenge / max(figur.w, 0.01));
				float amBoden = saturate(1.0 - abs(welt.y - figur.y) / 1.2);
				float staerke = naehe * naehe * amBoden * aktiv;
				druck.xz += weg / max(laenge, 0.05) * staerke;
				druck.y -= staerke;
			}
			druck.xz = druck.xz / max(1.0, length(druck.xz));
			druck.y = max(druck.y, -1.0);
			versatz += float3(druck.x * 0.4, druck.y * 0.3, druck.z * 0.4) * h;

			v.vertex.xyz += mul((float3x3)unity_WorldToObject, versatz);

			// Grosse, weiche Farbflaechen statt Zufall pro Bueschel: wirkt wie eine Wiese.
			float flaeche = sin(fuss.x * 0.045 + sin(fuss.z * 0.03) * 2.0) * sin(fuss.z * 0.05 + 1.7) * 0.5 + 0.5;
			v.color.rgb *= lerp(0.88, 1.1, flaeche);
			v.color.rgb = lerp(v.color.rgb, v.color.rgb * float3(1.12, 1.06, 0.75), (1.0 - flaeche) * 0.35 * h);
			// Glanz: Wo der Wind die Halme biegt, zeigen sie die helle Seite.
			v.color.a = boe * h * (1.0 - platt);
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed3 farbe = IN.color.rgb * _Color.rgb;
			o.Albedo = farbe * (1.0 + 0.12 * IN.color.a);
			o.Smoothness = _Glanz;
			o.Metallic = 0.0;
			o.Alpha = 1.0;
		}
		ENDCG
	}
	Fallback "Diffuse"
}
