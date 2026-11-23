using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Grashalme der Stufe "Ultra". Die Standorte legt das Editor-Skript als Punkte eines Netzes ab
// (Position, Bodennormale, Groesse in uv.x), gezeichnet wird mit DrawMeshInstanced in Feldern
// von ZELLE Metern. Nah voll dicht mit dem feinen Bueschel, weiter weg duenner und mit dem
// groben (weniger Halme). Die Felder sind gemischt, die ersten n liegen also gleichmaessig.
// Dazu die Trampelkarte: eine Draufsicht der Wiese, in die jedes Bild die Figuren eingedrueckt
// werden und die sich langsam erholt. Der Gras-Shader biegt die Halme danach.
public class GrasFeld : MonoBehaviour
{
	public Mesh punkte;

	public Mesh bueschel;

	public Mesh bueschelFern;

	public Material material;

	public Material trampelMaterial;

	// Zum Einstellen: Sichtweite (m), bis hier voll dicht (m), Dichte ganz hinten (Anteil).
	public float sichtweite = 45f;

	public float vollDicht = 14f;

	public float dichteHinten = 0.1f;

	// Sekunden, bis niedergetretenes Gras wieder steht.
	public float erholung = 5f;

	private const float ZELLE = 10f;

	private const int STAPEL = 1023;

	// Muss zur Laenge der Felder in den Shadern passen.
	private const int FIGUREN = 8;

	// Aufloesung der Trampelkarte: Bildpunkte pro Meter (hoechstens 1024 pro Seite).
	private const float PUNKTE_PRO_METER = 3f;

	private static readonly int FIGUREN_ID = Shader.PropertyToID("_ShootoutFiguren");
	private static readonly int ANZAHL_ID = Shader.PropertyToID("_ShootoutFigurenAnzahl");
	private static readonly int TRAMPEL_ID = Shader.PropertyToID("_ShootoutTrampel");
	private static readonly int BEREICH_ID = Shader.PropertyToID("_ShootoutTrampelBereich");
	private static readonly int AKTIV_ID = Shader.PropertyToID("_ShootoutTrampelAktiv");
	private static readonly int WEITE_ID = Shader.PropertyToID("_Weite");

	private readonly Vector4[] figuren = new Vector4[FIGUREN];
	private readonly Vector4[] richtungen = new Vector4[FIGUREN];

	private class Zelle
	{
		public Vector3 mitte;
		public int anzahl;
		public readonly List<Matrix4x4[]> stapel = new List<Matrix4x4[]>();
	}

	private List<Zelle> zellen;

	private RenderTexture trampelA;
	private RenderTexture trampelB;
	private Vector4 bereich;

	private void Update()
	{
		if (!Grafik.Ultra || punkte == null || bueschel == null || material == null)
		{
			return;
		}
		if (zellen == null)
		{
			aufteilen();
		}
		Transform beobachter = Grafik.Beobachter != null ? Grafik.Beobachter : (Camera.main != null ? Camera.main.transform : null);
		if (beobachter == null)
		{
			return;
		}
		Vector3 auge = beobachter.position;
		figurenSammeln(auge);
		trampeln();
		material.SetFloat(WEITE_ID, sichtweite);
		float weite = sichtweite + ZELLE;
		foreach (Zelle zelle in zellen)
		{
			float abstand = Vector3.Distance(zelle.mitte, auge);
			if (abstand > weite)
			{
				continue;
			}
			float t = Mathf.Clamp01((abstand - vollDicht) / Mathf.Max(1f, sichtweite - vollDicht));
			float anteil = Mathf.Lerp(dichteHinten, 1f, (1f - t) * (1f - t));
			int rest = Mathf.Max(1, Mathf.CeilToInt(zelle.anzahl * anteil));
			// Ab etwas hinter der vollen Dichte das grobe Bueschel (weniger Halme, kaum zu sehen)
			Mesh netz = abstand > vollDicht * 1.4f && bueschelFern != null ? bueschelFern : bueschel;
			foreach (Matrix4x4[] stapel in zelle.stapel)
			{
				if (rest <= 0)
				{
					break;
				}
				int zahl = Mathf.Min(rest, stapel.Length);
				Graphics.DrawMeshInstanced(netz, 0, material, stapel, zahl, null, ShadowCastingMode.Off, true, base.gameObject.layer);
				rest -= zahl;
			}
		}
	}

	// Fuesse und Laufrichtungen der naechsten Figuren (Spieler und Bots).
	private void figurenSammeln(Vector3 auge)
	{
		var liste = GrasDruck.Alle;
		var sortiert = new List<GrasDruck>(liste);
		if (sortiert.Count > FIGUREN)
		{
			sortiert.Sort((a, b) => (a.transform.position - auge).sqrMagnitude.CompareTo((b.transform.position - auge).sqrMagnitude));
		}
		int n = 0;
		for (int i = 0; i < sortiert.Count && n < FIGUREN; i++)
		{
			GrasDruck figur = sortiert[i];
			if (figur == null || !figur.amBoden)
			{
				continue;
			}
			Vector3 fuss = figur.transform.position + Vector3.down;
			figuren[n] = new Vector4(fuss.x, fuss.y, fuss.z, 0.75f);
			Vector3 tempo = figur.tempo / 10f;
			richtungen[n] = new Vector4(tempo.x, 0f, tempo.z, 0f);
			n++;
		}
		for (int i = n; i < FIGUREN; i++)
		{
			figuren[i] = new Vector4(0f, -10000f, 0f, 0f);
			richtungen[i] = Vector4.zero;
		}
		Shader.SetGlobalVectorArray(FIGUREN_ID, figuren);
		Shader.SetGlobalFloat(ANZAHL_ID, n);
	}

