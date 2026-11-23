using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Teil des Editor-Skripts: Grafikstufe "Hoch" (Einstellungen > Video). Ein Profil der
// Nachbearbeitung (Post Processing v2), ein globales Volumen in der Szene, die Schicht an
// den Kameras (aus, bis die Stufe eingeschaltet wird) und die Wasser-Materialien.
public static partial class SzeneAufbauen
{
	// Layer des Volumens. Die Kameras werten nur Volumen auf diesem Layer aus.
	const int LAYER_NACHBEARBEITUNG = 11;

	const string NACHBEARBEITUNG = ERZEUGT + "/Nachbearbeitung";

	const string PP_RESSOURCEN = "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset";

	// Das Paket wird nur ueber Reflexion angesprochen (siehe Grafik). Fehlt es, baut das Skript
	// alles andere trotzdem, nur ohne Nachbearbeitung, und sagt es in der Konsole.
	static ScriptableObject NachbearbeitungRessourcen()
	{
		if (!Grafik.NachbearbeitungVorhanden)
		{
			return null;
		}
		return AssetDatabase.LoadAssetAtPath<ScriptableObject>(PP_RESSOURCEN);
	}

	static bool nachbearbeitungGemeldet;

	static bool NachbearbeitungMoeglich()
	{
		if (NachbearbeitungRessourcen() != null)
		{
			return true;
		}
		if (!nachbearbeitungGemeldet)
		{
			nachbearbeitungGemeldet = true;
			Debug.LogWarning("Shootout: Paket \"Post Processing\" fehlt, \"Krasse Grafik\" bleibt ohne Nachbearbeitung. " +
				"Window > Package Manager > Unity Registry > Post Processing > Install, dann neu aufbauen.");
		}
		return false;
	}

	static void KameraNachbearbeitung(Camera kamera)
	{
		if (NachbearbeitungMoeglich())
		{
			Grafik.SchichtAnlegen(kamera, NachbearbeitungRessourcen(), 1 << LAYER_NACHBEARBEITUNG);
		}
	}

	static void NachbearbeitungVolumen()
	{
		if (!NachbearbeitungMoeglich())
		{
			return;
		}
		var go = new GameObject("Nachbearbeitung");
		go.layer = LAYER_NACHBEARBEITUNG;
		var typ = Grafik.PaketTyp("PostProcessVolume");
		var volumen = go.AddComponent(typ);
		typ.GetField("isGlobal").SetValue(volumen, true);
		typ.GetField("priority").SetValue(volumen, 0f);
		typ.GetField("sharedProfile").SetValue(volumen, NachbearbeitungProfil());

		// Stufe "Ultra": zweites Volumen mit Vorrang, nur dann aktiv (Bildschirm-Spiegelungen
		// brauchen Deferred Rendering, das Grafik.cs in "Ultra" einschaltet).
		var ultraGo = new GameObject("NachbearbeitungUltra");
		ultraGo.layer = LAYER_NACHBEARBEITUNG;
		var ultraVolumen = ultraGo.AddComponent(typ);
		typ.GetField("isGlobal").SetValue(ultraVolumen, true);
		typ.GetField("priority").SetValue(ultraVolumen, 1f);
		typ.GetField("sharedProfile").SetValue(ultraVolumen, NachbearbeitungUltraProfil());
		ultraGo.SetActive(false);
		var halter = new GameObject("GrafikUltra");
		UltraTausch(halter, new GameObject[0], new[] { ultraGo });
	}

