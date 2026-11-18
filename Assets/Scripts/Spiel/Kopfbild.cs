using System;
using System.IO;
using Unity.Netcode;
using UnityEngine;

// Eigenes Bild auf der vorderen Haelfte des Kopfes. Der Besitzer schickt sein Bild beim
// Spawn in Stuecken an den Server, der verteilt es an alle (auch an spaeter Beitretende).
public class Kopfbild : NetworkBehaviour
{
	// Material mit "Unlit/Transparent" (Editor-Skript traegt es ein, damit der Shader im Build ist).
	public Material material;

	// Groesser darf ein Bild nicht sein (Bytes), sonst nimmt der Server es nicht an.
	public const int MAX_GROESSE = 96 * 1024;

	// Stueckgroesse fuer die Uebertragung, unter der Paketgrenze von Unity Transport (6144).
	private const int STUECK = 4000;

	private MeshRenderer anzeige;

	private Texture2D textur;

	private Playermanager playermanager;

	// Empfang in Stuecken.
	private byte[] puffer;

	// Auf dem Server: das zuletzt vollstaendig erhaltene Bild dieses Spielers.
	private byte[] serverBild;

	private void Awake()
	{
		playermanager = GetComponent<Playermanager>();
		GameObject gesicht = new GameObject("Kopfbild");
		gesicht.transform.SetParent(base.transform, false);
		gesicht.layer = base.gameObject.layer;
		gesicht.AddComponent<MeshFilter>().sharedMesh = KopfbildNetz.Holen();
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
			hochladen(KopfbildSpeicher.Laden());
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
		base.OnDestroy();
	}

	private void hochladen(byte[] daten)
	{
		if (daten == null || daten.Length == 0 || daten.Length > MAX_GROESSE)
		{
			return;
		}
		bildBeginnenServerRpc(daten.Length);
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
	private void bildBeginnenServerRpc(int laenge)
	{
		puffer = laenge > 0 && laenge <= MAX_GROESSE ? new byte[laenge] : null;
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
		puffer = null;
		verteilen(serverBild, null);
	}

	[ServerRpc(RequireOwnership = false)]
	private void bildAnfordernServerRpc(ServerRpcParams rpcParams = default)
	{
		if (serverBild != null)
		{
			verteilen(serverBild, new ulong[] { rpcParams.Receive.SenderClientId });
		}
	}

	// Schickt das Bild an alle (empfaenger null) oder nur an bestimmte Clients.
	private void verteilen(byte[] daten, ulong[] empfaenger)
	{
		ClientRpcParams ziel = default(ClientRpcParams);
		if (empfaenger != null)
		{
			ziel.Send = new ClientRpcSendParams { TargetClientIds = empfaenger };
		}
		bildBeginnenClientRpc(daten.Length, ziel);
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
	private void bildBeginnenClientRpc(int laenge, ClientRpcParams rpcParams = default)
	{
		puffer = laenge > 0 && laenge <= MAX_GROESSE ? new byte[laenge] : null;
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
		anzeige.material.mainTexture = textur;
		anzeigeAktualisieren();
	}

	private void anzeigeAktualisieren()
	{
		// Das eigene Gesicht sieht man in der Ich-Perspektive nicht. Bilder anderer lassen
		// sich in den Einstellungen abschalten.
		anzeige.enabled = textur != null && !playermanager.istLokal && SettingsManager.fremdeKopfbilder;
	}
}

// Netz fuer die vordere Kopfhaelfte: Kapsel-Kopf (Radius 0,5, Kugelmitte auf 0,5 m) von
// knapp unter der Augenhoehe bis oben, von links nach rechts ueber die Vorderseite.
public static class KopfbildNetz
{
	private const float ABSTAND = 0.515f;

	private const float UNTEN = 0.25f;

	private const float OBEN = 0.98f;

	private const int SPALTEN = 24;

	private const int ZEILEN = 16;

	private static Mesh netz;

	public static Mesh Holen()
	{
		if (netz != null)
		{
			return netz;
		}
		netz = new Mesh();
		netz.name = "Kopfbild";
		Vector3[] punkte = new Vector3[(SPALTEN + 1) * (ZEILEN + 1)];
		Vector2[] uv = new Vector2[punkte.Length];
		int[] dreiecke = new int[SPALTEN * ZEILEN * 6];
		for (int zeile = 0; zeile <= ZEILEN; zeile++)
		{
			float v = (float)zeile / ZEILEN;
			float hoehe = Mathf.Lerp(UNTEN, OBEN, v);
			// Unter der Kugelmitte ist die Kapsel ein Zylinder, darueber eine Halbkugel.
			float radius = hoehe <= 0.5f ? ABSTAND : Mathf.Sqrt(Mathf.Max(0.0025f, ABSTAND * ABSTAND - (hoehe - 0.5f) * (hoehe - 0.5f)));
			for (int spalte = 0; spalte <= SPALTEN; spalte++)
			{
				float u = (float)spalte / SPALTEN;
				// Linker Bildrand auf der rechten Kopfseite: Wer den Spieler von vorne ansieht,
				// sieht das Bild dann richtig herum statt gespiegelt.
				float winkel = Mathf.Lerp(90f, -90f, u) * Mathf.Deg2Rad;
				int i = zeile * (SPALTEN + 1) + spalte;
				punkte[i] = new Vector3(Mathf.Sin(winkel) * radius, hoehe, Mathf.Cos(winkel) * radius);
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

// Das eigene Bild liegt als Datei im Benutzerordner des Spiels.
public static class KopfbildSpeicher
{
	public static string Pfad => Path.Combine(Application.persistentDataPath, "kopfbild.png");

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
}