	private void trampeln()
	{
		if (trampelMaterial == null)
		{
			Shader.SetGlobalFloat(AKTIV_ID, 0f);
			return;
		}
		trampelMaterial.SetVector("_Bereich", bereich);
		trampelMaterial.SetFloat("_Erholung", Time.deltaTime / Mathf.Max(0.1f, erholung));
		trampelMaterial.SetVectorArray("_Figuren", figuren);
		trampelMaterial.SetVectorArray("_Richtungen", richtungen);
		trampelMaterial.SetFloat("_Anzahl", Shader.GetGlobalFloat(ANZAHL_ID));
		Graphics.Blit(trampelA, trampelB, trampelMaterial);
		RenderTexture tausch = trampelA;
		trampelA = trampelB;
		trampelB = tausch;
		Shader.SetGlobalTexture(TRAMPEL_ID, trampelA);
		Shader.SetGlobalVector(BEREICH_ID, bereich);
		Shader.SetGlobalFloat(AKTIV_ID, 1f);
	}

	private RenderTexture neueKarte(int breite, int hoehe)
	{
		var karte = new RenderTexture(breite, hoehe, 0, RenderTextureFormat.ARGBHalf);
		karte.wrapMode = TextureWrapMode.Clamp;
		karte.filterMode = FilterMode.Bilinear;
		karte.Create();
		// Startwert: nichts niedergedrueckt, Richtung 0
		RenderTexture vorher = RenderTexture.active;
		RenderTexture.active = karte;
		GL.Clear(false, true, new Color(0f, 0.5f, 0.5f, 1f));
		RenderTexture.active = vorher;
		return karte;
	}

	private void OnDestroy()
	{
		if (trampelA != null)
		{
			trampelA.Release();
			Destroy(trampelA);
		}
		if (trampelB != null)
		{
			trampelB.Release();
			Destroy(trampelB);
		}
	}

	private void aufteilen()
	{
		zellen = new List<Zelle>();
		Vector3[] orte = punkte.vertices;
		Vector3[] normalen = punkte.normals;
		Vector2[] groessen = punkte.uv;
		var gruppen = new Dictionary<Vector2Int, List<Matrix4x4>>();
		var zufall = new System.Random(17);
		Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
		Vector2 max = new Vector2(float.MinValue, float.MinValue);
		for (int i = 0; i < orte.Length; i++)
		{
			Vector3 welt = base.transform.TransformPoint(orte[i]);
			min = Vector2.Min(min, new Vector2(welt.x, welt.z));
			max = Vector2.Max(max, new Vector2(welt.x, welt.z));
			Vector3 oben = normalen.Length > i ? base.transform.TransformDirection(normalen[i]) : Vector3.up;
			float groesse = groessen.Length > i ? groessen[i].x : 1f;
			Quaternion drehung = Quaternion.FromToRotation(Vector3.up, oben) * Quaternion.Euler(0f, (float)zufall.NextDouble() * 360f, 0f);
			var schluessel = new Vector2Int(Mathf.FloorToInt(welt.x / ZELLE), Mathf.FloorToInt(welt.z / ZELLE));
			if (!gruppen.TryGetValue(schluessel, out List<Matrix4x4> liste))
			{
				liste = new List<Matrix4x4>();
				gruppen[schluessel] = liste;
			}
			liste.Add(Matrix4x4.TRS(welt, drehung, Vector3.one * groesse));
		}
		foreach (KeyValuePair<Vector2Int, List<Matrix4x4>> paar in gruppen)
		{
			List<Matrix4x4> liste = paar.Value;
			// Mischen: Die ersten n liegen dann ueber das ganze Feld verteilt (fuers Ausduennen).
			for (int i = liste.Count - 1; i > 0; i--)
			{
				int j = zufall.Next(i + 1);
				Matrix4x4 tausch = liste[i];
				liste[i] = liste[j];
				liste[j] = tausch;
			}
			float hoehe = 0f;
			foreach (Matrix4x4 m in liste)
			{
				hoehe += m.m13;
			}
			var zelle = new Zelle
			{
				mitte = new Vector3((paar.Key.x + 0.5f) * ZELLE, hoehe / liste.Count, (paar.Key.y + 0.5f) * ZELLE),
				anzahl = liste.Count,
			};
			for (int start = 0; start < liste.Count; start += STAPEL)
			{
				zelle.stapel.Add(liste.GetRange(start, Mathf.Min(STAPEL, liste.Count - start)).ToArray());
			}
			zellen.Add(zelle);
		}
		// Trampelkarte ueber die ganze Wiese
		min -= Vector2.one * 2f;
		max += Vector2.one * 2f;
		Vector2 groesseWelt = max - min;
		int breite = Mathf.Clamp(Mathf.CeilToInt(groesseWelt.x * PUNKTE_PRO_METER), 16, 1024);
		int tiefe = Mathf.Clamp(Mathf.CeilToInt(groesseWelt.y * PUNKTE_PRO_METER), 16, 1024);
		bereich = new Vector4(min.x, min.y, 1f / groesseWelt.x, 1f / groesseWelt.y);
		if (trampelMaterial != null && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
		{
			// Eigene Kopie, sonst aendert das Spiel im Editor das gespeicherte Material.
			trampelMaterial = new Material(trampelMaterial);
			trampelA = neueKarte(breite, tiefe);
			trampelB = neueKarte(breite, tiefe);
		}
		else
		{
			trampelMaterial = null;
		}
	}
}
