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

	// Groesse, auf die jedes Bild verkleinert wird (quadratisch). Haelt die Uebertragung klein.
	private const int BILDGROESSE = 256;

	// Ab dieser Groesse als JPG statt PNG speichern (Bytes).
	private const int PNG_GRENZE = 60 * 1024;

	// Groesste Datei, die ueberhaupt gelesen wird.
	private const long MAX_DATEI = 20L * 1024 * 1024;

	private RenderTexture ziel;

	private Texture2D vorschauTextur;

	private void Awake()
	{
		// Gleiches Netz wie am Spieler, erst zur Laufzeit gesetzt (nicht in der Szene gespeichert).
		if (vorschauKopfbild != null)
		{
			vorschauKopfbild.GetComponent<MeshFilter>().sharedMesh = KopfbildNetz.Holen();
		}
	}

	private void Start()
	{
		waehlenKnopf.onClick.AddListener(bildWaehlen);
		entfernenKnopf.onClick.AddListener(bildEntfernen);
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
			Texture2D original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!original.LoadImage(File.ReadAllBytes(pfad)))
			{
				UnityEngine.Object.Destroy(original);
				status("Die Datei ist kein lesbares Bild.");
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
