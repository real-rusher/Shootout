using System;
using System.Globalization;
using System.IO;
using Unity.Netcode;
using UnityEngine;

// Eigenes Bild auf der vorderen Haelfte des Kopfes. Der Besitzer schickt sein Bild und die
// Platzierung beim Spawn in Stuecken an den Server, der verteilt es an alle (auch an spaeter
// Beitretende).
public class Kopfbild : NetworkBehaviour
{
	// Material mit "Unlit/Transparent" (Editor-Skript traegt es ein, damit der Shader im Build ist).
	public Material material;

	// Groesser darf ein Bild nicht sein (Bytes), sonst nimmt der Server es nicht an.
	public const int MAX_GROESSE = 96 * 1024;

	// Stueckgroesse fuer die Uebertragung, unter der Paketgrenze von Unity Transport (6144).
	private const int STUECK = 4000;

	private MeshRenderer anzeige;

	private MeshFilter filter;

	private Mesh netz;

	private Texture2D textur;

	private Playermanager playermanager;

	// Empfang in Stuecken.
	private byte[] puffer;

	private KopfbildLage empfangeneLage;

	// Auf dem Server: das zuletzt vollstaendig erhaltene Bild dieses Spielers.
	private byte[] serverBild;

	private KopfbildLage serverLage;

	private void Awake()
	{
		playermanager = GetComponent<Playermanager>();
		GameObject gesicht = new GameObject("Kopfbild");
		gesicht.transform.SetParent(base.transform, false);
		gesicht.layer = base.gameObject.layer;
		filter = gesicht.AddComponent<MeshFilter>();
		anzeige = gesicht.AddComponent<MeshRenderer>();
		anzeige.sharedMaterial = material;
		anzeige.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		anzeige.enabled = false;
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		SettingsManager.KopfbildEinstellungGeaendert += anzeigeAktualisieren;
		if (playermanager.istBot)
		{
			return;
		}
		if (base.IsOwner)
		{
			hochladen(KopfbildSpeicher.Laden(), KopfbildSpeicher.LageLaden());
		}
		else
		{
			// Wer spaeter dazukommt, fragt nach dem Bild.
			bildAnfordernServerRpc();
		}
	}

	public override void OnNetworkDespawn()
	{
		SettingsManager.KopfbildEinstellungGeaendert -= anzeigeAktualisieren;
		base.OnNetworkDespawn();
	}

	public override void OnDestroy()
	{
		if (textur != null)
		{
			UnityEngine.Object.Destroy(textur);
		}
		if (netz != null)
		{
			UnityEngine.Object.Destroy(netz);
		}
		base.OnDestroy();
	}

	private void hochladen(byte[] daten, KopfbildLage lage)
	{
		if (daten == null || daten.Length == 0 || daten.Length > MAX_GROESSE)
		{
			return;
		}
		bildBeginnenServerRpc(daten.Length, lage.hoehe, lage.groesse, lage.breite);
		for (int start = 0; start < daten.Length; start += STUECK)
		{
			int laenge = Mathf.Min(STUECK, daten.Length - start);
			byte[] stueck = new byte[laenge];
			Array.Copy(daten, start, stueck, 0, laenge);
			bildStueckServerRpc(start, stueck);
		}
		bildFertigServerRpc();
	}

	[ServerRpc]
	private void bildBeginnenServerRpc(int laenge, float hoehe, float groesse, float breite)
	{
		puffer = laenge > 0 && laenge <= MAX_GROESSE ? new byte[laenge] : null;
		empfangeneLage = KopfbildLage.Begrenzt(hoehe, groesse, breite);
	}

	[ServerRpc]
	private void bildStueckServerRpc(int start, byte[] stueck)
	{
		if (puffer != null && start >= 0 && start + stueck.Length <= puffer.Length)
		{
			Array.Copy(stueck, 0, puffer, start, stueck.Length);
		}
	}

	[ServerRpc]
	private void bildFertigServerRpc()
	{
		if (puffer == null)
		{
			return;
		}
		serverBild = puffer;
		serverLage = empfangeneLage;
		puffer = null;
		verteilen(serverBild, serverLage, null);
	}

	[ServerRpc(RequireOwnership = false)]
	private void bildAnfordernServerRpc(ServerRpcParams rpcParams = default)
	{
		if (serverBild != null)
		{
			verteilen(serverBild, serverLage, new ulong[] { rpcParams.Receive.SenderClientId });
		}
	}

