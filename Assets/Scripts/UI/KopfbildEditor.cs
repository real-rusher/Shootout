using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Menue "Aussehen": Bild vom PC waehlen, auf die vordere Kopfhaelfte legen, Vorschau.
// Die Vorschau zeigt ein Modell, das weit unter der Map auf einem eigenen Layer steht
// und von einer eigenen Kamera in eine RenderTexture gefilmt wird.
public class KopfbildEditor : MonoBehaviour
{
	public RawImage vorschauBild;

	public TMP_Text statusText;

	public Button waehlenKnopf;

	public Button entfernenKnopf;

	// Kamera und Modell der Vorschau (in der Szene, Layer "Vorschau").
	public Camera vorschauKamera;

	public Transform vorschauModell;

	public MeshRenderer vorschauKopfbild;

	// Lage am Kopf (KopfbildLage), wird sofort in der Vorschau gezeigt und gespeichert.
	public Slider hoeheRegler;

	public Slider groesseRegler;

	public Slider breiteRegler;

	private KopfbildLage lage;

	private Mesh vorschauNetz;

	// Groesse, auf die jedes Bild verkleinert wird (quadratisch). Haelt die Uebertragung klein.
	private const int BILDGROESSE = 256;

	// Ab dieser Groesse als JPG statt PNG speichern (Bytes).
	private const int PNG_GRENZE = 60 * 1024;

	// Groesste Datei, die ueberhaupt gelesen wird.
	private const long MAX_DATEI = 50L * 1024 * 1024;

	// So lange darf Windows fuer die Umwandlung brauchen (Sekunden).
	private const float UMWANDLUNG_ZEIT = 20f;

	private RenderTexture ziel;

	private Texture2D vorschauTextur;

	private void Start()
	{
		waehlenKnopf.onClick.AddListener(bildWaehlen);
		entfernenKnopf.onClick.AddListener(bildEntfernen);
		reglerEinrichten(hoeheRegler, KopfbildLage.HOEHE_MIN, KopfbildLage.HOEHE_MAX, wert => lage.hoehe = wert);
		reglerEinrichten(groesseRegler, KopfbildLage.GROESSE_MIN, KopfbildLage.GROESSE_MAX, wert => lage.groesse = wert);
		reglerEinrichten(breiteRegler, KopfbildLage.BREITE_MIN, KopfbildLage.BREITE_MAX, wert => lage.breite = wert);
	}

	private void reglerEinrichten(Slider regler, float min, float max, System.Action<float> setzen)
	{
		if (regler == null)
		{
			return;
		}
		regler.minValue = min;
		regler.maxValue = max;
		regler.wholeNumbers = false;
		regler.onValueChanged.AddListener(wert =>
		{
			setzen(wert);
			KopfbildSpeicher.LageSpeichern(lage);
			vorschauNetzAktualisieren();
		});
	}

	private void reglerAnzeigen()
	{
		if (hoeheRegler != null) hoeheRegler.SetValueWithoutNotify(lage.hoehe);
		if (groesseRegler != null) groesseRegler.SetValueWithoutNotify(lage.groesse);
		if (breiteRegler != null) breiteRegler.SetValueWithoutNotify(lage.breite);
	}

	private void vorschauNetzAktualisieren()
	{
		if (vorschauKopfbild == null)
		{
			return;
		}
		if (vorschauNetz != null)
		{
			UnityEngine.Object.Destroy(vorschauNetz);
		}
		vorschauNetz = KopfbildNetz.Erzeugen(lage);
		vorschauKopfbild.GetComponent<MeshFilter>().sharedMesh = vorschauNetz;
	}

	private void OnEnable()
	{
		if (ziel == null)
		{
			ziel = new RenderTexture(512, 512, 16);
			ziel.name = "KopfbildVorschau";
		}
		if (vorschauKamera != null)
		{
			vorschauKamera.targetTexture = ziel;
			vorschauKamera.gameObject.SetActive(true);
		}
		if (vorschauBild != null)
		{
			vorschauBild.texture = ziel;
		}
		lage = KopfbildSpeicher.LageLaden();
		reglerAnzeigen();
		vorschauNetzAktualisieren();
		vorschauLaden(KopfbildSpeicher.Laden());
		status(Dateiauswahl.Verfuegbar ? "Jedes Bildformat, das Windows öffnen kann. Andere Spieler sehen es auf deinem Kopf." : "Die Dateiauswahl gibt es nur unter Windows.");
	}

	private void OnDisable()
	{
		// Kamera aus, solange niemand die Vorschau sieht.
		if (vorschauKamera != null)
		{
			vorschauKamera.gameObject.SetActive(false);
		}
	}

