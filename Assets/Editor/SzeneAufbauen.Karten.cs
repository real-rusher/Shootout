using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Teil des Editor-Skripts: die Maps 4 bis 6 (Wuestenstadt, Tunnel, Himmelsinseln). Wie D-Day
// aus einfachen Formen und selbst erzeugten Texturen, mit festem Zufall (Host und Client
// muessen dieselbe Map bekommen). Waende und Boeden nutzen den Shader "Shootout/Weltraster".
public static partial class SzeneAufbauen
{
	const string KARTEN = ERZEUGT + "/Karten";

	// ------------------------------------------------------------------
	// Gemeinsame Hilfen

	// Material mit Textur nach Weltkoordinaten.
	// Material mit Textur nach Weltkoordinaten und Relief (Normalenkarte aus der Helligkeit).
	static Material KRaster(string name, KPixel pixel, Color farbe, float kachelnProMeter, float glanz, float relief = 6f)
	{
		Ordner(KARTEN);
		// Rueckfall auf Standard, falls der eigene Shader nicht uebersetzt werden konnte.
		var shader = Shader.Find("Shootout/Weltraster") ?? Shader.Find("Standard");
		var material = new Material(shader);
		material.name = name;
		material.mainTexture = PngAlsTextur(KARTEN + "/" + name + ".png", KTextur(256, pixel), TextureWrapMode.Repeat);
		if (material.HasProperty("_BumpMap"))
		{
			material.SetTexture("_BumpMap", UNormalTextur(name + "Relief", 256, (u, v) =>
			{
				var c = pixel(u, v);
				return c.r * 0.3f + c.g * 0.55f + c.b * 0.15f;
			}, relief));
		}
		material.color = farbe;
		material.SetFloat("_Skala", kachelnProMeter);
		material.SetFloat("_Glanz", glanz);
		return AlsAsset(material, KARTEN + "/" + name + ".mat");
	}

	static Material KFarbe(string name, Color farbe, float glanz)
	{
		Ordner(KARTEN);
		var material = new Material(Shader.Find("Standard"));
		material.name = name;
		material.color = farbe;
		material.SetFloat("_Glossiness", glanz);
		return AlsAsset(material, KARTEN + "/" + name + ".mat");
	}

	static Material KLeuchten(string name, Color farbe, float staerke)
	{
		Ordner(KARTEN);
		var material = new Material(Shader.Find("Standard"));
		material.name = name;
		material.color = farbe;
		material.EnableKeyword("_EMISSION");
		material.SetColor("_EmissionColor", farbe * staerke);
		material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
		return AlsAsset(material, KARTEN + "/" + name + ".mat");
	}

	// Kachelbares Rauschen (wiederholt sich nach "zellen" Zellen, ohne Naht).
	static float KRauschen(float x, float y, int zellen, int saat)
	{
		int x0 = Mathf.FloorToInt(x);
		int y0 = Mathf.FloorToInt(y);
		float fx = x - x0;
		float fy = y - y0;
		fx = fx * fx * (3f - 2f * fx);
		fy = fy * fy * (3f - 2f * fy);
		float a = KWert(x0, y0, zellen, saat);
		float b = KWert(x0 + 1, y0, zellen, saat);
		float c = KWert(x0, y0 + 1, zellen, saat);
		float d = KWert(x0 + 1, y0 + 1, zellen, saat);
		return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
	}

	static float KWert(int x, int y, int zellen, int saat)
	{
		x = ((x % zellen) + zellen) % zellen;
		y = ((y % zellen) + zellen) % zellen;
		uint h = (uint)(x * 374761393 + y * 668265263 + saat * 2147483647);
		h = (h ^ (h >> 13)) * 1274126177u;
		return (h ^ (h >> 16)) / (float)uint.MaxValue;
	}

	// Mehrere Rauschlagen uebereinander, Ergebnis 0..1.
	static float KWolke(float u, float v, int saat)
	{
		float summe = 0f;
		float gewicht = 0f;
		float staerke = 1f;
		int zellen = 4;
		for (int lage = 0; lage < 4; lage++)
		{
			summe += KRauschen(u * zellen, v * zellen, zellen, saat + lage) * staerke;
			gewicht += staerke;
			staerke *= 0.5f;
			zellen *= 2;
		}
		return summe / gewicht;
	}

	delegate Color KPixel(float u, float v);

