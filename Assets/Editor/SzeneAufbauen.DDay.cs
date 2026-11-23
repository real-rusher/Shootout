using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Teil des Editor-Skripts: die D-Day-Map. Alles wird aus einfachen Formen und selbst
// erzeugten Texturen gebaut, ohne fremde Grafiken. Zufall mit festem Startwert, damit jeder
// Aufbau dieselbe Map ergibt (sonst unterschieden sich Host und Client).
//
// Aufbau von Sued nach Nord (z): Meer mit Landungsbooten (Alliierte spawnen hier), Strand mit
// Panzerigeln und Pfaehlen, Kieswall, Steilhang mit drei Schluchten als Wegen nach oben,
// Hochebene mit Bunkern, Graeben und Baeumen (Verteidiger spawnen hinten).
public static partial class SzeneAufbauen
{
	const string DDAY = ERZEUGT + "/DDay";

	// Partikelmaterialien, auch fuer den Todeseffekt am Spieler.
	const string EFFEKTE = ERZEUGT + "/Effekte";

	const float DD_XMIN = -100f;

	const float DD_XMAX = 100f;

	const float DD_ZMIN = -110f;

	const float DD_ZMAX = 110f;

	const float DD_RASTER = 2f;

	// Hoehe der Hochebene ueber dem Wasser.
	const float DD_PLATEAU = 20f;

	// x-Lage der drei Schluchten (flacher Weg nach oben).
	static readonly float[] DD_SCHLUCHTEN = { -55f, 5f, 60f };

	// Bunker an der Hangkante, nicht in den Schluchten.
	static readonly float[] DD_BUNKER_X = { -28f, 32f, 85f };

	const float DD_BUNKER_Z = 50f;

	// Spawnbereiche der Verteidiger (x), hinten auf der Hochebene.
	static readonly float[] DD_VERTEIDIGER_X = { -50f, 0f, 50f };

	const float DD_VERTEIDIGER_Z = 92f;

	static float Glatt(float t)
	{
		t = Mathf.Clamp01(t);
		return t * t * (3f - 2f * t);
	}

	static float DDSchlucht(float x)
	{
		float w = 0f;
		foreach (var s in DD_SCHLUCHTEN)
		{
			w = Mathf.Max(w, Mathf.Exp(-Mathf.Pow((x - s) / 10f, 2f)));
		}
		return w;
	}

	static float DDStrand(float z)
	{
		if (z < -70f) return -4.5f;
		if (z < -25f) return Mathf.Lerp(-4.5f, 0.2f, Mathf.InverseLerp(-70f, -25f, z));
		if (z < 15f) return Mathf.Lerp(0.2f, 2.4f, Mathf.InverseLerp(-25f, 15f, z));
		return 2.4f;
	}

	// 0 = Strand, 1 = Hochebene. In den Schluchten steigt es flacher an.
	static float DDHangAnteil(float x, float z)
	{
		return Mathf.Lerp(Glatt(Mathf.InverseLerp(20f, 42f, z)), Glatt(Mathf.InverseLerp(8f, 64f, z)), DDSchlucht(x));
	}

	static float DDHoehe(float x, float z)
	{
		float t = DDHangAnteil(x, z);
		float w = DDSchlucht(x);
		float h = Mathf.Lerp(DDStrand(z), DD_PLATEAU, t);
		// Die Schluchten schneiden etwas in die Hochebene ein.
		h -= w * 3.5f * Glatt(Mathf.InverseLerp(40f, 70f, z)) * (1f - Glatt(Mathf.InverseLerp(80f, 105f, z)));
		h += (Mathf.PerlinNoise(x * 0.045f + 13.1f, z * 0.045f + 7.7f) - 0.5f) * Mathf.Lerp(0.4f, 2.4f, t);
		h += (Mathf.PerlinNoise(x * 0.17f + 3.3f, z * 0.17f + 1.9f) - 0.5f) * Mathf.Lerp(0.15f, 0.6f, t);
		// Unter den Bunkern eben, sonst ragte der Boden in den Innenraum.
		foreach (var bx in DD_BUNKER_X)
		{
			// Halbe Ausdehnung 8 x 7,5 m: Bunker samt Schutzwand am Eingang (z bis +6,6)
			float d = Mathf.Max(Mathf.Abs(x - bx) / 8f, Mathf.Abs(z - DD_BUNKER_Z - 1f) / 7.5f);
			h = Mathf.Lerp(h, DD_PLATEAU - 0.2f, 1f - Glatt(Mathf.InverseLerp(1f, 1.5f, d)));
		}
		return h;
	}

	static float DDSteilheit(float x, float z)
	{
		float dx = (DDHoehe(x + 1f, z) - DDHoehe(x - 1f, z)) * 0.5f;
		float dz = (DDHoehe(x, z + 1f) - DDHoehe(x, z - 1f)) * 0.5f;
		return new Vector2(dx, dz).magnitude;
	}

	// ------------------------------------------------------------------

