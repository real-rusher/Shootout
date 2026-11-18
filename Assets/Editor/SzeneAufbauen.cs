using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Baut Szene, UI und Prefabs aus den zurueckgewonnenen Daten in docs/ auf
// (Menue Shootout > Szene und Prefabs aufbauen). Ersetzt den Nachbau von Hand.
//
// Quellen:
//   docs/ui_layout.json                 ganzes UI mit Positionen, Farben, Texten, Knopf-Verdrahtung
//   docs/ui_layout_PlayerRowPrefab.json eine Zeile des Scoreboards
//   docs/werte.json                     Tuning-Werte, Spawn-Zonen, Lichter
//   Assets/Models/*.glb                 Maps und Laserpointer (glTFast importiert sie)
//
// Was die Daten nicht hergeben und deshalb geschaetzt ist, steht als Kommentar
// mit "GESCHAETZT" dabei (Positionen von Lichtern und Menue-Kamera, Groesse des
// Laserstrahls, Lage des Laserpointers, Namensschild).
//
// Ein zweiter Aufruf baut alles neu und ueberschreibt Szene und Prefabs.
public static partial class SzeneAufbauen
{
	const string SZENE = "Assets/Scenes/SampleScene.unity";
	const string PREFABS = "Assets/Prefabs";
	const string ERZEUGT = "Assets/Erzeugt";
	const string UI_DATEN = "docs/ui_layout.json";
	const string ZEILE_DATEN = "docs/ui_layout_PlayerRowPrefab.json";
	const string WERTE_DATEN = "docs/werte.json";
	const string GLB_ZIMMER = "Assets/Models/Map_Zimmer.glb";
	const string GLB_INFORAUM = "Assets/Models/Map_InfoRaum.glb";
	const string GLB_SPIELER = "Assets/Models/Player.glb";
	const string EINGABE = "Assets/Input/Shootout.inputactions";

	// GESCHAETZT: Im Build ist nur die Laenge (ueber localScale.z) festgelegt.
	// So ist der Strahl bei effectWidth 40 etwa 4 cm dick.
	const float LASER_HALBE_DICKE = 0.0005f;
	// LaserEffect skaliert z mit 100 * Entfernung, das Netz muss also 1/100 lang sein.
	const float LASER_LAENGE = 0.01f;

	// Welche Grafik hinter welcher Sprite-Nummer im Build steckt. Abgeleitet aus
	// den Stellen, an denen sie benutzt werden, und den Bildgroessen.
	static readonly Dictionary<string, string> SPRITES = new Dictionary<string, string>
	{
		{ "ext:3:117", "builtin:UI/Skin/Checkmark.psd" },
		{ "ext:3:118", "builtin:UI/Skin/UISprite.psd" },
		{ "ext:3:119", "builtin:UI/Skin/Background.psd" },
		{ "ext:3:120", "builtin:UI/Skin/InputFieldBackground.psd" },
		{ "ext:3:121", "builtin:UI/Skin/UIMask.psd" },
		{ "ext:3:122", "Assets/Sprites/BedRoom.png" },
		{ "ext:3:123", "Assets/Sprites/Crosshair.png" },
		{ "ext:3:124", "Assets/Sprites/screen1_button.png" },
		{ "ext:3:125", "Assets/Sprites/settings.png" },
		{ "ext:3:126", "Assets/Sprites/Hitmarker.png" },
		{ "ext:3:127", "Assets/Sprites/s4_joining.png" },
		{ "ext:3:128", "Assets/Sprites/copy_all.png" },
		{ "ext:3:130", "Assets/Sprites/InfoRaum.png" },
		{ "ext:3:131", "Assets/Sprites/logout.png" },
	};

	[MenuItem("Shootout/Szene und Prefabs aufbauen")]
	public static void Aufbauen()
	{
		if (!VorbedingungenErfuellt())
		{
			return;
		}
		try
		{
			Ordner(PREFABS);
			Ordner(ERZEUGT);
			Ordner("Assets/Scenes");

			var werte = D(JsonDatei(WERTE_DATEN));
			var komponenten = D(werte["komponenten"]);
			var szenenWerte = D(werte["szene"]);

			Fortschritt("Sprites", 0.05f);
			LayerVorschauEinrichten();
			// Im Editor wurden Shader beim ersten Zeigen im Hintergrund uebersetzt, bis dahin
			// war alles kurz blau. Ohne das wartet der Editor kurz, der Ladebildschirm deckt es ab.
			EditorSettings.asyncShaderCompilation = false;
			SpritesEinstellen();
			ZielfernrohrErzeugen();

			Fortschritt("Laser", 0.1f);
			var laser = LaserPrefab(komponenten);

			Fortschritt("Scoreboard-Zeile", 0.2f);
			var zeile = ZeilenPrefab();

			Fortschritt("Spieler", 0.3f);
			var spieler = SpielerPrefab(komponenten, laser);

			Fortschritt("Bot", 0.35f);
			var bot = BotPrefab(spieler);

			Fortschritt("Maps", 0.45f);
			var zimmer = MapPrefab(GLB_ZIMMER, "Shooter-Map", "Map_Zimmer", MapWerte(szenenWerte, "Map1_"), "Point Light Raum", szenenWerte);
			var infoRaum = MapPrefab(GLB_INFORAUM, "MessyRoomparts", "Map_InfoRaum", MapWerte(szenenWerte, "Map2_"), "Point Light Info", szenenWerte);

			Fortschritt("D-Day-Map", 0.5f);
			var dDay = DDayKarte();

			Fortschritt("Szene", 0.7f);
			SzeneBauen(szenenWerte, spieler, zeile, zimmer, infoRaum, bot, dDay);

			EditorUtility.ClearProgressBar();
			EditorUtility.DisplayDialog("Shootout",
				"Fertig: " + SZENE + " und die Prefabs in " + PREFABS + ".\n\n" +
				"Zum Testen: Play druecken, Namen eingeben, Host a LAN Game, eine Map waehlen. " +
				"Fuer Host und Client gleichzeitig: ParrelSync > Clones Manager.", "OK");
		}
		catch (Exception fehler)
		{
			EditorUtility.ClearProgressBar();
			Debug.LogException(fehler);
			EditorUtility.DisplayDialog("Shootout", "Aufbau abgebrochen:\n" + fehler.Message + "\n\nDetails in der Konsole.", "OK");
		}
	}

	// ------------------------------------------------------------------
	// Vorbedingungen