	static byte[] KTextur(int groesse, KPixel pixel)
	{
		var textur = new Texture2D(groesse, groesse, TextureFormat.RGB24, false);
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

	static GameObject KWurzel(string name, Color nebel, float dichte, float umgebung, float sonne)
	{
		var wurzel = new GameObject(name);
		var stimmung = wurzel.AddComponent<KartenAtmosphaere>();
		stimmung.nebelFarbe = nebel;
		stimmung.nebelDichte = dichte;
		stimmung.umgebungsFaktor = umgebung;
		stimmung.sonnenFaktor = sonne;
		return wurzel;
	}

	static GameObject KGruppe(Transform eltern, string name)
	{
		var gruppe = new GameObject(name);
		gruppe.transform.SetParent(eltern, false);
		return gruppe;
	}

	// Kollider genau nach dem sichtbaren Netz. Unitys Kapsel und Kugel passen bei ungleicher
	// Skalierung nicht: Ein flacher Zylinder bekam eine grosse Kugel als Kollider, eine flache
	// Kugel eine runde. Dann steht man in der Luft oder faellt durch Sichtbares.
	static void KNetzKollider(GameObject go)
	{
		var alt = go.GetComponent<Collider>();
		if (alt != null)
		{
			Object.DestroyImmediate(alt);
		}
		go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
	}

	static void KLampe(Transform eltern, Vector3 position, Color farbe, float staerke, float reichweite, Material birne, bool flackert)
	{
		var lampe = KGruppe(eltern, "Lampe");
		lampe.transform.localPosition = position;
		var kugel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		kugel.name = "Birne";
		Object.DestroyImmediate(kugel.GetComponent<Collider>());
		kugel.transform.SetParent(lampe.transform, false);
		kugel.transform.localScale = Vector3.one * 0.35f;
		kugel.GetComponent<Renderer>().sharedMaterial = birne;
		var licht = lampe.AddComponent<Light>();
		licht.type = LightType.Point;
		licht.color = farbe;
		licht.intensity = staerke;
		licht.range = reichweite;
		if (flackert)
		{
			lampe.AddComponent<Flackern>().staerke = 0.6f;
		}
	}

	static void KKiste(Transform eltern, Vector3 position, float groesse, float gier, Material holz)
	{
		Klotz(eltern, "Kiste", position + Vector3.up * groesse * 0.5f, Vector3.one * groesse, holz, new Vector3(0f, gier, 0f));
	}

	static void KFass(Transform eltern, Vector3 position, Material material)
	{
		var fass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		fass.name = "Fass";
		fass.transform.SetParent(eltern, false);
		fass.transform.localPosition = position + Vector3.up * 0.65f;
		fass.transform.localScale = new Vector3(0.9f, 0.65f, 0.9f);
		fass.GetComponent<Renderer>().sharedMaterial = material;
	}

	// ------------------------------------------------------------------
	// Info-Raum: Lichtstreifen an der Decke

	// Der Raum hat eine Decke (y = 48, das Modell ist riesig, die Figuren klein). Die Sonne der
	// Szene schien nur aufs Dach, innen leuchtete eine einzige Punktlampe: flach und grau. Jetzt
	// Lichtstreifen wie in einem echten Klassenraum, ein Deckenlicht von oben mit weichen
	// Schatten, einige Lampen fuer Helligkeitsverlauf. Die Sonne ist im Raum aus.
	const float IR_DECKE = 48f;

	static void InfoRaumLicht(GameObject wurzel, Light altesLicht)
	{
		// Decke und Waende werfen keinen Schatten, sonst laege der ganze Raum im Schatten der Decke.
		// Alles, was den ganzen Raum umschliesst (Decke, Waende, Boden), erkannt an der Groesse.
		foreach (var anzeige in wurzel.GetComponentsInChildren<Renderer>(true))
		{
			var g = anzeige.bounds.size;
			if (anzeige.name.StartsWith("Room") || g.x > 60f || g.z > 60f || g.y > 30f)
			{
				anzeige.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			}
		}
		altesLicht.intensity = 0.35f;
		var stimmung = wurzel.AddComponent<KartenAtmosphaere>();
		stimmung.nebelFarbe = new Color(0.75f, 0.76f, 0.78f);
		stimmung.nebelDichte = 0.0015f;
		// Ein Klassenraum ist ueberall hell: viel Umgebungslicht, weiche, schwache Schatten.
		stimmung.umgebungsFaktor = 1.9f;
		stimmung.sonnenFaktor = 0f;

		var leuchten = KLeuchten("Lichtstreifen", new Color(1f, 0.98f, 0.94f), 3f);
		var rahmen = KFarbe("Lampenrahmen", new Color(0.75f, 0.76f, 0.78f), 0.5f);
		var lampen = KGruppe(wurzel.transform, "Deckenlampen");
		// Reihen quer zum Raum, Streifen 22 m lang (ein echter Streifen, im Massstab des Raums)
		for (float z = -48f; z <= 12f; z += 15f)
		{
			for (float x = -80f; x <= 50f; x += 30f)
			{
				var streifen = Klotz(lampen.transform, "Lichtstreifen", new Vector3(x, IR_DECKE - 0.35f, z), new Vector3(22f, 0.3f, 2.2f), leuchten);
				Object.DestroyImmediate(streifen.GetComponent<Collider>());
				streifen.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				var gehaeuse = Klotz(lampen.transform, "Gehaeuse", new Vector3(x, IR_DECKE - 0.2f, z), new Vector3(22.8f, 0.4f, 3f), rahmen);
				Object.DestroyImmediate(gehaeuse.GetComponent<Collider>());
				gehaeuse.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			}
			// Je Reihe zwei Lampen fuer den Helligkeitsverlauf (ohne Schatten, die kommen von oben).
			foreach (var x in new[] { -65f, -25f, 15f, 45f })
			{
				var lampe = KGruppe(lampen.transform, "Raumlicht");
				lampe.transform.localPosition = new Vector3(x, IR_DECKE - 6f, z);
				var licht = lampe.AddComponent<Light>();
				licht.type = LightType.Point;
				licht.color = new Color(1f, 0.97f, 0.92f);
				licht.intensity = 0.8f;
				licht.range = 60f;
				licht.shadows = LightShadows.None;
			}
		}
		// Deckenlicht: fast senkrecht von oben, weiche Schatten unter Tischen und Stuehlen.
		var decke = KGruppe(wurzel.transform, "Deckenlicht");
		decke.transform.localRotation = Quaternion.Euler(78f, 20f, 0f);
		var oben = decke.AddComponent<Light>();
		oben.type = LightType.Directional;
		oben.color = new Color(1f, 0.97f, 0.93f);
		oben.intensity = 1.0f;
		oben.shadows = LightShadows.Soft;
		oben.shadowStrength = 0.45f;
	}

	// ------------------------------------------------------------------
	// Map 4: Wuestenstadt (eigener Grundriss im Stil einer Wuestenstadt)

	const float WS_ZELLE = 30f;

	static GameObject WuestenstadtKarte()
	{
		var zufall = new System.Random(4141);
		wsHausNummer = 0;
		var sandstein = KRaster("Sandstein", (u, v) =>
		{
			// Gemauerte Bloecke: 8 Reihen, versetzt, Fugen vertieft (dunkel = tiefer im Relief).
			float reihe = Mathf.Floor(v * 8f);
			float versatz = (reihe % 2f) * 0.5f;
			float bx = u * 4f + versatz;
			float by = v * 8f;
			float fx = bx - Mathf.Floor(bx);
			float fy = by - reihe;
			float fuge = Mathf.Min(Mathf.Min(fx, 1f - fx) * 0.5f, Mathf.Min(fy, 1f - fy));
			float block = KWert(Mathf.FloorToInt(bx) % 4, (int)reihe, 8, 13);
			float w = KWolke(u, v, 11);
			float kante = Mathf.SmoothStep(0.015f, 0.05f, fuge);
			var stein = new Color(0.78f, 0.66f, 0.48f) * (0.8f + 0.18f * block + 0.22f * w);
			var moertel = new Color(0.62f, 0.55f, 0.44f);
			return Color.Lerp(moertel, stein, kante);
		}, Color.white, 0.25f, 0.08f, 9f);
		var putz = KRaster("Putz", (u, v) =>
		{
			float w = KWolke(u, v, 23);
			return Color.Lerp(new Color(0.82f, 0.76f, 0.64f), new Color(0.7f, 0.6f, 0.47f), Mathf.Pow(w, 2.2f));
		}, Color.white, 0.2f, 0.05f);
		var pflaster = KRaster("Pflaster", (u, v) =>
		{
			// Steinplatten: Fugen alle 1/4, jede Platte etwas anders.
			float px = u * 4f;
			float py = v * 4f;
			float fuge = Mathf.Min(Mathf.Min(px - Mathf.Floor(px), Mathf.Ceil(px) - px), Mathf.Min(py - Mathf.Floor(py), Mathf.Ceil(py) - py));
			float platte = KWert(Mathf.FloorToInt(px), Mathf.FloorToInt(py), 4, 5);
			Color farbe = new Color(0.78f, 0.68f, 0.52f) * (0.85f + 0.2f * platte + 0.1f * KWolke(u, v, 7));
			return fuge < 0.04f ? farbe * 0.65f : farbe;
		}, Color.white, 0.5f, 0.12f);
		var holz = KFarbe("WuesteHolz", new Color(0.45f, 0.32f, 0.2f), 0.1f);
		var dunkel = KFarbe("Fenster", new Color(0.08f, 0.07f, 0.06f), 0.3f);
		var stoffe = new[]
		{
			KFarbe("StoffRot", new Color(0.7f, 0.18f, 0.12f), 0.05f),
			KFarbe("StoffBlau", new Color(0.15f, 0.3f, 0.6f), 0.05f),
			KFarbe("StoffGelb", new Color(0.85f, 0.65f, 0.2f), 0.05f),
			KFarbe("StoffGruen", new Color(0.2f, 0.45f, 0.25f), 0.05f),
		};
		var palmStamm = KFarbe("PalmStamm", new Color(0.42f, 0.32f, 0.2f), 0.05f);
		var palmBlatt = KFarbe("PalmBlatt", new Color(0.24f, 0.45f, 0.18f), 0.1f);
		var kuppel = KFarbe("Kuppel", new Color(0.2f, 0.5f, 0.55f), 0.4f);

		var wurzel = KWurzel("Wuestenstadt", new Color(0.86f, 0.78f, 0.64f), 0.0022f, 1f, 1f);
		// Falls man doch irgendwo durchfaellt: neu erscheinen statt endlos fallen.
		wurzel.AddComponent<KartenRegeln>().absturzHoehe = -20f;
		var boden = Klotz(wurzel.transform, "Boden", new Vector3(0f, -0.5f, 0f), new Vector3(156f, 1f, 156f), pflaster);
		var gebaeude = KGruppe(wurzel.transform, "Gebaeude");
		var deko = KGruppe(wurzel.transform, "Deko");

		// Aussenmauer
		foreach (var seite in new[] { new Vector4(0, 77, 156, 2), new Vector4(0, -77, 156, 2), new Vector4(77, 0, 2, 156), new Vector4(-77, 0, 2, 156) })
		{
			Klotz(wurzel.transform, "Stadtmauer", new Vector3(seite.x, 6f, seite.y), new Vector3(seite.z, 12f, seite.w), sandstein);
		}

		// 5 x 5 Zellen. Offene Plaetze: Spawn West und Ost, Markt in der Mitte, Palasthof
		// im Norden, Podest im Sueden. Sonst Haeuser mit Gassen dazwischen.
		for (int ix = -2; ix <= 2; ix++)
		{
			for (int iz = -2; iz <= 2; iz++)
			{
				var mitte = new Vector3(ix * WS_ZELLE, 0f, iz * WS_ZELLE);
				if ((ix == -2 || ix == 2) && iz == 0)
				{
					DDSpawnZone(wurzel.transform, mitte + new Vector3(0f, 1.5f, 0f), new Vector3(14f, 0.5f, 14f), ix < 0 ? Teams.ALLIIERTE : Teams.VERTEIDIGER);
					WsPalme(deko.transform, mitte + new Vector3(8f, 0f, 8f), zufall, palmStamm, palmBlatt);
					WsPalme(deko.transform, mitte + new Vector3(-8f, 0f, -8f), zufall, palmStamm, palmBlatt);
					// Kisten nur am Rand, nicht im Spawnbereich (+-7 m): Wer in einer Kiste erscheint,
					// steckt fest oder faellt durch.
					for (int k = 0; k < 4; k++)
					{
						float seite = k % 2 == 0 ? 1f : -1f;
						var rand = k < 2 ? new Vector3(seite * Zufall(zufall, 9.5f, 12f), 0f, Zufall(zufall, -10f, 10f)) : new Vector3(Zufall(zufall, -6f, 6f), 0f, seite * Zufall(zufall, 9.5f, 12f));
						KKiste(deko.transform, mitte + rand, Zufall(zufall, 1f, 1.6f), Zufall(zufall, 0f, 90f), holz);
					}
					continue;
				}
				if (ix == 0 && iz == 0)
				{
					WsMarkt(deko.transform, mitte, zufall, holz, stoffe, sandstein, palmStamm, palmBlatt);
					continue;
				}
				if (ix == 0 && iz == 1)
				{
					WsPalasthof(gebaeude.transform, mitte, sandstein, putz, kuppel);
					continue;
				}
				if (ix == 0 && iz == -1)
				{
					WsPodest(gebaeude.transform, mitte, sandstein, holz, zufall);
					continue;
				}
				if (ix == -1 && iz == 1)
				{
					WsTurm(gebaeude.transform, mitte, sandstein, kuppel);
					continue;
				}
				WsHaeuser(gebaeude.transform, mitte, zufall, sandstein, putz, dunkel, holz, stoffe);
			}
		}

		// Torboegen ueber einigen Gassen
		foreach (var bogen in new[] { new Vector4(-15, 0, 0, 0), new Vector4(15, 0, 0, 0), new Vector4(0, 15, 90, 0), new Vector4(0, -15, 90, 0), new Vector4(-45, 30, 0, 0), new Vector4(45, -30, 0, 0) })
		{
			WsTorbogen(gebaeude.transform, new Vector3(bogen.x, 0f, bogen.y), bogen.z, sandstein);
		}

		// Kisten und Faesser in den Gassen (Deckung)
		for (int i = 0; i < 40; i++)
		{
			float gx = (zufall.Next(4) - 1.5f) * WS_ZELLE;
			float z = Zufall(zufall, -70f, 70f);
			var ort = zufall.Next(2) == 0 ? new Vector3(gx + Zufall(zufall, -3f, 3f), 0f, z) : new Vector3(z, 0f, gx + Zufall(zufall, -3f, 3f));
			if (Mathf.Abs(Mathf.Abs(ort.x) - 2f * WS_ZELLE) < 9f && Mathf.Abs(ort.z) < 9f)
			{
				continue;
			}
			if (zufall.Next(3) == 0)
			{
				KFass(deko.transform, ort, holz);
			}
			else
			{
				KKiste(deko.transform, ort, Zufall(zufall, 1f, 1.5f), Zufall(zufall, 0f, 90f), holz);
			}
		}
		wurzel.layer = 2;
		WsAusstattung(deko.transform, stoffe, holz);
		WsStaub(wurzel.transform);
		WsLandschaft(wurzel);
		UltraSpiegelung(wurzel, new Vector3(0f, 15f, 0f), new Vector3(160f, 50f, 160f));
		return AlsPrefab(wurzel, PREFABS + "/Map_Wuestenstadt.prefab");
	}

	// Zwei bis vier Haeuser in einer Zelle, mit Fenstern, Markisen, manchmal Treppe aufs Dach.
	static void WsHaeuser(Transform eltern, Vector3 mitte, System.Random zufall, Material sandstein, Material putz, Material dunkel, Material holz, Material[] stoffe)
	{
		bool geteilt = zufall.Next(2) == 0;
		var teile = geteilt
			? new[] { new Vector4(-5.5f, 0f, 9f, 20f), new Vector4(5.5f, 0f, 9f, 20f) }
			: new[] { new Vector4(0f, -5.5f, 20f, 9f), new Vector4(0f, 5.5f, 20f, 9f) };
		foreach (var t in teile)
		{
			float hoehe = Zufall(zufall, 6f, 12f);
			var material = zufall.Next(2) == 0 ? sandstein : putz;
			var haus = KGruppe(eltern, "Haus");
			haus.transform.localPosition = mitte + new Vector3(t.x, 0f, t.y);
			Klotz(haus.transform, "Koerper", new Vector3(0f, hoehe * 0.5f, 0f), new Vector3(t.z, hoehe, t.w), material);
			// Bruestung auf dem Dach, damit man dort Deckung hat.
			Klotz(haus.transform, "Bruestung", new Vector3(0f, hoehe + 0.5f, t.w * 0.5f - 0.2f), new Vector3(t.z, 1f, 0.4f), material);
			Klotz(haus.transform, "Bruestung", new Vector3(0f, hoehe + 0.5f, -t.w * 0.5f + 0.2f), new Vector3(t.z, 1f, 0.4f), material);
			// Fenster, Balken, Zinnen: ein Netz pro Haus (sonst tausende Einzelteile).
			WsHausDetails(haus, new Vector3(t.z, hoehe, t.w), material, dunkel, holz, stoffe[uZufall.Next(stoffe.Length)]);
			// Markise ueber einer Tuer
			if (zufall.Next(2) == 0)
			{
				// Mit Kollider: Man springt hoch genug, um darauf zu landen.
				Klotz(haus.transform, "Markise", new Vector3(0f, 3.2f, t.w * 0.5f + 1f), new Vector3(4f, 0.1f, 2f), stoffe[zufall.Next(stoffe.Length)], new Vector3(-18f, 0f, 0f));
				Klotz(haus.transform, "Tuer", new Vector3(0f, 1.2f, t.w * 0.5f + 0.03f), new Vector3(1.6f, 2.4f, 0.06f), holz);
			}
			// Treppe aufs Dach an der Schmalseite. Stufen niedriger als die Stufenhoehe der
			// Spielfigur (0,3 m), damit man ohne Springen hinaufgeht.
			if (hoehe < 9.5f && zufall.Next(2) == 0)
			{
				int stufen = Mathf.CeilToInt(hoehe / 0.28f);
				for (int i = 0; i < stufen; i++)
				{
					float y = (i + 1) * hoehe / stufen;
					Klotz(haus.transform, "Stufe", new Vector3(t.z * 0.5f + 1f, y * 0.5f, -t.w * 0.5f + 1f + i * (t.w - 2f) / stufen), new Vector3(2f, y, (t.w - 2f) / stufen + 0.05f), material);
				}
			}
		}
	}

	static int wsHausNummer;

	// Fenster mit Sims, Holzsturz und Laeden, Holzbalken (Vigas) unter dem Dach, Zinnen auf
	// der Bruestung. Nur zum Ansehen, ohne Kollider (die Hauswand bleibt die Deckung).
	static void WsHausDetails(GameObject haus, Vector3 groesse, Material stein, Material dunkel, Material holz, Material laeden)
	{
		const int T_STEIN = 0, T_DUNKEL = 1, T_HOLZ = 2, T_LADEN = 3;
		var n = new Netzbau(4);
		float breite = groesse.x, hoehe = groesse.y, tiefe = groesse.z;
		foreach (var seite in new[] { -1f, 1f })
		{
			float wand = seite * tiefe * 0.5f;
			for (float x = -breite * 0.5f + 2f; x < breite * 0.5f - 1f; x += 3.5f)
			{
				for (float h = 2.5f; h < hoehe - 1.5f; h += 3.5f)
				{
					// Fensteroeffnung (dunkel, knapp vor der Wand), Sims, Sturz, Laeden
					n.Quader(Matrix4x4.TRS(new Vector3(x, h, wand + seite * 0.02f), Quaternion.identity, new Vector3(1.1f, 1.5f, 0.04f)), T_DUNKEL, Color.white);
					n.Quader(Matrix4x4.TRS(new Vector3(x, h - 0.85f, wand + seite * 0.12f), Quaternion.identity, new Vector3(1.5f, 0.14f, 0.28f)), T_STEIN, Color.white);
					n.Quader(Matrix4x4.TRS(new Vector3(x, h + 0.86f, wand + seite * 0.1f), Quaternion.identity, new Vector3(1.6f, 0.2f, 0.22f)), T_HOLZ, Color.white);
					foreach (var lr in new[] { -1f, 1f })
					{
						var drehung = Quaternion.Euler(0f, lr * seite * 25f, 0f);
						var mitte = new Vector3(x + lr * 0.85f, h, wand + seite * 0.18f);
						n.Quader(Matrix4x4.TRS(mitte, drehung, new Vector3(0.55f, 1.5f, 0.05f)), T_LADEN, Color.white);
					}
				}
			}
			// Vigas: runde Balkenkoepfe unter der Dachkante
			for (float x = -breite * 0.5f + 0.6f; x < breite * 0.5f - 0.3f; x += 1.1f)
			{
				n.Rohr(new Vector3(x, hoehe - 0.35f, wand), new Vector3(x, hoehe - 0.37f, wand + seite * 0.5f), 0.09f, 0.085f, 8, T_HOLZ, Color.white, Color.white, 0f, 1f, true);
			}
			// Zinnen auf der Bruestung
			for (float x = -breite * 0.5f + 0.4f; x < breite * 0.5f - 0.2f; x += 1.2f)
			{
				n.Quader(Matrix4x4.TRS(new Vector3(x, hoehe + 1.2f, wand - seite * 0.2f), Quaternion.identity, new Vector3(0.55f, 0.4f, 0.42f)), T_STEIN, Color.white);
			}
		}
		// Umlaufendes Gesims knapp unter der Dachkante
		foreach (var seite in new[] { -1f, 1f })
		{
			n.Quader(Matrix4x4.TRS(new Vector3(0f, hoehe - 0.25f, seite * tiefe * 0.5f), Quaternion.identity, new Vector3(breite + 0.3f, 0.14f, 0.3f)), T_STEIN, Color.white);
			n.Quader(Matrix4x4.TRS(new Vector3(seite * breite * 0.5f, hoehe - 0.25f, 0f), Quaternion.identity, new Vector3(0.3f, 0.14f, tiefe + 0.3f)), T_STEIN, Color.white);
		}
		var netz = AlsAsset(n.Fertig("HausDetails"), KARTEN + "/Haus" + (wsHausNummer++) + ".asset");
		var go = new GameObject("Details");
		go.transform.SetParent(haus.transform, false);
		go.AddComponent<MeshFilter>().sharedMesh = netz;
		go.AddComponent<MeshRenderer>().sharedMaterials = new[] { stein, dunkel, holz, laeden };
	}

	// Leben in den Gassen: Pflanzkuebel an den Hauswaenden, Waescheleinen ueber den Gassen,
	// Laternen. Alles ohne Einfluss aufs Spiel (Kuebel sind niedrig, Leinen hoch).
	static void WsAusstattung(Transform eltern, Material[] stoffe, Material holz)
	{
		var ton = KFarbe("Terrakotta", new Color(0.62f, 0.33f, 0.2f), 0.15f);
		var seil = KFarbe("Waescheleine", new Color(0.55f, 0.5f, 0.42f), 0.05f);
		var glas = KLeuchten("Laternenglas", new Color(1f, 0.75f, 0.4f), 1.5f);
		var gruppe = KGruppe(eltern, "Ausstattung");
		float[] gassen = { -45f, -15f, 15f, 45f };
		for (int i = 0; i < 36; i++)
		{
			// Gasse waehlen, dann an eine der beiden Hauswaende
			float gasse = gassen[uZufall.Next(gassen.Length)];
			float entlang = Zufall(uZufall, -70f, 70f);
			float wand = gasse + (uZufall.Next(2) == 0 ? -4.4f : 4.4f);
			bool nordSued = uZufall.Next(2) == 0;
			var ort = nordSued ? new Vector3(wand, 0f, entlang) : new Vector3(entlang, 0f, wand);
			if (Mathf.Abs(Mathf.Abs(ort.x) - 2f * WS_ZELLE) < 10f && Mathf.Abs(ort.z) < 10f)
			{
				continue;
			}
			var topf = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			topf.name = "Pflanzkuebel";
			topf.transform.SetParent(gruppe.transform, false);
			topf.transform.localPosition = ort + Vector3.up * 0.35f;
			topf.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);
			topf.GetComponent<Renderer>().sharedMaterial = ton;
			KNetzKollider(topf);
			var pflanze = new GameObject("Pflanze");
			pflanze.transform.SetParent(gruppe.transform, false);
			pflanze.transform.localPosition = ort + Vector3.up * 0.65f;
			pflanze.transform.localScale = Vector3.one * Zufall(uZufall, 0.35f, 0.55f);
			pflanze.transform.localRotation = Quaternion.Euler(0f, Zufall(uZufall, 0f, 360f), 0f);
			pflanze.AddComponent<MeshFilter>().sharedMesh = uBuesche[uZufall.Next(uBuesche.Length)];
			pflanze.AddComponent<MeshRenderer>().sharedMaterials = new[] { uRinde, uBusch };
		}
		for (int i = 0; i < 14; i++)
		{
			float gasse = gassen[uZufall.Next(gassen.Length)];
			float entlang = Mathf.Round(Zufall(uZufall, -65f, 65f) / 30f) * 30f + Zufall(uZufall, -8f, 8f);
			bool nordSued = uZufall.Next(2) == 0;
			float hoehe = Zufall(uZufall, 4.5f, 6f);
			var a = nordSued ? new Vector3(gasse - 4.6f, hoehe, entlang) : new Vector3(entlang, hoehe, gasse - 4.6f);
			var b = nordSued ? new Vector3(gasse + 4.6f, hoehe, entlang) : new Vector3(entlang, hoehe, gasse + 4.6f);
			var leine = Klotz(gruppe.transform, "Waescheleine", (a + b) * 0.5f - Vector3.up * 0.15f, new Vector3(0.03f, 0.03f, 9.2f), seil);
			leine.transform.localRotation = Quaternion.LookRotation(b - a);
			Object.DestroyImmediate(leine.GetComponent<Collider>());
			int tuecher = 2 + uZufall.Next(3);
			for (int k = 0; k < tuecher; k++)
			{
				float t = (k + 1f) / (tuecher + 1f) + Zufall(uZufall, -0.05f, 0.05f);
				var punkt = Vector3.Lerp(a, b, t) - Vector3.up * (0.15f + 0.6f * Mathf.Sin(t * Mathf.PI) * 0.25f);
				var tuch = Klotz(gruppe.transform, "Tuch", punkt - Vector3.up * 0.5f, new Vector3(Zufall(uZufall, 0.7f, 1.2f), Zufall(uZufall, 0.8f, 1.3f), 0.02f), stoffe[uZufall.Next(stoffe.Length)]);
				tuch.transform.localRotation = Quaternion.LookRotation(b - a) * Quaternion.Euler(0f, 90f, Zufall(uZufall, -4f, 4f));
				Object.DestroyImmediate(tuch.GetComponent<Collider>());
			}
			// Laterne in der Mitte der Leine
			var laterne = Klotz(gruppe.transform, "Laterne", Vector3.Lerp(a, b, 0.5f) - Vector3.up * 0.55f, new Vector3(0.22f, 0.32f, 0.22f), glas);
			Object.DestroyImmediate(laterne.GetComponent<Collider>());
			var kappe = Klotz(gruppe.transform, "Laternenkappe", Vector3.Lerp(a, b, 0.5f) - Vector3.up * 0.36f, new Vector3(0.28f, 0.06f, 0.28f), holz);
			Object.DestroyImmediate(kappe.GetComponent<Collider>());
		}
	}

