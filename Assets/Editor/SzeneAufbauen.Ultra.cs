using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Teil des Editor-Skripts: Grafikstufe "Ultra". Alles wird hier erzeugt, ohne fremde Modelle:
// Texturen (Blaetter, Rinde, Palmwedel, Flammen-Daumenkino, Tropfen), Netze (Baeume aus Aesten
// und Blattbueschen, Buesche, Palmkronen, Grashalme, drei Waffen) und die Teile, die
// UltraSchalter ein- und ausschaltet. Fester Zufall: Jeder PC baut dieselben Pflanzen.
public static partial class SzeneAufbauen
{
	const string ULTRA = ERZEUGT + "/Ultra";

	static Material uLaub;
	static Material uBusch;
	static Material uWedel;
	static Material uRinde;
	static Material uGras;
	static Material uFlammen;
	static Material uFunke;
	static Material uTropfen;
	static Material uRing;
	static Material uBlitz;
	static Mesh[] uBaeume;
	static Mesh[] uBuesche;
	static Mesh uPalmKrone;
	static Mesh uGrasBueschel;
	static Mesh uGrasBueschelFern;
	static Material uTrampeln;

	// Eigener Zufall fuer Ultra-Teile, damit die Maps sonst genau so bleiben wie vorher.
	static System.Random uZufall;

	// Einmal pro Aufbau, vor den Maps und dem Spieler.
	static void UltraVorbereiten()
	{
		Ordner(ULTRA);
		uZufall = new System.Random(7777);
		uBodenDetail = null;
		uBodenRelief = null;
		uLaub = ULaubMaterial("Laub", ULaubTextur("Laub", 21, 75, 0.09f, 0.14f, new Color(0.11f, 0.24f, 0.07f), new Color(0.36f, 0.52f, 0.15f)), 0.6f);
		uBusch = ULaubMaterial("Buschlaub", ULaubTextur("Buschlaub", 33, 120, 0.06f, 0.095f, new Color(0.1f, 0.2f, 0.06f), new Color(0.3f, 0.45f, 0.14f)), 0.35f);
		uWedel = ULaubMaterial("Palmwedel", UWedelTextur(), 0.8f);

		uRinde = new Material(Shader.Find("Standard"));
		uRinde.name = "Rinde";
		uRinde.mainTexture = UTextur("Rinde", 256, 256, URindePixel(), false, TextureWrapMode.Repeat, 0f);
		uRinde.SetFloat("_Glossiness", 0.08f);
		uRinde = AlsAsset(uRinde, ULTRA + "/Rinde.mat");

		var grasShader = Shader.Find("Shootout/Gras") ?? Shader.Find("Standard");
		uGras = new Material(grasShader);
		uGras.name = "Grashalme";
		// Noetig fuer DrawMeshInstanced.
		uGras.enableInstancing = true;
		uGras = AlsAsset(uGras, ULTRA + "/Grashalme.mat");

		uFlammen = UPartikelMaterial("Flammen", "Legacy Shaders/Particles/Additive", UTextur("Flammen", 512, 512, UFlammenPixel(), true, TextureWrapMode.Clamp, 0f));
		uFunke = UPartikelMaterial("Funke", "Legacy Shaders/Particles/Additive", UTextur("Funke", 64, 64, (u, v) => UPunkt(u, v, 2f), true, TextureWrapMode.Clamp, 0f));
		uTropfen = UPartikelMaterial("Tropfen", "Legacy Shaders/Particles/Alpha Blended", UTextur("Tropfen", 64, 64, (u, v) => UPunkt(u, v, 1.5f), true, TextureWrapMode.Clamp, 0f));
		uRing = UPartikelMaterial("Wasserring", "Legacy Shaders/Particles/Alpha Blended", UTextur("Wasserring", 128, 128, URingPixel, true, TextureWrapMode.Clamp, 0f));
		uBlitz = UPartikelMaterial("Muendungsblitz", "Legacy Shaders/Particles/Additive", UTextur("Muendungsblitz", 128, 128, UBlitzPixel, true, TextureWrapMode.Clamp, 0f));

		uBaeume = new Mesh[3];
		for (int i = 0; i < uBaeume.Length; i++)
		{
			uBaeume[i] = AlsAsset(UBaumNetz(100 + i), ULTRA + "/Baum" + i + ".asset");
		}
		uBuesche = new Mesh[3];
		for (int i = 0; i < uBuesche.Length; i++)
		{
			uBuesche[i] = AlsAsset(UBuschNetz(200 + i), ULTRA + "/Busch" + i + ".asset");
		}
		uPalmKrone = AlsAsset(UPalmKroneNetz(300), ULTRA + "/Palmkrone.asset");
		uGrasBueschel = AlsAsset(UGrasNetz(400, 24, 1f), ULTRA + "/Grasbueschel.asset");
		uGrasBueschelFern = AlsAsset(UGrasNetz(401, 9, 1.7f), ULTRA + "/GrasbueschelFern.asset");
		var trampelShader = Shader.Find("Hidden/Shootout/Trampeln");
		if (trampelShader != null)
		{
			uTrampeln = new Material(trampelShader);
			uTrampeln.name = "Trampeln";
			uTrampeln = AlsAsset(uTrampeln, ULTRA + "/Trampeln.mat");
		}
	}

	// ------------------------------------------------------------------
	// Einbauen: einfache Teile gegen Ultra-Teile tauschen

	// Haengt das Ultra-Netz an die Gruppe und tauscht es mit allen bisherigen Kindern.
	static GameObject UltraPflanze(GameObject gruppe, Mesh netz, Material[] materialien, float groesse, float gier)
	{
		var einfach = new List<GameObject>();
		foreach (Transform kind in gruppe.transform)
		{
			einfach.Add(kind.gameObject);
		}
		var ultra = new GameObject("Ultra");
		ultra.transform.SetParent(gruppe.transform, false);
		ultra.transform.localRotation = Quaternion.Euler(0f, gier, 0f);
		ultra.transform.localScale = Vector3.one * groesse;
		ultra.AddComponent<MeshFilter>().sharedMesh = netz;
		ultra.AddComponent<MeshRenderer>().sharedMaterials = materialien;
		ultra.SetActive(false);
		UltraTausch(gruppe, einfach.ToArray(), new[] { ultra });
		return ultra;
	}

	static void UltraTausch(GameObject traeger, GameObject[] einfach, GameObject[] ultra)
	{
		var schalter = traeger.AddComponent<UltraSchalter>();
		schalter.einfach = einfach;
		schalter.ultra = ultra;
	}

	static void UltraBaum(GameObject baum, System.Random zufall, float groesse)
	{
		UltraPflanze(baum, uBaeume[zufall.Next(uBaeume.Length)], new[] { uRinde, uLaub }, groesse, Zufall(zufall, 0f, 360f));
	}

	static void UltraBusch(GameObject busch, System.Random zufall, float groesse)
	{
		UltraPflanze(busch, uBuesche[zufall.Next(uBuesche.Length)], new[] { uRinde, uBusch }, groesse, Zufall(zufall, 0f, 360f));
	}

	// Palme: nur die Wedel tauschen, der Stamm bleibt.
	static void UltraPalme(GameObject palme, Vector3 krone, float gier)
	{
		var wedel = new List<GameObject>();
		foreach (Transform kind in palme.transform)
		{
			if (kind.name == "Wedel")
			{
				wedel.Add(kind.gameObject);
			}
		}
		var ultra = new GameObject("Ultra");
		ultra.transform.SetParent(palme.transform, false);
		ultra.transform.localPosition = krone;
		ultra.transform.localRotation = Quaternion.Euler(0f, gier, 0f);
		ultra.AddComponent<MeshFilter>().sharedMesh = uPalmKrone;
		ultra.AddComponent<MeshRenderer>().sharedMaterial = uWedel;
		ultra.SetActive(false);
		UltraTausch(palme, wedel.ToArray(), new[] { ultra });
	}

