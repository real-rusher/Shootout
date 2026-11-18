using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Teil des Editor-Skripts fuer alles, was ueber das Original hinausgeht: Bots, Kopfbild,
// neues UI (aus Assets/Editor/UiErweiterung.json), Zielfernrohr.
public static partial class SzeneAufbauen
{
	// Layer fuer die Vorschau im Kopfbild-Editor. Nur deren Kamera sieht ihn.
	const int LAYER_VORSCHAU = 10;

	const string UI_ERWEITERUNG = "Assets/Editor/UiErweiterung.json";

	// Wo die Vorschau-Figur steht: weit unter allen Maps.
	static readonly Vector3 VORSCHAU_ORT = new Vector3(0f, -500f, 0f);

	struct Vorschau
	{
		public Camera kamera;
		public Transform modell;
		public MeshRenderer kopfbild;
	}

	static void LayerVorschauEinrichten()
	{
		var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
		if (tagManager == null || tagManager.Length == 0)
		{
			return;
		}
		var so = new SerializedObject(tagManager[0]);
		var layer = so.FindProperty("layers").GetArrayElementAtIndex(LAYER_VORSCHAU);
		if (layer.stringValue != "Vorschau")
		{
			layer.stringValue = "Vorschau";
			so.ApplyModifiedPropertiesWithoutUndo();
		}
	}

	static Material KopfbildMaterial()
	{
		// Unlit/Transparent: Bild in echten Farben, PNG-Transparenz bleibt erhalten.
		var material = new Material(Shader.Find("Unlit/Transparent"));
		material.name = "Kopfbild";
		return AlsAsset(material, ERZEUGT + "/Kopfbild.mat");
	}

