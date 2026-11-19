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
	static Material KRaster(string name, byte[] png, Color farbe, float kachelnProMeter, float glanz)
	{
		Ordner(KARTEN);
		// Rueckfall auf Standard, falls der eigene Shader nicht uebersetzt werden konnte.
		var shader = Shader.Find("Shootout/Weltraster") ?? Shader.Find("Standard");
		var material = new Material(shader);
		material.name = name;
		material.mainTexture = PngAlsTextur(KARTEN + "/" + name + ".png", png, TextureWrapMode.Repeat);
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
	// Map 4: Wuestenstadt (eigener Grundriss im Stil einer Wuestenstadt)

	const float WS_ZELLE = 30f;

	static GameObject WuestenstadtKarte()
	{
		var zufall = new System.Random(4141);
		var sandstein = KRaster("Sandstein", KTextur(256, (u, v) =>
		{
			float w = KWolke(u, v, 11);
			float baender = 0.04f * Mathf.Sin(v * Mathf.PI * 16f);
			return new Color(0.86f, 0.74f, 0.55f) * (0.82f + 0.3f * w + baender);
		}), Color.white, 0.25f, 0.08f);
		var putz = KRaster("Putz", KTextur(256, (u, v) =>
		{
			float w = KWolke(u, v, 23);
			return Color.Lerp(new Color(0.93f, 0.87f, 0.74f), new Color(0.8f, 0.7f, 0.55f), Mathf.Pow(w, 2.2f));
		}), Color.white, 0.2f, 0.05f);
		var pflaster = KRaster("Pflaster", KTextur(256, (u, v) =>
		{
			// Steinplatten: Fugen alle 1/4, jede Platte etwas anders.
			float px = u * 4f;
			float py = v * 4f;
			float fuge = Mathf.Min(Mathf.Min(px - Mathf.Floor(px), Mathf.Ceil(px) - px), Mathf.Min(py - Mathf.Floor(py), Mathf.Ceil(py) - py));
			float platte = KWert(Mathf.FloorToInt(px), Mathf.FloorToInt(py), 4, 5);
			Color farbe = new Color(0.78f, 0.68f, 0.52f) * (0.85f + 0.2f * platte + 0.1f * KWolke(u, v, 7));
			return fuge < 0.04f ? farbe * 0.65f : farbe;
		}), Color.white, 0.5f, 0.12f);
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

		var wurzel = KWurzel("Wuestenstadt", new Color(0.86f, 0.78f, 0.64f), 0.004f, 1f, 1f);
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
			// Fenster auf den langen Seiten (dunkle Flaechen, leicht vorgesetzt)
			for (float s = -t.z * 0.5f + 2f; s < t.z * 0.5f - 1f; s += 3.5f)
			{
				for (float h = 2.5f; h < hoehe - 1f; h += 3.5f)
				{
					var fenster = Klotz(haus.transform, "Fenster", new Vector3(s, h, t.w * 0.5f + 0.03f), new Vector3(1.2f, 1.6f, 0.06f), dunkel);
					Object.DestroyImmediate(fenster.GetComponent<Collider>());
					fenster = Klotz(haus.transform, "Fenster", new Vector3(s, h, -t.w * 0.5f - 0.03f), new Vector3(1.2f, 1.6f, 0.06f), dunkel);
					Object.DestroyImmediate(fenster.GetComponent<Collider>());
				}
			}
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
		var beton = KRaster("Beton", KTextur(256, (u, v) =>
		{
			float w = KWolke(u, v, 31);
			float flecken = Mathf.Pow(KWolke(u + 0.37f, v, 37), 3f);
			return new Color(0.5f, 0.5f, 0.48f) * (0.75f + 0.35f * w - 0.25f * flecken);
		}), Color.white, 0.22f, 0.15f);
		var boden = KRaster("Tunnelboden", KTextur(256, (u, v) =>
		{
			float w = KWolke(u, v, 41);
			float streifen = Mathf.Abs(u - 0.5f) < 0.01f ? 0.6f : 0f;
			return new Color(0.3f, 0.3f, 0.29f) * (0.8f + 0.3f * w) + new Color(0.5f, 0.45f, 0.1f) * streifen;
		}), Color.white, 0.18f, 0.3f);
		var rohr = KFarbe("Rohr", new Color(0.35f, 0.22f, 0.15f), 0.4f);
		var holz = KFarbe("TunnelHolz", new Color(0.4f, 0.3f, 0.2f), 0.1f);
		var birne = KLeuchten("Gluehbirne", new Color(1f, 0.85f, 0.55f), 3f);

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
		var fels = KRaster("Inselfels", KTextur(256, (u, v) =>
		{
			float w = KWolke(u, v, 51);
			return Color.Lerp(new Color(0.36f, 0.32f, 0.3f), new Color(0.55f, 0.5f, 0.44f), w);
		}), Color.white, 0.15f, 0.1f);
		var gras = KRaster("Inselgras", KTextur(256, (u, v) =>
		{
			float w = KWolke(u, v, 61);
			return Color.Lerp(new Color(0.22f, 0.42f, 0.16f), new Color(0.42f, 0.6f, 0.25f), w);
		}), Color.white, 0.3f, 0.05f);
		var holz = KFarbe("Bruckenholz", new Color(0.46f, 0.33f, 0.2f), 0.1f);
		var seil = KFarbe("Seil", new Color(0.6f, 0.52f, 0.36f), 0.05f);
		var ruine = KRaster("Ruinenstein", KTextur(256, (u, v) =>
		{
			float w = KWolke(u, v, 71);
			return new Color(0.78f, 0.76f, 0.7f) * (0.8f + 0.3f * w);
		}), Color.white, 0.3f, 0.1f);
		var feld = KLeuchten("Sprungfeld", new Color(0.2f, 0.9f, 1f), 2.5f);
		var wolke = KWolkenMaterial();

		var wurzel = KWurzel("Himmelsinseln", new Color(0.72f, 0.82f, 0.94f), 0.003f, 1.1f, 1f);
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

	static void HiInsel(Transform eltern, Insel insel, System.Random zufall, Material fels, Material gras)
	{
		var go = KGruppe(eltern, "Insel");
		go.transform.localPosition = insel.ort;
		// Grasdeckel, begehbar
		var deckel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		deckel.name = "Grasdeckel";
		deckel.transform.SetParent(go.transform, false);
		deckel.transform.localPosition = new Vector3(0f, -0.5f, 0f);
		deckel.transform.localScale = new Vector3(insel.radius * 2f, 0.5f, insel.radius * 2f);
		deckel.GetComponent<Renderer>().sharedMaterial = gras;
		// Die Kapsel des Zylinders ist oben rund, darauf stuende man schief. Deshalb ein flaches Netz.
		KNetzKollider(deckel);
		// Felsen darunter, nach unten schmaler
		float r = insel.radius;
		for (int stufe = 0; stufe < 4; stufe++)
		{
			var kugel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			kugel.name = "Fels";
			kugel.transform.SetParent(go.transform, false);
			float breite = r * 2f * (1f - stufe * 0.22f);
			float halbeHoehe = r * 0.4f;
			// Oberseite knapp unter dem Gras. Vorher ragten die Felsen als graue Kuppeln ueber
			// die Insel, ohne Kollider: Man fiel hindurch.
			kugel.transform.localPosition = new Vector3(Zufall(zufall, -0.5f, 0.5f), -0.6f - halbeHoehe - stufe * r * 0.35f, Zufall(zufall, -0.5f, 0.5f));
			kugel.transform.localScale = new Vector3(breite, halbeHoehe * 2f, breite);
			kugel.GetComponent<Renderer>().sharedMaterial = fels;
			KNetzKollider(kugel);
		}
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
			int anzahl = Mathf.RoundToInt(Mathf.PI * insel.radius * insel.radius * 1.8f);
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
