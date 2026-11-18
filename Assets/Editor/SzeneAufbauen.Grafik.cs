using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
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

	static PostProcessResources NachbearbeitungRessourcen()
	{
		var ressourcen = AssetDatabase.LoadAssetAtPath<PostProcessResources>(PP_RESSOURCEN);
		if (ressourcen == null)
		{
			throw new Exception("Paket \"Post Processing\" fehlt. Unity einmal neu starten, damit es aus Packages/manifest.json geladen wird.");
		}
		return ressourcen;
	}

	static void KameraNachbearbeitung(Camera kamera)
	{
		var schicht = kamera.gameObject.AddComponent<PostProcessLayer>();
		schicht.Init(NachbearbeitungRessourcen());
		schicht.volumeLayer = 1 << LAYER_NACHBEARBEITUNG;
		schicht.volumeTrigger = kamera.transform;
		// SMAA: scharfe Kanten ohne das Verschmieren von TAA bei schnellen Drehungen.
		schicht.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
		schicht.enabled = false;
	}

	static void NachbearbeitungVolumen()
	{
		var go = new GameObject("Nachbearbeitung");
		go.layer = LAYER_NACHBEARBEITUNG;
		var volumen = go.AddComponent<PostProcessVolume>();
		volumen.isGlobal = true;
		volumen.priority = 0f;
		volumen.sharedProfile = NachbearbeitungProfil();
	}

	// Die Werte sind zum Einstellen gedacht (im Editor: Assets/Erzeugt/Nachbearbeitung/Profil.asset).
	static PostProcessProfile NachbearbeitungProfil()
	{
		Ordner(NACHBEARBEITUNG);
		var pfad = NACHBEARBEITUNG + "/Profil.asset";
		// Neu anlegen statt ueberschreiben: Die Effekte sind Unter-Assets, CopySerialized
		// wuerde auf die alten verweisen. Die Szene wird ohnehin neu gebaut.
		if (AssetDatabase.LoadAssetAtPath<Object>(pfad) != null)
		{
			AssetDatabase.DeleteAsset(pfad);
		}
		var profil = ScriptableObject.CreateInstance<PostProcessProfile>();
		AssetDatabase.CreateAsset(profil, pfad);

		// Leuchten: Lampen, Feuer, Sprungfelder und Sonnenreflexe strahlen.
		var bloom = Effekt<Bloom>(profil);
		bloom.intensity.Override(2.2f);
		bloom.threshold.Override(1.05f);
		bloom.softKnee.Override(0.6f);
		bloom.diffusion.Override(7f);

		// Umgebungsverdeckung: Ecken, Fugen und Spalten werden dunkler, alles wirkt plastischer.
		var verdeckung = Effekt<AmbientOcclusion>(profil);
		verdeckung.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
		verdeckung.intensity.Override(0.75f);
		verdeckung.thicknessModifier.Override(1.2f);

		// Filmischer Look: ACES, etwas mehr Kontrast und Saettigung, leicht warm.
		var farbe = Effekt<ColorGrading>(profil);
		farbe.gradingMode.Override(GradingMode.HighDefinitionRange);
		farbe.tonemapper.Override(Tonemapper.ACES);
		farbe.postExposure.Override(0.45f);
		farbe.contrast.Override(12f);
		farbe.saturation.Override(12f);
		farbe.temperature.Override(4f);

		var vignette = Effekt<Vignette>(profil);
		vignette.intensity.Override(0.3f);
		vignette.smoothness.Override(0.45f);

		// Ganz leichte Farbsaeume am Rand, wie bei einer echten Linse.
		var linse = Effekt<ChromaticAberration>(profil);
		linse.intensity.Override(0.08f);

		EditorUtility.SetDirty(profil);
		AssetDatabase.SaveAssets();
		return profil;
	}

	static T Effekt<T>(PostProcessProfile profil) where T : PostProcessEffectSettings
	{
		// AddSettings legt den Effekt nur im Speicher an, als Unter-Asset bleibt er gespeichert.
		var effekt = profil.AddSettings<T>();
		AssetDatabase.AddObjectToAsset(effekt, profil);
		return effekt;
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
