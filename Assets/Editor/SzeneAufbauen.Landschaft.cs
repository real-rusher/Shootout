using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Teil des Editor-Skripts: Landschaft ausserhalb der Spielflaeche. Huegel, Duenen und Berge, die
// nahtlos an die Map anschliessen, damit sie nicht wie eine Insel im leeren Raum wirkt; dazu Meer
// bis zum Horizont und ein Wolkenmeer. Nur zum Ansehen: Die Grenzen der Map bleiben die
// unsichtbaren Waende, die Landschaft hat keine Kollider.
public static partial class SzeneAufbauen
{
	const string LANDSCHAFT = ERZEUGT + "/Landschaft";

	delegate float LHoehe(float x, float z);

	// Stuetzstellen einer Achse: innen grob (dort liegt die Map darueber), am Rand fein, nach
	// aussen immer groeber. So passt die Landschaft am Rand genau an, ohne zu viele Punkte.
	static List<float> LAchse(float innenVon, float innenBis, float weiteVorn, float weiteHinten, float innenSchritt)
	{
		var achse = new List<float>();
		var vorn = new List<float>();
		for (float abstand = 3f, schritt = 3f; abstand < weiteVorn; schritt *= 1.12f, abstand += schritt)
		{
			vorn.Add(innenVon - abstand);
		}
		vorn.Reverse();
		achse.AddRange(vorn);
		int innenAnzahl = Mathf.Max(1, Mathf.CeilToInt((innenBis - innenVon) / innenSchritt));
		for (int i = 0; i <= innenAnzahl; i++)
		{
			achse.Add(Mathf.Lerp(innenVon, innenBis, (float)i / innenAnzahl));
		}
		for (float abstand = 3f, schritt = 3f; abstand < weiteHinten; schritt *= 1.12f, abstand += schritt)
		{
			achse.Add(innenBis + abstand);
		}
		return achse;
	}

	// Gelaende um das Rechteck innen. Vierecke ganz innen fallen weg (dort ist die Map).
	static GameObject Landschaft(Transform eltern, string name, Rect innen, Vector4 weite, LHoehe hoehe, Material material)
	{
		Ordner(LANDSCHAFT);
		var xs = LAchse(innen.xMin, innen.xMax, weite.x, weite.y, 25f);
		var zs = LAchse(innen.yMin, innen.yMax, weite.z, weite.w, 25f);
		var punkte = new Vector3[xs.Count * zs.Count];
		for (int j = 0; j < zs.Count; j++)
		{
			for (int i = 0; i < xs.Count; i++)
			{
				punkte[j * xs.Count + i] = new Vector3(xs[i], hoehe(xs[i], zs[j]), zs[j]);
			}
		}
		var dreiecke = new List<int>();
		for (int j = 0; j < zs.Count - 1; j++)
		{
			for (int i = 0; i < xs.Count - 1; i++)
			{
				bool ganzInnen = xs[i] >= innen.xMin && xs[i + 1] <= innen.xMax && zs[j] >= innen.yMin && zs[j + 1] <= innen.yMax;
				if (ganzInnen)
				{
					continue;
				}
				int a = j * xs.Count + i;
				int b = a + 1;
				int c = a + xs.Count;
				int d = c + 1;
				dreiecke.Add(a);
				dreiecke.Add(c);
				dreiecke.Add(b);
				dreiecke.Add(b);
				dreiecke.Add(c);
				dreiecke.Add(d);
			}
		}
		var netz = new Mesh();
		netz.name = name;
		netz.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		netz.vertices = punkte;
		netz.SetTriangles(dreiecke, 0);
		netz.RecalculateNormals();
		netz.RecalculateBounds();
		var go = new GameObject(name);
		go.transform.SetParent(eltern, false);
		go.AddComponent<MeshFilter>().sharedMesh = AlsAsset(netz, LANDSCHAFT + "/" + name + ".asset");
		go.AddComponent<MeshRenderer>().sharedMaterial = material;
		return go;
	}

