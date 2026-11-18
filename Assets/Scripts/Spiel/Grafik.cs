using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// Grafikstufe "Hoch" (Einstellungen > Video): Nachbearbeitung (Bloom, Umgebungsverdeckung,
// Farbkorrektur mit ACES, Vignette, Kantenglaettung), weiche Schatten aller Sonnen, hohe
// Schattenqualitaet, besseres Wasser. "Normal" ist der Stand des Originals.
public static class Grafik
{
	public static bool Hoch { get; private set; }

	// Wird ausgeloest, wenn die Stufe wechselt (Wasser tauscht dann sein Material).
	public static event Action Geaendert;

	// Werte von "Normal", beim ersten Umschalten gemerkt.
	private static bool gemerkt;

	private static ShadowQuality schatten;

	private static ShadowResolution schattenAufloesung;

	private static float schattenWeite;

	private static int schattenKaskaden;

	private static AnisotropicFiltering anisotrop;

	private static int lichter;

	private static bool weichePartikel;

	public static void Setzen(bool hoch)
	{
		if (!gemerkt)
		{
			schatten = QualitySettings.shadows;
			schattenAufloesung = QualitySettings.shadowResolution;
			schattenWeite = QualitySettings.shadowDistance;
			schattenKaskaden = QualitySettings.shadowCascades;
			anisotrop = QualitySettings.anisotropicFiltering;
			lichter = QualitySettings.pixelLightCount;
			weichePartikel = QualitySettings.softParticles;
			gemerkt = true;
		}
		Hoch = hoch;
		if (hoch)
		{
			QualitySettings.shadows = ShadowQuality.All;
			QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
			QualitySettings.shadowDistance = 160f;
			QualitySettings.shadowCascades = 4;
			QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
			QualitySettings.pixelLightCount = 8;
			QualitySettings.softParticles = true;
		}
		else
		{
			QualitySettings.shadows = schatten;
			QualitySettings.shadowResolution = schattenAufloesung;
			QualitySettings.shadowDistance = schattenWeite;
			QualitySettings.shadowCascades = schattenKaskaden;
			QualitySettings.anisotropicFiltering = anisotrop;
			QualitySettings.pixelLightCount = lichter;
			QualitySettings.softParticles = weichePartikel;
		}
		Anwenden();
		Geaendert?.Invoke();
	}

	// Auf alle Kameras und Sonnen anwenden (auch auf spaeter erzeugte, deshalb oeffentlich).
	public static void Anwenden()
	{
		foreach (PostProcessLayer schicht in UnityEngine.Object.FindObjectsOfType<PostProcessLayer>(true))
		{
			schicht.enabled = Hoch;
		}
		foreach (Light licht in UnityEngine.Object.FindObjectsOfType<Light>(true))
		{
			if (licht.type != LightType.Directional)
			{
				continue;
			}
			SonnenSchatten merker = licht.GetComponent<SonnenSchatten>();
			if (merker == null)
			{
				merker = licht.gameObject.AddComponent<SonnenSchatten>();
				merker.normal = licht.shadows;
			}
			licht.shadows = Hoch ? LightShadows.Soft : merker.normal;
		}
	}

	// Nachbearbeitung an eine zur Laufzeit erzeugte Kamera haengen (Schulterkamera).
	public static void KameraAusstatten(Camera kamera, PostProcessResources ressourcen, LayerMask volumen)
	{
		if (ressourcen == null || kamera.GetComponent<PostProcessLayer>() != null)
		{
			return;
		}
		PostProcessLayer schicht = kamera.gameObject.AddComponent<PostProcessLayer>();
		schicht.Init(ressourcen);
		schicht.volumeLayer = volumen;
		schicht.volumeTrigger = kamera.transform;
		schicht.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
		schicht.enabled = Hoch;
	}
}