	// Gras als Punkte (Position, Bodennormale, Groesse) fuer GrasFeld.
	static void UltraGras(GameObject wurzel, string name, List<Vector3> orte, List<Vector3> normalen, List<float> groessen)
	{
		var netz = new Mesh();
		netz.name = name;
		netz.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		netz.SetVertices(orte);
		netz.SetNormals(normalen);
		var uv = new List<Vector2>();
		var indizes = new int[orte.Count];
		for (int i = 0; i < orte.Count; i++)
		{
			uv.Add(new Vector2(groessen[i], 0f));
			indizes[i] = i;
		}
		netz.SetUVs(0, uv);
		netz.SetIndices(indizes, MeshTopology.Points, 0);
		netz.RecalculateBounds();
		var feld = wurzel.AddComponent<GrasFeld>();
		feld.punkte = AlsAsset(netz, ULTRA + "/" + name + ".asset");
		feld.bueschel = uGrasBueschel;
		feld.bueschelFern = uGrasBueschelFern;
		feld.material = uGras;
		feld.trampelMaterial = uTrampeln;
	}

	// Spiegelungen: eine Sonde ueber der ganzen Map, nur in "Ultra" an.
	static void UltraSpiegelung(GameObject wurzel, Vector3 mitte, Vector3 groesse)
	{
		var sonde = new GameObject("Spiegelung");
		sonde.transform.SetParent(wurzel.transform, false);
		sonde.transform.localPosition = mitte;
		var probe = sonde.AddComponent<ReflectionProbe>();
		probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
		probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
		probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
		probe.size = groesse;
		probe.resolution = 256;
		probe.hdr = true;
		probe.importance = 1;
		sonde.AddComponent<SpiegelungAuffrischen>();
		sonde.SetActive(false);
		var halter = new GameObject("UltraSchalter");
		halter.transform.SetParent(wurzel.transform, false);
		UltraTausch(halter, new GameObject[0], new[] { sonde });
	}