	private void Update()
	{
		// Modell langsam hin und her drehen, die Vorderseite bleibt im Blick.
		if (vorschauModell != null)
		{
			vorschauModell.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.unscaledTime * 0.8f) * 40f, 0f);
		}
	}

	private void bildWaehlen()
	{
		if (umwandlungLaeuft)
		{
			return;
		}
		string pfad = Dateiauswahl.BildWaehlen("Bild für deinen Kopf wählen");
		if (string.IsNullOrEmpty(pfad))
		{
			return;
		}
		StartCoroutine(bildLaden(pfad));
	}

	private bool umwandlungLaeuft;

	// Erst direkt mit Unity versuchen (PNG, JPG). Alles andere wandelt Windows in PNG um.
	private IEnumerator bildLaden(string pfad)
	{
		byte[] inhalt;
		try
		{
			if (new FileInfo(pfad).Length > MAX_DATEI)
			{
				status("Das Bild ist zu groß (höchstens " + (MAX_DATEI / (1024 * 1024)) + " MB).");
				yield break;
			}
			inhalt = File.ReadAllBytes(pfad);
		}
		catch (Exception fehler)
		{
			status("Datei nicht lesbar: " + fehler.Message);
			yield break;
		}

		Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		string format = echtesFormat(inhalt);
		bool gelesen = (format == "PNG" || format == "JPG") && original.LoadImage(inhalt);
		if (!gelesen)
		{
			if (!Bildumwandlung.Verfuegbar)
			{
				UnityEngine.Object.Destroy(original);
				status("Dieses Format geht nur unter Windows. Bitte als PNG oder JPG speichern.");
				yield break;
			}
			status("Bild wird umgewandelt …");
			string ziel = Path.Combine(Application.temporaryCachePath, "kopfbild_umgewandelt.png");
			if (File.Exists(ziel))
			{
				File.Delete(ziel);
			}
			umwandlungLaeuft = true;
			System.Diagnostics.Process prozess = null;
			try
			{
				prozess = Bildumwandlung.Starten(pfad, ziel);
			}
			catch (Exception fehler)
			{
				UnityEngine.Debug.LogWarning("Umwandlung nicht gestartet: " + fehler.Message);
			}
			float bis = Time.unscaledTime + UMWANDLUNG_ZEIT;
			while (prozess != null && !prozess.HasExited && Time.unscaledTime < bis)
			{
				yield return null;
			}
			umwandlungLaeuft = false;
			if (prozess != null && !prozess.HasExited)
			{
				prozess.Kill();
			}
			gelesen = File.Exists(ziel) && original.LoadImage(File.ReadAllBytes(ziel));
			if (!gelesen)
			{
				UnityEngine.Object.Destroy(original);
				status(format == "AVIF oder HEIC"
					? "Windows kann AVIF oder HEIC nur mit der kostenlosen Erweiterung aus dem Microsoft Store öffnen (HEIF- bzw. AV1-Bilderweiterung)."
					: "Windows kann diese Datei nicht als Bild öffnen (" + format + ").");
				yield break;
			}
		}

		byte[] daten = verkleinern(original);
		UnityEngine.Object.Destroy(original);
		if (daten.Length > Kopfbild.MAX_GROESSE)
		{
			status("Das Bild ist nach dem Verkleinern noch zu groß.");
			yield break;
		}
		KopfbildSpeicher.Speichern(daten);
		vorschauLaden(daten);
		status("Gespeichert. Gilt ab dem nächsten Spiel.");
	}

	// Format nach den ersten Bytes der Datei, nicht nach der Endung.
	private static string echtesFormat(byte[] d)
	{
		if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) return "PNG";
		if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "JPG";
		if (d.Length >= 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return "WebP";
		if (d.Length >= 6 && d[0] == 'G' && d[1] == 'I' && d[2] == 'F') return "GIF";
		if (d.Length >= 2 && d[0] == 'B' && d[1] == 'M') return "BMP";
		if (d.Length >= 12 && d[4] == 'f' && d[5] == 't' && d[6] == 'y' && d[7] == 'p') return "AVIF oder HEIC";
		return "ein unbekanntes Format";
	}

	private void bildEntfernen()
	{
		KopfbildSpeicher.Loeschen();
		vorschauLaden(null);
		status("Bild entfernt.");
	}

	// Auf BILDGROESSE x BILDGROESSE bringen (gestreckt, so liegt es auch auf dem Kopf).
	private static byte[] verkleinern(Texture2D original)
	{
		RenderTexture zwischen = RenderTexture.GetTemporary(BILDGROESSE, BILDGROESSE, 0, RenderTextureFormat.ARGB32);
		RenderTexture vorher = RenderTexture.active;
		Graphics.Blit(original, zwischen);
		RenderTexture.active = zwischen;
		Texture2D klein = new Texture2D(BILDGROESSE, BILDGROESSE, TextureFormat.RGBA32, false);
		klein.ReadPixels(new Rect(0, 0, BILDGROESSE, BILDGROESSE), 0, 0);
		klein.Apply();
		RenderTexture.active = vorher;
		RenderTexture.ReleaseTemporary(zwischen);
		byte[] daten = klein.EncodeToPNG();
		if (daten.Length > PNG_GRENZE)
		{
			daten = klein.EncodeToJPG(85);
		}
		UnityEngine.Object.Destroy(klein);
		return daten;
	}

	private void vorschauLaden(byte[] daten)
	{
		if (vorschauKopfbild == null)
		{
			return;
		}
		if (daten == null)
		{
			vorschauKopfbild.enabled = false;
			return;
		}
		if (vorschauTextur == null)
		{
			vorschauTextur = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			vorschauTextur.wrapMode = TextureWrapMode.Clamp;
		}
		vorschauKopfbild.enabled = vorschauTextur.LoadImage(daten);
		vorschauKopfbild.material.mainTexture = vorschauTextur;
	}

	private void status(string text)
	{
		if (statusText != null)
		{
			statusText.text = text;
		}
	}
}
