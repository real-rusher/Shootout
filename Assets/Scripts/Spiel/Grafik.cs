using System;
using System.Reflection;
using UnityEngine;

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

	// Die Nachbearbeitung kommt aus dem Paket "Post Processing". Es wird nur ueber Reflexion
	// angesprochen: Fehlt das Paket (Unity hat es nicht geladen), laeuft das Spiel trotzdem,
	// nur ohne diese Effekte. Vorher brach dann das ganze Uebersetzen ab.
	private const string PP = "UnityEngine.Rendering.PostProcessing.";

	public static Type PaketTyp(string name)
	{
		return Type.GetType(PP + name + ", Unity.Postprocessing.Runtime");
	}

	private static readonly Type schichtTyp = PaketTyp("PostProcessLayer");

	public static bool NachbearbeitungVorhanden => schichtTyp != null;

	// Auf alle Kameras und Sonnen anwenden (auch auf spaeter erzeugte, deshalb oeffentlich).
	public static void Anwenden()
	{
		if (schichtTyp != null)
		{
			foreach (UnityEngine.Object schicht in UnityEngine.Object.FindObjectsOfType(schichtTyp, true))
			{
				((Behaviour)schicht).enabled = Hoch;
			}
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
	public static void KameraAusstatten(Camera kamera, ScriptableObject ressourcen, LayerMask volumen)
	{
		if (schichtTyp == null || ressourcen == null || kamera.GetComponent(schichtTyp) != null)
		{
			return;
		}
		SchichtAnlegen(kamera, ressourcen, volumen).enabled = Hoch;
	}

	// Legt die Schicht an (auch das Editor-Skript nutzt das). Ressourcen ist das
	// PostProcessResources-Asset des Pakets.
	public static Behaviour SchichtAnlegen(Camera kamera, ScriptableObject ressourcen, LayerMask volumen)
	{
		// Waehrend des Einrichtens aus, damit OnEnable nicht ohne Ressourcen laeuft.
		GameObject go = kamera.gameObject;
		bool aktiv = go.activeSelf;
		go.SetActive(false);
		Behaviour schicht = (Behaviour)go.AddComponent(schichtTyp);
		schichtTyp.GetMethod("Init").Invoke(schicht, new object[] { ressourcen });
		schichtTyp.GetField("volumeLayer").SetValue(schicht, volumen);
		schichtTyp.GetField("volumeTrigger").SetValue(schicht, kamera.transform);
		// SMAA: scharfe Kanten ohne das Verschmieren von TAA bei schnellen Drehungen.
		FieldInfo glaetten = schichtTyp.GetField("antialiasingMode");
		glaetten.SetValue(schicht, Enum.Parse(glaetten.FieldType, "SubpixelMorphologicalAntialiasing"));
		schicht.enabled = false;
		go.SetActive(aktiv);
		return schicht;
	}
}
