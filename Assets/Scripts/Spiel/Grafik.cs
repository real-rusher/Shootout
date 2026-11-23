using System;
using System.Reflection;
using UnityEngine;

// Grafikstufe "Hoch" (Einstellungen > Video): Nachbearbeitung (Bloom, Umgebungsverdeckung,
// Farbkorrektur mit ACES, Vignette, Kantenglaettung), weiche Schatten aller Sonnen, hohe
// Schattenqualitaet, besseres Wasser. "Normal" ist der Stand des Originals.
// Stufe "Ultra" (schliesst "Hoch" ein): Baeume, Buesche und Palmen aus Blaettern, Grashalme,
// Wasserspritzer, Feuer mit Funken, Waffenmodelle, Spiegelungen, feinere Texturen, Deferred
// Rendering mit Bildschirm-Spiegelungen. Was nur dort zu sehen ist, schaltet UltraSchalter.
public static class Grafik
{
	public static bool Hoch { get; private set; }

	public static bool Ultra { get; private set; }

	// Kamera des eigenen Spielers (setzt playerlooking), z. B. fuer die Sichtweite des Grases.
	public static Transform Beobachter;

	// Wird ausgeloest, wenn die Stufe wechselt (Wasser tauscht dann sein Material).
	public static event Action Geaendert;

	// Werte von "Normal", beim ersten Umschalten gemerkt.
	private static bool gemerkt;

	private static AnisotropicFiltering anisotrop;

	private static int lichter;

	private static bool weichePartikel;

	private static float lodFaktor;

	private static bool spiegelungen;

	public static void Setzen(bool hoch)
	{
		Stufe(hoch, Ultra && hoch);
	}

	// "Ultra" an schaltet auch "Hoch" an.
	public static void SetzeUltra(bool ultra)
	{
		Stufe(Hoch || ultra, ultra);
	}

	private static void Stufe(bool hoch, bool ultra)
	{
		if (!gemerkt)
		{
			anisotrop = QualitySettings.anisotropicFiltering;
			lichter = QualitySettings.pixelLightCount;
			weichePartikel = QualitySettings.softParticles;
			lodFaktor = QualitySettings.lodBias;
			spiegelungen = QualitySettings.realtimeReflectionProbes;
			gemerkt = true;
		}
		Hoch = hoch;
		Ultra = ultra;
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
			// "Normal": einfache Schatten, aber immer welche (Figuren, Haeuser, Baeume).
			QualitySettings.shadows = ShadowQuality.All;
			QualitySettings.shadowResolution = ShadowResolution.Medium;
			QualitySettings.shadowDistance = 70f;
			QualitySettings.shadowCascades = 2;
			QualitySettings.anisotropicFiltering = anisotrop;
			QualitySettings.pixelLightCount = Mathf.Max(lichter, 4);
			QualitySettings.softParticles = weichePartikel;
		}
		// Feste Schattenkacheln: Sonst wandern und flackern Schattenkanten, wenn man sich bewegt.
		QualitySettings.shadowProjection = ShadowProjection.StableFit;
		QualitySettings.lodBias = ultra ? 2.5f : lodFaktor;
		QualitySettings.realtimeReflectionProbes = ultra || spiegelungen;
		if (ultra)
		{
			QualitySettings.shadowDistance = 250f;
		}
		Shader.SetGlobalFloat("_ShootoutUltra", ultra ? 1f : 0f);
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
		// Deferred Rendering in "Ultra": noetig fuer die Bildschirm-Spiegelungen der
		// Nachbearbeitung, und viele Lampen kosten kaum mehr. Kameras mit Zieltextur
		// (Vorschau im Kopfbild-Editor) bleiben, wie sie sind.
		foreach (Camera kamera in UnityEngine.Object.FindObjectsOfType<Camera>(true))
		{
			if (kamera.targetTexture == null)
			{
				kamera.renderingPath = Ultra ? RenderingPath.DeferredShading : RenderingPath.UsePlayerSettings;
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
			// Nur Sonnen, die schon Schatten werfen, bekommen weiche. Vorher bekam auch das
			// Aufhelllicht Schatten: Die zeigten in eine Richtung, in der keine Sonne steht.
			licht.shadows = Hoch && merker.normal != LightShadows.None ? LightShadows.Soft : merker.normal;
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