	// Schickt das Bild an alle (empfaenger null) oder nur an bestimmte Clients.
	private void verteilen(byte[] daten, KopfbildLage lage, ulong[] empfaenger)
	{
		ClientRpcParams ziel = default(ClientRpcParams);
		if (empfaenger != null)
		{
			ziel.Send = new ClientRpcSendParams { TargetClientIds = empfaenger };
		}
		bildBeginnenClientRpc(daten.Length, lage.hoehe, lage.groesse, lage.breite, ziel);
		for (int start = 0; start < daten.Length; start += STUECK)
		{
			int laenge = Mathf.Min(STUECK, daten.Length - start);
			byte[] stueck = new byte[laenge];
			Array.Copy(daten, start, stueck, 0, laenge);
			bildStueckClientRpc(start, stueck, ziel);
		}
		bildFertigClientRpc(ziel);
	}

	[ClientRpc]
	private void bildBeginnenClientRpc(int laenge, float hoehe, float groesse, float breite, ClientRpcParams rpcParams = default)
	{
		puffer = laenge > 0 && laenge <= MAX_GROESSE ? new byte[laenge] : null;
		empfangeneLage = KopfbildLage.Begrenzt(hoehe, groesse, breite);
	}

	[ClientRpc]
	private void bildStueckClientRpc(int start, byte[] stueck, ClientRpcParams rpcParams = default)
	{
		if (puffer != null && start >= 0 && start + stueck.Length <= puffer.Length)
		{
			Array.Copy(stueck, 0, puffer, start, stueck.Length);
		}
	}

	[ClientRpc]
	private void bildFertigClientRpc(ClientRpcParams rpcParams = default)
	{
		if (puffer == null)
		{
			return;
		}
		byte[] daten = puffer;
		puffer = null;
		if (textur == null)
		{
			textur = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			textur.wrapMode = TextureWrapMode.Clamp;
		}
		if (!textur.LoadImage(daten))
		{
			return;
		}
		if (netz != null)
		{
			UnityEngine.Object.Destroy(netz);
		}
		netz = KopfbildNetz.Erzeugen(empfangeneLage);
		filter.sharedMesh = netz;
		anzeige.material.mainTexture = textur;
		anzeigeAktualisieren();
	}

	private void anzeigeAktualisieren()
	{
		// Das eigene Gesicht sieht man in der Ich-Perspektive nicht. Bilder anderer lassen
		// sich in den Einstellungen abschalten.
		anzeige.enabled = textur != null && netz != null && !playermanager.istLokal && SettingsManager.fremdeKopfbilder;
	}
}

// Wo das Bild am Kopf liegt. Wird im Menue "Aussehen" eingestellt und mitgeschickt.
public struct KopfbildLage
{
	// Verschiebung nach oben oder unten (m).
	public float hoehe;

	// Faktor auf die Hoehe des Bildes.
	public float groesse;

	// Wie weit das Bild um den Kopf reicht (Grad, 180 = vordere Haelfte).
	public float breite;

	public const float HOEHE_MIN = -0.3f;

	public const float HOEHE_MAX = 0.3f;

	public const float GROESSE_MIN = 0.4f;

	public const float GROESSE_MAX = 1.4f;

	public const float BREITE_MIN = 60f;

	public const float BREITE_MAX = 240f;

	public static KopfbildLage Standard => new KopfbildLage { hoehe = 0f, groesse = 1f, breite = 180f };

	// Begrenzt auf erlaubte Werte, damit niemand riesige Flaechen verschicken kann.
	public static KopfbildLage Begrenzt(float hoehe, float groesse, float breite)
	{
		return new KopfbildLage
		{
			hoehe = Mathf.Clamp(hoehe, HOEHE_MIN, HOEHE_MAX),
			groesse = Mathf.Clamp(groesse, GROESSE_MIN, GROESSE_MAX),
			breite = Mathf.Clamp(breite, BREITE_MIN, BREITE_MAX),
		};
	}
}

// Netz fuer das Kopfbild: liegt auf der Kapsel (Radius 0,5, Kugelmitte des Kopfes auf 0,5 m),
// unter der Kugelmitte auf dem Zylinder, darueber auf der Halbkugel.
public static class KopfbildNetz
{
	private const float ABSTAND = 0.515f;

	// Standardlage: von knapp ueber der Koerpermitte bis fast zum Scheitel.
	private const float MITTE = 0.615f;

	private const float HALBE_HOEHE = 0.365f;

	private const int SPALTEN = 24;

	private const int ZEILEN = 16;