	// Schwarzer Rand mit rundem Loch fuer den Sniper-Zoom.
	static void ZielfernrohrErzeugen()
	{
		const int groesse = 512;
		var textur = new Texture2D(groesse, groesse, TextureFormat.RGBA32, false);
		var punkte = new Color32[groesse * groesse];
		float mitte = (groesse - 1) * 0.5f;
		for (int y = 0; y < groesse; y++)
		{
			for (int x = 0; x < groesse; x++)
			{
				float abstand = new Vector2(x - mitte, y - mitte).magnitude / mitte;
				// Weicher Uebergang am Rand der Linse.
				float deckung = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.94f, 1f, abstand));
				punkte[y * groesse + x] = new Color32(0, 0, 0, (byte)(deckung * 255f));
			}
		}
		textur.SetPixels32(punkte);
		textur.Apply();
		PngAlsSprite(ERZEUGT + "/Zielfernrohr.png", textur.EncodeToPNG());
		Object.DestroyImmediate(textur);
	}

	// Speichert ein PNG und importiert es als Sprite.
	static Sprite PngAlsSprite(string pfad, byte[] png)
	{
		File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, pfad), png);
		AssetDatabase.ImportAsset(pfad, ImportAssetOptions.ForceUpdate);
		var importer = (TextureImporter)AssetImporter.GetAtPath(pfad);
		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Single;
		importer.alphaIsTransparency = true;
		importer.mipmapEnabled = false;
		importer.SaveAndReimport();
		return AssetDatabase.LoadAssetAtPath<Sprite>(pfad);
	}

	// Die GLBs enthalten die Farben des Originals so, wie Unity sie gespeichert hatte. glTFast
	// haelt sie fuer lineare Werte und rechnet sie in Gamma um (baseColorLinear.gamma), dadurch
	// wurde alles heller und weisslicher. Deshalb eigene Standard-Materialien mit den
	// urspruenglichen Werten (die Umrechnung zurueck mit .linear).
	static void MaterialienErsetzen(GameObject modell, string ordner)
	{
		Ordner(ordner);
		var ersetzt = new Dictionary<Material, Material>();
		foreach (var anzeige in modell.GetComponentsInChildren<Renderer>(true))
		{
			var materialien = anzeige.sharedMaterials;
			for (int i = 0; i < materialien.Length; i++)
			{
				var alt = materialien[i];
				if (alt == null)
				{
					continue;
				}
				Material neu;
				if (!ersetzt.TryGetValue(alt, out neu))
				{
					neu = StandardAusGltf(alt);
					neu = AlsAsset(neu, ordner + "/" + DateiName(alt.name) + ".mat");
					ersetzt[alt] = neu;
				}
				materialien[i] = neu;
			}
			anzeige.sharedMaterials = materialien;
		}
	}

	static Material StandardAusGltf(Material gltf)
	{
		var neu = new Material(Shader.Find("Standard"));
		neu.name = gltf.name;
		Color farbe = gltf.HasProperty("baseColorFactor") ? (Color)gltf.GetVector("baseColorFactor") : gltf.color;
		neu.color = gltf.HasProperty("baseColorFactor") ? farbe.linear : farbe;
		float rauheit = gltf.HasProperty("roughnessFactor") ? gltf.GetFloat("roughnessFactor") : 0.9f;
		neu.SetFloat("_Glossiness", 1f - rauheit);
		neu.SetFloat("_Metallic", gltf.HasProperty("metallicFactor") ? gltf.GetFloat("metallicFactor") : 0f);
		if (gltf.HasProperty("emissiveFactor"))
		{
			Color leuchten = gltf.GetColor("emissiveFactor").linear;
			if (leuchten.maxColorComponent > 0.001f)
			{
				neu.EnableKeyword("_EMISSION");
				neu.SetColor("_EmissionColor", leuchten);
				neu.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
			}
		}
		return neu;
	}

	static string DateiName(string name)
	{
		var sb = new StringBuilder();
		foreach (char zeichen in name)
		{
			sb.Append(char.IsLetterOrDigit(zeichen) && zeichen < 128 ? zeichen : '_');
		}
		return sb.Length > 0 ? sb.ToString() : "Material";
	}

	// Bot: das Spieler-Prefab mit BotSteuerung, ohne AudioListener.
	static GameObject BotPrefab(GameObject spieler)
	{
		var bot = (GameObject)PrefabUtility.InstantiatePrefab(spieler);
		PrefabUtility.UnpackPrefabInstance(bot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
		bot.name = "bot";
		bot.AddComponent<BotSteuerung>();
		foreach (var ohr in bot.GetComponentsInChildren<AudioListener>(true))
		{
			Object.DestroyImmediate(ohr);
		}
		return AlsPrefab(bot, PREFABS + "/bot.prefab");
	}

	// Figur, Licht und Kamera fuer die Vorschau im Kopfbild-Editor.
	static Vorschau KopfbildVorschauBauen()
	{
		var wurzel = new GameObject("KopfbildVorschau");
		wurzel.transform.position = VORSCHAU_ORT;

		var modell = new GameObject("Modell");
		modell.transform.SetParent(wurzel.transform, false);
		var koerper = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		koerper.name = "Koerper";
		Object.DestroyImmediate(koerper.GetComponent<Collider>());
		koerper.transform.SetParent(modell.transform, false);

		// Gleiche Flaeche wie am Spieler (Kopfbild.cs). Das Netz setzt KopfbildEditor erst
		// zur Laufzeit, sonst landete es als Kopie in der Szene.
		var gesicht = new GameObject("Kopfbild");
		gesicht.transform.SetParent(modell.transform, false);
		gesicht.AddComponent<MeshFilter>();
		var anzeige = gesicht.AddComponent<MeshRenderer>();
		anzeige.sharedMaterial = KopfbildMaterial();
		anzeige.enabled = false;

		var lichtGo = new GameObject("Licht");
		lichtGo.transform.SetParent(wurzel.transform, false);
		lichtGo.transform.localPosition = new Vector3(1.2f, 1.6f, 2.2f);
		var licht = lichtGo.AddComponent<Light>();
		licht.type = LightType.Point;
		licht.range = 8f;
		licht.intensity = 1.4f;
		licht.cullingMask = 1 << LAYER_VORSCHAU;

		var kameraGo = new GameObject("Kamera");
		kameraGo.transform.SetParent(wurzel.transform, false);
		kameraGo.transform.localPosition = new Vector3(0f, 0.6f, 2.8f);
		kameraGo.transform.LookAt(wurzel.transform.position + new Vector3(0f, 0.45f, 0f));
		var kamera = kameraGo.AddComponent<Camera>();
		kamera.clearFlags = CameraClearFlags.SolidColor;
		kamera.backgroundColor = new Color(0.17f, 0.17f, 0.19f);
		kamera.cullingMask = 1 << LAYER_VORSCHAU;
		kamera.fieldOfView = 30f;
		kamera.nearClipPlane = 0.1f;
		kamera.farClipPlane = 10f;
		// KopfbildEditor schaltet sie nur ein, solange das Menue offen ist.
		kameraGo.SetActive(false);

		LayerSetzen(wurzel, LAYER_VORSCHAU);
		return new Vorschau { kamera = kamera, modell = modell.transform, kopfbild = anzeige };
	}

	// ------------------------------------------------------------------
	// Neues UI

	static void UiErweitern(UiBau bau, Dictionary<string, object> uiDaten, Sprite dDayVorschau)
	{
		var voll = Path.Combine(Directory.GetParent(Application.dataPath).FullName, UI_ERWEITERUNG);
		var daten = D(MiniJson.Lesen(File.ReadAllText(voll, Encoding.UTF8)));

		// Map-Knoepfe enger, damit drei nebeneinander passen.
		foreach (var a in L(daten["rects"]))
		{
			var aenderung = D(a);
			RectSetzen(bau.Pfad((string)aenderung["pfad"]).GetComponent<RectTransform>(), D(aenderung["rect"]));
		}

		// D-Day-Knopf als Kopie des Info-Raum-Knopfs, in beiden Host-Fenstern.
		foreach (var fenster in new[] { "Host a LAN Game", "Host a GLOBAL Game" })
		{
			var basis = "Canvas/Start Screen/Enter Username/Lobby Screen/" + fenster + "/UI/";
			// Die Knoepfe sind jetzt schmaler: Bild nicht verzerren, sondern beschneiden (Maske).
			bau.Pfad(basis + "BedRoomBtn/Img").GetComponent<Image>().preserveAspect = true;
			bau.Pfad(basis + "InfoRoomBtn/Img").GetComponent<Image>().preserveAspect = true;
			var info = bau.Pfad(basis + "InfoRoomBtn");
			var knopf = Object.Instantiate(info, info.transform.parent);
			knopf.name = "DDayBtn";
			RectSetzen(knopf.GetComponent<RectTransform>(), D(daten["dDayKnopf"]));
			knopf.transform.Find("Img").GetComponent<Image>().sprite = dDayVorschau;
			knopf.transform.Find("Text (TMP)").GetComponent<TMP_Text>().text = "D-Day";
			bau.pfade[basis + "DDayBtn"] = knopf;
			// Gleicher Aufruf wie bei den anderen Maps, nur mit Map 3. Die LAN-Methode bleibt dieselbe.
			var methode = fenster.Contains("LAN") ? "startHost" : "globalstartHost";
			bau.ereignisse.Add(new Ereignis
			{
				pfad = basis + "DDayBtn",
				ereignis = knopf.GetComponent<Button>().onClick,
				aufruf = "Game Manager(MonoBehaviour)." + methode + "(3)"
			});
		}

		foreach (var n in L(daten["neu"]))
		{
			var eintrag = D(n);
			var eltern = (string)eintrag["eltern"];
			var knoten = VorlagenAufloesen(D(eintrag["knoten"]), uiDaten);
			UiKnoten(knoten, bau.Pfad(eltern).transform, eltern, bau);
		}

		// Texte, die wie Knoepfe wirken ("press escape to continue"), lassen sich jetzt anklicken.
		foreach (var k in L(daten["klickbar"]))
		{
			var eintrag = D(k);
			var pfad = (string)eintrag["pfad"];
			var ziel = bau.Pfad(pfad);
			var knopf = ziel.AddComponent<Button>();
			knopf.targetGraphic = ziel.GetComponent<Graphic>();
			bau.ereignisse.Add(new Ereignis { pfad = pfad, ereignis = knopf.onClick, aufruf = (string)eintrag["aufruf"] });
		}

		// Ersatz fuer die Blink-Animationen des Originals.
		foreach (var p in L(daten["blinken"]))
		{
			bau.Pfad((string)p).AddComponent<Blinken>();
		}
	}

	// Ersetzt "vorlage" durch eine Kopie des Knotens aus ui_layout.json und uebernimmt
	// name, rect, active, titel. Kinder werden genauso behandelt.
	static Dictionary<string, object> VorlagenAufloesen(Dictionary<string, object> knoten, Dictionary<string, object> uiDaten)
	{
		Dictionary<string, object> ergebnis;
		if (knoten.ContainsKey("vorlage"))
		{
			var pfad = (string)knoten["vorlage"];
			var vorlage = KnotenSuchen(uiDaten, "Canvas", pfad);
			if (vorlage == null)
			{
				throw new System.Exception("Vorlage nicht gefunden: " + pfad);
			}
			ergebnis = D(Kopie(vorlage));
			foreach (var feld in new[] { "name", "rect", "active" })
			{
				if (knoten.ContainsKey(feld))
				{
					ergebnis[feld] = knoten[feld];
				}
			}
			if (!ergebnis.ContainsKey("active"))
			{
				ergebnis["active"] = true;
			}
			if (knoten.ContainsKey("titel"))
			{
				TitelSetzen(ergebnis, (string)knoten["titel"]);
			}
			if (knoten.ContainsKey("ohneEreignisse") && (bool)knoten["ohneEreignisse"])
			{
				EreignisseEntfernen(ergebnis);
			}
		}
		else
		{
			ergebnis = new Dictionary<string, object>(knoten);
		}
		if (knoten.ContainsKey("children") && !knoten.ContainsKey("vorlage"))
		{
			var kinder = new List<object>();
			foreach (var kind in L(knoten["children"]))
			{
				kinder.Add(VorlagenAufloesen(D(kind), uiDaten));
			}
			ergebnis["children"] = kinder;
		}
		return ergebnis;
	}

	static Dictionary<string, object> KnotenSuchen(Dictionary<string, object> knoten, string pfad, string gesucht)
	{
		if (pfad == gesucht)
		{
			return knoten;
		}
		if (!knoten.ContainsKey("children"))
		{
			return null;
		}
		foreach (var kind in L(knoten["children"]))
		{
			var k = D(kind);
			var treffer = KnotenSuchen(k, pfad + "/" + (string)k["name"], gesucht);
			if (treffer != null)
			{
				return treffer;
			}
		}
		return null;
	}

	static object Kopie(object wert)
	{
		var woerterbuch = wert as Dictionary<string, object>;
		if (woerterbuch != null)
		{
			var neu = new Dictionary<string, object>();
			foreach (var paar in woerterbuch)
			{
				neu[paar.Key] = Kopie(paar.Value);
			}
			return neu;
		}
		var liste = wert as List<object>;
		if (liste != null)
		{
			var neu = new List<object>();
			foreach (var element in liste)
			{
				neu.Add(Kopie(element));
			}
			return neu;
		}
		return wert;
	}

	static void TitelSetzen(Dictionary<string, object> knoten, string titel)
	{
		if (!knoten.ContainsKey("children"))
		{
			return;
		}
		foreach (var kind in L(knoten["children"]))
		{
			var k = D(kind);
			if ((string)k["name"] == "Title" && k.ContainsKey("text"))
			{
				D(k["text"])["text"] = titel;
			}
		}
	}

	static void EreignisseEntfernen(Dictionary<string, object> knoten)
	{
		foreach (var art in new[] { "Button", "Slider", "Toggle", "TMP_InputField" })
		{
			if (knoten.ContainsKey(art))
			{
				D(knoten[art])["onEvent"] = new List<object>();
			}
		}
		if (knoten.ContainsKey("children"))
		{
			foreach (var kind in L(knoten["children"]))
			{
				EreignisseEntfernen(D(kind));
			}
		}
	}

	// Welche Grafiken Mausklicks auffangen. Unity setzt das fuer jede neue Grafik auf "an". Dann
	// fangen auch unsichtbare Flaechen Klicks ab: Die Spielerliste lag im Pausemenue ueber den
	// Knoepfen links unten, die deshalb nicht reagierten. Klicks fangen jetzt nur noch Teile von
	// Bedienelementen und einfarbige, deckende Flaechen (Hintergrund von Fenstern, damit man nicht
	// durch sie hindurch klickt). Bilder ohne Funktion und reine Texte lassen Klicks durch.
	static void KlickflaechenSetzen(GameObject wurzel)
	{
		foreach (var grafik in wurzel.GetComponentsInChildren<Graphic>(true))
		{
			bool inBedienelement = false;
			for (var t = grafik.transform; t != null; t = t.parent)
			{
				if (t.GetComponent<Selectable>() != null)
				{
					inBedienelement = true;
					break;
				}
			}
			var bild = grafik as Image;
			bool deckenderHintergrund = bild != null && bild.sprite == null && bild.color.a >= 0.5f;
			grafik.raycastTarget = inBedienelement || deckenderHintergrund;
		}
	}

	// Verdrahtet die Felder der neuen und erweiterten Skripte.
	static void NeueFelderSetzen(UiBau bau, Vorschau vorschau)
	{
		var canvas = bau.Pfad("Canvas");

		var einstellungen = canvas.GetComponent<SettingsManager>();
		var v = "Canvas/SettingsScreen/SettingsScroller/Viewport/";
		Setze(einstellungen, "tabInhalte", new Object[] { bau.Pfad(v + "VideoSettings"), bau.Pfad(v + "SoundSettings"), bau.Pfad(v + "ControllSettings") });
		Setze(einstellungen, "scroller", bau.Komponente<ScrollRect>("Canvas/SettingsScreen/SettingsScroller"));
		Setze(einstellungen, "fpsSchalter", bau.Komponente<Toggle>(v + "VideoSettings/DisplayFPS/Toggle"));
		Setze(einstellungen, "fensterKnopf", bau.Komponente<Button>(v + "VideoSettings/WindowMode/Windowed"));
		Setze(einstellungen, "vollbildKnopf", bau.Komponente<Button>(v + "VideoSettings/WindowMode/FullScreen"));
		Setze(einstellungen, "kopfbilderSchalter", bau.Komponente<Toggle>(v + "VideoSettings/KopfbilderZeile/Toggle"));
		Setze(einstellungen, "lautstaerkeRegler", bau.Komponente<Slider>(v + "SoundSettings/LautstaerkeZeile/Slider"));
		Setze(einstellungen, "invertierenSchalter", bau.Komponente<Toggle>(v + "ControllSettings/InvertierenZeile/Toggle"));
		Setze(einstellungen, "drittePersonSchalter", bau.Komponente<Toggle>(v + "ControllSettings/DrittePersonZeile/Toggle"));
		Setze(einstellungen, "fensterTitel", bau.Komponente<TMP_Text>(v + "VideoSettings/WindowMode/Title"));

		var spielUi = canvas.GetComponent<UIGameManager>();
		var g = "Canvas/Game Screen/GameUI/";
		Setze(spielUi, "lebenText", bau.Komponente<TMP_Text>(g + "LebenRahmen/LebenText"));
		Setze(spielUi, "lebenBalken", bau.Komponente<RectTransform>(g + "LebenRahmen/LebenBalken"));
		Setze(spielUi, "waffeText", bau.Komponente<TMP_Text>(g + "WaffeText"));
		Setze(spielUi, "teamText", bau.Komponente<TMP_Text>(g + "TeamText"));
		Setze(spielUi, "fpsText", bau.Komponente<TMP_Text>(g + "FpsText"));
		Setze(spielUi, "fadenkreuz", bau.Pfad(g + "Crosshair"));
		Setze(spielUi, "zielfernrohr", bau.Pfad(g + "Zielfernrohr"));
		Setze(spielUi, "killText", bau.Komponente<TMP_Text>(g + "KillText"));
		Setze(spielUi, "teamKnopf", bau.Pfad("Canvas/Game Screen/PauseUI/bar/TeamBtn"));
		Setze(spielUi, "hinweisText", bau.Komponente<TMP_Text>(g + "HinweisText"));
		Setze(spielUi, "ladebildschirm", bau.Pfad("Canvas/Ladebildschirm"));
		Setze(spielUi, "ladeKarteText", bau.Komponente<TMP_Text>("Canvas/Ladebildschirm/KarteTxt"));

		var a = "Canvas/Start Screen/Aussehen/";
		var editor = bau.Pfad("Canvas/Start Screen/Aussehen").GetComponent<KopfbildEditor>();
		Setze(editor, "vorschauBild", bau.Komponente<RawImage>(a + "Vorschau"));
		Setze(editor, "statusText", bau.Komponente<TMP_Text>(a + "StatusTxt"));
		Setze(editor, "waehlenKnopf", bau.Komponente<Button>(a + "WaehlenBtn"));
		Setze(editor, "entfernenKnopf", bau.Komponente<Button>(a + "EntfernenBtn"));
		Setze(editor, "vorschauKamera", vorschau.kamera);
		Setze(editor, "vorschauModell", vorschau.modell);
		Setze(editor, "vorschauKopfbild", vorschau.kopfbild);
		Setze(editor, "hoeheRegler", bau.Komponente<Slider>(a + "HoeheZeile/Slider"));
		Setze(editor, "groesseRegler", bau.Komponente<Slider>(a + "GroesseZeile/Slider"));
		Setze(editor, "breiteRegler", bau.Komponente<Slider>(a + "BreiteZeile/Slider"));

		foreach (var fenster in new[] { "Host a LAN Game", "Host a GLOBAL Game" })
		{
			var o = "Canvas/Start Screen/Enter Username/Lobby Screen/" + fenster + "/UI/Optionen";
			var optionen = bau.Pfad(o).GetComponent<HostOptionen>();
			Setze(optionen, "teamsSchalter", bau.Komponente<Toggle>(o + "/TeamsZeile/Toggle"));
			Setze(optionen, "botsSchalter", bau.Komponente<Toggle>(o + "/BotsZeile/Toggle"));
			Setze(optionen, "spielerRegler", bau.Komponente<Slider>(o + "/SpielerZeile/Slider"));
			Setze(optionen, "spielerText", bau.Komponente<TMP_Text>(o + "/SpielerZeile/Title"));
		}
	}
}