	static GameObject DDayKarte()
	{
		Ordner(DDAY);
		var zufall = new System.Random(1944);

		var stahl = DDMaterial("Stahl", new Color(0.33f, 0.37f, 0.34f), 0.3f);
		var stahlDunkel = DDMaterial("StahlDunkel", new Color(0.2f, 0.22f, 0.21f), 0.3f);
		var rost = DDMaterial("Rost", new Color(0.32f, 0.2f, 0.14f), 0.1f);
		var holz = DDMaterial("Holz", new Color(0.38f, 0.28f, 0.18f), 0.05f);
		var beton = DDMaterial("Beton", new Color(0.56f, 0.56f, 0.53f), 0.05f);
		var sandsack = DDMaterial("Sandsack", new Color(0.58f, 0.52f, 0.38f), 0.02f);
		var stein = DDMaterial("Stein", new Color(0.5f, 0.49f, 0.46f), 0.1f);
		var fels = DDMaterial("Fels", new Color(0.45f, 0.43f, 0.4f), 0.1f);
		var busch = DDMaterial("Busch", new Color(0.2f, 0.3f, 0.13f), 0.05f);
		var laub = DDMaterial("Laub", new Color(0.16f, 0.27f, 0.12f), 0.05f);
		var rinde = DDMaterial("Rinde", new Color(0.27f, 0.2f, 0.14f), 0.05f);
		var panzer = DDMaterial("Panzer", new Color(0.28f, 0.31f, 0.22f), 0.15f);
		var verbrannt = DDMaterial("Verbrannt", new Color(0.07f, 0.065f, 0.06f), 0.05f);
		var mine = DDMaterial("Mine", new Color(0.24f, 0.26f, 0.2f), 0.3f);
		var draht = DDMaterial("Draht", new Color(0.23f, 0.2f, 0.18f), 0.35f);
		var lampenglas = KLeuchten("Bunkerlampe", new Color(1f, 0.85f, 0.6f), 2f);
		ddBootRumpf = DDMaterial("Bootsrumpf", new Color(0.36f, 0.4f, 0.37f), 0.25f);
		ddBootInnen = DDMaterial("Bootsinnen", new Color(0.24f, 0.27f, 0.25f), 0.15f);
		ddModellNummer = 0;
		// Schalbeton wie am Atlantikwall: waagrechte Bretterabdruecke, Wasserflecken
		var bunkerBeton = KRaster("Bunkerbeton", (u, v) =>
		{
			float brett = Mathf.Repeat(v * 10f, 1f);
			float fuge = Mathf.SmoothStep(0.02f, 0.08f, Mathf.Min(brett, 1f - brett));
			float maserung = KWolke(Mathf.Repeat(u * 0.5f, 1f), Mathf.Repeat(v * 10f, 1f), 61);
			float flecken = Mathf.Pow(KWolke(u, v, 63), 3f);
			float w = 0.62f + 0.12f * maserung + 0.12f * KWolke(u, v, 65) - 0.25f * flecken;
			return new Color(w, w * 0.99f, w * 0.95f) * Mathf.Lerp(0.8f, 1f, fuge);
		}, Color.white, 0.25f, 0.08f, 5f);

		var wurzel = new GameObject("DDay-Karte");
		var atmosphaere = wurzel.AddComponent<KartenAtmosphaere>();
		atmosphaere.nebelFarbe = new Color(0.6f, 0.64f, 0.68f);
		// Duenner als frueher: Hinter der Map liegen jetzt Kueste und Berge, die sollen zu sehen sein.
		atmosphaere.nebelDichte = 0.0025f;

		// Gelaende
		var boden = new GameObject("Boden");
		boden.transform.SetParent(wurzel.transform, false);
		var bodenNetz = AlsAsset(DDBodenNetz(), DDAY + "/Boden.asset");
		boden.AddComponent<MeshFilter>().sharedMesh = bodenNetz;
		var bodenMaterial = DDMaterial("Boden", Color.white, 0.05f);
		bodenMaterial.mainTexture = PngAlsTextur(DDAY + "/Boden.png", DDBodenTextur(), TextureWrapMode.Clamp);
		BodenDetailsSetzen(bodenMaterial, new Vector2((DD_XMAX - DD_XMIN) / 2f, (DD_ZMAX - DD_ZMIN) / 2f));
		EditorUtility.SetDirty(bodenMaterial);
		boden.AddComponent<MeshRenderer>().sharedMaterial = bodenMaterial;
		boden.AddComponent<MeshCollider>().sharedMesh = bodenNetz;

		// Meer
		var meer = new GameObject("Meer");
		meer.transform.SetParent(wurzel.transform, false);
		meer.AddComponent<MeshFilter>().sharedMesh = AlsAsset(DDWasserNetz(), DDAY + "/Wasser.asset");
		meer.AddComponent<MeshRenderer>().sharedMaterial = DDWasserMaterial();
		var wasser = meer.AddComponent<Wasser>();
		wasser.hochMaterial = WasserHochMaterial(DDAY);
		wasser.unterseiteMaterial = WasserUnterseiteMaterial(DDAY);
		FernesMeer(wurzel.transform, new Rect(-170f, -260f, 340f, 248f), 4000f, meer.GetComponent<MeshRenderer>().sharedMaterial, wasser.hochMaterial);
		DDLandschaft(wurzel);

		var effekte = new GameObject("Effekte");
		effekte.transform.SetParent(wurzel.transform, false);
		DDGischt(effekte.transform);

		// Landungsboote mit Spawnbereichen der Alliierten. Zwei brennen.
		var boote = new GameObject("Landungsboote");
		boote.transform.SetParent(wurzel.transform, false);
		float[] bootX = { -75f, -45f, -15f, 15f, 45f, 75f };
		for (int i = 0; i < bootX.Length; i++)
		{
			float z = i % 2 == 0 ? -40f : -35f;
			float x = bootX[i] + Zufall(zufall, -4f, 4f);
			float y = Mathf.Max(DDHoehe(x, z) - 0.3f, -0.6f);
			DDLandungsboot(boote.transform, new Vector3(x, y, z), Zufall(zufall, -8f, 8f), i == 1 || i == 4, stahl, stahlDunkel);
		}

		// Strandhindernisse
		var hindernisse = new GameObject("Strandhindernisse");
		hindernisse.transform.SetParent(wurzel.transform, false);
		for (int i = 0; i < 38; i++)
		{
			float x = Zufall(zufall, -96f, 96f);
			float z = Zufall(zufall, -24f, 4f);
			DDPanzerigelModell(hindernisse.transform, new Vector3(x, DDHoehe(x, z), z), Zufall(zufall, 0f, 360f), rost);
		}
		for (int i = 0; i < 22; i++)
		{
			float x = Zufall(zufall, -96f, 96f);
			float z = Zufall(zufall, -31f, -20f);
			DDHolzrampe(hindernisse.transform, new Vector3(x, DDHoehe(x, z), z), Zufall(zufall, -15f, 15f), holz, mine);
		}

		// Kieswall oben am Strand, mit Luecken vor den Schluchten.
		var wall = new GameObject("Kieswall");
		wall.transform.SetParent(wurzel.transform, false);
		for (float x = -97f; x <= 97f; x += 6f)
		{
			if (DDSchlucht(x) > 0.5f)
			{
				continue;
			}
			Klotz(wall.transform, "Wall", new Vector3(x, DDHoehe(x, 11f) + 0.45f, 11f), new Vector3(6.1f, 1.1f, 1.4f), stein, new Vector3(0f, Zufall(zufall, -3f, 3f), 0f));
		}

		// Stacheldrahtrollen hinter dem Kieswall, mit Luecken
		var drahtGruppe = new GameObject("Stacheldraht");
		drahtGruppe.transform.SetParent(wurzel.transform, false);
		for (float x = -94f; x <= 94f; x += 8f)
		{
			if (DDSchlucht(x) > 0.4f || uZufall.Next(4) == 0)
			{
				continue;
			}
			DDStacheldraht(drahtGruppe.transform, new Vector3(x, DDHoehe(x, 14f), 14f), Zufall(uZufall, -6f, 6f), 6f, draht, holz);
		}

		// Brennendes Panzerwrack am Strand
		var wrack = new GameObject("Panzerwrack");
		wrack.transform.SetParent(wurzel.transform, false);
		// Leicht im Sand eingesunken
		wrack.transform.localPosition = new Vector3(24f, DDHoehe(24f, -7f) - 0.12f, -7f);
		wrack.transform.localRotation = Quaternion.Euler(0f, 35f, 5f);
		DDPanzerModell(wrack, panzer, verbrannt, stahlDunkel, rost);
		DDFeuer(wrack.transform, new Vector3(0f, 1.9f, -1.2f), 1.5f);

		// Bunker
		var bunker = new GameObject("Bunker");
		bunker.transform.SetParent(wurzel.transform, false);
		foreach (var bx in DD_BUNKER_X)
		{
			DDBunkerNeu(bunker.transform, new Vector3(bx, DD_PLATEAU - 0.2f, DD_BUNKER_Z), bunkerBeton, holz, stahlDunkel, busch, sandsack, lampenglas);
		}

		// MG-Nester oben an den Schluchten, Grabenlinie dahinter.
		var stellungen = new GameObject("Stellungen");
		stellungen.transform.SetParent(wurzel.transform, false);
		foreach (var s in DD_SCHLUCHTEN)
		{
			DDSandsackRing(stellungen.transform, new Vector3(s + 7f, 0f, 58f), 2.3f, sandsack);
			DDSandsackRing(stellungen.transform, new Vector3(s - 7f, 0f, 61f), 2.3f, sandsack);
		}
		DDGraben(stellungen.transform, sandsack);

		// Felsen am Hang (Deckung beim Aufstieg)
		var felsen = new GameObject("Felsen");
		felsen.transform.SetParent(wurzel.transform, false);
		for (int versuch = 0, anzahl = 0; versuch < 400 && anzahl < 45; versuch++)
		{
			float x = Zufall(zufall, -97f, 97f);
			float z = Zufall(zufall, 12f, 48f);
			float t = DDHangAnteil(x, z);
			if (t < 0.1f || t > 0.92f)
			{
				continue;
			}
			float r = Zufall(zufall, 0.8f, 1.7f);
			var stueck = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			stueck.name = "Fels";
			stueck.transform.SetParent(felsen.transform, false);
			stueck.transform.localPosition = new Vector3(x, DDHoehe(x, z) + r * 0.35f, z);
			stueck.transform.localScale = new Vector3(r * 2f, r * Zufall(zufall, 1.1f, 1.6f), r * 2f * Zufall(zufall, 0.8f, 1.2f));
			stueck.GetComponent<Renderer>().sharedMaterial = fels;
			anzahl++;
		}

		// Pflanzen
		var pflanzen = new GameObject("Vegetation");
		pflanzen.transform.SetParent(wurzel.transform, false);
		DDBueschUndBaeume(pflanzen.transform, zufall, busch, laub, rinde);
		DDGras(pflanzen.transform, zufall);
		// Stufe "Ultra": die alten Grasflaechen gegen einzelne Halme tauschen.
		var altesGras = new List<GameObject>();
		foreach (Transform kind in pflanzen.transform)
		{
			if (kind.name == "Gras")
			{
				altesGras.Add(kind.gameObject);
			}
		}
		UltraTausch(pflanzen, altesGras.ToArray(), new GameObject[0]);
		DDUltraGras(wurzel);

		// Spawnbereiche
		var spawns = new GameObject("Spawns Verteidiger");
		spawns.transform.SetParent(wurzel.transform, false);
		foreach (var vx in DD_VERTEIDIGER_X)
		{
			DDSpawnZone(spawns.transform, new Vector3(vx, DD_PLATEAU + 3f, DD_VERTEIDIGER_Z), new Vector3(22f, 0.5f, 10f), Teams.VERTEIDIGER);
		}

		// Wege fuer Bots: die drei Schluchten, Punkte von unten nach oben.
		var wege = new GameObject("BotWege");
		wege.transform.SetParent(wurzel.transform, false);
		wege.AddComponent<BotWege>();
		foreach (var s in DD_SCHLUCHTEN)
		{
			var schlucht = new GameObject("Schlucht " + s);
			schlucht.transform.SetParent(wege.transform, false);
			foreach (var z in new[] { -12f, 0f, 12f, 22f, 32f, 42f, 52f, 62f, 74f })
			{
				var punkt = new GameObject("Punkt");
				punkt.transform.SetParent(schlucht.transform, false);
				punkt.transform.localPosition = new Vector3(s, DDHoehe(s, z) + 1f, z);
			}
		}

		// Unsichtbare Grenzen
		var grenzen = new GameObject("Grenzen");
		grenzen.transform.SetParent(wurzel.transform, false);
		DDWand(grenzen.transform, new Vector3(DD_XMIN - 1f, 20f, 0f), new Vector3(2f, 80f, 240f));
		DDWand(grenzen.transform, new Vector3(DD_XMAX + 1f, 20f, 0f), new Vector3(2f, 80f, 240f));
		DDWand(grenzen.transform, new Vector3(0f, 20f, -72f), new Vector3(210f, 80f, 2f));
		DDWand(grenzen.transform, new Vector3(0f, 20f, DD_ZMAX + 1f), new Vector3(210f, 80f, 2f));

		// Wurzel auf "Ignore Raycast" wie die anderen Maps, die Teile bleiben treffbar.
		wurzel.layer = 2;
		UltraSpiegelung(wurzel, new Vector3(0f, 15f, 20f), new Vector3(220f, 60f, 260f));
		return AlsPrefab(wurzel, PREFABS + "/Map_DDay.prefab");
	}