	public static Mesh Erzeugen(KopfbildLage lage)
	{
		float mitte = MITTE + lage.hoehe;
		float unten = mitte - HALBE_HOEHE * lage.groesse;
		float oben = Mathf.Min(mitte + HALBE_HOEHE * lage.groesse, 0.5f + ABSTAND - 0.01f);
		float halberWinkel = lage.breite * 0.5f;
		Mesh netz = new Mesh();
		netz.name = "Kopfbild";
		Vector3[] punkte = new Vector3[(SPALTEN + 1) * (ZEILEN + 1)];
		Vector2[] uv = new Vector2[punkte.Length];
		int[] dreiecke = new int[SPALTEN * ZEILEN * 6];
		for (int zeile = 0; zeile <= ZEILEN; zeile++)
		{
			float v = (float)zeile / ZEILEN;
			float y = Mathf.Lerp(unten, oben, v);
			float radius = y <= 0.5f ? ABSTAND : Mathf.Sqrt(Mathf.Max(0.0025f, ABSTAND * ABSTAND - (y - 0.5f) * (y - 0.5f)));
			for (int spalte = 0; spalte <= SPALTEN; spalte++)
			{
				float u = (float)spalte / SPALTEN;
				// Linker Bildrand auf der rechten Kopfseite: Wer den Spieler von vorne ansieht,
				// sieht das Bild dann richtig herum statt gespiegelt.
				float winkel = Mathf.Lerp(halberWinkel, -halberWinkel, u) * Mathf.Deg2Rad;
				int i = zeile * (SPALTEN + 1) + spalte;
				punkte[i] = new Vector3(Mathf.Sin(winkel) * radius, y, Mathf.Cos(winkel) * radius);
				uv[i] = new Vector2(u, v);
			}
		}
		int d = 0;
		for (int zeile = 0; zeile < ZEILEN; zeile++)
		{
			for (int spalte = 0; spalte < SPALTEN; spalte++)
			{
				int a = zeile * (SPALTEN + 1) + spalte;
				int b = a + 1;
				int c = a + SPALTEN + 1;
				int e = c + 1;
				// Nach aussen gewunden, damit man das Bild von vorne sieht.
				dreiecke[d++] = a;
				dreiecke[d++] = c;
				dreiecke[d++] = b;
				dreiecke[d++] = b;
				dreiecke[d++] = c;
				dreiecke[d++] = e;
			}
		}
		netz.vertices = punkte;
		netz.uv = uv;
		netz.triangles = dreiecke;
		netz.RecalculateNormals();
		netz.RecalculateBounds();
		return netz;
	}
}

// Das eigene Bild liegt als Datei im Benutzerordner des Spiels, eine pro Spielinstallation.
// Sonst teilten sich alle Instanzen auf einem PC (ParrelSync-Klone, mehrere Builds) dasselbe Bild.
public static class KopfbildSpeicher
{
	private static string kennung;

	// Kurze Kennung aus dem Ordner des Spiels. Der Klon von ParrelSync liegt in einem anderen Ordner.
	private static string Kennung
	{
		get
		{
			if (kennung == null)
			{
				uint summe = 2166136261;
				foreach (char zeichen in Application.dataPath)
				{
					summe = (summe ^ zeichen) * 16777619;
				}
				kennung = summe.ToString("x8");
			}
			return kennung;
		}
	}

	public static string Pfad => Path.Combine(Application.persistentDataPath, "kopfbild_" + Kennung + ".bild");

	private static string LagePfad => Path.Combine(Application.persistentDataPath, "kopfbild_" + Kennung + ".txt");

	public static byte[] Laden()
	{
		try
		{
			return File.Exists(Pfad) ? File.ReadAllBytes(Pfad) : null;
		}
		catch (Exception fehler)
		{
			Debug.LogWarning("Kopfbild nicht lesbar: " + fehler.Message);
			return null;
		}
	}

	public static void Speichern(byte[] daten)
	{
		File.WriteAllBytes(Pfad, daten);
	}

	public static void Loeschen()
	{
		if (File.Exists(Pfad))
		{
			File.Delete(Pfad);
		}
	}

	public static KopfbildLage LageLaden()
	{
		try
		{
			if (File.Exists(LagePfad))
			{
				string[] teile = File.ReadAllText(LagePfad).Split(';');
				return KopfbildLage.Begrenzt(
					float.Parse(teile[0], CultureInfo.InvariantCulture),
					float.Parse(teile[1], CultureInfo.InvariantCulture),
					float.Parse(teile[2], CultureInfo.InvariantCulture));
			}
		}
		catch (Exception fehler)
		{
			Debug.LogWarning("Lage des Kopfbilds nicht lesbar: " + fehler.Message);
		}
		return KopfbildLage.Standard;
	}

	public static void LageSpeichern(KopfbildLage lage)
	{
		File.WriteAllText(LagePfad, string.Format(CultureInfo.InvariantCulture, "{0};{1};{2}", lage.hoehe, lage.groesse, lage.breite));
	}
}
