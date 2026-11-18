using System;
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
	private const long MAX_DATEI = 20L * 1024 * 1024;

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
		status(Dateiauswahl.Verfuegbar ? "PNG oder JPG wählen. Andere Spieler sehen es auf deinem Kopf." : "Die Dateiauswahl gibt es nur unter Windows.");
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
		string pfad = Dateiauswahl.BildWaehlen("Bild für deinen Kopf wählen");
		if (string.IsNullOrEmpty(pfad))
		{
			return;
		}
		try
		{
			string endung = Path.GetExtension(pfad).ToLowerInvariant();
			if (endung != ".png" && endung != ".jpg" && endung != ".jpeg")
			{
				status("Nur PNG- oder JPG-Bilder.");
				return;
			}
			if (new FileInfo(pfad).Length > MAX_DATEI)
			{
				status("Das Bild ist zu groß (höchstens 20 MB).");
				return;
			}
			byte[] inhalt = File.ReadAllBytes(pfad);
			// Viele Bilder aus dem Internet heissen .png, sind aber WebP oder AVIF. Die kann
			// Unity nicht lesen, deshalb das echte Format nennen.
			string format = echtesFormat(inhalt);
			if (format != "PNG" && format != "JPG")
			{
				status("Die Datei ist eigentlich " + format + ", obwohl sie " + endung + " heißt. Bitte in Paint öffnen und als PNG oder JPG speichern.");
				return;
			}
			Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!original.LoadImage(inhalt))
			{
				UnityEngine.Object.Destroy(original);
				status("Unity kann dieses " + format + " nicht lesen. Bitte in Paint öffnen und neu speichern.");
				return;
			}
			byte[] daten = verkleinern(original);
			UnityEngine.Object.Destroy(original);
			if (daten.Length > Kopfbild.MAX_GROESSE)
			{
				status("Das Bild ist nach dem Verkleinern noch zu groß.");
				return;
			}
			KopfbildSpeicher.Speichern(daten);
			vorschauLaden(daten);
			status("Gespeichert. Gilt ab dem nächsten Spiel.");
		}
		catch (Exception fehler)
		{
			status("Fehler: " + fehler.Message);
		}
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