	static float Zufall(System.Random zufall, float von, float bis)
	{
		return von + (float)zufall.NextDouble() * (bis - von);
	}

	static Material DDMaterial(string name, Color farbe, float glanz)
	{
		var material = new Material(Shader.Find("Standard"));
		material.name = name;
		material.color = farbe;
		material.SetFloat("_Glossiness", glanz);
		return AlsAsset(material, DDAY + "/" + name + ".mat");
	}

	static GameObject Klotz(Transform eltern, string name, Vector3 position, Vector3 groesse, Material material, Vector3 drehung = default(Vector3))
	{
		var klotz = GameObject.CreatePrimitive(PrimitiveType.Cube);
		klotz.name = name;
		klotz.transform.SetParent(eltern, false);
		klotz.transform.localPosition = position;
		klotz.transform.localRotation = Quaternion.Euler(drehung);
		klotz.transform.localScale = groesse;
		klotz.GetComponent<Renderer>().sharedMaterial = material;
		return klotz;
	}

	static void DDWand(Transform eltern, Vector3 mitte, Vector3 groesse)
	{
		var wand = new GameObject("Grenze");
		wand.transform.SetParent(eltern, false);
		wand.transform.localPosition = mitte;
		wand.AddComponent<BoxCollider>().size = groesse;
	}

	static void DDSpawnZone(Transform eltern, Vector3 mitte, Vector3 groesse, int team)
	{
		var zone = new GameObject("Spawn " + Teams.Name(team));
		zone.transform.SetParent(eltern, false);
		zone.transform.localPosition = mitte;
		var box = zone.AddComponent<BoxCollider>();
		box.isTrigger = true;
		box.size = groesse;
		zone.AddComponent<TeamSpawnZone>().team = team;
	}

	// ------------------------------------------------------------------
	// Netze und Texturen

