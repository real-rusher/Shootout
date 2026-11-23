// Grashalme der Grafikstufe "Ultra". Einzelne Halme als Netz (keine Bildflaechen), Farbe aus
// den Punkten (Wurzel dunkel, Spitze hell), Wind ueber die Hoehe im Halm (uv.y), mit Boeen, die
// als Wellen ueber die Wiese laufen. Figuren (GrasFeld setzt _ShootoutFiguren) druecken das
// Gras zur Seite und nach unten. Wird mit Graphics.DrawMeshInstanced gezeichnet und blendet in
// der Ferne aus.
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

		// Global, von GrasFeld: Fuesse der Figuren (xyz) und Radius (w).
		float4 _ShootoutFiguren[8];
		float _ShootoutFigurenAnzahl;

		struct Input
		{
			float4 color : COLOR;
		};

		void vert (inout appdata_full v)
		{
			float3 fuss = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
			// In der Ferne schrumpfen die Halme in den Boden, statt ploetzlich zu verschwinden.
			float abstand = distance(fuss, _WorldSpaceCameraPos);
			float sichtbar = saturate((_Weite - abstand) / (_Weite * 0.25));
			v.vertex.xyz *= sichtbar;
			float3 welt = mul(unity_ObjectToWorld, v.vertex).xyz;
			float t = _Time.y;
			float h = v.texcoord.y * v.texcoord.y;

			// Wind: gleichmaessiges Wiegen plus Boeen, die als breite Wellen ueber die Wiese laufen.
			float boe = 0.45 + 0.55 * saturate(sin(dot(fuss.xz, float2(0.11, 0.07)) - t * 1.3) * 0.5 + 0.5);
			float wind = sin(t * 1.6 + fuss.x * 0.35 + fuss.z * 0.3) * 0.6 + sin(t * 3.7 + fuss.x * 1.3 - fuss.z) * 0.25 + 0.5;
			float3 versatz = float3(wind * 0.2, -abs(wind) * 0.05, wind * 0.12) * h * _Wind * boe;

			// Figuren druecken das Gras weg (bis Radius w, nur wenn sie am Boden sind).
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
			versatz += float3(druck.x * 0.45, druck.y * 0.35, druck.z * 0.45) * h;

			v.vertex.xyz += mul((float3x3)unity_WorldToObject, versatz);

			// Jedes Bueschel etwas anders getoent
			float zufall = frac(sin(dot(fuss.xz, float2(12.9898, 78.233))) * 43758.5453);
			v.color.rgb *= lerp(0.85, 1.12, zufall);
			v.color.rgb = lerp(v.color.rgb, v.color.rgb * float3(1.15, 1.05, 0.7), step(0.8, zufall) * v.texcoord.y);
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