	// Feuer: Flammen aus einem Daumenkino (16 Bilder), Kern, Funken, dichter Rauch.
	static void UltraFeuer(GameObject halter, float groesse)
	{
		var einfach = new List<GameObject>();
		foreach (Transform kind in halter.transform)
		{
			if (kind.GetComponent<ParticleSystem>() != null)
			{
				einfach.Add(kind.gameObject);
			}
		}
		var ultra = new GameObject("Ultra");
		ultra.transform.SetParent(halter.transform, false);

		var flammen = DDPartikel(ultra.transform, "Flammen", Vector3.zero, uFlammen);
		UFlammenSystem(flammen, groesse, 1f, new Color(1f, 0.62f, 0.25f), new Color(1f, 0.4f, 0.12f), 34f);
		var kern = DDPartikel(ultra.transform, "Kern", Vector3.zero, uFlammen);
		UFlammenSystem(kern, groesse * 0.6f, 0.55f, new Color(1f, 0.92f, 0.65f), new Color(1f, 0.75f, 0.4f), 26f);

		var funken = DDPartikel(ultra.transform, "Funken", new Vector3(0f, 0.3f * groesse, 0f), uFunke);
		var main = funken.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * groesse, 4f * groesse);
		main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.45f, 0.1f));
		main.gravityModifier = -0.12f;
		main.maxParticles = 120;
		var emission = funken.emission;
		emission.rateOverTime = 14f * groesse;
		var form = funken.shape;
		form.shapeType = ParticleSystemShapeType.Cone;
		form.angle = 25f;
		form.radius = 0.4f * groesse;
		var rauschen = funken.noise;
		rauschen.enabled = true;
		rauschen.strength = 1.4f;
		rauschen.frequency = 1.4f;
		rauschen.scrollSpeed = 0.6f;
		var farbe = funken.colorOverLifetime;
		farbe.enabled = true;
		farbe.color = new ParticleSystem.MinMaxGradient(Verlauf(new[] { new Color(1f, 0.85f, 0.5f), new Color(1f, 0.4f, 0.1f), new Color(0.6f, 0.1f, 0.05f) }, new[] { 1f, 1f, 0f }));
		var anzeige = funken.GetComponent<ParticleSystemRenderer>();
		anzeige.renderMode = ParticleSystemRenderMode.Stretch;
		anzeige.velocityScale = 0.05f;
		anzeige.lengthScale = 1.2f;

		DDPartikelMaterialien();
		var rauch = DDPartikel(ultra.transform, "Rauch", new Vector3(0f, 1.2f * groesse, 0f), ddRauchMaterial);
		main = rauch.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
		main.startSize = new ParticleSystem.MinMaxCurve(1.6f * groesse, 3f * groesse);
		main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.1f, 0.1f), new Color(0.22f, 0.21f, 0.2f));
		main.maxParticles = 160;
		emission = rauch.emission;
		emission.rateOverTime = 10f * groesse;
		form = rauch.shape;
		form.shapeType = ParticleSystemShapeType.Cone;
		form.angle = 10f;
		form.radius = 0.5f * groesse;
		farbe = rauch.colorOverLifetime;
		farbe.enabled = true;
		farbe.color = new ParticleSystem.MinMaxGradient(Verlauf(new[] { new Color(0.6f, 0.45f, 0.35f), Color.white, Color.white }, new[] { 0f, 0.75f, 0f }));
		var wachsen = rauch.sizeOverLifetime;
		wachsen.enabled = true;
		wachsen.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 3f));
		var drehen = rauch.rotationOverLifetime;
		drehen.enabled = true;
		drehen.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
		rauschen = rauch.noise;
		rauschen.enabled = true;
		rauschen.strength = 0.9f;
		rauschen.frequency = 0.35f;

		ultra.SetActive(false);
		UltraTausch(halter, einfach.ToArray(), new[] { ultra });
	}

	static void UFlammenSystem(ParticleSystem ps, float groesse, float lebenFaktor, Color hell, Color dunkel, float rate)
	{
		var main = ps.main;
		main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f * lebenFaktor, 1.1f * lebenFaktor);
		main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * groesse, 1.8f * groesse);
		main.startSize = new ParticleSystem.MinMaxCurve(1.1f * groesse, 2f * groesse);
		main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
		main.startColor = new ParticleSystem.MinMaxGradient(hell, dunkel);
		main.gravityModifier = -0.1f;
		main.maxParticles = 160;
		var emission = ps.emission;
		emission.rateOverTime = rate * Mathf.Max(0.5f, groesse);
		var form = ps.shape;
		form.shapeType = ParticleSystemShapeType.Cone;
		form.angle = 8f;
		form.radius = 0.45f * groesse;
		var bilder = ps.textureSheetAnimation;
		bilder.enabled = true;
		bilder.numTilesX = 4;
		bilder.numTilesY = 4;
		bilder.animation = ParticleSystemAnimationType.WholeSheet;
		bilder.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
		bilder.startFrame = new ParticleSystem.MinMaxCurve(0f, 15f);
		bilder.cycleCount = 2;
		var farbe = ps.colorOverLifetime;
		farbe.enabled = true;
		farbe.color = new ParticleSystem.MinMaxGradient(Verlauf(new[] { Color.white, new Color(1f, 0.7f, 0.4f), new Color(0.7f, 0.2f, 0.08f) }, new[] { 0f, 1f, 0.8f, 0f }));
		var wachsen = ps.sizeOverLifetime;
		wachsen.enabled = true;
		wachsen.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.35f)));
		var rauschen = ps.noise;
		rauschen.enabled = true;
		rauschen.strength = 0.35f;
		rauschen.frequency = 1.1f;
	}

	// ------------------------------------------------------------------
	// Waffen: Muendung im Ursprung, die Waffe liegt entlang -z (zum Schuetzen hin).

	// Lage der Muendung vor der Kamera, je Waffe (Index = Waffen-Nummer). Gewehr und Sniper
	// liegen naeher und mittiger, sonst sah man nur die Spitze. Das SMG bleibt, wie es war.
	static readonly Vector3[] W_MUENDUNG =
	{
		new Vector3(0.2f, -0.17f, 0.95f),
		new Vector3(0.45f, -0.35f, 1.2f),
		new Vector3(0.21f, -0.19f, 1.17f),
	};

	static GameObject[] UltraWaffen(Transform kamera)
	{
		var stahl = UWaffenMaterial("Waffenstahl", new Color(0.12f, 0.12f, 0.13f), 0.85f, 0.55f);
		var polymer = UWaffenMaterial("Polymer", new Color(0.07f, 0.07f, 0.075f), 0f, 0.35f);
		var sand = UWaffenMaterial("Sandfarbe", new Color(0.52f, 0.45f, 0.32f), 0f, 0.3f);
		var glas = UWaffenMaterial("Linse", new Color(0.04f, 0.08f, 0.12f), 0.6f, 0.95f);
		glas.EnableKeyword("_EMISSION");
		glas.SetColor("_EmissionColor", new Color(0.05f, 0.18f, 0.25f));
		EditorUtility.SetDirty(glas);
		var materialien = new[] { stahl, polymer, sand, glas };
		var netze = new[] { UGewehrNetz(), USmgNetz(), USniperNetz() };
		var namen = new[] { "Gewehr", "SMG", "Sniper" };
		var modelle = new GameObject[netze.Length];
		for (int i = 0; i < netze.Length; i++)
		{
			var netz = AlsAsset(netze[i], ULTRA + "/Waffe" + namen[i] + ".asset");
			var go = new GameObject("Waffe " + namen[i]);
			go.transform.SetParent(kamera, false);
			go.transform.localPosition = W_MUENDUNG[i];
			go.AddComponent<MeshFilter>().sharedMesh = netz;
			go.AddComponent<MeshRenderer>().sharedMaterials = materialien;
			go.SetActive(false);
			modelle[i] = go;
		}
		return modelle;
	}

	static Material UWaffenMaterial(string name, Color farbe, float metall, float glanz)
	{
		var material = new Material(Shader.Find("Standard"));
		material.name = name;
		material.color = farbe;
		material.SetFloat("_Metallic", metall);
		material.SetFloat("_Glossiness", glanz);
		return AlsAsset(material, ULTRA + "/" + name + ".mat");
	}

	const int W_STAHL = 0;
	const int W_POLYMER = 1;
	const int W_SAND = 2;
	const int W_GLAS = 3;

	static void WQ(Netzbau n, int teil, float x, float y, float z, float bx, float by, float bz, float kippen = 0f)
	{
		n.Quader(Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(kippen, 0f, 0f), new Vector3(bx, by, bz)), teil, Color.white);
	}

	static void WZ(Netzbau n, int teil, Vector3 a, Vector3 b, float ra, float rb, int seiten = 14)
	{
		n.Rohr(a, b, ra, rb, seiten, teil, Color.white, Color.white, 0f, 1f, true);
	}

	static Vector3 Z(float z, float y = 0f, float x = 0f)
	{
		return new Vector3(x, y, z);
	}

	static Mesh UGewehrNetz()
	{
		var n = new Netzbau(4);
		WZ(n, W_STAHL, Z(0f), Z(-0.06f), 0.014f, 0.014f, 8);
		WZ(n, W_STAHL, Z(-0.06f), Z(-0.24f), 0.0095f, 0.0095f);
		WQ(n, W_STAHL, 0f, 0.012f, -0.215f, 0.03f, 0.035f, 0.025f);
		WQ(n, W_STAHL, 0f, 0.045f, -0.215f, 0.008f, 0.03f, 0.01f);
		// Handschutz achteckig mit Schiene oben
		WZ(n, W_POLYMER, Z(-0.23f), Z(-0.56f), 0.026f, 0.026f, 8);
		WQ(n, W_STAHL, 0f, 0.03f, -0.395f, 0.022f, 0.008f, 0.33f);
		for (float z = -0.235f; z > -0.555f; z -= 0.012f)
		{
			WQ(n, W_STAHL, 0f, 0.036f, z, 0.024f, 0.005f, 0.006f);
		}
		// Gehaeuse oben und unten
		WQ(n, W_STAHL, 0f, 0.006f, -0.66f, 0.05f, 0.05f, 0.2f);
		WQ(n, W_STAHL, 0f, 0.034f, -0.66f, 0.022f, 0.008f, 0.2f);
		WQ(n, W_POLYMER, 0.026f, 0.004f, -0.64f, 0.004f, 0.018f, 0.05f);
		WQ(n, W_STAHL, 0f, 0.025f, -0.765f, 0.04f, 0.01f, 0.015f);
		WQ(n, W_STAHL, 0f, -0.03f, -0.67f, 0.046f, 0.04f, 0.17f);
		// Magazin gebogen
		WQ(n, W_POLYMER, 0f, -0.09f, -0.63f, 0.026f, 0.1f, 0.065f, -8f);
		WQ(n, W_POLYMER, 0f, -0.17f, -0.613f, 0.026f, 0.07f, 0.063f, -16f);
		// Abzug, Buegel, Griff
		WQ(n, W_STAHL, 0f, -0.058f, -0.71f, 0.012f, 0.004f, 0.06f);
		WQ(n, W_STAHL, 0f, -0.047f, -0.715f, 0.006f, 0.02f, 0.006f);
		WQ(n, W_POLYMER, 0f, -0.09f, -0.755f, 0.03f, 0.1f, 0.04f, 18f);
		// Schaft
		WZ(n, W_STAHL, Z(-0.76f), Z(-0.94f), 0.016f, 0.016f);
		WQ(n, W_POLYMER, 0f, -0.015f, -0.9f, 0.04f, 0.08f, 0.13f);
		WQ(n, W_POLYMER, 0f, -0.018f, -0.967f, 0.044f, 0.095f, 0.015f);
		// Rotpunktvisier
		WQ(n, W_STAHL, 0f, 0.043f, -0.64f, 0.02f, 0.012f, 0.04f);
		WZ(n, W_STAHL, Z(-0.61f, 0.06f), Z(-0.67f, 0.06f), 0.019f, 0.019f, 16);
		WZ(n, W_GLAS, Z(-0.608f, 0.06f), Z(-0.611f, 0.06f), 0.0165f, 0.0165f, 16);
		WZ(n, W_GLAS, Z(-0.669f, 0.06f), Z(-0.672f, 0.06f), 0.0165f, 0.0165f, 16);
		return n.Fertig("Gewehr");
	}

	static Mesh USmgNetz()
	{
		var n = new Netzbau(4);
		WZ(n, W_STAHL, Z(0f), Z(-0.17f), 0.021f, 0.021f, 16);
		WZ(n, W_STAHL, Z(-0.17f), Z(-0.2f), 0.009f, 0.009f);
		WQ(n, W_POLYMER, 0f, 0f, -0.32f, 0.045f, 0.06f, 0.24f);
		WQ(n, W_STAHL, 0f, 0.034f, -0.32f, 0.026f, 0.008f, 0.22f);
		WQ(n, W_STAHL, 0f, -0.13f, -0.28f, 0.022f, 0.2f, 0.035f, -5f);
		WQ(n, W_POLYMER, 0f, -0.07f, -0.215f, 0.03f, 0.08f, 0.03f);
		WQ(n, W_POLYMER, 0f, -0.08f, -0.41f, 0.03f, 0.1f, 0.04f, 15f);
		WQ(n, W_STAHL, 0f, -0.045f, -0.365f, 0.01f, 0.004f, 0.05f);
		WQ(n, W_STAHL, 0f, -0.035f, -0.37f, 0.005f, 0.018f, 0.005f);
		// Klappschaft aus zwei Stangen
		WQ(n, W_STAHL, 0.016f, -0.005f, -0.52f, 0.006f, 0.006f, 0.18f);
		WQ(n, W_STAHL, -0.016f, -0.005f, -0.52f, 0.006f, 0.006f, 0.18f);
		WQ(n, W_POLYMER, 0f, -0.02f, -0.61f, 0.042f, 0.07f, 0.012f);
		// Kimme und Korn
		WQ(n, W_STAHL, 0f, 0.05f, -0.23f, 0.006f, 0.022f, 0.008f);
		WQ(n, W_STAHL, 0f, 0.048f, -0.41f, 0.022f, 0.018f, 0.008f);
		return n.Fertig("SMG");
	}

	static Mesh USniperNetz()
	{
		var n = new Netzbau(4);
		WQ(n, W_STAHL, 0f, 0f, -0.035f, 0.036f, 0.03f, 0.07f);
		WQ(n, W_POLYMER, 0.0185f, 0f, -0.03f, 0.002f, 0.012f, 0.012f);
		WQ(n, W_POLYMER, -0.0185f, 0f, -0.03f, 0.002f, 0.012f, 0.012f);
		WZ(n, W_STAHL, Z(-0.07f), Z(-0.64f), 0.011f, 0.015f);
		// Schaft vorne, Gehaeuse, Kammerstengel
		WQ(n, W_SAND, 0f, -0.022f, -0.68f, 0.05f, 0.05f, 0.3f);
		WZ(n, W_STAHL, Z(-0.66f, 0.006f), Z(-0.92f, 0.006f), 0.022f, 0.022f, 16);
		WZ(n, W_STAHL, new Vector3(0.02f, 0.004f, -0.87f), new Vector3(0.066f, -0.022f, -0.87f), 0.005f, 0.005f, 8);
		WZ(n, W_STAHL, new Vector3(0.064f, -0.021f, -0.87f), new Vector3(0.08f, -0.03f, -0.87f), 0.011f, 0.011f, 10);
		WQ(n, W_STAHL, 0f, -0.05f, -0.81f, 0.03f, 0.05f, 0.08f);
		// Griff, Buegel, Abzug
		WQ(n, W_SAND, 0f, -0.085f, -0.965f, 0.032f, 0.1f, 0.045f, 15f);
		WQ(n, W_STAHL, 0f, -0.05f, -0.9f, 0.012f, 0.004f, 0.06f);
		WQ(n, W_STAHL, 0f, -0.04f, -0.905f, 0.006f, 0.018f, 0.006f);
		// Hinterschaft mit Wangenauflage
		WQ(n, W_SAND, 0f, -0.03f, -1.1f, 0.04f, 0.07f, 0.24f);
		WQ(n, W_SAND, 0f, 0.015f, -1.09f, 0.035f, 0.025f, 0.14f);
		WQ(n, W_POLYMER, 0f, -0.035f, -1.228f, 0.045f, 0.11f, 0.02f);
		// Zweibein, eingeklappt
		WZ(n, W_STAHL, new Vector3(0.016f, -0.045f, -0.54f), new Vector3(0.016f, -0.045f, -0.3f), 0.005f, 0.005f, 8);
		WZ(n, W_STAHL, new Vector3(-0.016f, -0.045f, -0.54f), new Vector3(-0.016f, -0.045f, -0.3f), 0.005f, 0.005f, 8);
		WQ(n, W_STAHL, 0f, -0.035f, -0.55f, 0.04f, 0.02f, 0.03f);
		// Zielfernrohr mit Objektiv, Okular, Tuermen und Ringen
		const float y = 0.068f;
		WZ(n, W_STAHL, Z(-0.64f, y), Z(-0.72f, y), 0.027f, 0.017f, 18);
		WZ(n, W_STAHL, Z(-0.72f, y), Z(-1.0f, y), 0.016f, 0.016f, 18);
		WZ(n, W_STAHL, Z(-1.0f, y), Z(-1.07f, y), 0.017f, 0.021f, 18);
		WZ(n, W_GLAS, Z(-0.638f, y), Z(-0.642f, y), 0.025f, 0.025f, 18);
		WZ(n, W_GLAS, Z(-1.068f, y), Z(-1.072f, y), 0.019f, 0.019f, 18);
		WZ(n, W_STAHL, new Vector3(0f, y + 0.014f, -0.86f), new Vector3(0f, y + 0.034f, -0.86f), 0.011f, 0.011f, 12);
		WZ(n, W_STAHL, new Vector3(0.014f, y, -0.86f), new Vector3(0.034f, y, -0.86f), 0.01f, 0.01f, 12);
		WQ(n, W_STAHL, 0f, 0.04f, -0.79f, 0.022f, 0.035f, 0.016f);
		WQ(n, W_STAHL, 0f, 0.04f, -0.95f, 0.022f, 0.035f, 0.016f);
		return n.Fertig("Sniper");
	}

	// ------------------------------------------------------------------
	// Pflanzen-Netze

	static Mesh UBaumNetz(int saat)
	{
		var z = new System.Random(saat);
		var n = new Netzbau(2);
		float hoehe = Zufall(z, 7f, 8.5f);
		var krone = new Vector3(0f, hoehe * 0.7f, 0f);
		float kronenRadius = hoehe * 0.42f;
		// Stamm mit leichtem Bogen
		var stamm = new List<Vector3> { Vector3.zero };
		var richtung = Vector3.up;
		for (int i = 0; i < 6; i++)
		{
			richtung = (richtung + new Vector3(Zufall(z, -0.12f, 0.12f), 0f, Zufall(z, -0.12f, 0.12f))).normalized;
			stamm.Add(stamm[i] + richtung * hoehe * 0.6f / 6f);
		}
		for (int i = 0; i < 6; i++)
		{
			float r0 = Mathf.Lerp(0.3f, 0.1f, i / 6f);
			float r1 = Mathf.Lerp(0.3f, 0.1f, (i + 1) / 6f);
			// Unten dunkler (Erde, Moos)
			var f0 = Color.Lerp(new Color(0.55f, 0.55f, 0.5f), Color.white, i / 3f);
			var f1 = Color.Lerp(new Color(0.55f, 0.55f, 0.5f), Color.white, (i + 1) / 3f);
			n.Rohr(stamm[i], stamm[i + 1], r0, r1, 9, 0, f0, f1, i * 0.5f, (i + 1) * 0.5f, false);
		}
		var blaetter = new List<Vector3>();
		int aeste = 7 + z.Next(3);
		for (int k = 0; k < aeste; k++)
		{
			float t = Mathf.Lerp(0.38f, 0.95f, k / (aeste - 1f)) + Zufall(z, -0.04f, 0.04f);
			var start = StammPunkt(stamm, t);
			float winkel = k * 137.5f + Zufall(z, -20f, 20f);
			float steigung = Mathf.Lerp(28f, 55f, t) + Zufall(z, -8f, 8f);
			var dir = Quaternion.Euler(-steigung, winkel, 0f) * Vector3.forward;
			UAst(n, z, start, dir, Mathf.Lerp(3.2f, 1.6f, t), Mathf.Lerp(0.1f, 0.05f, t), 1, blaetter);
		}
		// Spitze
		UAst(n, z, stamm[6], (Vector3.up + new Vector3(Zufall(z, -0.2f, 0.2f), 0f, Zufall(z, -0.2f, 0.2f))).normalized, 1.8f, 0.07f, 1, blaetter);
		foreach (var punkt in blaetter)
		{
			UBlattbuschel(n, z, punkt, krone, kronenRadius, 1, 7, 1.2f, 1.75f, 0.5f);
		}
		return n.Fertig("Baum" + saat);
	}

	static Vector3 StammPunkt(List<Vector3> stamm, float t)
	{
		float f = Mathf.Clamp01(t) * (stamm.Count - 1);
		int i = Mathf.Min(stamm.Count - 2, Mathf.FloorToInt(f));
		return Vector3.Lerp(stamm[i], stamm[i + 1], f - i);
	}

	static void UAst(Netzbau n, System.Random z, Vector3 start, Vector3 dir, float laenge, float radius, int stufe, List<Vector3> blaetter)
	{
		var punkte = new List<Vector3> { start };
		for (int i = 0; i < 3; i++)
		{
			// Aeste biegen sich nach aussen leicht nach unten, Spitzen wieder zum Licht.
			dir = (dir + new Vector3(Zufall(z, -0.15f, 0.15f), i == 2 ? 0.15f : -0.08f, Zufall(z, -0.15f, 0.15f))).normalized;
			punkte.Add(punkte[i] + dir * laenge / 3f);
		}
		for (int i = 0; i < 3; i++)
		{
			float r0 = Mathf.Lerp(radius, radius * 0.25f, i / 3f);
			float r1 = Mathf.Lerp(radius, radius * 0.25f, (i + 1) / 3f);
			n.Rohr(punkte[i], punkte[i + 1], r0, r1, 6, 0, Color.white, Color.white, 0f, laenge / 3f, false);
		}
		blaetter.Add(punkte[3]);
		if (stufe == 1)
		{
			blaetter.Add(punkte[2]);
			int zweige = 3 + z.Next(2);
			for (int j = 0; j < zweige; j++)
			{
				float t = Zufall(z, 0.35f, 0.9f);
				var ab = StammPunkt(punkte, t);
				var seite = Quaternion.AngleAxis(Zufall(z, 0f, 360f), dir) * Vector3.Cross(dir, Vector3.up).normalized;
				var zweigDir = (dir + seite * Zufall(z, 0.6f, 1.1f) + Vector3.up * 0.2f).normalized;
				UAst(n, z, ab, zweigDir, laenge * Zufall(z, 0.35f, 0.5f), radius * 0.4f, 2, blaetter);
			}
		}
	}

	// Blattkarten um einen Punkt. Die Normalen zeigen von der Kronenmitte weg: So wird die Krone
	// wie ein Ganzes beleuchtet, nicht jede Karte fuer sich. Innen dunkler (Vertex-Farbe).
	static void UBlattbuschel(Netzbau n, System.Random z, Vector3 punkt, Vector3 mitte, float radius, int teil, int anzahl, float minGroesse, float maxGroesse, float streuung)
	{
		for (int i = 0; i < anzahl; i++)
		{
			var ort = punkt + new Vector3(Zufall(z, -1f, 1f), Zufall(z, -0.7f, 1f), Zufall(z, -1f, 1f)) * streuung;
			var lage = Quaternion.Euler(Zufall(z, -70f, 70f), Zufall(z, 0f, 360f), Zufall(z, -40f, 40f));
			var normale = ((ort - mitte).normalized + Vector3.up * 0.4f).normalized;
			float aussen = Mathf.Clamp01((ort - mitte).magnitude / radius);
			float hell = Mathf.Lerp(0.5f, 1.05f, aussen) * Zufall(z, 0.9f, 1.1f);
			float groesse = Zufall(z, minGroesse, maxGroesse);
			n.Karte(ort, lage, groesse, groesse, normale, new Color(hell, hell, hell * 0.95f), teil);
		}
	}

	static Mesh UBuschNetz(int saat)
	{
		var z = new System.Random(saat);
		var n = new Netzbau(2);
		var mitte = new Vector3(0f, 0.55f, 0f);
		// Ein paar Zweige aus dem Boden
		for (int i = 0; i < 5; i++)
		{
			var dir = Quaternion.Euler(-Zufall(z, 35f, 70f), Zufall(z, 0f, 360f), 0f) * Vector3.forward;
			n.Rohr(Vector3.zero, dir * Zufall(z, 0.8f, 1.2f), 0.035f, 0.01f, 5, 0, new Color(0.6f, 0.6f, 0.6f), Color.white, 0f, 1f, false);
		}
		for (int i = 0; i < 36; i++)
		{
			var richtung = new Vector3(Zufall(z, -1f, 1f), Zufall(z, -0.3f, 1f), Zufall(z, -1f, 1f)).normalized;
			var ort = mitte + Vector3.Scale(richtung, new Vector3(1.05f, 0.7f, 1.05f)) * Zufall(z, 0.45f, 1f);
			ort.y = Mathf.Max(ort.y, 0.25f);
			var lage = Quaternion.Euler(Zufall(z, -60f, 60f), Zufall(z, 0f, 360f), Zufall(z, -40f, 40f));
			var normale = ((ort - mitte).normalized + Vector3.up * 0.5f).normalized;
			float hell = Mathf.Lerp(0.55f, 1.05f, Mathf.Clamp01(ort.y / 1.2f)) * Zufall(z, 0.9f, 1.1f);
			float groesse = Zufall(z, 0.8f, 1.15f);
			n.Karte(ort, lage, groesse, groesse, normale, new Color(hell, hell, hell), 1);
		}
		return n.Fertig("Busch" + saat);
	}

	// Neun Wedel, je ein Streifen mit Knick in der Mitte (V-Form), nach aussen haengend.
	static Mesh UPalmKroneNetz(int saat)
	{
		var z = new System.Random(saat);
		var n = new Netzbau(1);
		for (int w = 0; w < 9; w++)
		{
			float winkel = w * 40f + Zufall(z, -10f, 10f);
			var waagrecht = Quaternion.Euler(0f, winkel, 0f) * Vector3.forward;
			var quer = Vector3.Cross(Vector3.up, waagrecht);
			float laenge = Zufall(z, 3.8f, 4.6f);
			float steigung = Zufall(z, 0.35f, 0.75f);
			float haengen = Zufall(z, 1.1f, 1.6f);
			const int stuecke = 10;
			int basis = n.Anzahl;
			for (int i = 0; i <= stuecke; i++)
			{
				float s = (float)i / stuecke;
				var ruecken = waagrecht * laenge * s + Vector3.up * laenge * (steigung * s - haengen * s * s) * 0.5f;
				float breite = 0.75f;
				float hell = Mathf.Lerp(0.6f, 1.05f, s);
				var farbe = new Color(hell, hell, hell);
				// Seiten etwas tiefer als der Ruecken: Der Wedel ist gefaltet.
				n.Punkt(ruecken - quer * breite - Vector3.up * 0.12f, (Vector3.up - quer * 0.3f).normalized, new Vector2(0f, s), farbe);
				n.Punkt(ruecken, Vector3.up, new Vector2(0.5f, s), farbe);
				n.Punkt(ruecken + quer * breite - Vector3.up * 0.12f, (Vector3.up + quer * 0.3f).normalized, new Vector2(1f, s), farbe);
			}
			for (int i = 0; i < stuecke; i++)
			{
				int a = basis + i * 3;
				int b = a + 3;
				n.Dreieck(0, a, b, a + 1);
				n.Dreieck(0, a + 1, b, b + 1);
				n.Dreieck(0, a + 1, b + 1, a + 2);
				n.Dreieck(0, a + 2, b + 1, b + 2);
			}
		}
		return n.Fertig("Palmkrone");
	}

	// Ein Bueschel aus duennen, gebogenen Halmen (Wurzel dunkel, Spitze hell). Alle Normalen zeigen
	// nach oben: Die Wiese wird so wie ein Ganzes beleuchtet. uv.y = Hoehe im Halm (fuer den Wind).
	// breite > 1 fuer das grobe Bueschel in der Ferne (weniger, breitere Halme).
	static Mesh UGrasNetz(int saat, int halme, float breite)
	{
		var z = new System.Random(saat);
		var n = new Netzbau(1);
		for (int h = 0; h < halme; h++)
		{
			float winkel = Zufall(z, 0f, Mathf.PI * 2f);
			float r = Mathf.Sqrt((float)z.NextDouble()) * 0.22f;
			var fuss = new Vector3(Mathf.Cos(winkel) * r, 0f, Mathf.Sin(winkel) * r);
			float hoehe = Zufall(z, 0.38f, 0.72f);
			float b = Zufall(z, 0.018f, 0.03f) * breite;
			// Halme neigen sich nach aussen und leicht in eine gemeinsame Richtung
			var richtung = (new Vector3(fuss.x, 0f, fuss.z).normalized * 0.6f + Quaternion.Euler(0f, Zufall(z, 0f, 360f), 0f) * Vector3.forward * 0.4f).normalized;
			var quer = Vector3.Cross(Vector3.up, richtung).normalized;
			float biegung = Zufall(z, 0.08f, 0.3f);
			float frisch = Zufall(z, 0f, 1f);
			var spitze = Color.Lerp(new Color(0.33f, 0.55f, 0.15f), new Color(0.43f, 0.64f, 0.2f), frisch);
			var mitte = Color.Lerp(new Color(0.13f, 0.3f, 0.07f), new Color(0.18f, 0.37f, 0.08f), frisch);
			var wurzel = new Color(0.06f, 0.13f, 0.04f);
			const int stuecke = 4;
			int basis = n.Anzahl;
			for (int i = 0; i <= stuecke; i++)
			{
				float s = (float)i / stuecke;
				var ort = fuss + Vector3.up * hoehe * s + richtung * biegung * s * s;
				// Spitz zulaufend, ganz oben ein Punkt
				float halbe = b * (1f - s);
				var farbe = s < 0.5f ? Color.Lerp(wurzel, mitte, s * 2f) : Color.Lerp(mitte, spitze, (s - 0.5f) * 2f);
				n.Punkt(ort - quer * halbe, Vector3.up, new Vector2(0f, s), farbe);
				n.Punkt(ort + quer * halbe, Vector3.up, new Vector2(1f, s), farbe);
			}
			for (int i = 0; i < stuecke; i++)
			{
				int a = basis + i * 2;
				n.Dreieck(0, a, a + 2, a + 1);
				n.Dreieck(0, a + 1, a + 2, a + 3);
			}
		}
		return n.Fertig("Grasbueschel" + saat);
	}

	// ------------------------------------------------------------------
	// Texturen

	delegate Color UPixel(float u, float v);

	static Texture2D UTextur(string name, int breite, int hoehe, UPixel pixel, bool alpha, TextureWrapMode wrap, float schnitt)
	{
		var punkte = new Color[breite * hoehe];
		for (int y = 0; y < hoehe; y++)
		{
			for (int x = 0; x < breite; x++)
			{
				punkte[y * breite + x] = pixel((x + 0.5f) / breite, (y + 0.5f) / hoehe);
			}
		}
		return UTexturAusPunkten(name, breite, hoehe, punkte, alpha, wrap, schnitt);
	}

	static Texture2D UTexturAusPunkten(string name, int breite, int hoehe, Color[] punkte, bool alpha, TextureWrapMode wrap, float schnitt)
	{
		var textur = new Texture2D(breite, hoehe, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
		textur.SetPixels(punkte);
		textur.Apply();
		var png = textur.EncodeToPNG();
		Object.DestroyImmediate(textur);
		var pfad = ULTRA + "/" + name + ".png";
		File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, pfad), png);
		AssetDatabase.ImportAsset(pfad, ImportAssetOptions.ForceUpdate);
		var importer = (TextureImporter)AssetImporter.GetAtPath(pfad);
		importer.textureType = TextureImporterType.Default;
		importer.wrapMode = wrap;
		importer.alphaIsTransparency = alpha;
		importer.maxTextureSize = 1024;
		if (schnitt > 0f)
		{
			// Ohne das verschwinden ausgeschnittene Blaetter in der Ferne (kleinere Mipmaps).
			importer.mipMapsPreserveCoverage = true;
			importer.alphaTestReferenceValue = schnitt;
		}
		importer.SaveAndReimport();
		return AssetDatabase.LoadAssetAtPath<Texture2D>(pfad);
	}

	static Material ULaubMaterial(string name, Texture2D textur, float wind)
	{
		var shader = Shader.Find("Shootout/Laub") ?? Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
		var material = new Material(shader);
		material.name = name;
		material.mainTexture = textur;
		material.SetFloat("_Cutoff", 0.45f);
		material.SetFloat("_Wind", wind);
		return AlsAsset(material, ULTRA + "/" + name + ".mat");
	}

	static Material UPartikelMaterial(string name, string shader, Texture2D textur)
	{
		var material = new Material(Shader.Find(shader));
		material.name = name;
		material.mainTexture = textur;
		return AlsAsset(material, ULTRA + "/" + name + ".mat");
	}

	// Viele einzelne Blaetter an einem Zweig, auf durchsichtigem Grund.
	static Texture2D ULaubTextur(string name, int saat, int anzahl, float minLaenge, float maxLaenge, Color dunkel, Color hell)
	{
		const int g = 512;
		var z = new System.Random(saat);
		var punkte = new Color[g * g];
		// Durchsichtig, aber mit Blattfarbe: Sonst entstehen an den Raendern dunkle Saeume.
		var grund = new Color(dunkel.r, dunkel.g, dunkel.b, 0f);
		for (int i = 0; i < punkte.Length; i++)
		{
			punkte[i] = grund;
		}
		// Zweig von unten in die Mitte
		var zweigFarbe = new Color(0.3f, 0.22f, 0.14f, 1f);
		for (float t = 0f; t < 0.62f; t += 0.002f)
		{
			ULinie(punkte, g, 0.5f + 0.04f * Mathf.Sin(t * 9f), t, 0.006f * (1.2f - t), zweigFarbe);
		}
		var basis = new Vector2(0.5f, 0.08f);
		for (int i = 0; i < anzahl; i++)
		{
			float r = Mathf.Sqrt((float)z.NextDouble()) * 0.36f;
			float w = (float)z.NextDouble() * Mathf.PI * 2f;
			var mitte = new Vector2(0.5f, 0.55f) + new Vector2(Mathf.Cos(w), Mathf.Sin(w) * 0.95f) * r;
			var weg = mitte - basis;
			float winkel = Mathf.Atan2(weg.y, weg.x) + Zufall(z, -0.6f, 0.6f);
			float laenge = Zufall(z, minLaenge, maxLaenge);
			var farbe = Color.Lerp(dunkel, hell, (float)z.NextDouble()) * Zufall(z, 0.85f, 1.15f);
			UBlatt(punkte, g, mitte, winkel, laenge, laenge * 0.42f, farbe);
		}
		return UTexturAusPunkten(name, g, g, punkte, true, TextureWrapMode.Clamp, 0.45f);
	}

	static void ULinie(Color[] punkte, int g, float u, float v, float dicke, Color farbe)
	{
		int r = Mathf.CeilToInt(dicke * g);
		int cx = Mathf.RoundToInt(u * g);
		int cy = Mathf.RoundToInt(v * g);
		for (int y = cy - r; y <= cy + r; y++)
		{
			for (int x = cx - r; x <= cx + r; x++)
			{
				if (x >= 0 && y >= 0 && x < g && y < g && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
				{
					punkte[y * g + x] = farbe;
				}
			}
		}
	}

	// Ein Blatt: spitz zulaufend, Mittelrippe dunkler, eine Haelfte heller (Licht von der Seite).
	static void UBlatt(Color[] punkte, int g, Vector2 mitte, float winkel, float laenge, float breite, Color farbe)
	{
		var achse = new Vector2(Mathf.Cos(winkel), Mathf.Sin(winkel));
		var quer = new Vector2(-achse.y, achse.x);
		float aussen = laenge * 0.5f + breite;
		int x0 = Mathf.Max(0, Mathf.FloorToInt((mitte.x - aussen) * g));
		int x1 = Mathf.Min(g - 1, Mathf.CeilToInt((mitte.x + aussen) * g));
		int y0 = Mathf.Max(0, Mathf.FloorToInt((mitte.y - aussen) * g));
		int y1 = Mathf.Min(g - 1, Mathf.CeilToInt((mitte.y + aussen) * g));
		for (int y = y0; y <= y1; y++)
		{
			for (int x = x0; x <= x1; x++)
			{
				var d = new Vector2((x + 0.5f) / g, (y + 0.5f) / g) - mitte;
				float a = Vector2.Dot(d, achse) / laenge + 0.5f;
				float b = Vector2.Dot(d, quer);
				if (a < 0f || a > 1f)
				{
					continue;
				}
				float halb = breite * 0.5f * Mathf.Pow(Mathf.Sin(a * Mathf.PI), 0.75f);
				if (Mathf.Abs(b) > halb)
				{
					continue;
				}
				float rand = Mathf.Abs(b) / Mathf.Max(halb, 0.0001f);
				float licht = 0.9f + 0.18f * Mathf.Sign(b) - 0.15f * rand * rand;
				if (Mathf.Abs(b) < 0.0025f)
				{
					licht *= 0.75f;
				}
				var c = farbe * licht;
				c.a = 1f;
				punkte[y * g + x] = c;
			}
		}
	}

	// Palmwedel: Mittelrippe, schraege Fiederblaetter, zur Spitze kuerzer.
	static Texture2D UWedelTextur()
	{
		return UTextur("Palmwedel", 256, 512, (u, v) =>
		{
			float d = Mathf.Abs(u - 0.5f);
			var grund = new Color(0.2f, 0.32f, 0.12f, 0f);
			if (d < 0.014f)
			{
				return new Color(0.42f, 0.4f, 0.22f, 1f);
			}
			float breite = 0.48f * Mathf.Sin(Mathf.Clamp01(v * 1.05f + 0.02f) * Mathf.PI * 0.95f);
			if (d > breite)
			{
				return grund;
			}
			// Fiedern verlaufen schraeg nach aussen und zur Spitze.
			float s = v * 46f - d * 26f;
			float fieder = s - Mathf.Floor(s);
			float dicke = 0.62f * (1f - d / Mathf.Max(breite, 0.01f)) + 0.12f;
			if (fieder > dicke)
			{
				return grund;
			}
			float nummer = Mathf.Floor(s);
			float zufall = Mathf.Repeat(Mathf.Sin(nummer * 12.9898f) * 43758.5453f, 1f);
			float hell = 0.75f + 0.35f * zufall - 0.25f * (d / Mathf.Max(breite, 0.01f));
			var farbe = Color.Lerp(new Color(0.18f, 0.36f, 0.12f), new Color(0.36f, 0.52f, 0.18f), zufall) * hell;
			farbe.a = 1f;
			return farbe;
		}, true, TextureWrapMode.Clamp, 0.45f);
	}

	static UPixel URindePixel()
	{
		return (u, v) =>
		{
			// Laengsrillen: in u fein, in v grob (beide Richtungen nahtlos).
			float rillen = URauschen(u * 24f, v * 4f, 24, 4, 3);
			float grob = URauschen(u * 6f, v * 2f, 6, 2, 9);
			float risse = Mathf.Clamp01((rillen - 0.32f) * 4f);
			var farbe = new Color(0.33f, 0.26f, 0.19f) * (0.55f + 0.35f * grob + 0.25f * rillen);
			return Color.Lerp(farbe * 0.45f, farbe, risse);
		};
	}

	static float URauschen(float x, float y, int zx, int zy, int saat)
	{
		int x0 = Mathf.FloorToInt(x);
		int y0 = Mathf.FloorToInt(y);
		float fx = x - x0;
		float fy = y - y0;
		fx = fx * fx * (3f - 2f * fx);
		fy = fy * fy * (3f - 2f * fy);
		float a = UWert(x0, y0, zx, zy, saat);
		float b = UWert(x0 + 1, y0, zx, zy, saat);
		float c = UWert(x0, y0 + 1, zx, zy, saat);
		float d = UWert(x0 + 1, y0 + 1, zx, zy, saat);
		return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
	}

	static float UWert(int x, int y, int zx, int zy, int saat)
	{
		x = ((x % zx) + zx) % zx;
		y = ((y % zy) + zy) % zy;
		return KWert(x * 131 + y, y * 7 + x, 1 << 20, saat);
	}

	// Daumenkino 4 x 4: Flamme, deren Zungen nach oben wandern. Das letzte Bild geht nahtlos ins erste.
	static UPixel UFlammenPixel()
	{
		return (u, v) =>
		{
			int spalte = Mathf.Min(3, Mathf.FloorToInt(u * 4f));
			int zeile = Mathf.Min(3, Mathf.FloorToInt(v * 4f));
			int bild = (3 - zeile) * 4 + spalte;
			float t = bild / 16f;
			float x = u * 4f - spalte;
			float y = v * 4f - zeile;
			float wackeln = (KWolke(Mathf.Repeat(y * 0.4f - t, 1f), 0.3f, 5) - 0.5f) * 0.5f * y;
			// Runde Basis, nach oben schmal
			float breite = 0.36f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1f, 0.15f + y * 0.85f)), 0.8f) * (1f - y * 0.5f) + 0.01f;
			float abstand = Mathf.Abs(x - 0.5f - wackeln) / breite;
			float form = Mathf.Clamp01(1f - abstand * abstand);
			// Grobe und feine Wirbel, die feinen steigen doppelt so schnell (Zungen, Fetzen).
			float grob = KWolke(x * 0.7f, Mathf.Repeat(y * 0.7f - t, 1f), 3);
			float fein = KWolke(x * 1.4f + 0.5f, Mathf.Repeat(y * 1.2f - 2f * t, 1f), 11);
			float n = grob * 0.55f + fein * 0.45f;
			float flamme = Mathf.Clamp01(Mathf.Clamp01(form * (0.3f + n) - y * 0.5f - 0.08f) * 1.6f);
			flamme = Mathf.Pow(flamme, 1.6f) * Mathf.SmoothStep(0f, 1f, y / 0.12f);
			// Am Rand des Bildes sicher null, sonst sieht man Kanten.
			flamme *= Mathf.Clamp01(Mathf.Min(x, 1f - x, y, 1f - y) * 12f);
			return new Color(flamme, flamme, flamme, flamme);
		};
	}

	static Color UPunkt(float u, float v, float haerte)
	{
		float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
		float a = Mathf.Pow(Mathf.Clamp01(1f - r), haerte);
		return new Color(1f, 1f, 1f, a);
	}

	static Color URingPixel(float u, float v)
	{
		float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
		float ring = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.08f, 2f));
		float innen = Mathf.Clamp01(1f - r) * 0.15f;
		return new Color(1f, 1f, 1f, Mathf.Clamp01(ring * 0.9f + innen) * Mathf.Clamp01((1f - r) * 8f));
	}

	static Color UBlitzPixel(float u, float v)
	{
		var d = new Vector2(u - 0.5f, v - 0.5f);
		float r = d.magnitude * 2f;
		float winkel = Mathf.Atan2(d.y, d.x);
		float kern = Mathf.Pow(Mathf.Clamp01(1f - r * 1.6f), 1.5f);
		float strahlen = Mathf.Pow(Mathf.Abs(Mathf.Cos(winkel * 3f)), 24f) * Mathf.Clamp01(1f - r);
		float a = Mathf.Clamp01(kern + strahlen * 0.8f);
		return new Color(1f, 1f, 1f, a);
	}

	// ------------------------------------------------------------------
	// Boden-Details: kachelnde Textur mit Koernung, Steinchen und Relief (Detail-Karten des
	// Standard-Shaders, liegen ueber der groben Farbtextur der Map). Alle Grafikstufen.

	static Texture2D uBodenDetail;
	static Texture2D uBodenRelief;

	static void BodenDetailsSetzen(Material material, Vector2 kacheln)
	{
		if (uBodenDetail == null || uBodenRelief == null)
		{
			uBodenDetail = UTextur("BodenDetail", 512, 512, (u, v) =>
			{
				// Mittelwert um 0,5: Der Standard-Shader rechnet Details mal 2, die Grundfarbe bleibt.
				float h = UBodenHoehe(u, v);
				float korn = KWert(Mathf.FloorToInt(u * 512f), Mathf.FloorToInt(v * 512f), 512, 91);
				float wert = 0.5f + (h - 0.45f) * 0.35f + (korn - 0.5f) * 0.07f;
				return new Color(wert, wert, wert * 0.98f);
			}, false, TextureWrapMode.Repeat, 0f);
			uBodenRelief = UNormalTextur("BodenRelief", 512, UBodenHoehe, 8f);
		}
		material.EnableKeyword("_NORMALMAP");
		material.EnableKeyword("_DETAIL_MULX2");
		material.SetTexture("_DetailAlbedoMap", uBodenDetail);
		material.SetTexture("_DetailNormalMap", uBodenRelief);
		material.SetFloat("_DetailNormalMapScale", 0.9f);
		material.SetTextureScale("_DetailAlbedoMap", kacheln);
		EditorUtility.SetDirty(material);
	}

	// Hoehe des Bodens im Kleinen (0..1, nahtlos): Wellen im Sand, Klumpen, Steinchen.
	static float UBodenHoehe(float u, float v)
	{
		float grob = KWolke(u, v, 81);
		float fein = KWolke(Mathf.Repeat(u * 3f, 1f), Mathf.Repeat(v * 3f, 1f), 83);
		float korn = KWolke(Mathf.Repeat(u * 8f, 1f), Mathf.Repeat(v * 8f, 1f), 84);
		// Steinchen: 12 x 12 Zellen, in gut einem Viertel ein unregelmaessiges Steinchen.
		const int zellen = 12;
		float stein = 0f;
		int zx = Mathf.FloorToInt(u * zellen);
		int zy = Mathf.FloorToInt(v * zellen);
		float verzerrung = 1f + 0.6f * (KWolke(Mathf.Repeat(u * 4f, 1f), Mathf.Repeat(v * 4f, 1f), 89) - 0.5f);
		for (int dy = -1; dy <= 1; dy++)
		{
			for (int dx = -1; dx <= 1; dx++)
			{
				int cx = zx + dx;
				int cy = zy + dy;
				if (KWert(cx, cy, zellen, 85) < 0.72f)
				{
					continue;
				}
				float mx = (cx + 0.2f + 0.6f * KWert(cx, cy, zellen, 86)) / zellen;
				float my = (cy + 0.2f + 0.6f * KWert(cx, cy, zellen, 87)) / zellen;
				float r = 0.008f + 0.02f * KWert(cx, cy, zellen, 88);
				float d = new Vector2((u - mx) * 1.3f, v - my).magnitude / r * verzerrung;
				stein = Mathf.Max(stein, Mathf.Sqrt(Mathf.Clamp01(1f - d * d)) * (0.5f + 0.5f * KWert(cx, cy, zellen, 90)));
			}
		}
		return Mathf.Clamp01(grob * 0.45f + fein * 0.3f + korn * 0.25f + stein * 0.35f);
	}

	delegate float UHoehe(float u, float v);

	// Normalenkarte aus einer nahtlosen Hoehe (Mittelwert der Nachbarn).
	static Texture2D UNormalTextur(string name, int groesse, UHoehe hoehe, float staerke)
	{
		var werte = new float[groesse * groesse];
		for (int y = 0; y < groesse; y++)
		{
			for (int x = 0; x < groesse; x++)
			{
				werte[y * groesse + x] = hoehe((x + 0.5f) / groesse, (y + 0.5f) / groesse);
			}
		}
		var punkte = new Color[groesse * groesse];
		for (int y = 0; y < groesse; y++)
		{
			for (int x = 0; x < groesse; x++)
			{
				float l = werte[y * groesse + (x + groesse - 1) % groesse];
				float r = werte[y * groesse + (x + 1) % groesse];
				float u = werte[((y + groesse - 1) % groesse) * groesse + x];
				float o = werte[((y + 1) % groesse) * groesse + x];
				var n = new Vector3((l - r) * staerke, (u - o) * staerke, 1f).normalized;
				punkte[y * groesse + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
			}
		}
		var textur = UTexturAusPunkten(name, groesse, groesse, punkte, false, TextureWrapMode.Repeat, 0f);
		var pfad = AssetDatabase.GetAssetPath(textur);
		var importer = (TextureImporter)AssetImporter.GetAtPath(pfad);
		importer.textureType = TextureImporterType.NormalMap;
		importer.SaveAndReimport();
		return AssetDatabase.LoadAssetAtPath<Texture2D>(pfad);
	}

	// ------------------------------------------------------------------
	// Netze zusammensetzen

	class Netzbau
	{
		readonly List<Vector3> orte = new List<Vector3>();
		readonly List<Vector3> normalen = new List<Vector3>();
		readonly List<Vector2> uv = new List<Vector2>();
		readonly List<Color> farben = new List<Color>();
		readonly List<int>[] dreiecke;

		public Netzbau(int teile)
		{
			dreiecke = new List<int>[teile];
			for (int i = 0; i < teile; i++)
			{
				dreiecke[i] = new List<int>();
			}
		}

		public int Anzahl => orte.Count;

		public int Punkt(Vector3 ort, Vector3 normale, Vector2 koordinate, Color farbe)
		{
			orte.Add(ort);
			normalen.Add(normale);
			uv.Add(koordinate);
			farben.Add(farbe);
			return orte.Count - 1;
		}

		public void Dreieck(int teil, int a, int b, int c)
		{
			dreiecke[teil].Add(a);
			dreiecke[teil].Add(b);
			dreiecke[teil].Add(c);
		}

		// Rohr von a nach b, Radius ra nach rb, wahlweise mit Deckeln.
		public void Rohr(Vector3 a, Vector3 b, float ra, float rb, int seiten, int teil, Color fa, Color fb, float vVon, float vBis, bool deckel)
		{
			var achse = (b - a).normalized;
			var hilfe = Mathf.Abs(achse.y) < 0.95f ? Vector3.up : Vector3.right;
			var u = Vector3.Cross(achse, hilfe).normalized;
			var w = Vector3.Cross(achse, u);
			int basis = Anzahl;
			for (int i = 0; i <= seiten; i++)
			{
				float winkel = i * Mathf.PI * 2f / seiten;
				var richtung = u * Mathf.Cos(winkel) + w * Mathf.Sin(winkel);
				Punkt(a + richtung * ra, richtung, new Vector2((float)i / seiten, vVon), fa);
				Punkt(b + richtung * rb, richtung, new Vector2((float)i / seiten, vBis), fb);
			}
			for (int i = 0; i < seiten; i++)
			{
				int p = basis + i * 2;
				// Im Uhrzeigersinn von aussen gesehen (Unitys Vorderseite).
				Dreieck(teil, p, p + 2, p + 1);
				Dreieck(teil, p + 1, p + 2, p + 3);
			}
			if (!deckel)
			{
				return;
			}
			foreach (var ende in new[] { 0, 1 })
			{
				var ort = ende == 0 ? a : b;
				float r = ende == 0 ? ra : rb;
				var normale = ende == 0 ? -achse : achse;
				int mitte = Punkt(ort, normale, new Vector2(0.5f, 0.5f), fa);
				int rand = Anzahl;
				for (int i = 0; i <= seiten; i++)
				{
					float winkel = i * Mathf.PI * 2f / seiten;
					Punkt(ort + (u * Mathf.Cos(winkel) + w * Mathf.Sin(winkel)) * r, normale, new Vector2(0.5f + 0.5f * Mathf.Cos(winkel), 0.5f + 0.5f * Mathf.Sin(winkel)), fa);
				}
				for (int i = 0; i < seiten; i++)
				{
					if (ende == 0)
					{
						Dreieck(teil, mitte, rand + i + 1, rand + i);
					}
					else
					{
						Dreieck(teil, mitte, rand + i, rand + i + 1);
					}
				}
			}
		}

		// Wuerfel der Groesse 1, mit m verschoben, gedreht und skaliert.
		public void Quader(Matrix4x4 m, int teil, Color farbe)
		{
			var seiten = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
			foreach (var n in seiten)
			{
				var a = n.y != 0f ? Vector3.right : Vector3.up;
				var b = Vector3.Cross(n, a);
				int basis = Anzahl;
				var welt = m.MultiplyVector(n).normalized;
				for (int i = 0; i < 4; i++)
				{
					float sa = (i == 1 || i == 2) ? 0.5f : -0.5f;
					float sb = i >= 2 ? 0.5f : -0.5f;
					Punkt(m.MultiplyPoint3x4(n * 0.5f + a * sa + b * sb), welt, new Vector2(sa + 0.5f, sb + 0.5f), farbe);
				}
				// Reihenfolge so, dass die Vorderseite nach aussen (n) zeigt.
				if (Vector3.Dot(Vector3.Cross(a, b), n) > 0f)
				{
					Dreieck(teil, basis, basis + 1, basis + 2);
					Dreieck(teil, basis, basis + 2, basis + 3);
				}
				else
				{
					Dreieck(teil, basis, basis + 2, basis + 1);
					Dreieck(teil, basis, basis + 3, basis + 2);
				}
			}
		}

		// Flache Karte (Blattbueschel), beidseitig sichtbar ueber den Shader (Cull Off).
		public void Karte(Vector3 mitte, Quaternion lage, float breite, float hoehe, Vector3 normale, Color farbe, int teil)
		{
			var rechts = lage * Vector3.right * breite * 0.5f;
			var oben = lage * Vector3.up * hoehe * 0.5f;
			int a = Punkt(mitte - rechts - oben, normale, new Vector2(0f, 0f), farbe);
			int b = Punkt(mitte + rechts - oben, normale, new Vector2(1f, 0f), farbe);
			int c = Punkt(mitte + rechts + oben, normale, new Vector2(1f, 1f), farbe);
			int d = Punkt(mitte - rechts + oben, normale, new Vector2(0f, 1f), farbe);
			Dreieck(teil, a, c, b);
			Dreieck(teil, a, d, c);
		}

		public Mesh Fertig(string name)
		{
			var netz = new Mesh();
			netz.name = name;
			if (orte.Count > 65000)
			{
				netz.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			}
			netz.SetVertices(orte);
			netz.SetNormals(normalen);
			netz.SetUVs(0, uv);
			netz.SetColors(farben);
			netz.subMeshCount = dreiecke.Length;
			for (int i = 0; i < dreiecke.Length; i++)
			{
				netz.SetTriangles(dreiecke[i], i);
			}
			netz.RecalculateBounds();
			netz.RecalculateTangents();
			return netz;
		}
	}
}