	static ScriptableObject NachbearbeitungUltraProfil()
	{
		var pfad = NACHBEARBEITUNG + "/ProfilUltra.asset";
		if (AssetDatabase.LoadAssetAtPath<Object>(pfad) != null)
		{
			AssetDatabase.DeleteAsset(pfad);
		}
		var profil = ScriptableObject.CreateInstance(Grafik.PaketTyp("PostProcessProfile"));
		AssetDatabase.CreateAsset(profil, pfad);

		// Spiegelungen auf Wasser, Metall und glatten Boeden.
		var spiegel = Effekt(profil, "ScreenSpaceReflections");
		EffektWert(spiegel, "preset", "High");
		EffektWert(spiegel, "maximumMarchDistance", 150f);
		EffektWert(spiegel, "distanceFade", 0.3f);

		// Kraeftigere Verdeckung in Ecken und unter Blaettern.
		var verdeckung = Effekt(profil, "AmbientOcclusion");
		EffektWert(verdeckung, "intensity", 1.05f);
		EffektWert(verdeckung, "thicknessModifier", 1.6f);

		EditorUtility.SetDirty(profil);
		AssetDatabase.SaveAssets();
		return profil;
	}

	// Die Werte sind zum Einstellen gedacht (im Editor: Assets/Erzeugt/Nachbearbeitung/Profil.asset).
	static ScriptableObject NachbearbeitungProfil()
	{
		Ordner(NACHBEARBEITUNG);
		var pfad = NACHBEARBEITUNG + "/Profil.asset";
		// Neu anlegen statt ueberschreiben: Die Effekte sind Unter-Assets, CopySerialized
		// wuerde auf die alten verweisen. Die Szene wird ohnehin neu gebaut.
		if (AssetDatabase.LoadAssetAtPath<Object>(pfad) != null)
		{
			AssetDatabase.DeleteAsset(pfad);
		}
		var profil = ScriptableObject.CreateInstance(Grafik.PaketTyp("PostProcessProfile"));
		AssetDatabase.CreateAsset(profil, pfad);

		// Leuchten: Lampen, Feuer, Sprungfelder und Sonnenreflexe strahlen.
		var bloom = Effekt(profil, "Bloom");
		EffektWert(bloom, "intensity", 0.7f);
		EffektWert(bloom, "threshold", 1.3f);
		EffektWert(bloom, "softKnee", 0.6f);
		EffektWert(bloom, "diffusion", 7f);

		// Umgebungsverdeckung: Ecken, Fugen und Spalten werden dunkler, alles wirkt plastischer.
		var verdeckung = Effekt(profil, "AmbientOcclusion");
		EffektWert(verdeckung, "mode", "MultiScaleVolumetricObscurance");
		EffektWert(verdeckung, "intensity", 0.75f);
		EffektWert(verdeckung, "thicknessModifier", 1.2f);

		// Filmischer Look: ACES, etwas mehr Kontrast und Saettigung, leicht warm.
		var farbe = Effekt(profil, "ColorGrading");
		EffektWert(farbe, "gradingMode", "HighDefinitionRange");
		EffektWert(farbe, "tonemapper", "ACES");
		EffektWert(farbe, "postExposure", -0.1f);
		EffektWert(farbe, "contrast", 8f);
		EffektWert(farbe, "saturation", 6f);
		EffektWert(farbe, "temperature", 4f);

		var vignette = Effekt(profil, "Vignette");
		EffektWert(vignette, "intensity", 0.3f);
		EffektWert(vignette, "smoothness", 0.45f);

		// Ganz leichte Farbsaeume am Rand, wie bei einer echten Linse.
		var linse = Effekt(profil, "ChromaticAberration");
		EffektWert(linse, "intensity", 0.08f);

		EditorUtility.SetDirty(profil);
		AssetDatabase.SaveAssets();
		return profil;
	}

	static ScriptableObject Effekt(ScriptableObject profil, string name)
	{
		// AddSettings legt den Effekt nur im Speicher an, als Unter-Asset bleibt er gespeichert.
		var hinzufuegen = profil.GetType().GetMethod("AddSettings", new[] { typeof(Type) });
		var effekt = (ScriptableObject)hinzufuegen.Invoke(profil, new object[] { Grafik.PaketTyp(name) });
		AssetDatabase.AddObjectToAsset(effekt, profil);
		return effekt;
	}