	static Mesh DDBodenNetz()
	{
		int nx = Mathf.RoundToInt((DD_XMAX - DD_XMIN) / DD_RASTER) + 1;
		int nz = Mathf.RoundToInt((DD_ZMAX - DD_ZMIN) / DD_RASTER) + 1;
		var punkte = new Vector3[nx * nz];
		var uv = new Vector2[punkte.Length];
		for (int j = 0; j < nz; j++)
		{
			for (int i = 0; i < nx; i++)
			{
				float x = DD_XMIN + i * DD_RASTER;
				float z = DD_ZMIN + j * DD_RASTER;
				punkte[j * nx + i] = new Vector3(x, DDHoehe(x, z), z);
				uv[j * nx + i] = new Vector2((float)i / (nx - 1), (float)j / (nz - 1));
			}
		}
		var dreiecke = new int[(nx - 1) * (nz - 1) * 6];
		int d = 0;
		for (int j = 0; j < nz - 1; j++)
		{
			for (int i = 0; i < nx - 1; i++)
			{
				int a = j * nx + i;
				int b = a + 1;
				int c = a + nx;
				int e = c + 1;
				// Normalen nach oben
				dreiecke[d++] = a;
				dreiecke[d++] = c;
				dreiecke[d++] = b;
				dreiecke[d++] = b;
				dreiecke[d++] = c;
				dreiecke[d++] = e;
			}
		}
		var netz = new Mesh();
		netz.name = "DDayBoden";
		netz.vertices = punkte;
		netz.uv = uv;
		netz.triangles = dreiecke;
		netz.RecalculateNormals();
		// Fuer die Detail-Normalen (Relief) braucht das Netz Tangenten.
		netz.RecalculateTangents();
		netz.RecalculateBounds();
		return netz;
	}

	static byte[] DDBodenTextur()
	{
		// 1024: ein Bildpunkt etwa 20 cm. Feinere Details kommen aus der Detailtextur.
		const int groesse = 1024;
		var textur = new Texture2D(groesse, groesse, TextureFormat.RGB24, false);
		var punkte = new Color[groesse * groesse];
		var unterWasser = new Color(0.36f, 0.35f, 0.28f);
		var nassSand = new Color(0.52f, 0.47f, 0.36f);
		var sand = new Color(0.79f, 0.72f, 0.53f);
		var gras = new Color(0.34f, 0.44f, 0.21f);
		var erde = new Color(0.43f, 0.35f, 0.24f);
		for (int py = 0; py < groesse; py++)
		{
			for (int px = 0; px < groesse; px++)
			{
				float x = Mathf.Lerp(DD_XMIN, DD_XMAX, px / (groesse - 1f));
				float z = Mathf.Lerp(DD_ZMIN, DD_ZMAX, py / (groesse - 1f));
				float h = DDHoehe(x, z);
				Color farbe = h < -1f ? unterWasser : (h < 0.5f ? Color.Lerp(nassSand, sand, Mathf.InverseLerp(-1f, 0.5f, h)) : sand);
				float grasAnteil = DDGrasFlaeche(x, z);
				float erdeAnteil = DDErdeAnteil(x, z);
				// Gras mal saftiger, mal trockener, in grossen Flecken
				var grasHier = Color.Lerp(gras, new Color(0.46f, 0.47f, 0.25f), Mathf.PerlinNoise(x * 0.06f + 30f, z * 0.06f) * 0.8f);
				farbe = Color.Lerp(farbe, grasHier, grasAnteil);
				farbe = Color.Lerp(farbe, erde, erdeAnteil);
				farbe *= 0.88f + 0.24f * Mathf.PerlinNoise(x * 0.35f, z * 0.35f);
				farbe *= 0.94f + 0.12f * Mathf.PerlinNoise(x * 0.07f + 11f, z * 0.07f + 5f);
				farbe.a = 1f;
				punkte[py * groesse + px] = farbe;
			}
		}
		textur.SetPixels(punkte);
		textur.Apply();
		var png = textur.EncodeToPNG();
		Object.DestroyImmediate(textur);
		return png;
	}

	// Anteil Gras (0..1) an einer Stelle, wie auf der Bodentextur. Auch fuer die Grashalme.
	static float DDGrasFlaeche(float x, float z)
	{
		float t = DDHangAnteil(x, z);
		float steil = DDSteilheit(x, z);
		float gras = Glatt(Mathf.InverseLerp(0.45f, 0.75f, t)) * (1f - Glatt(Mathf.InverseLerp(0.8f, 1.3f, steil)));
		return gras * (1f - DDErdeAnteil(x, z));
	}

	// Offene Erde: steile Haenge und ausgetretene Wege in den Schluchten.
	static float DDErdeAnteil(float x, float z)
	{
		float t = DDHangAnteil(x, z);
		float steil = DDSteilheit(x, z);
		float w = DDSchlucht(x);
		float erde = Glatt(Mathf.InverseLerp(0.7f, 1.2f, steil)) * Glatt(Mathf.InverseLerp(0.15f, 0.4f, t));
		float weg = 0.75f * Glatt(Mathf.InverseLerp(0.55f, 0.85f, w)) * Glatt(Mathf.InverseLerp(0.03f, 0.15f, t)) * (1f - Glatt(Mathf.InverseLerp(0.9f, 1f, t)));
		return Mathf.Max(erde, weg);
	}