	static bool VorbedingungenErfuellt()
	{
#if !ENABLE_INPUT_SYSTEM
		EditorUtility.DisplayDialog("Shootout",
			"Das neue Input System ist nicht aktiv.\n\nEdit > Project Settings > Player > Other Settings > " +
			"Active Input Handling auf \"Input System Package (New)\" oder \"Both\" stellen. Unity startet neu, " +
			"danach das Menue noch einmal aufrufen.", "OK");
		return false;
#else
		// Ohne die TMP-Grundausstattung (Schrift, Einstellungen) bleiben alle Texte unsichtbar.
		if (Resources.Load<TMP_Settings>("TMP Settings") == null)
		{
			TMP_PackageResourceImporter.ImportResources(true, false, false);
			EditorUtility.DisplayDialog("Shootout",
				"TextMeshPro Essentials werden gerade importiert. Danach das Menue Shootout > Szene und Prefabs aufbauen noch einmal aufrufen.", "OK");
			return false;
		}
		foreach (var glb in new[] { GLB_ZIMMER, GLB_INFORAUM, GLB_SPIELER })
		{
			if (AssetDatabase.LoadAssetAtPath<GameObject>(glb) == null)
			{
				EditorUtility.DisplayDialog("Shootout",
					glb + " ist nicht als Modell importiert. Ist das Paket glTFast installiert (Packages/manifest.json) und sind die Dateien echte GLBs (git lfs pull)?", "OK");
				return false;
			}
		}
		if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(EINGABE) == null)
		{
			EditorUtility.DisplayDialog("Shootout", EINGABE + " fehlt.", "OK");
			return false;
		}
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
		{
			return false;
		}
		if (File.Exists(SZENE) && !EditorUtility.DisplayDialog("Shootout",
			SZENE + " und die Prefabs gibt es schon. Neu aufbauen und ueberschreiben? Aenderungen von Hand an Szene und Prefabs gehen verloren.",
			"Neu aufbauen", "Abbrechen"))
		{
			return false;
		}
		return true;
#endif
	}

	// ------------------------------------------------------------------
	// Sprites

	static void SpritesEinstellen()
	{
		foreach (var pfad in SPRITES.Values)
		{
			if (pfad.StartsWith("builtin:"))
			{
				continue;
			}
			var importer = AssetImporter.GetAtPath(pfad) as TextureImporter;
			if (importer == null)
			{
				throw new Exception("Bild fehlt: " + pfad);
			}
			if (importer.textureType != TextureImporterType.Sprite)
			{
				importer.textureType = TextureImporterType.Sprite;
				importer.spriteImportMode = SpriteImportMode.Single;
				importer.alphaIsTransparency = true;
				importer.mipmapEnabled = false;
				importer.SaveAndReimport();
			}
		}
	}

	static Sprite SpriteFuer(string kennung)
	{
		string pfad;
		if (kennung == null || !SPRITES.TryGetValue(kennung, out pfad))
		{
			if (kennung != null)
			{
				Debug.LogWarning("Shootout: unbekannte Sprite-Nummer " + kennung);
			}
			return null;
		}
		if (pfad.StartsWith("builtin:"))
		{
			return AssetDatabase.GetBuiltinExtraResource<Sprite>(pfad.Substring("builtin:".Length));
		}
		return AssetDatabase.LoadAssetAtPath<Sprite>(pfad);
	}

	// Sprite ueber Kennung aus dem Build (ext:3:...) oder direkt als "datei:Pfad" / "builtin:Pfad".
	static Sprite SpriteAusKennung(string kennung)
	{
		if (kennung != null && kennung.StartsWith("datei:"))
		{
			return AssetDatabase.LoadAssetAtPath<Sprite>(kennung.Substring("datei:".Length));
		}
		if (kennung != null && kennung.StartsWith("builtin:"))
		{
			return AssetDatabase.GetBuiltinExtraResource<Sprite>(kennung.Substring("builtin:".Length));
		}
		return SpriteFuer(kennung);
	}

	// ------------------------------------------------------------------
	// Laser

	static GameObject LaserPrefab(Dictionary<string, object> komponenten)
	{
		// Quader von z = -LASER_LAENGE bis 0: LaserEffect stellt das Objekt an den
		// Laserpointer und dreht es so, dass -z zum Trefferpunkt zeigt.
		var wuerfel = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
		var netz = new Mesh();
		netz.name = "LaserStrahl";
		var punkte = wuerfel.vertices;
		for (int i = 0; i < punkte.Length; i++)
		{
			var p = punkte[i];
			punkte[i] = new Vector3(p.x * 2f * LASER_HALBE_DICKE, p.y * 2f * LASER_HALBE_DICKE, (p.z - 0.5f) * LASER_LAENGE);
		}
		netz.vertices = punkte;
		netz.normals = wuerfel.normals;
		netz.uv = wuerfel.uv;
		netz.triangles = wuerfel.triangles;
		netz.RecalculateBounds();
		netz = AlsAsset(netz, ERZEUGT + "/LaserStrahl.asset");

		// Farbe aus LaserEffect.glb (Material "Laser")
		var material = NeuesMaterial("Laser", new Color(0.380f, 0.537f, 1f, 1f));

		var go = new GameObject("Laser");
		go.AddComponent<MeshFilter>().sharedMesh = netz;
		var renderer = go.AddComponent<MeshRenderer>();
		renderer.sharedMaterial = material;
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		var effekt = go.AddComponent<LaserEffect>();
		WerteSetzen(effekt, D(L(komponenten["LaserEffect"])[0]));
		return AlsPrefab(go, PREFABS + "/Laser.prefab");
	}

	// ------------------------------------------------------------------
	// Spieler

	static GameObject SpielerPrefab(Dictionary<string, object> komponenten, GameObject laser)
	{
		var wurzel = new GameObject("player");

		// Werte von CharacterController und Koerper stehen nicht in den Daten.
		// Unity-Standard (Hoehe 2, Radius 0,5) passt zur Kamera auf 0,88 m (playerlooking).
		var controller = wurzel.AddComponent<CharacterController>();
		controller.center = Vector3.zero;
		controller.height = 2f;
		controller.radius = 0.5f;
		wurzel.AddComponent<NetworkObject>();
		var netzWurzel = wurzel.AddComponent<NetworkClientTransform>();
		var bewegung = wurzel.AddComponent<playermovment>();
		var blick = wurzel.AddComponent<playerlooking>();
		var verwaltung = wurzel.AddComponent<Playermanager>();
		var schiessen = wurzel.AddComponent<playershooting>();

		var transformWerte = L(komponenten["NetworkClientTransform"]);
		WerteSetzen(netzWurzel, D(transformWerte[0]));
		WerteSetzen(bewegung, D(L(komponenten["playermovment"])[0]));
		WerteSetzen(blick, D(L(komponenten["playerlooking"])[0]));
		WerteSetzen(schiessen, D(L(komponenten["playershooting"])[0]));

		// Kamera: playerlooking setzt sie nach dem Tod auf 0,88 m ueber die Wurzel zurueck.
		var kameraGo = new GameObject("Main Camera");
		kameraGo.transform.SetParent(wurzel.transform, false);
		kameraGo.transform.localPosition = new Vector3(0f, 0.88f, 0f);
		var kamera = kameraGo.AddComponent<Camera>();
		// Nur die Kamera des eigenen Spielers wird eingeschaltet (playerlooking.switchCameraTo).
		kamera.enabled = false;
		kamera.nearClipPlane = 0.05f;
		kamera.fieldOfView = Waffen.NORMALES_SICHTFELD;
		// Die Vorschau des Kopfbild-Editors steht auf einem eigenen Layer, den sieht nur ihre Kamera.
		kamera.cullingMask = ~(1 << LAYER_VORSCHAU);
		kameraGo.AddComponent<AudioListener>();
		var netzKamera = kameraGo.AddComponent<NetworkClientTransform>();
		WerteSetzen(netzKamera, D(transformWerte[1]));
		// Abweichung vom Original: Dort lief die Kamera im Weltraum mit Drehung X und Z. Netcode
		// setzt dann die nicht uebertragene Y-Drehung fest, und die Waffe anderer Spieler blieb
		// seitlich in einer Richtung stehen. Lokal und nur die Neigung (X) ist das, was die Kamera
		// gegenueber der Figur wirklich tut, die Seitendrehung kommt von der Figur.
		Setze(netzKamera, "InLocalSpace", true);
		Setze(netzKamera, "SyncRotAngleX", true);
		Setze(netzKamera, "SyncRotAngleY", false);
		Setze(netzKamera, "SyncRotAngleZ", false);

		// Laserpointer aus Player.glb. GESCHAETZT: Lage an der Kamera. Der Drehpunkt
		// liegt an der Spitze, weil dort der Strahl beginnt (LaserEffect).
		var pointer = new GameObject("Laserpointer");
		pointer.transform.SetParent(kameraGo.transform, false);
		pointer.transform.localPosition = new Vector3(0.45f, -0.35f, 1.2f);
		var modell = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GLB_SPIELER));
		modell.name = "Modell";
		var grenzen = Grenzen(modell);
		modell.transform.SetParent(pointer.transform, false);
		modell.transform.localPosition = -new Vector3(grenzen.center.x, grenzen.center.y, grenzen.max.z);
		foreach (var c in modell.GetComponentsInChildren<Collider>(true))
		{
			Object.DestroyImmediate(c);
		}
		MaterialienErsetzen(modell, ERZEUGT + "/Spieler");

		// Koerper: Unity-Kapsel (im Original MeshFilter + CapsuleCollider ohne eigenes Modell)
		var koerper = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		koerper.name = "playerbody";
		koerper.transform.SetParent(wurzel.transform, false);

		// Namensschild. GESCHAETZT: Hoehe und Groesse.
		var schild = new GameObject("NameLabel");
		schild.transform.SetParent(wurzel.transform, false);
		schild.transform.localPosition = new Vector3(0f, 1.5f, 0f);
		// playerlooking dreht das Schild mit LookAt zur Kamera, damit zeigt seine
		// Rueckseite zum Betrachter. Mit x = -1 ist die Schrift trotzdem lesbar.
		schild.transform.localScale = new Vector3(-1f, 1f, 1f);
		var schildText = schild.AddComponent<TextMeshPro>();
		schildText.text = "Name";
		schildText.fontSize = 3f;
		schildText.alignment = TextAlignmentOptions.Center;
		schildText.enableWordWrapping = false;
		schildText.rectTransform.sizeDelta = new Vector2(6f, 1f);

		Setze(blick, "cameraTransform", kameraGo.transform);
		Setze(verwaltung, "nameLabel", schildText);
		Setze(verwaltung, "body", koerper);
		Setze(verwaltung, "laserPointer", pointer);
		Setze(schiessen, "laser", laser);

		// Eigenes Bild auf der vorderen Kopfhaelfte (Kopfbild-Editor im Menue).
		var kopfbild = wurzel.AddComponent<Kopfbild>();
		Setze(kopfbild, "material", KopfbildMaterial());

		// Rote Explosion beim Tod.
		DDPartikelMaterialien();
		Setze(verwaltung, "todesMaterial", ddFeuerMaterial);

		// Eigener Spieler liegt auf "Ignore Raycast", fremde setzt Playermanager auf 8 und 9.
		LayerSetzen(wurzel, 2);
		return AlsPrefab(wurzel, PREFABS + "/player.prefab");
	}

	// ------------------------------------------------------------------
	// Maps

	static Dictionary<string, object> MapWerte(Dictionary<string, object> szenenWerte, string anfang)
	{
		foreach (var paar in szenenWerte)
		{
			if (paar.Key.StartsWith(anfang))
			{
				return D(paar.Value);
			}
		}
		throw new Exception("Keine Werte fuer " + anfang + " in " + WERTE_DATEN);
	}

	static GameObject MapPrefab(string glbPfad, string wurzelName, string prefabName, Dictionary<string, object> mapWerte, string lichtName, Dictionary<string, object> szenenWerte)
	{
		var wurzel = new GameObject(wurzelName);
		var modell = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(glbPfad));
		modell.name = "Modell";
		modell.transform.SetParent(wurzel.transform, false);
		MaterialienErsetzen(modell, ERZEUGT + "/" + prefabName);

		// Im Original hatte jedes Teil einen MeshCollider.
		foreach (var filter in modell.GetComponentsInChildren<MeshFilter>(true))
		{
			if (filter.GetComponent<Collider>() == null)
			{
				filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
			}
		}

		// Die GLBs sind schon in Weltkoordinaten exportiert, die Skalierung der
		// Original-Wurzel (Info-Raum 0,8) steckt also schon in den Punkten. Die
		// Spawn-Boxen sind aber in lokalen Koordinaten dieser Wurzel gespeichert
		// und muessen deshalb hier mit der Skalierung umgerechnet werden.
		var skalierung = F(D(D(mapWerte["rootTransform"])["m_LocalScale"])["x"]);
		foreach (var b in L(mapWerte["spawnBoxes_lokal"]))
		{
			var box = D(b);
			var collider = wurzel.AddComponent<BoxCollider>();
			collider.isTrigger = true;
			collider.center = V3(box["center"]) * skalierung;
			collider.size = V3(box["size"]) * skalierung;
		}

		// GESCHAETZT: Position des Lichts. Intensitaet und Reichweite aus werte.json.
		var grenzen = Grenzen(modell);
		var lichtGo = new GameObject(lichtName);
		lichtGo.transform.SetParent(wurzel.transform, false);
		lichtGo.transform.localPosition = new Vector3(grenzen.center.x, grenzen.min.y + grenzen.size.y * 0.8f, grenzen.center.z);
		LichtEinstellen(lichtGo.AddComponent<Light>(), LichtWerte(szenenWerte, lichtName));

		// Wurzel auf "Ignore Raycast", die Teile bleiben auf Default und sind treffbar.
		wurzel.layer = 2;
		return AlsPrefab(wurzel, PREFABS + "/" + prefabName + ".prefab");
	}

	static Dictionary<string, object> LichtWerte(Dictionary<string, object> szenenWerte, string name)
	{
		foreach (var l in L(szenenWerte["Lights"]))
		{
			var licht = D(l);
			if ((string)licht["go"] == name)
			{
				return licht;
			}
		}
		throw new Exception("Licht " + name + " fehlt in " + WERTE_DATEN);
	}

	static void LichtEinstellen(Light licht, Dictionary<string, object> werte)
	{
		licht.type = (string)werte["type"] == "Directional" ? LightType.Directional : LightType.Point;
		var farbe = L(werte["color"]);
		licht.color = new Color(F(farbe[0]), F(farbe[1]), F(farbe[2]));
		licht.intensity = F(werte["intensity"]);
		licht.range = F(werte["range"]);
		licht.shadows = (LightShadows)(int)F(werte["shadows"]);
	}

	// ------------------------------------------------------------------
	// Scoreboard-Zeile

	static GameObject ZeilenPrefab()
	{
		var bau = new UiBau();
		var zeile = UiKnoten(D(JsonDatei(ZEILE_DATEN)), null, null, bau);
		KlickflaechenSetzen(zeile);
		return AlsPrefab(zeile, PREFABS + "/Row.prefab");
	}

	// ------------------------------------------------------------------
	// Szene

	static void SzeneBauen(Dictionary<string, object> szenenWerte, GameObject spieler, GameObject zeile, GameObject zimmer, GameObject infoRaum, GameObject bot, GameObject dDay)
	{
		var szene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
		// Gleich speichern: Das NetworkObject in der Szene braucht die GUID der Szene fuer seine Kennung.
		EditorSceneManager.SaveScene(szene, SZENE);

		RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
		RenderSettings.fog = false;

		// Vorschaubild der D-Day-Map fuer den Knopf im Host-Fenster.
		var dDayVorschau = DDayVorschauRendern(dDay);

		// Lichter. GESCHAETZT: Richtungen.
		var lichter = new GameObject("Lights");
		var licht1 = new GameObject("Directional Light (1)");
		licht1.transform.SetParent(lichter.transform, false);
		licht1.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
		LichtEinstellen(licht1.AddComponent<Light>(), LichtWerte(szenenWerte, "Directional Light (1)"));
		var licht2 = new GameObject("Directional Light (2)");
		licht2.transform.SetParent(lichter.transform, false);
		licht2.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
		LichtEinstellen(licht2.AddComponent<Light>(), LichtWerte(szenenWerte, "Directional Light (2)"));

		// Menue-Kamera (Manager.uiCam). GESCHAETZT: Blick ins Zimmer, das beim Start aktiv ist.
		var kameraGo = new GameObject("Camera");
		kameraGo.tag = "MainCamera";
		kameraGo.transform.position = new Vector3(15f, 15f, -15f);
		kameraGo.transform.LookAt(new Vector3(-5f, 5f, 5f));
		kameraGo.AddComponent<Camera>().cullingMask = ~(1 << LAYER_VORSCHAU);
		kameraGo.AddComponent<AudioListener>();

		var vorschau = KopfbildVorschauBauen();

		var eventSystem = new GameObject("EventSystem");
		eventSystem.AddComponent<EventSystem>();
		// Kein AssignDefaultActions: Das legt im Editor nur ein Asset im Speicher an, das
		// beim Speichern verloren geht. Reset() des Moduls traegt beim Hinzufuegen die
		// Standard-Aktionen des Pakets ein, und zur Laufzeit ergaenzt OnEnable fehlende.
		eventSystem.AddComponent<InputSystemUIInputModule>();

		// Network Manager
		var netzGo = new GameObject("Network Manager");
		var netz = netzGo.AddComponent<NetworkManager>();
		if (netz.NetworkConfig == null)
		{
			netz.NetworkConfig = new NetworkConfig();
		}
		var transport = netzGo.AddComponent<UnityTransport>();
		// Im Original (Netcode 1.0.x) lauschte der Host bei leerer Listen-Adresse auf seiner
		// LAN-IP. Netcode 1.14 lauscht dann nur auf 127.0.0.1, andere PCs kaemen nicht durch.
		// 0.0.0.0 heisst: auf allen Adressen des PCs, Port bleibt 7777 wie im LAN-Code.
		transport.ConnectionData.ServerListenAddress = "0.0.0.0";
		netz.NetworkConfig.NetworkTransport = transport;
		netz.NetworkConfig.PlayerPrefab = spieler;
		netz.NetworkConfig.TickRate = 60;
		netz.NetworkConfig.EnableSceneManagement = true;
		netz.NetworkConfig.ForceSamePrefabs = true;
		netz.RunInBackground = true;
		var liste = PrefabListe();
		foreach (var netzPrefab in new[] { spieler, bot })
		{
			if (!liste.Contains(netzPrefab))
			{
				liste.Add(new NetworkPrefab { Prefab = netzPrefab });
				EditorUtility.SetDirty(liste);
			}
		}
		netz.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { liste };
		netzGo.AddComponent<Spielversion>();

		// Game Manager
		var spielGo = new GameObject("Game Manager");
		spielGo.layer = 2;
		spielGo.AddComponent<NetworkObject>();
		var manager = spielGo.AddComponent<Manager>();
		var eingabe = spielGo.AddComponent<PlayerInput>();
		var verbindung = spielGo.AddComponent<ConnectionManager>();
		spielGo.AddComponent<PlayersManager>();
		spielGo.AddComponent<Spawnlocation>();
		var runde = spielGo.AddComponent<Runde>();
		Setze(runde, "botPrefab", bot);
		Setze(eingabe, "m_Actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(EINGABE));
		// Das Spiel startet im Menue, Manager schaltet spaeter auf "Player" um.
		Setze(eingabe, "m_DefaultActionMap", "UI");
		Setze(eingabe, "m_NotificationBehavior", (int)PlayerNotifications.InvokeCSharpEvents);
		Setze(manager, "oldMapPrefab", zimmer);
		Setze(manager, "newMapPrefab", infoRaum);
		Setze(manager, "dDayMapPrefab", dDay);
		Setze(manager, "playerInput", eingabe);
		Setze(manager, "connectionManager", verbindung);
		Setze(manager, "uiCam", kameraGo);
		Setze(verbindung, "networkManager", netz);

		// UI
		var bau = new UiBau();
		var uiDaten = D(JsonDatei(UI_DATEN));
		var canvasGo = UiKnoten(uiDaten, null, null, bau);
		// Neues UI (Einstellungen, HUD, Aussehen, Host-Optionen, D-Day-Knopf) aus UiErweiterung.json.
		UiErweitern(bau, uiDaten, dDayVorschau);
		var canvas = canvasGo.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		// GESCHAETZT: Bezugsaufloesung. Die Positionen im UI passen zu 1920 x 1080.
		var skalierer = canvasGo.GetComponent<CanvasScaler>();
		skalierer.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		skalierer.referenceResolution = new Vector2(1920f, 1080f);
		// Nach der Breite skalieren: Viele Texte liegen in Balken, die ueber die ganze Breite
		// gehen, und rechnen mit 1920 Einheiten Breite. Bei 0,5 wurden sie in schmalen
		// Fenstern zu eng und liefen ineinander.
		skalierer.matchWidthOrHeight = 0f;

		// Im Original reicht der Lobbycode (rechtsbuendig) bis x = 1427, "(press escape to
		// continue)" beginnt schon bei x = 1413: Die Texte ueberlappten. 40 Einheiten Abstand.
		var lobbyText = bau.Pfad("Canvas/Game Screen/PauseUI/bar/Lobbycode").GetComponent<RectTransform>();
		lobbyText.offsetMax += new Vector2(-54f, 0f);

		var ui = canvasGo.GetComponent<UIManager>();
		Setze(ui, "startingUI", bau.Pfad("Canvas/Start Screen"));
		Setze(ui, "enterUsernameButton", bau.Komponente<Button>("Canvas/Start Screen/Enter Username/UI/EnterUsernameBtn"));

		var spielUi = canvasGo.GetComponent<UIGameManager>();
		Setze(spielUi, "gameScreen", bau.Pfad("Canvas/Game Screen"));
		Setze(spielUi, "gameUI", bau.Pfad("Canvas/Game Screen/GameUI"));
		Setze(spielUi, "pauseUI", bau.Pfad("Canvas/Game Screen/PauseUI"));
		Setze(spielUi, "deathUI", bau.Pfad("Canvas/Game Screen/DeathUI"));
		Setze(spielUi, "playerListUI", bau.Pfad("Canvas/Game Screen/Playerlist"));
		Setze(spielUi, "lobbyCode", bau.Komponente<TMP_Text>("Canvas/Game Screen/PauseUI/bar/Lobbycode"));
		Setze(spielUi, "copyIP", bau.Komponente<Button>("Canvas/Game Screen/PauseUI/bar/CopyIPBtn"));
		Setze(spielUi, "playerListPanel", bau.Pfad("Canvas/Game Screen/Playerlist").transform);
		Setze(spielUi, "playerRowPrefab", zeile);
		Setze(spielUi, "killedByField", bau.Komponente<TMP_Text>("Canvas/Game Screen/DeathUI/Bar/KilledByField"));
		Setze(spielUi, "killsText", bau.Komponente<TMP_Text>("Canvas/Game Screen/GameUI/KillCount"));
		Setze(spielUi, "DeathsText", bau.Komponente<TMP_Text>("Canvas/Game Screen/GameUI/DeathCount"));
		Setze(spielUi, "Hitmarker", bau.Pfad("Canvas/Game Screen/GameUI/Hitmarker"));
		Setze(spielUi, "fireCoolDown", bau.Komponente<Slider>("Canvas/Game Screen/GameUI/CoolDown"));

		var einstellungen = canvasGo.GetComponent<SettingsManager>();
		Setze(einstellungen, "settingsScreen", bau.Pfad("Canvas/SettingsScreen"));
		Setze(einstellungen, "c_SensiSlider", bau.Komponente<Slider>("Canvas/SettingsScreen/SettingsScroller/Viewport/ControllSettings/Sensitivity/Slider"));
		Setze(einstellungen, "tabButtons", new Object[]
		{
			bau.Komponente<Button>("Canvas/SettingsScreen/Tabs/VideoTab"),
			bau.Komponente<Button>("Canvas/SettingsScreen/Tabs/SoundsTab"),
			bau.Komponente<Button>("Canvas/SettingsScreen/Tabs/ControllTab"),
		});
		Setze(einstellungen, "changeUsernameField", bau.Komponente<TMP_InputField>("Canvas/SettingsScreen/Tabs/ChangeNameField"));
		Setze(einstellungen, "changeUsernameBtn", bau.Komponente<Button>("Canvas/SettingsScreen/Tabs/ChangeNameBtn"));
		Setze(einstellungen, "changeUsernameBtnTxt", bau.Komponente<TMP_Text>("Canvas/SettingsScreen/Tabs/ChangeNameBtn/Text (TMP)"));

		NeueFelderSetzen(bau, vorschau);

		var ziele = new Dictionary<string, GameObject> { { "Canvas", canvasGo }, { "Game Manager", spielGo } };
		foreach (var e in bau.ereignisse)
		{
			EreignisVerdrahten(e, ziele);
		}

		KlickflaechenSetzen(canvasGo);

		// Inaktive Teile erst ganz am Ende abschalten, damit beim Verdrahten alles erreichbar ist.
		foreach (var go in bau.abschalten)
		{
			go.SetActive(false);
		}

		EditorSceneManager.MarkSceneDirty(szene);
		EditorSceneManager.SaveScene(szene, SZENE);
		InBuildSettings(SZENE);

		// Neu laden und noch einmal speichern, damit NetworkObject seine Kennung
		// aus der jetzt gespeicherten Szene berechnet.
		var neu = EditorSceneManager.OpenScene(SZENE, OpenSceneMode.Single);
		EditorSceneManager.MarkSceneDirty(neu);
		EditorSceneManager.SaveScene(neu);
		AssetDatabase.SaveAssets();
	}

	static NetworkPrefabsList PrefabListe()
	{
		var funde = AssetDatabase.FindAssets("t:NetworkPrefabsList");
		if (funde.Length > 0)
		{
			return AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(funde[0]));
		}
		var liste = ScriptableObject.CreateInstance<NetworkPrefabsList>();
		AssetDatabase.CreateAsset(liste, "Assets/DefaultNetworkPrefabs.asset");
		return liste;
	}

	static void InBuildSettings(string pfad)
	{
		var szenen = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
		szenen.RemoveAll(s => s.path == pfad);
		szenen.Insert(0, new EditorBuildSettingsScene(pfad, true));
		EditorBuildSettings.scenes = szenen.ToArray();
	}

	// ------------------------------------------------------------------
	// UI aus JSON

	class Ereignis
	{
		public string pfad;
		public UnityEventBase ereignis;
		public string aufruf;
	}

	class UiBau
	{
		public readonly Dictionary<string, GameObject> pfade = new Dictionary<string, GameObject>();
		public readonly List<Ereignis> ereignisse = new List<Ereignis>();
		public readonly List<GameObject> abschalten = new List<GameObject>();

		public GameObject Pfad(string pfad)
		{
			GameObject go;
			if (!pfade.TryGetValue(pfad, out go))
			{
				throw new Exception("Im UI fehlt " + pfad);
			}
			return go;
		}

		public T Komponente<T>(string pfad) where T : Component
		{
			var k = Pfad(pfad).GetComponent<T>();
			if (k == null)
			{
				throw new Exception(pfad + " hat kein " + typeof(T).Name);
			}
			return k;
		}
	}

	static GameObject UiKnoten(Dictionary<string, object> knoten, Transform eltern, string elternPfad, UiBau bau)
	{
		var name = (string)knoten["name"];
		var pfad = elternPfad == null ? name : elternPfad + "/" + name;
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		if (eltern != null)
		{
			go.transform.SetParent(eltern, false);
		}
		if (bau.pfade.ContainsKey(pfad))
		{
			Debug.LogWarning("Shootout: doppelter UI-Pfad " + pfad);
		}
		else
		{
			bau.pfade[pfad] = go;
		}

		var skripte = new List<string>();
		if (knoten.ContainsKey("scripts"))
		{
			foreach (var s in L(knoten["scripts"]))
			{
				skripte.Add((string)s);
			}
		}

		// Die Wurzel des Canvas hat kein sinnvolles Rect, den Rest macht der Canvas selbst.
		var rt = go.GetComponent<RectTransform>();
		if (!skripte.Contains("CanvasScaler"))
		{
			RectSetzen(rt, D(knoten["rect"]));
		}
		else
		{
			go.AddComponent<Canvas>();
		}

		Image bild = null;
		if (knoten.ContainsKey("image"))
		{
			var daten = D(knoten["image"]);
			bild = go.AddComponent<Image>();
			bild.color = Farbe((string)daten["color"]);
			var kennung = daten["sprite"] as string;
			bild.sprite = SpriteAusKennung(kennung);
			string quelle;
			if (kennung != null && SPRITES.TryGetValue(kennung, out quelle) && quelle.StartsWith("builtin:") && !quelle.EndsWith("Checkmark.psd"))
			{
				bild.type = Image.Type.Sliced;
			}
		}

		TextMeshProUGUI text = null;
		if (knoten.ContainsKey("text"))
		{
			var daten = D(knoten["text"]);
			text = go.AddComponent<TextMeshProUGUI>();
			text.text = (string)daten["text"];
			text.fontSize = F(daten["size"]);
			text.color = Farbe((string)daten["color"]);
			// Im Build stehen die Werte von HorizontalAlignmentOptions (1 links, 2 Mitte, 4 rechts).
			text.horizontalAlignment = (HorizontalAlignmentOptions)(int)F(daten["align"]);
			text.verticalAlignment = VerticalAlignmentOptions.Middle;
			text.fontStyle = (bool)daten["bold"] ? FontStyles.Bold : FontStyles.Normal;
		}

		// Kinder vor den Komponenten, die auf Kinder zeigen (Eingabefeld, Slider ...)
		if (knoten.ContainsKey("children"))
		{
			foreach (var kind in L(knoten["children"]))
			{
				UiKnoten(D(kind), go.transform, pfad, bau);
			}
		}

		foreach (var skript in skripte)
		{
			switch (skript)
			{
				case "CanvasScaler": go.AddComponent<CanvasScaler>(); break;
				case "GraphicRaycaster": go.AddComponent<GraphicRaycaster>(); break;
				case "UIManager": go.AddComponent<UIManager>(); break;
				case "UIGameManager": go.AddComponent<UIGameManager>(); break;
				case "SettingsManager": go.AddComponent<SettingsManager>(); break;
				case "Mask":
					// Der Viewport zeigt seine Maske nicht, die Map-Knoepfe schon.
					go.AddComponent<Mask>().showMaskGraphic = name != "Viewport";
					break;
				case "RectMask2D": go.AddComponent<RectMask2D>(); break;
				case "LayoutElement": go.AddComponent<LayoutElement>().ignoreLayout = true; break;
				case "ScrollRect":
					var scroll = go.AddComponent<ScrollRect>();
					scroll.horizontal = false;
					scroll.viewport = Kind(go, "Viewport") as RectTransform;
					var leiste = Kind(go, "Scrollbar Vertical");
					scroll.verticalScrollbar = leiste == null ? null : leiste.GetComponent<Scrollbar>();
					// Im Original nicht bekannt, welcher Bereich scrollt. Ohne Inhalt scrollt nichts.
					scroll.content = null;
					break;
				case "Scrollbar":
					var balken = go.AddComponent<Scrollbar>();
					balken.direction = Scrollbar.Direction.BottomToTop;
					var griff = Kind(go, "Handle");
					balken.handleRect = griff as RectTransform;
					balken.targetGraphic = griff == null ? null : griff.GetComponent<Graphic>();
					break;
				case "connectingAnim":
					Setze(go.AddComponent<connectingAnim>(), "connectingTxt", text);
					break;
				// Neu in UiErweiterung.json:
				case "RawImage": go.AddComponent<RawImage>().color = Color.white; break;
				case "Blinken": go.AddComponent<Blinken>(); break;
				case "KopfbildEditor": go.AddComponent<KopfbildEditor>(); break;
				case "HostOptionen": go.AddComponent<HostOptionen>(); break;
				case "playerListRow":
					var zeile = go.AddComponent<playerListRow>();
					Setze(zeile, "rect", rt);
					Setze(zeile, "img", bild);
					Setze(zeile, "indexTxt", KindText(go, "indexTxt"));
					Setze(zeile, "nameTxt", KindText(go, "playerNameTxt"));
					Setze(zeile, "killsTxt", KindText(go, "killsTxt"));
					Setze(zeile, "deathsTxt", KindText(go, "deathTxt"));
					Setze(zeile, "kdTxt", KindText(go, "k/dTxt"));
					break;
				default:
					Debug.LogWarning("Shootout: unbekanntes Skript " + skript + " an " + pfad);
					break;
			}
		}

		if (knoten.ContainsKey("Button"))
		{
			var knopf = go.AddComponent<Button>();
			knopf.targetGraphic = bild;
			EreignisseMerken(bau, pfad, knopf.onClick, D(knoten["Button"]));
		}

		if (knoten.ContainsKey("TMP_InputField"))
		{
			var feld = go.AddComponent<TMP_InputField>();
			feld.targetGraphic = bild;
			feld.textViewport = Kind(go, "Text Area") as RectTransform;
			var eingabeText = KindText(go, "Text");
			feld.textComponent = eingabeText;
			feld.placeholder = KindText(go, "Placeholder");
			if (eingabeText != null)
			{
				feld.fontAsset = eingabeText.font;
			}
			EreignisseMerken(bau, pfad, feld.onValueChanged, D(knoten["TMP_InputField"]));
		}

		if (knoten.ContainsKey("Slider"))
		{
			var daten = D(knoten["Slider"]);
			var regler = go.AddComponent<Slider>();
			var fuellung = Kind(go, "Fill");
			var griff = Kind(go, "Handle");
			regler.fillRect = fuellung as RectTransform;
			regler.handleRect = griff as RectTransform;
			regler.targetGraphic = griff == null ? null : griff.GetComponent<Graphic>();
			// Der Abklingbalken hat keinen Griff und ist nur Anzeige.
			regler.interactable = griff != null;
			regler.direction = Slider.Direction.LeftToRight;
			regler.minValue = F(daten["min"]);
			regler.maxValue = F(daten["max"]);
			regler.value = F(daten["value"]);
			EreignisseMerken(bau, pfad, regler.onValueChanged, daten);
		}

		if (knoten.ContainsKey("Toggle"))
		{
			var schalter = go.AddComponent<Toggle>();
			var haken = Kind(go, "Checkmark");
			var hintergrund = Kind(go, "Background");
			schalter.graphic = haken == null ? null : haken.GetComponent<Graphic>();
			schalter.targetGraphic = hintergrund == null ? null : hintergrund.GetComponent<Graphic>();
			schalter.isOn = true;
			EreignisseMerken(bau, pfad, schalter.onValueChanged, D(knoten["Toggle"]));
		}

		if (!(bool)knoten["active"])
		{
			bau.abschalten.Add(go);
		}
		return go;
	}

	static void RectSetzen(RectTransform rt, Dictionary<string, object> rect)
	{
		// Fehlende Angaben (nur in UiErweiterung.json moeglich) mit ueblichen Werten.
		rt.anchorMin = rect.ContainsKey("anchorMin") ? V2(rect["anchorMin"]) : new Vector2(0.5f, 0.5f);
		rt.anchorMax = rect.ContainsKey("anchorMax") ? V2(rect["anchorMax"]) : rt.anchorMin;
		rt.pivot = rect.ContainsKey("pivot") ? V2(rect["pivot"]) : new Vector2(0.5f, 0.5f);
		rt.sizeDelta = rect.ContainsKey("size") ? V2(rect["size"]) : new Vector2(100f, 100f);
		rt.anchoredPosition = rect.ContainsKey("pos") ? V2(rect["pos"]) : Vector2.zero;
	}

	// Sucht ein Kind (in jeder Tiefe) mit genau diesem Namen.
	static Transform Kind(GameObject go, string name)
	{
		foreach (var t in go.GetComponentsInChildren<Transform>(true))
		{
			if (t != go.transform && t.name == name)
			{
				return t;
			}
		}
		return null;
	}

	static TMP_Text KindText(GameObject go, string name)
	{
		var t = Kind(go, name);
		return t == null ? null : t.GetComponent<TMP_Text>();
	}

	static void EreignisseMerken(UiBau bau, string pfad, UnityEventBase ereignis, Dictionary<string, object> daten)
	{
		if (!daten.ContainsKey("onEvent"))
		{
			return;
		}
		foreach (var a in L(daten["onEvent"]))
		{
			bau.ereignisse.Add(new Ereignis { pfad = pfad, ereignis = ereignis, aufruf = (string)a });
		}
	}

	// Aufrufe im Format "Canvas(MonoBehaviour).continueBranch(Enter Username)".
	// Ein leeres Argument heisst im Build: Standardwert (0, false, "").
	static void EreignisVerdrahten(Ereignis e, Dictionary<string, GameObject> ziele)
	{
		var treffer = Regex.Match(e.aufruf, @"^(.+)\(MonoBehaviour\)\.(\w+)\((.*)\)$");
		if (!treffer.Success)
		{
			Debug.LogWarning("Shootout: Aufruf nicht verstanden: " + e.aufruf + " an " + e.pfad);
			return;
		}
		GameObject ziel;
		if (!ziele.TryGetValue(treffer.Groups[1].Value, out ziel))
		{
			Debug.LogWarning("Shootout: Ziel " + treffer.Groups[1].Value + " unbekannt (" + e.pfad + ")");
			return;
		}
		var methode = treffer.Groups[2].Value;
		var argument = treffer.Groups[3].Value;

		MonoBehaviour komponente = null;
		MethodInfo ohne = null;
		MethodInfo mit = null;
		foreach (var k in ziel.GetComponents<MonoBehaviour>())
		{
			foreach (var m in k.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			{
				if (m.Name != methode || m.ReturnType != typeof(void))
				{
					continue;
				}
				var p = m.GetParameters();
				if (p.Length == 0)
				{
					ohne = m;
					komponente = k;
				}
				else if (p.Length == 1 && (p[0].ParameterType == typeof(int) || p[0].ParameterType == typeof(string) || p[0].ParameterType == typeof(bool)))
				{
					// Bei mehreren Varianten (continueBranch mit GameObject oder string) gilt die mit einfachem Typ.
					mit = m;
					komponente = k;
				}
			}
			if (komponente != null)
			{
				break;
			}
		}
		if (komponente == null)
		{
			Debug.LogWarning("Shootout: Methode " + methode + " nicht gefunden (" + e.pfad + ")");
			return;
		}

		// Mit Argument die Variante mit Parameter, sonst lieber die ohne.
		// revertBranch(Enter Username) und closeSettings(2) haben im Code keinen Parameter,
		// das Argument im Build ist ein Rest und faellt weg.
		var gewaehlt = (argument != "" && mit != null) || ohne == null ? mit : ohne;
		if (gewaehlt.GetParameters().Length == 0)
		{
			UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(e.ereignis, (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), komponente, gewaehlt));
			return;
		}
		var typ = gewaehlt.GetParameters()[0].ParameterType;
		if (typ == typeof(int))
		{
			int zahl = argument == "" ? 0 : int.Parse(argument, CultureInfo.InvariantCulture);
			UnityEditor.Events.UnityEventTools.AddIntPersistentListener(e.ereignis, (UnityAction<int>)Delegate.CreateDelegate(typeof(UnityAction<int>), komponente, gewaehlt), zahl);
		}
		else if (typ == typeof(string))
		{
			UnityEditor.Events.UnityEventTools.AddStringPersistentListener(e.ereignis, (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), komponente, gewaehlt), argument);
		}
		else
		{
			// connectAsClientBtn(bool isLAN): Die Daten zeigen kein Argument. Der Knopf im
			// LAN-Fenster muss true bekommen, sonst wuerde er global verbinden.
			bool wert = argument == "" ? e.pfad.Contains("Join a LAN Game") : bool.Parse(argument);
			UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(e.ereignis, (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), komponente, gewaehlt), wert);
		}
	}

	// ------------------------------------------------------------------
	// Hilfen fuer Assets

	static GameObject AlsPrefab(GameObject go, string pfad)
	{
		PrefabUtility.SaveAsPrefabAsset(go, pfad);
		Object.DestroyImmediate(go);
		// Noch einmal oeffnen und speichern: NetworkObject berechnet seine Kennung
		// erst, wenn das Prefab als Datei mit GUID existiert.
		var inhalt = PrefabUtility.LoadPrefabContents(pfad);
		PrefabUtility.SaveAsPrefabAsset(inhalt, pfad);
		PrefabUtility.UnloadPrefabContents(inhalt);
		return AssetDatabase.LoadAssetAtPath<GameObject>(pfad);
	}

	static T AlsAsset<T>(T asset, string pfad) where T : Object
	{
		// Vorhandenes Asset ueberschreiben statt loeschen und neu anlegen. Sonst behielt der
		// Editor, der das Skript ausgefuehrt hat, alte Kopien im Speicher (Laser unsichtbar).
		var vorhanden = AssetDatabase.LoadAssetAtPath<T>(pfad);
		if (vorhanden != null)
		{
			EditorUtility.CopySerialized(asset, vorhanden);
			vorhanden.name = Path.GetFileNameWithoutExtension(pfad);
			EditorUtility.SetDirty(vorhanden);
			Object.DestroyImmediate(asset);
			return vorhanden;
		}
		if (AssetDatabase.LoadAssetAtPath<Object>(pfad) != null)
		{
			AssetDatabase.DeleteAsset(pfad);
		}
		AssetDatabase.CreateAsset(asset, pfad);
		return AssetDatabase.LoadAssetAtPath<T>(pfad);
	}

	static Material NeuesMaterial(string name, Color farbe)
	{
		var material = new Material(Shader.Find("Standard"));
		material.name = name;
		material.color = farbe;
		return AlsAsset(material, ERZEUGT + "/" + name + ".mat");
	}

	static void Ordner(string pfad)
	{
		if (AssetDatabase.IsValidFolder(pfad))
		{
			return;
		}
		var eltern = Path.GetDirectoryName(pfad).Replace('\\', '/');
		AssetDatabase.CreateFolder(eltern, Path.GetFileName(pfad));
	}

	static Bounds Grenzen(GameObject go)
	{
		var renderer = go.GetComponentsInChildren<Renderer>(true);
		if (renderer.Length == 0)
		{
			return new Bounds(go.transform.position, Vector3.zero);
		}
		var b = renderer[0].bounds;
		for (int i = 1; i < renderer.Length; i++)
		{
			b.Encapsulate(renderer[i].bounds);
		}
		return b;
	}

	static void LayerSetzen(GameObject go, int layer)
	{
		foreach (var t in go.GetComponentsInChildren<Transform>(true))
		{
			t.gameObject.layer = layer;
		}
	}

	static void Fortschritt(string schritt, float anteil)
	{
		EditorUtility.DisplayProgressBar("Shootout aufbauen", schritt, anteil);
	}

	// ------------------------------------------------------------------
	// Felder setzen (auch private [SerializeField])

	static void Setze(Object ziel, string feld, object wert)
	{
		var so = new SerializedObject(ziel);
		var p = so.FindProperty(feld);
		if (p == null)
		{
			throw new Exception(ziel.GetType().Name + " hat kein Feld " + feld);
		}
		if (wert is Object[])
		{
			var liste = (Object[])wert;
			p.arraySize = liste.Length;
			for (int i = 0; i < liste.Length; i++)
			{
				p.GetArrayElementAtIndex(i).objectReferenceValue = liste[i];
			}
		}
		else if (wert == null || wert is Object)
		{
			p.objectReferenceValue = (Object)wert;
		}
		else if (wert is string)
		{
			p.stringValue = (string)wert;
		}
		else if (p.propertyType == SerializedPropertyType.Boolean)
		{
			p.boolValue = Convert.ToDouble(wert, CultureInfo.InvariantCulture) != 0;
		}
		else if (p.propertyType == SerializedPropertyType.Integer || p.propertyType == SerializedPropertyType.Enum)
		{
			p.intValue = (int)Convert.ToDouble(wert, CultureInfo.InvariantCulture);
		}
		else if (p.propertyType == SerializedPropertyType.Float)
		{
			p.floatValue = (float)Convert.ToDouble(wert, CultureInfo.InvariantCulture);
		}
		else
		{
			throw new Exception("Feld " + feld + " an " + ziel.GetType().Name + " hat einen unerwarteten Typ " + p.propertyType);
		}
		so.ApplyModifiedPropertiesWithoutUndo();
	}

	// Setzt alle Werte aus einem Abschnitt von werte.json. Die Namen dort sind die Feldnamen.
	static void WerteSetzen(Object ziel, Dictionary<string, object> werte)
	{
		foreach (var paar in werte)
		{
			Setze(ziel, paar.Key, paar.Value);
		}
	}

	// ------------------------------------------------------------------
	// JSON

	static object JsonDatei(string relativ)
	{
		var voll = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relativ);
		return MiniJson.Lesen(File.ReadAllText(voll, Encoding.UTF8));
	}

	static Dictionary<string, object> D(object o)
	{
		return (Dictionary<string, object>)o;
	}

	static List<object> L(object o)
	{
		return (List<object>)o;
	}

	static float F(object o)
	{
		return (float)Convert.ToDouble(o, CultureInfo.InvariantCulture);
	}

	static Vector2 V2(object o)
	{
		var l = L(o);
		return new Vector2(F(l[0]), F(l[1]));
	}

	static Vector3 V3(object o)
	{
		var l = L(o);
		return new Vector3(F(l[0]), F(l[1]), F(l[2]));
	}

	static Color Farbe(string hex)
	{
		Color farbe;
		if (!ColorUtility.TryParseHtmlString(hex, out farbe))
		{
			throw new Exception("Farbe nicht lesbar: " + hex);
		}
		return farbe;
	}

	// Kleiner JSON-Leser. JsonUtility kann keine beliebig verschachtelten Daten mit
	// wechselnden Feldern lesen, deshalb hier von Hand.
	static class MiniJson
	{
		public static object Lesen(string s)
		{
			int i = 0;
			var wert = Wert(s, ref i);
			Leer(s, ref i);
			if (i != s.Length)
			{
				throw new Exception("JSON: Zeichen nach dem Ende an Stelle " + i);
			}
			return wert;
		}

		static void Leer(string s, ref int i)
		{
			while (i < s.Length && char.IsWhiteSpace(s[i]))
			{
				i++;
			}
		}

		static void Erwarte(string s, ref int i, char c)
		{
			Leer(s, ref i);
			if (i >= s.Length || s[i] != c)
			{
				throw new Exception("JSON: '" + c + "' erwartet an Stelle " + i);
			}
			i++;
		}

		static object Wert(string s, ref int i)
		{
			Leer(s, ref i);
			if (i >= s.Length)
			{
				throw new Exception("JSON: unerwartetes Ende");
			}
			char c = s[i];
			if (c == '{')
			{
				i++;
				var d = new Dictionary<string, object>();
				Leer(s, ref i);
				if (s[i] == '}')
				{
					i++;
					return d;
				}
				while (true)
				{
					Leer(s, ref i);
					var schluessel = Zeichenkette(s, ref i);
					Erwarte(s, ref i, ':');
					d[schluessel] = Wert(s, ref i);
					Leer(s, ref i);
					if (s[i] == ',')
					{
						i++;
						continue;
					}
					Erwarte(s, ref i, '}');
					return d;
				}
			}
			if (c == '[')
			{
				i++;
				var l = new List<object>();
				Leer(s, ref i);
				if (s[i] == ']')
				{
					i++;
					return l;
				}
				while (true)
				{
					l.Add(Wert(s, ref i));
					Leer(s, ref i);
					if (s[i] == ',')
					{
						i++;
						continue;
					}
					Erwarte(s, ref i, ']');
					return l;
				}
			}
			if (c == '"')
			{
				return Zeichenkette(s, ref i);
			}
			if (string.CompareOrdinal(s, i, "true", 0, 4) == 0)
			{
				i += 4;
				return true;
			}
			if (string.CompareOrdinal(s, i, "false", 0, 5) == 0)
			{
				i += 5;
				return false;
			}
			if (string.CompareOrdinal(s, i, "null", 0, 4) == 0)
			{
				i += 4;
				return null;
			}
			int start = i;
			while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
			{
				i++;
			}
			if (start == i)
			{
				throw new Exception("JSON: unerwartetes Zeichen '" + c + "' an Stelle " + i);
			}
			return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
		}

		static string Zeichenkette(string s, ref int i)
		{
			Erwarte(s, ref i, '"');
			var sb = new StringBuilder();
			while (true)
			{
				if (i >= s.Length)
				{
					throw new Exception("JSON: Zeichenkette ohne Ende");
				}
				char c = s[i++];
				if (c == '"')
				{
					return sb.ToString();
				}
				if (c != '\\')
				{
					sb.Append(c);
					continue;
				}
				char e = s[i++];
				switch (e)
				{
					case 'n': sb.Append('\n'); break;
					case 't': sb.Append('\t'); break;
					case 'r': sb.Append('\r'); break;
					case 'b': sb.Append('\b'); break;
					case 'f': sb.Append('\f'); break;
					case 'u':
						sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
						i += 4;
						break;
					default: sb.Append(e); break;
				}
			}
		}
	}
}