	// Feiner Staub, der langsam durch die Luft treibt
	static void WsStaub(Transform eltern)
	{
		DDPartikelMaterialien();
		var staub = DDPartikel(eltern, "Staub", new Vector3(0f, 4f, 0f), ddRauchMaterial);
		staub.transform.localRotation = Quaternion.identity;
		var main = staub.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
		main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.85f, 0.65f, 0.35f), new Color(1f, 0.92f, 0.75f, 0.2f));
		main.maxParticles = 900;
		var emission = staub.emission;
		emission.rateOverTime = 70f;
		var form = staub.shape;
		form.shapeType = ParticleSystemShapeType.Box;
		form.scale = new Vector3(150f, 8f, 150f);
		var rauschen = staub.noise;
		rauschen.enabled = true;
		rauschen.strength = 0.4f;
		rauschen.frequency = 0.15f;
		var geschwindigkeit = staub.velocityOverLifetime;
		geschwindigkeit.enabled = true;
		geschwindigkeit.x = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
		geschwindigkeit.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
		geschwindigkeit.z = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
		var farbe = staub.colorOverLifetime;
		farbe.enabled = true;
		farbe.color = new ParticleSystem.MinMaxGradient(Verlauf(new[] { Color.white, Color.white }, new[] { 0f, 1f, 0f }));
	}

	static void WsTorbogen(Transform eltern, Vector3 ort, float gier, Material material)
	{
		var bogen = KGruppe(eltern, "Torbogen");
		bogen.transform.localPosition = ort;
		bogen.transform.localRotation = Quaternion.Euler(0f, gier, 0f);
		Klotz(bogen.transform, "Pfeiler", new Vector3(-4.6f, 3.5f, 0f), new Vector3(1.4f, 7f, 1.6f), material);
		Klotz(bogen.transform, "Pfeiler", new Vector3(4.6f, 3.5f, 0f), new Vector3(1.4f, 7f, 1.6f), material);
		Klotz(bogen.transform, "Sturz", new Vector3(0f, 7.4f, 0f), new Vector3(10.6f, 1.4f, 1.8f), material);
		// Angedeutete Rundung aus drei schraegen Steinen
		Klotz(bogen.transform, "Rundung", new Vector3(-3.2f, 6.2f, 0f), new Vector3(2.2f, 0.8f, 1.6f), material, new Vector3(0f, 0f, -35f));
		Klotz(bogen.transform, "Rundung", new Vector3(3.2f, 6.2f, 0f), new Vector3(2.2f, 0.8f, 1.6f), material, new Vector3(0f, 0f, 35f));
	}

	static void WsMarkt(Transform eltern, Vector3 mitte, System.Random zufall, Material holz, Material[] stoffe, Material stein, Material stamm, Material blatt)
	{
		// Brunnen in der Mitte
		var becken = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		becken.name = "Brunnen";
		becken.transform.SetParent(eltern, false);
		becken.transform.localPosition = mitte + new Vector3(0f, 0.5f, 0f);
		becken.transform.localScale = new Vector3(6f, 0.5f, 6f);
		becken.GetComponent<Renderer>().sharedMaterial = stein;
		KNetzKollider(becken);
		Klotz(eltern, "Brunnensaeule", mitte + new Vector3(0f, 1.8f, 0f), new Vector3(0.8f, 2.6f, 0.8f), stein);
		// Marktstaende im Kreis
		for (int i = 0; i < 6; i++)
		{
			float winkel = i * 60f + 30f;
			var richtung = Quaternion.Euler(0f, winkel, 0f) * Vector3.forward;
			var stand = KGruppe(eltern, "Marktstand");
			stand.transform.localPosition = mitte + richtung * 10f;
			stand.transform.localRotation = Quaternion.LookRotation(-richtung);
			Klotz(stand.transform, "Tisch", new Vector3(0f, 0.5f, 0f), new Vector3(3.5f, 1f, 1.5f), holz);
			foreach (var x in new[] { -1.6f, 1.6f })
			{
				Klotz(stand.transform, "Pfosten", new Vector3(x, 1.4f, -0.8f), new Vector3(0.15f, 2.8f, 0.15f), holz);
				Klotz(stand.transform, "Pfosten", new Vector3(x, 1.2f, 0.8f), new Vector3(0.15f, 2.4f, 0.15f), holz);
			}
			Klotz(stand.transform, "Stoffdach", new Vector3(0f, 2.65f, 0f), new Vector3(3.8f, 0.08f, 2f), stoffe[i % stoffe.Length], new Vector3(-12f, 0f, 0f));
			KKiste(stand.transform, new Vector3(Zufall(zufall, -1f, 1f), 1f, 0f), 0.5f, Zufall(zufall, 0f, 45f), holz);
		}
		WsPalme(eltern, mitte + new Vector3(11f, 0f, 0f), zufall, stamm, blatt);
		WsPalme(eltern, mitte + new Vector3(-11f, 0f, 0f), zufall, stamm, blatt);
	}

	static void WsPalasthof(Transform eltern, Vector3 mitte, Material sandstein, Material putz, Material kuppel)
	{
		// Saeulengang an drei Seiten, offen nach Sueden
		for (float x = -10f; x <= 10f; x += 4f)
		{
			Klotz(eltern, "Saeule", mitte + new Vector3(x, 3f, 10f), new Vector3(1f, 6f, 1f), sandstein);
		}
		for (float z = -6f; z <= 6f; z += 4f)
		{
			Klotz(eltern, "Saeule", mitte + new Vector3(-10f, 3f, z), new Vector3(1f, 6f, 1f), sandstein);
			Klotz(eltern, "Saeule", mitte + new Vector3(10f, 3f, z), new Vector3(1f, 6f, 1f), sandstein);
		}
		Klotz(eltern, "Gangdach", mitte + new Vector3(0f, 6.3f, 11f), new Vector3(22f, 0.6f, 3f), putz);
		Klotz(eltern, "Gangdach", mitte + new Vector3(-11f, 6.3f, 0f), new Vector3(3f, 0.6f, 20f), putz);
		Klotz(eltern, "Gangdach", mitte + new Vector3(11f, 6.3f, 0f), new Vector3(3f, 0.6f, 20f), putz);
		// Kleines Kuppelhaus in der Mitte des Hofs
		Klotz(eltern, "Pavillon", mitte + new Vector3(0f, 2f, 2f), new Vector3(6f, 4f, 6f), putz);
		var halbkugel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		halbkugel.name = "Kuppel";
		halbkugel.transform.SetParent(eltern, false);
		halbkugel.transform.localPosition = mitte + new Vector3(0f, 4f, 2f);
		halbkugel.transform.localScale = new Vector3(5.5f, 4f, 5.5f);
		halbkugel.GetComponent<Renderer>().sharedMaterial = kuppel;
		KNetzKollider(halbkugel);
	}

	static void WsPodest(Transform eltern, Vector3 mitte, Material stein, Material holz, System.Random zufall)
	{
		// Erhoehte Plattform mit Rampen an zwei Seiten und Kisten darauf
		Klotz(eltern, "Podest", mitte + new Vector3(0f, 1f, 0f), new Vector3(14f, 2f, 14f), stein);
		Klotz(eltern, "Rampe", mitte + new Vector3(0f, 0.9f, -9.5f), new Vector3(5f, 0.4f, 6f), stein, new Vector3(-18f, 0f, 0f));
		Klotz(eltern, "Rampe", mitte + new Vector3(0f, 0.9f, 9.5f), new Vector3(5f, 0.4f, 6f), stein, new Vector3(18f, 0f, 0f));
		for (int i = 0; i < 5; i++)
		{
			KKiste(eltern, mitte + new Vector3(Zufall(zufall, -5f, 5f), 2f, Zufall(zufall, -5f, 5f)), Zufall(zufall, 1.1f, 1.6f), Zufall(zufall, 0f, 90f), holz);
		}
	}

	static void WsTurm(Transform eltern, Vector3 mitte, Material stein, Material kuppel)
	{
		Klotz(eltern, "Turmsockel", mitte + new Vector3(0f, 3f, 0f), new Vector3(9f, 6f, 9f), stein);
		var turm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		turm.name = "Turm";
		turm.transform.SetParent(eltern, false);
		turm.transform.localPosition = mitte + new Vector3(0f, 14f, 0f);
		turm.transform.localScale = new Vector3(4f, 9f, 4f);
		turm.GetComponent<Renderer>().sharedMaterial = stein;
		KNetzKollider(turm);
		var spitze = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		spitze.name = "Turmkuppel";
		spitze.transform.SetParent(eltern, false);
		spitze.transform.localPosition = mitte + new Vector3(0f, 23.5f, 0f);
		spitze.transform.localScale = new Vector3(4.6f, 4f, 4.6f);
		spitze.GetComponent<Renderer>().sharedMaterial = kuppel;
		KNetzKollider(spitze);
	}

	static void WsPalme(Transform eltern, Vector3 fuss, System.Random zufall, Material stamm, Material blatt)
	{
		var palme = KGruppe(eltern, "Palme");
		palme.transform.localPosition = fuss;
		float hoehe = Zufall(zufall, 7f, 10f);
		float neigung = Zufall(zufall, -8f, 8f);
		for (int i = 0; i < 5; i++)
		{
			var stueck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			stueck.name = "Stamm";
			if (i > 0)
			{
				Object.DestroyImmediate(stueck.GetComponent<Collider>());
			}
			stueck.transform.SetParent(palme.transform, false);
			stueck.transform.localPosition = new Vector3(neigung * 0.05f * i, hoehe * (i + 0.5f) / 5f, 0f);
			stueck.transform.localScale = new Vector3(0.55f - i * 0.05f, hoehe / 10f, 0.55f - i * 0.05f);
			stueck.GetComponent<Renderer>().sharedMaterial = stamm;
		}
		var krone = new Vector3(neigung * 0.25f, hoehe, 0f);
		for (int i = 0; i < 7; i++)
		{
			var wedel = Klotz(palme.transform, "Wedel", krone, new Vector3(0.9f, 0.08f, 4.2f), blatt);
			Object.DestroyImmediate(wedel.GetComponent<Collider>());
			wedel.transform.localRotation = Quaternion.Euler(0f, i * 360f / 7f, 0f) * Quaternion.Euler(25f, 0f, 0f) * Quaternion.Euler(0f, 0f, 0f);
			wedel.transform.localPosition = krone + wedel.transform.localRotation * new Vector3(0f, 0f, 1.8f);
		}
		UltraPalme(palme, krone, Zufall(uZufall, 0f, 360f));
	}

	// ------------------------------------------------------------------
	// Map 5: Tunnel (Netz aus Gaengen, eine Halle in der Mitte)

	const int TU_ZELLEN = 7;

	const float TU_ABSTAND = 16f;

	const float TU_BREITE = 6f;

	const float TU_HOEHE = 5f;

	static GameObject TunnelKarte()
	{
		var zufall = new System.Random(5151);
		var beton = KRaster("Beton", (u, v) =>
		{
			// Schalbeton: Platten mit Fugen, Ankerloecher, Wasserflecken, die nach unten laufen.
			float px = u * 2f;
			float py = v * 2f;
			float fx = px - Mathf.Floor(px);
			float fy = py - Mathf.Floor(py);
			float fuge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
			float w = KWolke(u, v, 31);
			float flecken = Mathf.Pow(KWolke(u, Mathf.Repeat(v * 0.25f, 1f), 37), 3f);
			float loch = 1f;
			foreach (var a in new[] { new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.2f), new Vector2(0.2f, 0.8f), new Vector2(0.8f, 0.8f) })
			{
				loch = Mathf.Min(loch, Mathf.SmoothStep(0.015f, 0.03f, new Vector2(fx - a.x, fy - a.y).magnitude));
			}
			var farbe = new Color(0.52f, 0.52f, 0.5f) * (0.78f + 0.3f * w - 0.3f * flecken);
			farbe *= Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0.004f, 0.012f, fuge));
			return farbe * Mathf.Lerp(0.35f, 1f, loch);
		}, Color.white, 0.22f, 0.15f, 7f);
		var boden = KRaster("Tunnelboden", (u, v) =>
		{
			// Estrich mit Dehnfugen, Oelflecken und einer gelben Mittellinie.
			float fy = Mathf.Repeat(v * 3f, 1f);
			float fuge = Mathf.Min(fy, 1f - fy);
			float w = KWolke(u, v, 41);
			float oel = Mathf.Pow(KWolke(Mathf.Repeat(u * 2f, 1f), Mathf.Repeat(v * 2f, 1f), 43), 4f);
			float streifen = Mathf.Abs(u - 0.5f) < 0.012f ? 0.6f : 0f;
			var farbe = new Color(0.32f, 0.32f, 0.31f) * (0.8f + 0.3f * w - 0.5f * oel) + new Color(0.5f, 0.45f, 0.1f) * streifen;
			return farbe * Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0.004f, 0.012f, fuge));
		}, Color.white, 0.18f, 0.3f, 6f);
		var rohr = KFarbe("Rohr", new Color(0.35f, 0.22f, 0.15f), 0.4f);
		var holz = KFarbe("TunnelHolz", new Color(0.4f, 0.3f, 0.2f), 0.1f);
		var birne = KLeuchten("Gluehbirne", new Color(1f, 0.85f, 0.55f), 3f);
		var sockel = KFarbe("Sockel", new Color(0.16f, 0.16f, 0.17f), 0.2f);
		var rost = KFarbe("Gitterrost", new Color(0.22f, 0.17f, 0.13f), 0.45f);
		rost.SetFloat("_Metallic", 0.6f);
		EditorUtility.SetDirty(rost);
		var pfuetze = KFarbe("Pfuetze", new Color(0.05f, 0.05f, 0.06f), 0.97f);

		var wurzel = KWurzel("Tunnel", new Color(0.04f, 0.04f, 0.05f), 0.012f, 0.15f, 0f);
		// Taschenlampe an jeder Figur. Falls man doch irgendwo durchfaellt: neu erscheinen.
		var regeln = wurzel.AddComponent<KartenRegeln>();
		regeln.taschenlampe = true;
		regeln.absturzHoehe = -20f;
		var bau = KGruppe(wurzel.transform, "Gaenge");
		var deko = KGruppe(wurzel.transform, "Deko");

		// Labyrinth per Tiefensuche, dann ein Drittel Extra-Verbindungen fuer Rundwege.
		var offen = new bool[TU_ZELLEN, TU_ZELLEN, 4];
		var besucht = new bool[TU_ZELLEN, TU_ZELLEN];
		int[] dx = { 1, 0, -1, 0 };
		int[] dz = { 0, 1, 0, -1 };
		var stapel = new Stack<Vector2Int>();
		stapel.Push(new Vector2Int(0, 0));
		besucht[0, 0] = true;
		while (stapel.Count > 0)
		{
			var hier = stapel.Peek();
			var moeglich = new List<int>();
			for (int r = 0; r < 4; r++)
			{
				int nx = hier.x + dx[r];
				int nz = hier.y + dz[r];
				if (nx >= 0 && nz >= 0 && nx < TU_ZELLEN && nz < TU_ZELLEN && !besucht[nx, nz])
				{
					moeglich.Add(r);
				}
			}
			if (moeglich.Count == 0)
			{
				stapel.Pop();
				continue;
			}
			int wahl = moeglich[zufall.Next(moeglich.Count)];
			offen[hier.x, hier.y, wahl] = true;
			offen[hier.x + dx[wahl], hier.y + dz[wahl], (wahl + 2) % 4] = true;
			besucht[hier.x + dx[wahl], hier.y + dz[wahl]] = true;
			stapel.Push(new Vector2Int(hier.x + dx[wahl], hier.y + dz[wahl]));
		}
		for (int x = 0; x < TU_ZELLEN; x++)
		{
			for (int z = 0; z < TU_ZELLEN; z++)
			{
				for (int r = 0; r < 2; r++)
				{
					int nx = x + dx[r];
					int nz = z + dz[r];
					if (nx < TU_ZELLEN && nz < TU_ZELLEN && zufall.Next(3) == 0)
					{
						offen[x, z, r] = true;
						offen[nx, nz, (r + 2) % 4] = true;
					}
				}
			}
		}
		// Die Halle in der Mitte hat Ausgaenge in alle Richtungen.
		int m = TU_ZELLEN / 2;
		for (int r = 0; r < 4; r++)
		{
			offen[m, m, r] = true;
			offen[m + dx[r], m + dz[r], (r + 2) % 4] = true;
		}

		float versatz = (TU_ZELLEN - 1) * TU_ABSTAND * 0.5f;
		for (int x = 0; x < TU_ZELLEN; x++)
		{
			for (int z = 0; z < TU_ZELLEN; z++)
			{
				var mitte = new Vector3(x * TU_ABSTAND - versatz, 0f, z * TU_ABSTAND - versatz);
				bool halle = x == m && z == m;
				float knoten = halle ? TU_ABSTAND - 2f : TU_BREITE;
				float hoehe = halle ? 9f : TU_HOEHE;
				// Boden und Decke des Knotens
				Klotz(bau.transform, "Boden", mitte + new Vector3(0f, -0.25f, 0f), new Vector3(knoten, 0.5f, knoten), boden);
				Klotz(bau.transform, "Decke", mitte + new Vector3(0f, hoehe + 0.25f, 0f), new Vector3(knoten, 0.5f, knoten), beton);
				for (int r = 0; r < 4; r++)
				{
					var richtung = new Vector3(dx[r], 0f, dz[r]);
					var quer = new Vector3(-dz[r], 0f, dx[r]);
					if (!offen[x, z, r])
					{
						// Geschlossene Seite: Wand
						Klotz(bau.transform, "Wand", mitte + richtung * (knoten * 0.5f + 0.25f) + Vector3.up * hoehe * 0.5f, abs(quer) * (knoten + 1f) + abs(richtung) * 0.5f + Vector3.up * hoehe, beton);
						continue;
					}
					if (halle)
					{
						// Halle: Wandstuecke neben der Oeffnung, darueber ein Sturz
						float seite = (knoten - TU_BREITE) * 0.5f;
						foreach (var s in new[] { -1f, 1f })
						{
							Klotz(bau.transform, "Hallenwand", mitte + richtung * (knoten * 0.5f + 0.25f) + quer * s * (TU_BREITE * 0.5f + seite * 0.5f) + Vector3.up * hoehe * 0.5f, abs(quer) * seite + abs(richtung) * 0.5f + Vector3.up * hoehe, beton);
						}
						Klotz(bau.transform, "Sturz", mitte + richtung * (knoten * 0.5f + 0.25f) + Vector3.up * (TU_HOEHE + (hoehe - TU_HOEHE) * 0.5f), abs(quer) * TU_BREITE + abs(richtung) * 0.5f + Vector3.up * (hoehe - TU_HOEHE), beton);
					}
					// Gang zum Nachbarn (nur nach +x und +z, sonst doppelt)
					if (r < 2)
					{
						bool nachbarHalle = x + dx[r] == m && z + dz[r] == m;
						float startAbstand = knoten * 0.5f;
						float endAbstand = TU_ABSTAND - (nachbarHalle ? (TU_ABSTAND - 2f) * 0.5f : TU_BREITE * 0.5f);
						float laenge = endAbstand - startAbstand;
						if (laenge <= 0.01f)
						{
							continue;
						}
						var gangMitte = mitte + richtung * (startAbstand + laenge * 0.5f);
						Klotz(bau.transform, "Gangboden", gangMitte + new Vector3(0f, -0.25f, 0f), abs(richtung) * laenge + abs(quer) * TU_BREITE + Vector3.up * 0.5f, boden);
						Klotz(bau.transform, "Gangdecke", gangMitte + new Vector3(0f, TU_HOEHE + 0.25f, 0f), abs(richtung) * laenge + abs(quer) * TU_BREITE + Vector3.up * 0.5f, beton);
						foreach (var s in new[] { -1f, 1f })
						{
							Klotz(bau.transform, "Gangwand", gangMitte + quer * s * (TU_BREITE * 0.5f + 0.25f) + Vector3.up * TU_HOEHE * 0.5f, abs(richtung) * laenge + abs(quer) * 0.5f + Vector3.up * TU_HOEHE, beton);
							// Rohr an der Wand
							var leitung = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
							leitung.name = "Rohr";
							Object.DestroyImmediate(leitung.GetComponent<Collider>());
							leitung.transform.SetParent(deko.transform, false);
							leitung.transform.localPosition = gangMitte + quer * s * (TU_BREITE * 0.5f - 0.25f) + Vector3.up * (TU_HOEHE - 0.6f);
							leitung.transform.localRotation = Quaternion.FromToRotation(Vector3.up, richtung);
							leitung.transform.localScale = new Vector3(0.3f, laenge * 0.5f, 0.3f);
							leitung.GetComponent<Renderer>().sharedMaterial = rohr;
						}
						TuGangDetails(bau.transform, deko.transform, gangMitte, richtung, quer, laenge, beton, sockel, rost, pfuetze);
					}
				}
				// Licht: nicht in jedem Knoten, manche flackern. Das macht die Gaenge unheimlich.
				if (halle || zufall.Next(10) < 7)
				{
					KLampe(deko.transform, mitte + Vector3.up * (hoehe - 0.4f), new Color(1f, 0.8f, 0.55f), halle ? 3f : 2f, halle ? 22f : 13f, birne, !halle && zufall.Next(4) == 0);
				}
				// Deckung: Kisten und Faesser
				if (halle)
				{
					foreach (var p in new[] { new Vector3(-3.5f, 0f, -3.5f), new Vector3(3.5f, 0f, 3.5f), new Vector3(-3.5f, 0f, 3.5f), new Vector3(3.5f, 0f, -3.5f) })
					{
						Klotz(bau.transform, "Pfeiler", mitte + p + Vector3.up * hoehe * 0.5f, new Vector3(1.2f, hoehe, 1.2f), beton);
					}
					KKiste(deko.transform, mitte + new Vector3(0f, 0f, 0f), 1.6f, 20f, holz);
				}
				else if (zufall.Next(3) == 0 && !TuSpawnKnoten(x, z))
				{
					KKiste(deko.transform, mitte + new Vector3(Zufall(zufall, -1.5f, 1.5f), 0f, Zufall(zufall, -1.5f, 1.5f)), Zufall(zufall, 1f, 1.4f), Zufall(zufall, 0f, 90f), holz);
				}
				// Eckpfosten: Wo zwei Seiten offen sind, blieb an der Ecke ein Spalt zwischen den Waenden.
				float ecke = knoten * 0.5f + 0.25f;
				foreach (var e in new[] { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f) })
				{
					Klotz(bau.transform, "Eckpfosten", mitte + e * ecke + Vector3.up * hoehe * 0.5f, new Vector3(0.5f, hoehe, 0.5f), beton);
				}
			}
		}
		DDSpawnZone(wurzel.transform, new Vector3(-versatz, 1.5f, -versatz), new Vector3(4f, 0.5f, 4f), Teams.ALLIIERTE);
		DDSpawnZone(wurzel.transform, new Vector3(-versatz + TU_ABSTAND, 1.5f, -versatz), new Vector3(4f, 0.5f, 4f), Teams.ALLIIERTE);
		DDSpawnZone(wurzel.transform, new Vector3(versatz, 1.5f, versatz), new Vector3(4f, 0.5f, 4f), Teams.VERTEIDIGER);
		DDSpawnZone(wurzel.transform, new Vector3(versatz - TU_ABSTAND, 1.5f, versatz), new Vector3(4f, 0.5f, 4f), Teams.VERTEIDIGER);
		wurzel.layer = 2;
		return AlsPrefab(wurzel, PREFABS + "/Map_Tunnel.prefab");
	}

	// Ein Gang bekommt Betonrippen alle 4 m (Pfeiler an den Waenden, Balken unter der Decke),
	// dunkle Sockelleisten, eine Kabeltrasse mit Kabeln, ab und zu eine Pfuetze und einen Rost.
	static void TuGangDetails(Transform bau, Transform deko, Vector3 mitte, Vector3 richtung, Vector3 quer, float laenge, Material beton, Material sockel, Material rost, Material pfuetze)
	{
		int rippen = Mathf.Max(1, Mathf.FloorToInt(laenge / 4f));
		for (int i = 0; i < rippen; i++)
		{
			var ort = mitte + richtung * ((i + 0.5f) * laenge / rippen - laenge * 0.5f);
			foreach (var s in new[] { -1f, 1f })
			{
				Klotz(bau, "Rippe", ort + quer * s * (TU_BREITE * 0.5f - 0.12f) + Vector3.up * TU_HOEHE * 0.5f, abs(quer) * 0.25f + abs(richtung) * 0.4f + Vector3.up * TU_HOEHE, beton);
			}
			Klotz(bau, "Rippe", ort + Vector3.up * (TU_HOEHE - 0.18f), abs(quer) * TU_BREITE + abs(richtung) * 0.4f + Vector3.up * 0.36f, beton);
		}
		foreach (var s in new[] { -1f, 1f })
		{
			var leiste = Klotz(deko, "Sockel", mitte + quer * s * (TU_BREITE * 0.5f - 0.05f) + Vector3.up * 0.15f, abs(richtung) * laenge + abs(quer) * 0.1f + Vector3.up * 0.3f, sockel);
			Object.DestroyImmediate(leiste.GetComponent<Collider>());
		}
		// Kabeltrasse auf einer Seite, mit drei Kabeln
		float seite = uZufall.Next(2) == 0 ? -1f : 1f;
		var trasse = Klotz(deko, "Kabeltrasse", mitte + quer * seite * (TU_BREITE * 0.5f - 0.4f) + Vector3.up * (TU_HOEHE - 1.1f), abs(richtung) * laenge + abs(quer) * 0.4f + Vector3.up * 0.05f, rost);
		Object.DestroyImmediate(trasse.GetComponent<Collider>());
		for (int k = 0; k < 3; k++)
		{
			var kabel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			kabel.name = "Kabel";
			Object.DestroyImmediate(kabel.GetComponent<Collider>());
			kabel.transform.SetParent(deko, false);
			kabel.transform.localPosition = mitte + quer * seite * (TU_BREITE * 0.5f - 0.27f - k * 0.12f) + Vector3.up * (TU_HOEHE - 1.04f);
			kabel.transform.localRotation = Quaternion.FromToRotation(Vector3.up, richtung);
			kabel.transform.localScale = new Vector3(0.06f, laenge * 0.5f, 0.06f);
			kabel.GetComponent<Renderer>().sharedMaterial = k == 1 ? rost : pfuetze;
		}
		if (uZufall.Next(3) == 0)
		{
			var lache = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			lache.name = "Pfuetze";
			Object.DestroyImmediate(lache.GetComponent<Collider>());
			lache.transform.SetParent(deko, false);
			lache.transform.localPosition = mitte + richtung * Zufall(uZufall, -laenge * 0.3f, laenge * 0.3f) + quer * Zufall(uZufall, -1.5f, 1.5f) + Vector3.up * 0.005f;
			lache.transform.localScale = new Vector3(Zufall(uZufall, 1f, 2.2f), 0.005f, Zufall(uZufall, 0.7f, 1.6f));
			lache.GetComponent<Renderer>().sharedMaterial = pfuetze;
			lache.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		}
		if (uZufall.Next(2) == 0)
		{
			var gitter = Klotz(deko, "Rost", mitte + richtung * Zufall(uZufall, -laenge * 0.3f, laenge * 0.3f) + Vector3.up * 0.01f, abs(richtung) * 0.9f + abs(quer) * 1.2f + Vector3.up * 0.02f, rost);
			Object.DestroyImmediate(gitter.GetComponent<Collider>());
		}
	}

	// Knoten mit Spawnbereich (siehe DDSpawnZone oben): dort keine Kisten.
	static bool TuSpawnKnoten(int x, int z)
	{
		int letzte = TU_ZELLEN - 1;
		return (z == 0 && (x == 0 || x == 1)) || (z == letzte && (x == letzte || x == letzte - 1));
	}

	static Vector3 abs(Vector3 v)
	{
		return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
	}

	// ------------------------------------------------------------------
	// Map 6: Himmelsinseln (eigene Idee: schwebende Inseln, Haengebruecken, Sprungfelder)

	struct Insel
	{
		public Vector3 ort;
		public float radius;

		public Insel(float x, float y, float z, float r)
		{
			ort = new Vector3(x, y, z);
			radius = r;
		}
	}

	static GameObject HimmelsinselnKarte()
	{
		var zufall = new System.Random(6161);
		hiInselNummer = 0;
		var fels = KRaster("Inselfels", (u, v) =>
		{
			float w = KWolke(u, v, 51);
			return Color.Lerp(new Color(0.36f, 0.32f, 0.3f), new Color(0.55f, 0.5f, 0.44f), w);
		}, Color.white, 0.15f, 0.1f);
		var gras = KRaster("Inselgras", (u, v) =>
		{
			float w = KWolke(u, v, 61);
			return Color.Lerp(new Color(0.22f, 0.42f, 0.16f), new Color(0.42f, 0.6f, 0.25f), w);
		}, Color.white, 0.3f, 0.05f);
		var holz = KFarbe("Bruckenholz", new Color(0.46f, 0.33f, 0.2f), 0.1f);
		var seil = KFarbe("Seil", new Color(0.6f, 0.52f, 0.36f), 0.05f);
		var ruine = KRaster("Ruinenstein", (u, v) =>
		{
			float w = KWolke(u, v, 71);
			return new Color(0.78f, 0.76f, 0.7f) * (0.8f + 0.3f * w);
		}, Color.white, 0.3f, 0.1f);
		var feld = KLeuchten("Sprungfeld", new Color(0.2f, 0.9f, 1f), 2.5f);
		var wolke = KWolkenMaterial();

		var wurzel = KWurzel("Himmelsinseln", new Color(0.72f, 0.82f, 0.94f), 0.0012f, 1.1f, 1f);
		wurzel.AddComponent<KartenRegeln>().absturzHoehe = -25f;

		var inseln = new[]
		{
			new Insel(-70f, 10f, 0f, 14f),   // 0 Spawn Alliierte
			new Insel(-35f, 18f, -25f, 10f), // 1
			new Insel(-35f, 6f, 25f, 11f),   // 2
			new Insel(0f, 14f, 0f, 16f),     // 3 Mitte mit Ruinen
			new Insel(35f, 18f, 25f, 10f),   // 4
			new Insel(35f, 6f, -25f, 11f),   // 5
			new Insel(70f, 10f, 0f, 14f),    // 6 Spawn Verteidiger
			new Insel(0f, 32f, -42f, 8f),    // 7 hoch oben, nur per Sprungfeld
		};
		var gruppe = KGruppe(wurzel.transform, "Inseln");
		for (int i = 0; i < inseln.Length; i++)
		{
			HiInsel(gruppe.transform, inseln[i], zufall, fels, gras);
		}

		var bruecken = KGruppe(wurzel.transform, "Bruecken");
		foreach (var paar in new[] { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 4), new Vector2Int(3, 5), new Vector2Int(4, 6), new Vector2Int(5, 6) })
		{
			HiBruecke(bruecken.transform, inseln[paar.x], inseln[paar.y], holz, seil);
		}

		// Sprungfelder: Spawninseln auf die tiefen Inseln, Mitte auf die hohe Insel, hohe Insel
		// zurueck. Nachgerechnet mit der Schwerkraft des Spiels (9,81 x 4) und freiem Flug
		// (playermovment.schleudern): Landung jeweils nahe der Mitte der Zielinsel.
		var felder = KGruppe(wurzel.transform, "Sprungfelder");
		HiSprungfeld(felder.transform, inseln[0].ort + new Vector3(8f, 0f, 7f), inseln[2].ort, 27f, 20f, feld);
		HiSprungfeld(felder.transform, inseln[6].ort + new Vector3(-8f, 0f, -7f), inseln[5].ort, 27f, 20f, feld);
		HiSprungfeld(felder.transform, inseln[3].ort + new Vector3(0f, 0f, -9f), inseln[7].ort, 21f, 42f, feld);
		HiSprungfeld(felder.transform, inseln[7].ort + new Vector3(0f, 0f, 3f), inseln[3].ort, 31f, 12f, feld);

		// Ruinen in der Mitte (Deckung), Saeulen auf den anderen Inseln
		var deko = KGruppe(wurzel.transform, "Ruinen");
		for (int i = 0; i < 8; i++)
		{
			// Um 22,5 Grad versetzt, damit auf den Achsen (Sprungfelder) keine Saeule steht.
			float winkel = i * 45f + 22.5f;
			var richtung = Quaternion.Euler(0f, winkel, 0f) * Vector3.forward;
			float hoehe = i % 3 == 0 ? 2f : Zufall(zufall, 3.5f, 6f);
			Klotz(deko.transform, "Saeule", inseln[3].ort + richtung * 10f + Vector3.up * hoehe * 0.5f, new Vector3(1.4f, hoehe, 1.4f), ruine);
		}
		Klotz(deko.transform, "Mauerrest", inseln[3].ort + new Vector3(-6f, 1.2f, 4f), new Vector3(6f, 2.4f, 0.8f), ruine, new Vector3(0f, 20f, 0f));
		Klotz(deko.transform, "Mauerrest", inseln[3].ort + new Vector3(6f, 0.9f, -4f), new Vector3(5f, 1.8f, 0.8f), ruine, new Vector3(0f, -30f, 0f));
		foreach (var index in new[] { 1, 2, 4, 5 })
		{
			Klotz(deko.transform, "Stein", inseln[index].ort + new Vector3(Zufall(zufall, -4f, 4f), 1f, Zufall(zufall, -4f, 4f)), new Vector3(3f, 2f, 1f), ruine, new Vector3(0f, Zufall(zufall, 0f, 180f), 0f));
		}
		DDBueschUndBaeumeInseln(deko.transform, inseln, zufall);

		// Wolken unter den Inseln
		var wolken = KGruppe(wurzel.transform, "Wolken");
		for (int i = 0; i < 26; i++)
		{
			var kugel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			kugel.name = "Wolke";
			Object.DestroyImmediate(kugel.GetComponent<Collider>());
			kugel.transform.SetParent(wolken.transform, false);
			kugel.transform.localPosition = new Vector3(Zufall(zufall, -110f, 110f), Zufall(zufall, -18f, -8f), Zufall(zufall, -80f, 80f));
			kugel.transform.localScale = new Vector3(Zufall(zufall, 18f, 34f), Zufall(zufall, 4f, 7f), Zufall(zufall, 14f, 26f));
			var anzeige = kugel.GetComponent<Renderer>();
			anzeige.sharedMaterial = wolke;
			anzeige.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		}

		// Wasserfall von der Mitte
		HiWasserfall(wurzel.transform, inseln[3].ort + new Vector3(15.5f, 0f, 2f));

		DDSpawnZone(wurzel.transform, inseln[0].ort + Vector3.up * 2f, new Vector3(10f, 0.5f, 10f), Teams.ALLIIERTE);
		DDSpawnZone(wurzel.transform, inseln[6].ort + Vector3.up * 2f, new Vector3(10f, 0.5f, 10f), Teams.VERTEIDIGER);
		wurzel.layer = 2;
		HiUltraGras(wurzel, inseln);
		HiLandschaft(wurzel, wolke);
		UltraSpiegelung(wurzel, new Vector3(0f, 15f, 0f), new Vector3(200f, 80f, 140f));
		return AlsPrefab(wurzel, PREFABS + "/Map_Himmelsinseln.prefab");
	}

	static Material KWolkenMaterial()
	{
		var material = new Material(Shader.Find("Standard"));
		material.name = "Wolke";
		material.color = new Color(1f, 1f, 1f, 0.55f);
		material.SetFloat("_Glossiness", 0f);
		material.SetFloat("_Mode", 2f);
		material.SetOverrideTag("RenderType", "Transparent");
		material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		material.SetInt("_ZWrite", 0);
		material.EnableKeyword("_ALPHABLEND_ON");
		material.renderQueue = 3000;
		Ordner(KARTEN);
		return AlsAsset(material, KARTEN + "/Wolke.mat");
	}

	static int hiInselNummer;

	// Schwebende Insel: unregelmaessige Grasdecke (Umriss nie kleiner als der Radius, Bruecken und
	// Spawns liegen innerhalb), Kante mit Erdlippe, darunter ein zerkluefteter Felszapfen mit
	// Schichten und Vorspruengen, herabhaengende Wurzeln. Alles mit Kollider nach dem Netz.
	static void HiInsel(Transform eltern, Insel insel, System.Random zufall, Material fels, Material gras)
	{
		var go = KGruppe(eltern, "Insel");
		go.transform.localPosition = insel.ort;
		const int seiten = 48;
		const int ringe = 14;
		float r = insel.radius;
		float saat = Zufall(zufall, 0f, 100f);
		var umriss = new float[seiten];
		for (int i = 0; i < seiten; i++)
		{
			float w = i * Mathf.PI * 2f / seiten;
			umriss[i] = r * (1f + 0.09f * Mathf.PerlinNoise(Mathf.Cos(w) * 1.6f + saat, Mathf.Sin(w) * 1.6f + saat));
		}

		// Grasdecke: flach oben (begehbar), aussen eine Lippe nach unten
		var deckPunkte = new List<Vector3> { Vector3.zero };
		for (int i = 0; i < seiten; i++)
		{
			float w = i * Mathf.PI * 2f / seiten;
			deckPunkte.Add(new Vector3(Mathf.Cos(w), 0f, Mathf.Sin(w)) * umriss[i]);
		}
		for (int i = 0; i < seiten; i++)
		{
			float w = i * Mathf.PI * 2f / seiten;
			deckPunkte.Add(new Vector3(Mathf.Cos(w) * umriss[i] * 1.03f, -0.6f, Mathf.Sin(w) * umriss[i] * 1.03f));
		}
		var deckDreiecke = new List<int>();
		for (int i = 0; i < seiten; i++)
		{
			int a = 1 + i;
			int b = 1 + (i + 1) % seiten;
			deckDreiecke.AddRange(new[] { 0, b, a });
			int la = 1 + seiten + i;
			int lb = 1 + seiten + (i + 1) % seiten;
			deckDreiecke.AddRange(new[] { a, b, la, b, lb, la });
		}
		var deckUv = new List<Vector2>();
		foreach (var p in deckPunkte)
		{
			deckUv.Add(new Vector2(p.x * 0.1f, p.z * 0.1f + p.y * 0.1f));
		}
		var deck = HiNetz(go.transform, "Grasdecke", deckPunkte, deckUv, deckDreiecke, gras);
		deck.AddComponent<MeshCollider>().sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;

		// Felszapfen: Ringe nach unten, Radius schrumpft, Rauschen und Schichtkanten
		float tiefe = r * Zufall(zufall, 1.4f, 1.9f);
		var punkte = new List<Vector3>();
		var uv = new List<Vector2>();
		for (int k = 0; k <= ringe; k++)
		{
			float t = (float)k / ringe;
			float y = -0.5f - t * tiefe;
			// Schichten: alle paar Meter ein Vorsprung, dazwischen eingezogen
			float schicht = 1f + 0.07f * Mathf.Sin(t * Mathf.PI * 7f + saat);
			for (int i = 0; i < seiten; i++)
			{
				float w = i * Mathf.PI * 2f / seiten;
				float rauschen = Mathf.PerlinNoise(Mathf.Cos(w) * 2.2f + saat * 2f, t * 4f + Mathf.Sin(w) * 2.2f);
				float radius = umriss[i] * 1.02f * (1f - Mathf.Pow(t, 1.25f) * 0.93f) * (0.82f + 0.36f * rauschen) * schicht;
				if (k == 0)
				{
					radius = umriss[i] * 1.02f;
				}
				punkte.Add(new Vector3(Mathf.Cos(w) * radius, y + (rauschen - 0.5f) * 0.8f * t, Mathf.Sin(w) * radius));
				uv.Add(new Vector2((float)i / seiten * 4f, t * 3f));
			}
		}
		punkte.Add(new Vector3(Zufall(zufall, -1f, 1f), -0.5f - tiefe - r * 0.25f, Zufall(zufall, -1f, 1f)));
		uv.Add(new Vector2(0.5f, 3.5f));
		var dreiecke = new List<int>();
		for (int k = 0; k < ringe; k++)
		{
			for (int i = 0; i < seiten; i++)
			{
				int a = k * seiten + i;
				int b = k * seiten + (i + 1) % seiten;
				int c = a + seiten;
				int d = b + seiten;
				dreiecke.AddRange(new[] { a, b, c, b, d, c });
			}
		}
		int spitze = punkte.Count - 1;
		for (int i = 0; i < seiten; i++)
		{
			dreiecke.AddRange(new[] { ringe * seiten + i, ringe * seiten + (i + 1) % seiten, spitze });
		}
		var felsen = HiNetz(go.transform, "Felszapfen", punkte, uv, dreiecke, fels);
		felsen.AddComponent<MeshCollider>().sharedMesh = felsen.GetComponent<MeshFilter>().sharedMesh;

		// Herabhaengende Wurzeln an der Kante
		var wurzelNetz = new Netzbau(1);
		int wurzeln = Mathf.RoundToInt(r * 1.2f);
		for (int i = 0; i < wurzeln; i++)
		{
			int stelle = zufall.Next(seiten);
			float w = stelle * Mathf.PI * 2f / seiten;
			var start = new Vector3(Mathf.Cos(w), 0f, Mathf.Sin(w)) * umriss[stelle] * 1.0f + Vector3.down * 0.6f;
			float laenge = Zufall(zufall, 1.5f, 5f);
			var vorher = start;
			for (int g = 1; g <= 4; g++)
			{
				var naechster = start + Vector3.down * laenge * g / 4f + new Vector3(Mathf.Cos(w), 0f, Mathf.Sin(w)) * (0.15f * g + Zufall(zufall, -0.15f, 0.15f));
				wurzelNetz.Rohr(vorher, naechster, 0.06f * (1.2f - g * 0.2f), 0.06f * (1f - g * 0.2f), 5, 0, Color.white, Color.white, 0f, 1f, false);
				vorher = naechster;
			}
		}
		var wurzelGo = new GameObject("Wurzeln");
		wurzelGo.transform.SetParent(go.transform, false);
		wurzelGo.AddComponent<MeshFilter>().sharedMesh = AlsAsset(wurzelNetz.Fertig("Wurzeln"), KARTEN + "/InselWurzeln" + (hiInselNummer++) + ".asset");
		wurzelGo.AddComponent<MeshRenderer>().sharedMaterial = uRinde;
	}

	static GameObject HiNetz(Transform eltern, string name, List<Vector3> punkte, List<Vector2> uv, List<int> dreiecke, Material material)
	{
		var netz = new Mesh();
		netz.name = name;
		netz.SetVertices(punkte);
		netz.SetUVs(0, uv);
		netz.SetTriangles(dreiecke, 0);
		netz.RecalculateNormals();
		netz.RecalculateTangents();
		netz.RecalculateBounds();
		var go = new GameObject(name);
		go.transform.SetParent(eltern, false);
		go.AddComponent<MeshFilter>().sharedMesh = AlsAsset(netz, KARTEN + "/Insel" + name + (hiInselNummer++) + ".asset");
		go.AddComponent<MeshRenderer>().sharedMaterial = material;
		return go;
	}

	static void HiBruecke(Transform eltern, Insel a, Insel b, Material holz, Material seil)
	{
		var flach = b.ort - a.ort;
		flach.y = 0f;
		var richtung = flach.normalized;
		var start = a.ort + richtung * (a.radius - 1f);
		var ende = b.ort - richtung * (b.radius - 1f);
		var bruecke = KGruppe(eltern, "Bruecke");
		var quer = Vector3.Cross(Vector3.up, richtung);
		float laenge = Vector3.Distance(start, ende);
		int bretter = Mathf.CeilToInt(laenge / 0.9f);
		for (int i = 0; i <= bretter; i++)
		{
			float t = (float)i / bretter;
			// Leicht durchhaengend
			var punkt = Vector3.Lerp(start, ende, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 1.2f;
			// Bretter folgen der Neigung und ueberlappen, so entsteht eine begehbare Rampe.
			var tangente = (ende - start) + Vector3.down * Mathf.Cos(t * Mathf.PI) * Mathf.PI * 1.2f;
			var brett = Klotz(bruecke.transform, "Brett", punkt - Vector3.up * 0.1f, new Vector3(3f, 0.2f, 1.05f), holz);
			brett.transform.localRotation = Quaternion.LookRotation(tangente);
		}
		foreach (var s in new[] { -1.6f, 1.6f })
		{
			for (int i = 0; i < bretter; i += 2)
			{
				float t0 = (float)i / bretter;
				float t1 = Mathf.Min(1f, (float)(i + 2) / bretter);
				var p0 = Vector3.Lerp(start, ende, t0) + Vector3.down * Mathf.Sin(t0 * Mathf.PI) * 1.2f + quer * s + Vector3.up;
				var p1 = Vector3.Lerp(start, ende, t1) + Vector3.down * Mathf.Sin(t1 * Mathf.PI) * 1.2f + quer * s + Vector3.up;
				var stueck = Klotz(bruecke.transform, "Seil", (p0 + p1) * 0.5f, new Vector3(0.08f, 0.08f, Vector3.Distance(p0, p1)), seil);
				stueck.transform.localRotation = Quaternion.LookRotation(p1 - p0);
				// Kein Hindernis: von der Bruecke kann man fallen, das gehoert zur Map.
				Object.DestroyImmediate(stueck.GetComponent<Collider>());
			}
		}
	}

	static void HiSprungfeld(Transform eltern, Vector3 ort, Vector3 ziel, float vor, float hoch, Material material)
	{
		var feld = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		feld.name = "Sprungfeld";
		Object.DestroyImmediate(feld.GetComponent<Collider>());
		feld.transform.SetParent(eltern, false);
		feld.transform.localPosition = ort + Vector3.up * 0.05f;
		feld.transform.localScale = new Vector3(3f, 0.05f, 3f);
		feld.GetComponent<Renderer>().sharedMaterial = material;
		var richtung = ziel - ort;
		richtung.y = 0f;
		var ausloeser = KGruppe(eltern, "Ausloeser");
		ausloeser.transform.localPosition = ort + Vector3.up * 0.8f;
		ausloeser.transform.localRotation = Quaternion.LookRotation(richtung.normalized);
		var box = ausloeser.AddComponent<BoxCollider>();
		box.isTrigger = true;
		box.size = new Vector3(2.6f, 1.6f, 2.6f);
		var sprung = ausloeser.AddComponent<Sprungfeld>();
		sprung.vor = vor;
		sprung.hoch = hoch;
		// Leuchtende Funken nach oben
		DDPartikelMaterialien();
		var funken = DDPartikel(ausloeser.transform, "Funken", Vector3.zero, ddFeuerMaterial);
		var main = funken.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
		main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.9f, 1f), new Color(0.6f, 1f, 1f));
		main.maxParticles = 80;
		var emission = funken.emission;
		emission.rateOverTime = 25f;
		var form = funken.shape;
		form.shapeType = ParticleSystemShapeType.Circle;
		form.radius = 1.2f;
		funken.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
	}

	static void HiWasserfall(Transform eltern, Vector3 kante)
	{
		DDPartikelMaterialien();
		var fall = DDPartikel(eltern, "Wasserfall", kante, ddRauchMaterial);
		fall.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
		var main = fall.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 6f);
		main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.88f, 1f, 0.8f), new Color(1f, 1f, 1f, 0.6f));
		main.gravityModifier = 0.8f;
		main.maxParticles = 600;
		var emission = fall.emission;
		emission.rateOverTime = 150f;
		var form = fall.shape;
		form.shapeType = ParticleSystemShapeType.Box;
		form.scale = new Vector3(3f, 0.3f, 0.3f);
	}

	static void DDBueschUndBaeumeInseln(Transform eltern, Insel[] inseln, System.Random zufall)
	{
		var busch = AssetDatabase.LoadAssetAtPath<Material>(DDAY + "/Busch.mat");
		var laub = AssetDatabase.LoadAssetAtPath<Material>(DDAY + "/Laub.mat");
		var rinde = AssetDatabase.LoadAssetAtPath<Material>(DDAY + "/Rinde.mat");
		foreach (var insel in inseln)
		{
			for (int i = 0; i < 3; i++)
			{
				float winkel = Zufall(zufall, 0f, 360f) * Mathf.Deg2Rad;
				float abstand = Zufall(zufall, insel.radius * 0.5f, insel.radius * 0.85f);
				var ort = insel.ort + new Vector3(Mathf.Cos(winkel) * abstand, 0f, Mathf.Sin(winkel) * abstand);
				var gruppe = KGruppe(eltern, "Busch");
				gruppe.transform.localPosition = ort;
				var kugel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				kugel.name = "Laub";
				Object.DestroyImmediate(kugel.GetComponent<Collider>());
				kugel.transform.SetParent(gruppe.transform, false);
				kugel.transform.localPosition = Vector3.up * 0.5f;
				float groesse = Zufall(zufall, 0.7f, 1.2f);
				kugel.transform.localScale = new Vector3(2f, 1.4f, 2f) * groesse;
				kugel.GetComponent<Renderer>().sharedMaterial = busch;
				UltraBusch(gruppe, uZufall, groesse);
			}
			if (insel.radius >= 11f)
			{
				var baum = KGruppe(eltern, "Baum");
				baum.transform.localPosition = insel.ort + new Vector3(insel.radius * 0.5f, 0f, -insel.radius * 0.4f);
				var stamm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				stamm.name = "Stamm";
				stamm.transform.SetParent(baum.transform, false);
				stamm.transform.localPosition = new Vector3(0f, 2.2f, 0f);
				stamm.transform.localScale = new Vector3(0.4f, 2.2f, 0.4f);
				stamm.GetComponent<Renderer>().sharedMaterial = rinde;
				var krone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				krone.name = "Krone";
				Object.DestroyImmediate(krone.GetComponent<Collider>());
				krone.transform.SetParent(baum.transform, false);
				krone.transform.localPosition = new Vector3(0f, 5f, 0f);
				krone.transform.localScale = Vector3.one * 4f;
				krone.GetComponent<Renderer>().sharedMaterial = laub;
				UltraBaum(baum, uZufall, 0.85f);
			}
		}
	}

	// Grashalme auf den Inseln (Stufe "Ultra"), dicht, bis knapp an den Rand.
	static void HiUltraGras(GameObject wurzel, Insel[] inseln)
	{
		var orte = new List<Vector3>();
		var normalen = new List<Vector3>();
		var groessen = new List<float>();
		foreach (var insel in inseln)
		{
			int anzahl = Mathf.RoundToInt(Mathf.PI * insel.radius * insel.radius * 5.7f);
			for (int i = 0; i < anzahl; i++)
			{
				float winkel = Zufall(uZufall, 0f, Mathf.PI * 2f);
				float abstand = Mathf.Sqrt((float)uZufall.NextDouble()) * (insel.radius - 0.8f);
				orte.Add(insel.ort + new Vector3(Mathf.Cos(winkel) * abstand, -0.02f, Mathf.Sin(winkel) * abstand));
				normalen.Add(Vector3.up);
				groessen.Add(Zufall(uZufall, 0.75f, 1.25f));
			}
		}
		UltraGras(wurzel, "GrasHimmelsinseln", orte, normalen, groessen);
	}

	// ------------------------------------------------------------------
	// Vorschaubilder fuer die Host-Fenster

	static Sprite KarteVorschauRendern(GameObject karte, string pfad, Vector3 kamera, Vector3 blick, string[] ausblenden)
	{
		var instanz = (GameObject)PrefabUtility.InstantiatePrefab(karte);
		// Teile ausblenden, die die Sicht verdecken (Decken im Tunnel)
		if (ausblenden != null)
		{
			foreach (var anzeige in instanz.GetComponentsInChildren<Renderer>(true))
			{
				foreach (var name in ausblenden)
				{
					if (anzeige.name.StartsWith(name))
					{
						anzeige.enabled = false;
					}
				}
			}
		}
		var lichtGo = new GameObject("VorschauLicht");
		var licht = lichtGo.AddComponent<Light>();
		licht.type = LightType.Directional;
		licht.intensity = 1.1f;
		licht.shadows = LightShadows.Soft;
		lichtGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
		var kameraGo = new GameObject("VorschauKamera");
		kameraGo.transform.position = kamera;
		kameraGo.transform.LookAt(blick);
		var kam = kameraGo.AddComponent<Camera>();
		kam.fieldOfView = 55f;
		kam.farClipPlane = 800f;
		kam.clearFlags = CameraClearFlags.Skybox;

		const int breite = 1024;
		const int hoehe = 576;
		var ziel = new RenderTexture(breite, hoehe, 24);
		kam.targetTexture = ziel;
		kam.Render();
		var vorher = RenderTexture.active;
		RenderTexture.active = ziel;
		var bild = new Texture2D(breite, hoehe, TextureFormat.RGB24, false);
		bild.ReadPixels(new Rect(0, 0, breite, hoehe), 0, 0);
		bild.Apply();
		RenderTexture.active = vorher;
		kam.targetTexture = null;
		var png = bild.EncodeToPNG();
		Object.DestroyImmediate(bild);
		Object.DestroyImmediate(ziel);
		Object.DestroyImmediate(kameraGo);
		Object.DestroyImmediate(lichtGo);
		Object.DestroyImmediate(instanz);
		return PngAlsSprite(pfad, png);
	}
}