	static Texture2D PngAlsTextur(string pfad, byte[] png, TextureWrapMode wrap)
	{
		File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, pfad), png);
		AssetDatabase.ImportAsset(pfad, ImportAssetOptions.ForceUpdate);
		var importer = (TextureImporter)AssetImporter.GetAtPath(pfad);
		importer.textureType = TextureImporterType.Default;
		importer.wrapMode = wrap;
		importer.alphaIsTransparency = true;
		importer.maxTextureSize = 1024;
		importer.SaveAndReimport();
		return AssetDatabase.LoadAssetAtPath<Texture2D>(pfad);
	}

	static Mesh DDWasserNetz()
	{
		// 4 m: fein genug, dass die grossen Wellen (14 m) als Wellen zu sehen sind.
		const float schritt = 4f;
		const float xVon = -170f;
		const float xBis = 170f;
		const float zVon = -260f;
		const float zBis = -12f;
		int nx = Mathf.RoundToInt((xBis - xVon) / schritt) + 1;
		int nz = Mathf.RoundToInt((zBis - zVon) / schritt) + 1;
		var punkte = new Vector3[nx * nz];
		for (int j = 0; j < nz; j++)
		{
			for (int i = 0; i < nx; i++)
			{
				punkte[j * nx + i] = new Vector3(xVon + i * schritt, 0f, zVon + j * schritt);
			}
		}
		var dreiecke = new int[(nx - 1) * (nz - 1) * 6];
		int d = 0;
		for (int j = 0; j < nz - 1; j++)
		{
			for (int i = 0; i < nx - 1; i++)
			{
				int a = j * nx + i;
				int b = a + 1;
				int c = a + nx;
				int e = c + 1;
				dreiecke[d++] = a;
				dreiecke[d++] = c;
				dreiecke[d++] = b;
				dreiecke[d++] = b;
				dreiecke[d++] = c;
				dreiecke[d++] = e;
			}
		}
		var netz = new Mesh();
		netz.name = "DDayWasser";
		netz.vertices = punkte;
		netz.triangles = dreiecke;
		netz.RecalculateNormals();
		// Tangenten fuer den Wasser-Shader (Kraeuselung pro Pixel): x nach rechts, z nach vorn.
		var tangenten = new Vector4[punkte.Length];
		for (int i = 0; i < tangenten.Length; i++)
		{
			tangenten[i] = new Vector4(1f, 0f, 0f, -1f);
		}
		netz.tangents = tangenten;
		// Die Wellen bewegen die Punkte, die Grenzen deshalb etwas hoeher.
		netz.bounds = new Bounds(new Vector3(0f, 0f, (zVon + zBis) * 0.5f), new Vector3(xBis - xVon, 2f, zBis - zVon));
		return netz;
	}

	static Material DDWasserMaterial()
	{
		// Eigener Shader mit Wellen und Kraeuselung. Fehlt er, der alte Weg: Standard im Modus Fade.
		var shader = Shader.Find("Shootout/Wasser");
		Material material;
		if (shader != null)
		{
			material = new Material(shader);
			material.name = "Wasser";
			material.SetColor("_Farbe", new Color(0.2f, 0.4f, 0.45f, 0.88f));
			material.SetColor("_Tiefe", new Color(0.05f, 0.18f, 0.22f, 0.65f));
			material.SetFloat("_Glanz", 0.8f);
			return AlsAsset(material, DDAY + "/Wasser.mat");
		}
		material = new Material(Shader.Find("Standard"));
		material.name = "Wasser";
		material.color = new Color(0.12f, 0.32f, 0.38f, 0.72f);
		material.SetFloat("_Glossiness", 0.85f);
		material.SetFloat("_Mode", 2f);
		material.SetOverrideTag("RenderType", "Transparent");
		material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		material.SetInt("_ZWrite", 0);
		material.DisableKeyword("_ALPHATEST_ON");
		material.EnableKeyword("_ALPHABLEND_ON");
		material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		material.renderQueue = 3000;
		return AlsAsset(material, DDAY + "/Wasser.mat");
	}

	// ------------------------------------------------------------------
	// Bauteile

	static Material ddBootRumpf;
	static Material ddBootInnen;

	static void DDLandungsboot(Transform eltern, Vector3 position, float gier, bool brennt, Material stahl, Material stahlDunkel)
	{
		var boot = new GameObject("Landungsboot");
		boot.transform.SetParent(eltern, false);
		boot.transform.localPosition = position;
		boot.transform.localRotation = Quaternion.Euler(0f, gier, 0f);
		Klotz(boot.transform, "Boden", new Vector3(0f, 0.15f, 0f), new Vector3(3.6f, 0.3f, 10f), stahl);
		Klotz(boot.transform, "WandLinks", new Vector3(-1.675f, 1.1f, 0f), new Vector3(0.25f, 2.2f, 10f), stahl);
		Klotz(boot.transform, "WandRechts", new Vector3(1.675f, 1.1f, 0f), new Vector3(0.25f, 2.2f, 10f), stahl);
		Klotz(boot.transform, "Heck", new Vector3(0f, 1.1f, -4.875f), new Vector3(3.6f, 2.2f, 0.25f), stahl);
		Klotz(boot.transform, "Steuerstand", new Vector3(0f, 1.5f, -3.9f), new Vector3(1.4f, 1.0f, 0.9f), stahlDunkel);
		// Rampe vorne heruntergeklappt, man laeuft zum Strand hinaus.
		var gelenk = new GameObject("Rampe");
		gelenk.transform.SetParent(boot.transform, false);
		gelenk.transform.localPosition = new Vector3(0f, 0.3f, 5f);
		gelenk.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
		Klotz(gelenk.transform, "Rampenplatte", new Vector3(0f, 0f, 1.35f), new Vector3(3.4f, 0.2f, 2.7f), stahl);
		// Die Kaesten bleiben als Kollision, zu sehen ist das genaue Modell.
		foreach (var anzeige in boot.GetComponentsInChildren<MeshRenderer>(true))
		{
			Object.DestroyImmediate(anzeige.GetComponent<MeshFilter>());
			Object.DestroyImmediate(anzeige);
		}
		DDLandungsbootModell(boot, ddBootRumpf, stahl, ddBootInnen, stahlDunkel);
		DDSpawnZone(boot.transform, new Vector3(0f, 1.4f, -0.8f), new Vector3(2.4f, 0.4f, 5.5f), Teams.ALLIIERTE);
		if (brennt)
		{
			DDFeuer(boot.transform, new Vector3(0.8f, 1.2f, -4.3f), 1.1f);
		}
	}

	static void DDSandsackRing(Transform eltern, Vector3 mitte, float radius, Material material)
	{
		var ring = new GameObject("MG-Nest");
		ring.transform.SetParent(eltern, false);
		ring.transform.localPosition = Vector3.zero;
		var saecke = new Netzbau(1);
		// Offen nach hinten (Norden), damit man hineinkommt.
		for (int i = 0; i < 7; i++)
		{
			float winkel = Mathf.Lerp(120f, 420f, i / 6f);
			float rad = winkel * Mathf.Deg2Rad;
			var position = new Vector3(mitte.x + Mathf.Sin(rad) * radius, 0f, mitte.z - Mathf.Cos(rad) * radius);
			position.y = DDHoehe(position.x, position.z) + 0.45f;
			var kasten = Klotz(ring.transform, "Sandsack", position, new Vector3(1.8f, 0.9f, 0.7f), material, new Vector3(0f, -winkel, 0f));
			// Kasten bleibt als Kollision, zu sehen sind einzelne Saecke
			Object.DestroyImmediate(kasten.GetComponent<MeshRenderer>());
			Object.DestroyImmediate(kasten.GetComponent<MeshFilter>());
			var rechts = Quaternion.Euler(0f, -winkel, 0f) * Vector3.right;
			var fuss = position - Vector3.up * 0.45f;
			DDSandsackReihe(saecke, fuss - rechts * 0.95f, fuss + rechts * 0.95f, 4, uZufall);
		}
		DDNetz(ring.transform, "Sandsaecke", saecke, new[] { material });
	}

	// Zickzack-Graben aus Sandsaecken quer ueber die Hochebene, offen an den Schluchten.
	static void DDGraben(Transform eltern, Material material)
	{
		var graben = new GameObject("Graben");
		graben.transform.SetParent(eltern, false);
		var punkte = new List<Vector3>();
		int k = 0;
		for (float x = -98f; x <= 98f; x += 8f, k++)
		{
			float z = k % 2 == 0 ? 66f : 71f;
			punkte.Add(new Vector3(x, DDHoehe(x, z), z));
		}
		for (int i = 0; i < punkte.Count - 1; i++)
		{
			var a = punkte[i];
			var b = punkte[i + 1];
			var mitte = (a + b) * 0.5f;
			if (DDSchlucht(mitte.x) > 0.35f)
			{
				continue;
			}
			var richtung = b - a;
			richtung.y = 0f;
			float laenge = richtung.magnitude;
			float gier = Mathf.Atan2(richtung.x, richtung.z) * Mathf.Rad2Deg;
			mitte.y = DDHoehe(mitte.x, mitte.z) + 0.5f;
			Klotz(graben.transform, "Grabenwand", mitte, new Vector3(0.9f, 1.0f, laenge), material, new Vector3(0f, gier, 0f));
		}
	}

	static void DDBueschUndBaeume(Transform eltern, System.Random zufall, Material busch, Material laub, Material rinde)
	{
		for (int versuch = 0, anzahl = 0; versuch < 800 && anzahl < 70; versuch++)
		{
			float x = Zufall(zufall, -97f, 97f);
			float z = Zufall(zufall, 25f, 108f);
			if (DDHangAnteil(x, z) < 0.5f || imWeg(x, z))
			{
				continue;
			}
			var gruppe = new GameObject("Busch");
			gruppe.transform.SetParent(eltern, false);
			gruppe.transform.localPosition = new Vector3(x, DDHoehe(x, z), z);
			int teile = 2 + zufall.Next(3);
			for (int i = 0; i < teile; i++)
			{
				float r = Zufall(zufall, 0.6f, 1.2f);
				var kugel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				kugel.name = "Laub";
				// Bueschen verdecken nur, sie halten keine Schuesse auf.
				Object.DestroyImmediate(kugel.GetComponent<Collider>());
				kugel.transform.SetParent(gruppe.transform, false);
				kugel.transform.localPosition = new Vector3(Zufall(zufall, -0.8f, 0.8f), r * 0.6f, Zufall(zufall, -0.8f, 0.8f));
				kugel.transform.localScale = new Vector3(r * 2f, r * 1.5f, r * 2f);
				kugel.GetComponent<Renderer>().sharedMaterial = busch;
			}
			UltraBusch(gruppe, uZufall, Zufall(uZufall, 1f, 1.35f));
			anzahl++;
		}
		for (int versuch = 0, anzahl = 0; versuch < 600 && anzahl < 24; versuch++)
		{
			float x = Zufall(zufall, -97f, 97f);
			float z = Zufall(zufall, 76f, 108f);
			if (imWeg(x, z))
			{
				continue;
			}
			var baum = new GameObject("Baum");
			baum.transform.SetParent(eltern, false);
			baum.transform.localPosition = new Vector3(x, DDHoehe(x, z), z);
			float groesse = Zufall(zufall, 0.85f, 1.25f);
			var stamm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			stamm.name = "Stamm";
			stamm.transform.SetParent(baum.transform, false);
			stamm.transform.localPosition = new Vector3(0f, 2.2f * groesse, 0f);
			stamm.transform.localScale = new Vector3(0.35f, 2.2f, 0.35f) * groesse;
			stamm.GetComponent<Renderer>().sharedMaterial = rinde;
			for (int i = 0; i < 2; i++)
			{
				var krone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				krone.name = "Krone";
				Object.DestroyImmediate(krone.GetComponent<Collider>());
				krone.transform.SetParent(baum.transform, false);
				krone.transform.localPosition = new Vector3(Zufall(zufall, -0.5f, 0.5f), (4.6f + i * 1.3f) * groesse, Zufall(zufall, -0.5f, 0.5f));
				krone.transform.localScale = Vector3.one * Zufall(zufall, 3.2f, 4.4f) * groesse;
				krone.GetComponent<Renderer>().sharedMaterial = laub;
			}
			UltraBaum(baum, uZufall, groesse);
			anzahl++;
		}
	}

	// Nicht in Bunker, Spawnbereiche und Graben pflanzen.
	static bool imWeg(float x, float z)
	{
		foreach (var bx in DD_BUNKER_X)
		{
			if (Mathf.Abs(x - bx) < 8f && Mathf.Abs(z - DD_BUNKER_Z) < 7f) return true;
		}
		foreach (var vx in DD_VERTEIDIGER_X)
		{
			if (Mathf.Abs(x - vx) < 13f && Mathf.Abs(z - DD_VERTEIDIGER_Z) < 7f) return true;
		}
		return z > 63f && z < 74f;
	}

	// Grasbueschel aus gekreuzten Flaechen, zu wenigen grossen Netzen zusammengefasst.
	static void DDGras(Transform eltern, System.Random zufall)
	{
		var material = new Material(Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse"));
		material.name = "Gras";
		material.mainTexture = PngAlsTextur(DDAY + "/Gras.png", DDGrasTextur(zufall), TextureWrapMode.Clamp);
		material.SetFloat("_Cutoff", 0.4f);
		material.color = new Color(0.85f, 0.95f, 0.8f);
		material = AlsAsset(material, DDAY + "/Gras.mat");

		const float feld = 50f;
		var stuecke = new Dictionary<int, List<Vector4>>();
		for (int versuch = 0, anzahl = 0; versuch < 9000 && anzahl < 2600; versuch++)
		{
			float x = Zufall(zufall, -98f, 98f);
			float z = Zufall(zufall, 14f, 109f);
			float t = DDHangAnteil(x, z);
			bool passt = t > 0.45f || (DDSchlucht(x) > 0.4f && t > 0.2f);
			if (!passt || DDSteilheit(x, z) > 1f || imWeg(x, z))
			{
				continue;
			}
			int schluessel = Mathf.FloorToInt((x - DD_XMIN) / feld) * 100 + Mathf.FloorToInt((z - DD_ZMIN) / feld);
			if (!stuecke.ContainsKey(schluessel))
			{
				stuecke[schluessel] = new List<Vector4>();
			}
			stuecke[schluessel].Add(new Vector4(x, z, Zufall(zufall, 0f, 180f), Zufall(zufall, 0.7f, 1.3f)));
			anzahl++;
		}
		int nummer = 0;
		foreach (var paar in stuecke)
		{
			var netz = DDGrasNetz(paar.Value);
			netz = AlsAsset(netz, DDAY + "/Gras" + nummer + ".asset");
			var go = new GameObject("Gras");
			go.transform.SetParent(eltern, false);
			go.AddComponent<MeshFilter>().sharedMesh = netz;
			var anzeige = go.AddComponent<MeshRenderer>();
			anzeige.sharedMaterial = material;
			anzeige.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			nummer++;
		}
	}

	static Mesh DDGrasNetz(List<Vector4> bueschel)
	{
		var punkte = new List<Vector3>();
		var uv = new List<Vector2>();
		var dreiecke = new List<int>();
		foreach (var b in bueschel)
		{
			var fuss = new Vector3(b.x, DDHoehe(b.x, b.y) - 0.05f, b.y);
			float breite = 1.0f * b.w;
			float hoehe = 0.75f * b.w;
			for (int kreuz = 0; kreuz < 2; kreuz++)
			{
				float winkel = (b.z + kreuz * 90f) * Mathf.Deg2Rad;
				var seite = new Vector3(Mathf.Cos(winkel), 0f, Mathf.Sin(winkel)) * breite * 0.5f;
				// Beide Seiten, weil der Shader Rueckseiten nicht zeichnet.
				for (int s = 0; s < 2; s++)
				{
					int start = punkte.Count;
					punkte.Add(fuss - seite);
					punkte.Add(fuss + seite);
					punkte.Add(fuss + seite + Vector3.up * hoehe);
					punkte.Add(fuss - seite + Vector3.up * hoehe);
					uv.Add(new Vector2(0f, 0f));
					uv.Add(new Vector2(1f, 0f));
					uv.Add(new Vector2(1f, 1f));
					uv.Add(new Vector2(0f, 1f));
					if (s == 0)
					{
						dreiecke.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
					}
					else
					{
						dreiecke.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
					}
				}
			}
		}
		var netz = new Mesh();
		netz.name = "Gras";
		if (punkte.Count > 65000)
		{
			netz.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		}
		netz.SetVertices(punkte);
		netz.SetUVs(0, uv);
		netz.SetTriangles(dreiecke, 0);
		netz.RecalculateNormals();
		// Normalen nach oben: Gras wird dann gleichmaessig hell statt je nach Seite dunkel.
		var normalen = new Vector3[punkte.Count];
		for (int i = 0; i < normalen.Length; i++)
		{
			normalen[i] = Vector3.up;
		}
		netz.normals = normalen;
		netz.RecalculateBounds();
		return netz;
	}

	static byte[] DDGrasTextur(System.Random zufall)
	{
		const int groesse = 128;
		var textur = new Texture2D(groesse, groesse, TextureFormat.RGBA32, false);
		var punkte = new Color32[groesse * groesse];
		for (int i = 0; i < 30; i++)
		{
			float fussX = Zufall(zufall, 10f, groesse - 10f);
			float laenge = Zufall(zufall, 55f, 120f);
			float biegung = Zufall(zufall, -25f, 25f);
			float dicke = Zufall(zufall, 2.5f, 4.5f);
			var farbe = Color.Lerp(new Color(0.25f, 0.42f, 0.15f), new Color(0.55f, 0.6f, 0.3f), (float)zufall.NextDouble());
			for (int y = 0; y < Mathf.Min(groesse, (int)laenge); y++)
			{
				float anteil = y / laenge;
				float mitte = fussX + biegung * anteil * anteil;
				float halbe = dicke * (1f - anteil) * 0.5f + 0.4f;
				for (int x = Mathf.Max(0, (int)(mitte - halbe)); x <= Mathf.Min(groesse - 1, (int)(mitte + halbe)); x++)
				{
					punkte[y * groesse + x] = Color.Lerp(farbe * 0.7f, farbe, anteil);
				}
			}
		}
		textur.SetPixels32(punkte);
		textur.Apply();
		var png = textur.EncodeToPNG();
		Object.DestroyImmediate(textur);
		return png;
	}

	// ------------------------------------------------------------------
	// Effekte

	static Material ddFeuerMaterial;

	static Material ddRauchMaterial;

	static void DDPartikelMaterialien()
	{
		if (ddFeuerMaterial != null && ddRauchMaterial != null)
		{
			return;
		}
		// Weicher runder Fleck als Partikel-Textur.
		const int groesse = 64;
		var textur = new Texture2D(groesse, groesse, TextureFormat.RGBA32, false);
		var punkte = new Color[groesse * groesse];
		float mitte = (groesse - 1) * 0.5f;
		for (int y = 0; y < groesse; y++)
		{
			for (int x = 0; x < groesse; x++)
			{
				float abstand = new Vector2(x - mitte, y - mitte).magnitude / mitte;
				float a = Mathf.Pow(Mathf.Clamp01(1f - abstand), 1.8f);
				punkte[y * groesse + x] = new Color(1f, 1f, 1f, a);
			}
		}
		textur.SetPixels(punkte);
		textur.Apply();
		var png = textur.EncodeToPNG();
		Object.DestroyImmediate(textur);
		Ordner(EFFEKTE);
		var partikel = PngAlsTextur(EFFEKTE + "/Partikel.png", png, TextureWrapMode.Clamp);

		var feuer = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
		feuer.name = "Feuer";
		feuer.mainTexture = partikel;
		ddFeuerMaterial = AlsAsset(feuer, EFFEKTE + "/Feuer.mat");
		var rauch = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
		rauch.name = "Rauch";
		rauch.mainTexture = partikel;
		ddRauchMaterial = AlsAsset(rauch, EFFEKTE + "/Rauch.mat");
	}

	static ParticleSystem DDPartikel(Transform eltern, string name, Vector3 position, Material material)
	{
		var go = new GameObject(name);
		go.transform.SetParent(eltern, false);
		go.transform.localPosition = position;
		// Partikelsysteme senden entlang ihrer z-Achse, hier nach oben.
		go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
		var ps = go.AddComponent<ParticleSystem>();
		go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
		var main = ps.main;
		main.loop = true;
		main.playOnAwake = true;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		return ps;
	}

	static Gradient Verlauf(Color[] farben, float[] deckung)
	{
		var verlauf = new Gradient();
		var farbSchluessel = new GradientColorKey[farben.Length];
		for (int i = 0; i < farben.Length; i++)
		{
			farbSchluessel[i] = new GradientColorKey(farben[i], farben.Length == 1 ? 0f : i / (farben.Length - 1f));
		}
		var deckSchluessel = new GradientAlphaKey[deckung.Length];
		for (int i = 0; i < deckung.Length; i++)
		{
			deckSchluessel[i] = new GradientAlphaKey(deckung[i], deckung.Length == 1 ? 0f : i / (deckung.Length - 1f));
		}
		verlauf.SetKeys(farbSchluessel, deckSchluessel);
		return verlauf;
	}

	static void DDFeuer(Transform eltern, Vector3 position, float groesse)
	{
		DDPartikelMaterialien();
		var halter = new GameObject("Feuer");
		halter.transform.SetParent(eltern, false);
		halter.transform.localPosition = position;

		var feuer = DDPartikel(halter.transform, "Flammen", Vector3.zero, ddFeuerMaterial);
		var main = feuer.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * groesse, 2.8f * groesse);
		main.startSize = new ParticleSystem.MinMaxCurve(0.7f * groesse, 1.5f * groesse);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.35f), new Color(1f, 0.45f, 0.1f));
		main.maxParticles = 250;
		main.gravityModifier = -0.15f;
		var emission = feuer.emission;
		emission.rateOverTime = 45f * groesse;
		var form = feuer.shape;
		form.shapeType = ParticleSystemShapeType.Cone;
		form.angle = 12f;
		form.radius = 0.5f * groesse;
		var farbe = feuer.colorOverLifetime;
		farbe.enabled = true;
		farbe.color = new ParticleSystem.MinMaxGradient(Verlauf(new[] { new Color(1f, 0.9f, 0.5f), new Color(1f, 0.45f, 0.1f), new Color(0.5f, 0.1f, 0.05f) }, new[] { 0f, 1f, 0f }));
		var groessenVerlauf = feuer.sizeOverLifetime;
		groessenVerlauf.enabled = true;
		groessenVerlauf.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
		var rauschen = feuer.noise;
		rauschen.enabled = true;
		rauschen.strength = 0.6f;
		rauschen.frequency = 0.9f;

		var rauch = DDPartikel(halter.transform, "Rauch", new Vector3(0f, 0.8f * groesse, 0f), ddRauchMaterial);
		main = rauch.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
		main.startSize = new ParticleSystem.MinMaxCurve(1.5f * groesse, 3f * groesse);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.2f, 0.2f), new Color(0.35f, 0.35f, 0.35f));
		main.maxParticles = 120;
		emission = rauch.emission;
		emission.rateOverTime = 9f * groesse;
		form = rauch.shape;
		form.shapeType = ParticleSystemShapeType.Cone;
		form.angle = 8f;
		form.radius = 0.4f * groesse;
		farbe = rauch.colorOverLifetime;
		farbe.enabled = true;
		farbe.color = new ParticleSystem.MinMaxGradient(Verlauf(new[] { Color.white, Color.white }, new[] { 0f, 0.55f, 0f }));
		groessenVerlauf = rauch.sizeOverLifetime;
		groessenVerlauf.enabled = true;
		groessenVerlauf.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
		rauschen = rauch.noise;
		rauschen.enabled = true;
		rauschen.strength = 0.8f;
		rauschen.frequency = 0.4f;

		var lichtGo = new GameObject("Feuerlicht");
		lichtGo.transform.SetParent(halter.transform, false);
		lichtGo.transform.localPosition = new Vector3(0f, 1f, 0f);
		var licht = lichtGo.AddComponent<Light>();
		licht.type = LightType.Point;
		licht.color = new Color(1f, 0.6f, 0.25f);
		licht.intensity = 2.2f;
		licht.range = 10f * groesse;
		lichtGo.AddComponent<Flackern>();
		UltraFeuer(halter, groesse);
	}

	// Grashalme fuer "Ultra": die ganze gruene Flaeche der Bodentextur, knapp 6 Bueschel pro m2.
	// Raster mit Zufallsversatz statt reinem Zufall, damit es keine kahlen Stellen gibt.
	static void DDUltraGras(GameObject wurzel)
	{
		var orte = new List<Vector3>();
		var normalen = new List<Vector3>();
		var groessen = new List<float>();
		const float raster = 0.42f;
		for (float z = -20f; z < DD_ZMAX - 1f; z += raster)
		{
			for (float x = DD_XMIN + 1f; x < DD_XMAX - 1f; x += raster)
			{
				float px = x + Zufall(uZufall, 0f, raster);
				float pz = z + Zufall(uZufall, 0f, raster);
				float anteil = Mathf.Clamp01(DDGrasFlaeche(px, pz) * 1.2f);
				if ((float)uZufall.NextDouble() >= anteil || DDGrasFrei(px, pz))
				{
					continue;
				}
				orte.Add(new Vector3(px, DDHoehe(px, pz) - 0.03f, pz));
				var normale = new Vector3(DDHoehe(px - 0.5f, pz) - DDHoehe(px + 0.5f, pz), 1f, DDHoehe(px, pz - 0.5f) - DDHoehe(px, pz + 0.5f));
				normalen.Add(normale.normalized);
				// Am Rand der Wiese kleiner, mittendrin hoeher
				groessen.Add(Zufall(uZufall, 0.75f, 1.15f) * Mathf.Lerp(0.7f, 1.15f, anteil));
			}
		}
		UltraGras(wurzel, "GrasDDay", orte, normalen, groessen);
	}

	// Kein Gras in Bunkern und auf den Spawnplaetzen der Verteidiger (das Gras ragte durch den Boden).
	static bool DDGrasFrei(float x, float z)
	{
		foreach (var bx in DD_BUNKER_X)
		{
			if (Mathf.Abs(x - bx) < 6f && Mathf.Abs(z - DD_BUNKER_Z) < 5f) return true;
		}
		foreach (var vx in DD_VERTEIDIGER_X)
		{
			if (Mathf.Abs(x - vx) < 11f && Mathf.Abs(z - DD_VERTEIDIGER_Z) < 5f) return true;
		}
		return false;
	}

	// Weisse Gischt entlang der Wasserlinie.
	static void DDGischt(Transform eltern)
	{
		DDPartikelMaterialien();
		var gischt = DDPartikel(eltern, "Gischt", new Vector3(0f, 0.1f, -26f), ddRauchMaterial);
		var main = gischt.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
		main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.7f), new Color(0.85f, 0.92f, 0.95f, 0.5f));
		main.gravityModifier = 0.3f;
		main.maxParticles = 700;
		var emission = gischt.emission;
		emission.rateOverTime = 140f;
		var form = gischt.shape;
		form.shapeType = ParticleSystemShapeType.Box;
		form.scale = new Vector3(190f, 3f, 0.2f);
		var farbe = gischt.colorOverLifetime;
		farbe.enabled = true;
		farbe.color = new ParticleSystem.MinMaxGradient(Verlauf(new[] { Color.white, Color.white }, new[] { 0f, 0.8f, 0f }));
	}

	// ------------------------------------------------------------------
	// Vorschaubild fuer das Host-Fenster

	static Sprite DDayVorschauRendern(GameObject dDay)
	{
		var karte = (GameObject)PrefabUtility.InstantiatePrefab(dDay);
		var lichtGo = new GameObject("VorschauLicht");
		var licht = lichtGo.AddComponent<Light>();
		licht.type = LightType.Directional;
		licht.intensity = 1.1f;
		lichtGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
		var kameraGo = new GameObject("VorschauKamera");
		kameraGo.transform.position = new Vector3(-75f, 38f, -95f);
		kameraGo.transform.LookAt(new Vector3(10f, 8f, 25f));
		var kamera = kameraGo.AddComponent<Camera>();
		kamera.fieldOfView = 50f;
		kamera.farClipPlane = 600f;
		kamera.clearFlags = CameraClearFlags.Skybox;

		const int breite = 1024;
		const int hoehe = 576;
		var ziel = new RenderTexture(breite, hoehe, 24);
		kamera.targetTexture = ziel;
		kamera.Render();
		var vorher = RenderTexture.active;
		RenderTexture.active = ziel;
		var bild = new Texture2D(breite, hoehe, TextureFormat.RGB24, false);
		bild.ReadPixels(new Rect(0, 0, breite, hoehe), 0, 0);
		bild.Apply();
		RenderTexture.active = vorher;
		kamera.targetTexture = null;
		var png = bild.EncodeToPNG();

		Object.DestroyImmediate(bild);
		Object.DestroyImmediate(ziel);
		Object.DestroyImmediate(kameraGo);
		Object.DestroyImmediate(lichtGo);
		Object.DestroyImmediate(karte);
		return PngAlsSprite(DDAY + "/Vorschau.png", png);
	}
}