	static Material LGelaende(string name, KPixel flach, Color flachFarbe, KPixel fels, Color felsFarbe, Color uferFarbe, float uferBis, float skala)
	{
		Ordner(LANDSCHAFT);
		var shader = Shader.Find("Shootout/Gelaende") ?? Shader.Find("Standard");
		var material = new Material(shader);
		material.name = name;
		var flachTextur = PngAlsTextur(LANDSCHAFT + "/" + name + "Flach.png", KTextur(256, flach), TextureWrapMode.Repeat);
		material.mainTexture = flachTextur;
		if (material.HasProperty("_Flach"))
		{
			material.SetTexture("_Flach", flachTextur);
			material.SetTexture("_Fels", PngAlsTextur(LANDSCHAFT + "/" + name + "Fels.png", KTextur(256, fels), TextureWrapMode.Repeat));
			material.SetColor("_FlachFarbe", flachFarbe);
			material.SetColor("_FelsFarbe", felsFarbe);
			material.SetColor("_UferFarbe", uferFarbe);
			material.SetFloat("_UferBis", uferBis);
			material.SetFloat("_Skala", skala);
		}
		else
		{
			material.color = flachFarbe;
		}
		return AlsAsset(material, LANDSCHAFT + "/" + name + ".mat");
	}

	// Fels: Schichten und Risse, gut aus der Ferne
	static Color LFelsPixel(float u, float v)
	{
		float w = KWolke(u, v, 501);
		float schicht = 0.5f + 0.5f * Mathf.Sin((v + 0.15f * KWolke(u, v, 503)) * Mathf.PI * 12f);
		float riss = Mathf.Pow(KWolke(Mathf.Repeat(u * 2f, 1f), Mathf.Repeat(v * 2f, 1f), 505), 3f);
		float wert = 0.6f + 0.35f * w + 0.12f * schicht - 0.35f * riss;
		return new Color(wert, wert, wert);
	}

	static Color LGrasPixel(float u, float v)
	{
		float w = KWolke(u, v, 511);
		float fein = KWolke(Mathf.Repeat(u * 4f, 1f), Mathf.Repeat(v * 4f, 1f), 513);
		float wert = 0.75f + 0.25f * w + 0.15f * fein;
		return new Color(wert * 0.95f, wert, wert * 0.85f);
	}

	static Color LSandPixel(float u, float v)
	{
		float w = KWolke(u, v, 521);
		float rippel = 0.5f + 0.5f * Mathf.Sin((u * 3f + v * 14f + 0.6f * KWolke(u, v, 523)) * Mathf.PI * 2f);
		float wert = 0.85f + 0.12f * w + 0.06f * rippel;
		return new Color(wert, wert, wert);
	}

	static float LRauschen(float x, float z, float massstab, float versatz)
	{
		return Mathf.PerlinNoise(x * massstab + versatz, z * massstab + versatz * 0.7f);
	}

	// Grat-Rauschen fuer Bergketten (scharfe Kaemme)
	static float LGrat(float x, float z, float massstab, float versatz)
	{
		float summe = 0f;
		float staerke = 0.6f;
		for (int i = 0; i < 4; i++)
		{
			float n = 1f - Mathf.Abs(LRauschen(x, z, massstab, versatz + i * 17f) * 2f - 1f);
			summe += n * n * staerke;
			massstab *= 2.1f;
			staerke *= 0.5f;
		}
		return summe;
	}