	// Setzt einen Wert eines Effekts und schaltet ihn ein (Override). Aufzaehlungen als Text.
	static void EffektWert(ScriptableObject effekt, string feld, object wert)
	{
		var info = effekt.GetType().GetField(feld);
		if (info == null)
		{
			throw new Exception(effekt.GetType().Name + " hat kein Feld " + feld);
		}
		var parameter = info.GetValue(effekt);
		var ueberschreiben = parameter.GetType().GetMethod("Override");
		var typ = ueberschreiben.GetParameters()[0].ParameterType;
		var passend = typ.IsEnum ? Enum.Parse(typ, (string)wert) : Convert.ChangeType(wert, typ);
		ueberschreiben.Invoke(parameter, new[] { passend });
	}

	// Himmel fuer Spiegelungen: oben blau, am Horizont hell, unten erdig. 64 x 64 pro Seite.
	static Cubemap HimmelsWuerfel()
	{
		const int g = 64;
		var wuerfel = new Cubemap(g, TextureFormat.RGBA32, true);
		var oben = new Color(0.36f, 0.52f, 0.78f);
		var horizont = new Color(0.78f, 0.82f, 0.86f);
		var unten = new Color(0.3f, 0.28f, 0.25f);
		foreach (CubemapFace seite in System.Enum.GetValues(typeof(CubemapFace)))
		{
			if (seite == CubemapFace.Unknown)
			{
				continue;
			}
			var punkte = new Color[g * g];
			for (int y = 0; y < g; y++)
			{
				for (int x = 0; x < g; x++)
				{
					float u = (x + 0.5f) / g * 2f - 1f;
					float v = (y + 0.5f) / g * 2f - 1f;
					Vector3 r;
					switch (seite)
					{
						case CubemapFace.PositiveX: r = new Vector3(1f, -v, -u); break;
						case CubemapFace.NegativeX: r = new Vector3(-1f, -v, u); break;
						case CubemapFace.PositiveY: r = new Vector3(u, 1f, v); break;
						case CubemapFace.NegativeY: r = new Vector3(u, -1f, -v); break;
						case CubemapFace.PositiveZ: r = new Vector3(u, -v, 1f); break;
						default: r = new Vector3(-u, -v, -1f); break;
					}
					float hoehe = r.normalized.y;
					punkte[y * g + x] = hoehe >= 0f ? Color.Lerp(horizont, oben, Mathf.Pow(hoehe, 0.6f)) : Color.Lerp(horizont, unten, Mathf.Pow(-hoehe, 0.4f));
				}
			}
			wuerfel.SetPixels(punkte, seite);
		}
		wuerfel.Apply();
		return AlsAsset(wuerfel, NACHBEARBEITUNG + "/Himmel.cubemap");
	}

	// ------------------------------------------------------------------
	// Wasser

	// Eigener Shader: von oben dunkel und durchsichtig, flach angesehen hell und spiegelnd.
	static Material WasserHochMaterial(string ordner)
	{
		var shader = Shader.Find("Shootout/Wasser");
		if (shader == null)
		{
			// Ohne den Shader bleibt das normale Wasser (Wasser nimmt dann sein eigenes Material).
			return null;
		}
		var material = new Material(shader);
		material.name = "WasserHoch";
		return AlsAsset(material, ordner + "/WasserHoch.mat");
	}

	// Unterseite: Von unten sieht man die Oberflaeche als helle, glitzernde Decke.
	static Material WasserUnterseiteMaterial(string ordner)
	{
		var material = new Material(Shader.Find("Standard"));
		material.name = "WasserUnterseite";
		material.color = new Color(0.45f, 0.75f, 0.8f, 0.8f);
		material.SetFloat("_Glossiness", 0.95f);
		material.SetFloat("_Mode", 2f);
		material.SetOverrideTag("RenderType", "Transparent");
		material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		material.SetInt("_ZWrite", 0);
		material.EnableKeyword("_ALPHABLEND_ON");
		// Etwas Eigenleuchten: Das Licht von oben kommt durch, auch im Schatten der Welle.
		material.EnableKeyword("_EMISSION");
		material.SetColor("_EmissionColor", new Color(0.12f, 0.3f, 0.34f));
		material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
		material.renderQueue = 3000;
		return AlsAsset(material, ordner + "/WasserUnterseite.mat");
	}
}
