// Textur nach Weltkoordinaten statt nach UV (triplanar): Grosse und kleine Kloetze bekommen
// dieselbe Kachelgroesse, ohne gestreckte Muster. Dazu Relief aus einer Normalenkarte, ebenfalls
// triplanar ("Whiteout"-Mischung). Fuer die selbst gebauten Maps.
Shader "Shootout/Weltraster"
{
	Properties
	{
		_MainTex ("Textur", 2D) = "white" {}
		_BumpMap ("Relief", 2D) = "bump" {}
		_Relief ("Reliefstaerke", Range(0, 2)) = 1
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
		sampler2D _BumpMap;
		half _Relief;
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
			INTERNAL_DATA
		};

		// Normale in Weltkoordinaten in den Tangentenraum des Netzes umrechnen (fuer o.Normal).
		float3 weltZuTangente(Input IN, float3 normale)
		{
			float3 t0 = WorldNormalVector(IN, float3(1, 0, 0));
			float3 t1 = WorldNormalVector(IN, float3(0, 1, 0));
			float3 t2 = WorldNormalVector(IN, float3(0, 0, 1));
			return normalize(mul(float3x3(t0, t1, t2), normale));
		}

		half3 relief(float2 uv)
		{
			half3 n = UnpackNormal(tex2D(_BumpMap, uv));
			n.xy *= _Relief;
			return n;
		}

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			float3 weltNormale = WorldNormalVector(IN, float3(0, 0, 1));
			float3 gewicht = pow(abs(weltNormale), 4.0);
			gewicht /= (gewicht.x + gewicht.y + gewicht.z);
			float3 p = IN.worldPos * _Skala;
			float3 vorzeichen = sign(weltNormale);
			float2 uvX = p.zy;
			float2 uvY = p.xz;
			float2 uvZ = p.xy;
			uvX.x *= vorzeichen.x;
			uvY.x *= vorzeichen.y;
			uvZ.x *= -vorzeichen.z;

			fixed4 farbe = tex2D(_MainTex, uvX) * gewicht.x + tex2D(_MainTex, uvY) * gewicht.y + tex2D(_MainTex, uvZ) * gewicht.z;
			float3 f = p * 5.3;
			fixed3 fein = (tex2D(_MainTex, f.zy) * gewicht.x + tex2D(_MainTex, f.xz) * gewicht.y + tex2D(_MainTex, f.xy) * gewicht.z).rgb;
			half detail = lerp(1.0, 0.6 + 0.8 * dot(fein, half3(0.333, 0.333, 0.333)), _ShootoutUltra);
			o.Albedo = farbe.rgb * _Color.rgb * detail;
			o.Smoothness = _Glanz * detail;
			o.Metallic = 0.0;
			o.Alpha = 1.0;

			// Relief je Achse, in Weltrichtung gedreht und gemischt.
			half3 nX = relief(uvX);
			half3 nY = relief(uvY);
			half3 nZ = relief(uvZ);
			nX.x *= vorzeichen.x;
			nY.x *= vorzeichen.y;
			nZ.x *= -vorzeichen.z;
			half3 absN = abs(weltNormale);
			nX = half3(nX.xy + weltNormale.zy, absN.x * nX.z);
			nY = half3(nY.xy + weltNormale.xz, absN.y * nY.z);
			nZ = half3(nZ.xy + weltNormale.xy, absN.z * nZ.z);
			float3 neu = normalize(nX.zyx * gewicht.x + nY.xzy * gewicht.y + nZ.xyz * gewicht.z);
			o.Normal = weltZuTangente(IN, neu);
		}
		ENDCG
	}
	Fallback "Diffuse"
}