	// Meer bis zum Horizont um das nahe Wasser herum (drei Flaechen, ohne Wellenbewegung der
	// Punkte; die Kraeuselung macht der Shader). Material wie das nahe Wasser, je nach Stufe.
	static void FernesMeer(Transform eltern, Rect nah, float weite, Material normal, Material hoch)
	{
		var meer = new GameObject("FernesMeer");
		meer.transform.SetParent(eltern, false);
		var schalter = meer.AddComponent<FernesWasser>();
		schalter.normalMaterial = normal;
		schalter.hochMaterial = hoch;
		var teile = new[]
		{
			new Rect(-weite, -weite, weite * 2f, nah.yMin + weite),
			new Rect(-weite, nah.yMin, nah.xMin + weite, nah.height + 80f),
			new Rect(nah.xMax, nah.yMin, weite - nah.xMax, nah.height + 80f),
		};
		for (int t = 0; t < teile.Length; t++)
		{
			var r = teile[t];
			const int n = 24;
			var punkte = new Vector3[(n + 1) * (n + 1)];
			var tangenten = new Vector4[punkte.Length];
			for (int j = 0; j <= n; j++)
			{
				for (int i = 0; i <= n; i++)
				{
					punkte[j * (n + 1) + i] = new Vector3(Mathf.Lerp(r.xMin, r.xMax, (float)i / n), 0f, Mathf.Lerp(r.yMin, r.yMax, (float)j / n));
					tangenten[j * (n + 1) + i] = new Vector4(1f, 0f, 0f, -1f);
				}
			}
			var dreiecke = new List<int>();
			for (int j = 0; j < n; j++)
			{
				for (int i = 0; i < n; i++)
				{
					int a = j * (n + 1) + i;
					dreiecke.AddRange(new[] { a, a + n + 1, a + 1, a + 1, a + n + 1, a + n + 2 });
				}
			}
			var netz = new Mesh();
			netz.name = "FernesMeer" + t;
			netz.vertices = punkte;
			netz.SetTriangles(dreiecke, 0);
			netz.RecalculateNormals();
			netz.tangents = tangenten;
			netz.RecalculateBounds();
			var go = new GameObject("Meer" + t);
			go.transform.SetParent(meer.transform, false);
			go.AddComponent<MeshFilter>().sharedMesh = AlsAsset(netz, LANDSCHAFT + "/FernesMeer" + t + ".asset");
			var anzeige = go.AddComponent<MeshRenderer>();
			anzeige.sharedMaterial = normal;
			anzeige.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		}
	}

	// ------------------------------------------------------------------
	// D-Day: Kueste und Huegel

	static float DDLandschaftHoehe(float x, float z)
	{
		float cx = Mathf.Clamp(x, DD_XMIN, DD_XMAX);
		float cz = Mathf.Clamp(z, DD_ZMIN, DD_ZMAX);
		float basis = DDHoehe(cx, cz);
		bool innen = x >= DD_XMIN && x <= DD_XMAX && z >= DD_ZMIN && z <= DD_ZMAX;
		if (innen)
		{
			// Unter dem Boden der Map versteckt
			return basis - 3f;
		}
		float seitlich = Mathf.Max(0f, Mathf.Max(DD_XMIN - x, x - DD_XMAX));
		float landwaerts = Mathf.Max(0f, z - DD_ZMAX);
		float abstand = Mathf.Sqrt(seitlich * seitlich + landwaerts * landwaerts);
		// Zur See hin geht der Strand seitlich einfach weiter, landeinwaerts steigt es an.
		float land = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-25f, 35f, z));
		float huegel = (1f - Mathf.Exp(-abstand / 90f)) * (14f + 26f * LRauschen(x, z, 0.004f, 100f));
		float wellen = (1f - Mathf.Exp(-abstand / 30f)) * 4f * (LRauschen(x, z, 0.02f, 40f) - 0.4f);
		float berge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(350f, 1000f, abstand)) * (40f + 140f * LGrat(x, z, 0.0016f, 7f));
		// Felsige Landzungen weit links und rechts, die ins Meer ragen
		float landzunge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(260f, 480f, Mathf.Abs(x))) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-140f, -20f, z)) * (22f + 18f * LRauschen(x, z, 0.01f, 3f));
		float hoehe = basis + (huegel + wellen + berge) * land + landzunge;
		if (z < DD_ZMIN)
		{
			// Weiter draussen faellt der Meeresboden ab.
			hoehe -= (DD_ZMIN - z) * 0.04f;
		}
		return hoehe;
	}

	static void DDLandschaft(GameObject wurzel)
	{
		var material = LGelaende("DDayLandschaft", LGrasPixel, new Color(0.36f, 0.45f, 0.22f), LFelsPixel, new Color(0.55f, 0.53f, 0.48f), new Color(0.79f, 0.72f, 0.53f), 1.2f, 0.06f);
		Landschaft(wurzel.transform, "Landschaft", new Rect(DD_XMIN, DD_ZMIN, DD_XMAX - DD_XMIN, DD_ZMAX - DD_ZMIN), new Vector4(1600f, 1600f, 250f, 1800f), DDLandschaftHoehe, material);
	}

	// ------------------------------------------------------------------
	// Wuestenstadt: Duenen hinter der Stadtmauer, Felsberge am Horizont

	const float WS_RAND = 78f;

	static float WsLandschaftHoehe(float x, float z)
	{
		float dx = Mathf.Max(0f, Mathf.Abs(x) - WS_RAND);
		float dz = Mathf.Max(0f, Mathf.Abs(z) - WS_RAND);
		float abstand = Mathf.Sqrt(dx * dx + dz * dz);
		if (abstand <= 0f)
		{
			return -1f;
		}
		float rand = 1f - Mathf.Exp(-abstand / 35f);
		// Sicheldunen: schraege Wellen mit Rauschen verbogen
		float richtung = x * 0.045f + z * 0.02f + 3f * LRauschen(x, z, 0.006f, 20f);
		float duenen = rand * (3f + 5f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(richtung), 2f) + 10f * LRauschen(x, z, 0.008f, 60f));
		float berge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(320f, 900f, abstand)) * (50f + 160f * LGrat(x, z, 0.0018f, 31f));
		return duenen + berge - 0.2f;
	}

	static void WsLandschaft(GameObject wurzel)
	{
		var material = LGelaende("WuesteLandschaft", LSandPixel, new Color(0.88f, 0.74f, 0.52f), LFelsPixel, new Color(0.62f, 0.45f, 0.32f), new Color(0.86f, 0.74f, 0.55f), -100f, 0.05f);
		Landschaft(wurzel.transform, "Landschaft", new Rect(-WS_RAND, -WS_RAND, WS_RAND * 2f, WS_RAND * 2f), new Vector4(1500f, 1500f, 1500f, 1500f), WsLandschaftHoehe, material);
	}

	// ------------------------------------------------------------------
	// Himmelsinseln: Wolkenmeer und Berggipfel, die aus den Wolken ragen

	static float HiLandschaftHoehe(float x, float z)
	{
		float abstand = Mathf.Sqrt(x * x + z * z);
		float gipfel = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(500f, 1100f, abstand)) * (60f + 260f * LGrat(x, z, 0.0013f, 77f));
		return -90f + gipfel;
	}

	static void HiLandschaft(GameObject wurzel, Material wolke)
	{
		var material = LGelaende("HimmelLandschaft", LGrasPixel, new Color(0.5f, 0.58f, 0.42f), LFelsPixel, new Color(0.58f, 0.58f, 0.62f), Color.white, -1000f, 0.03f);
		Landschaft(wurzel.transform, "Gipfel", new Rect(-400f, -400f, 800f, 800f), new Vector4(2200f, 2200f, 2200f, 2200f), HiLandschaftHoehe, material);
		// Wolkenmeer: grosse durchscheinende Flaeche unter allem
		var meer = GameObject.CreatePrimitive(PrimitiveType.Plane);
		meer.name = "Wolkenmeer";
		Object.DestroyImmediate(meer.GetComponent<Collider>());
		meer.transform.SetParent(wurzel.transform, false);
		meer.transform.localPosition = new Vector3(0f, -32f, 0f);
		meer.transform.localScale = new Vector3(600f, 1f, 600f);
		var anzeige = meer.GetComponent<Renderer>();
		anzeige.sharedMaterial = LWolkenmeerMaterial(wolke);
		anzeige.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
	}

	static Material LWolkenmeerMaterial(Material vorlage)
	{
		var material = new Material(vorlage);
		material.name = "Wolkenmeer";
		material.mainTexture = PngAlsTextur(LANDSCHAFT + "/Wolkenmeer.png", UTexturRgba(256, (u, v) =>
		{
			float w = KWolke(u, v, 531);
			float a = Mathf.Clamp01((w - 0.3f) * 2.2f);
			return new Color(1f, 1f, 1f, 0.55f + 0.45f * a);
		}), TextureWrapMode.Repeat);
		material.color = new Color(1f, 1f, 1f, 0.92f);
		material.mainTextureScale = new Vector2(60f, 60f);
		return AlsAsset(material, LANDSCHAFT + "/Wolkenmeer.mat");
	}

	static byte[] UTexturRgba(int groesse, KPixel pixel)
	{
		var textur = new Texture2D(groesse, groesse, TextureFormat.RGBA32, false);
		var punkte = new Color[groesse * groesse];
		for (int y = 0; y < groesse; y++)
		{
			for (int x = 0; x < groesse; x++)
			{
				punkte[y * groesse + x] = pixel((float)x / groesse, (float)y / groesse);
			}
		}
		textur.SetPixels(punkte);
		textur.Apply();
		var png = textur.EncodeToPNG();
		Object.DestroyImmediate(textur);
		return png;
	}
}
