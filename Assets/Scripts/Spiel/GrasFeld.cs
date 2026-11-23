using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Grashalme der Stufe "Ultra". Die Standorte legt das Editor-Skript als Punkte eines Netzes ab
// (Position, Bodennormale, Groesse in uv.x), gezeichnet wird mit DrawMeshInstanced in Feldern
// von ZELLE Metern. Nah voll dicht, nach hinten immer duenner (die Felder sind gemischt, die
// ersten n Bueschel liegen also gleichmaessig verteilt). Ohne Kollider, rein zum Ansehen.
// Gibt ausserdem die Figuren an den Shader weiter: Wo jemand laeuft, biegt sich das Gras weg.
public class GrasFeld : MonoBehaviour
{
	public Mesh punkte;

	public Mesh bueschel;

	public Material material;

	// Zum Einstellen: Sichtweite (m), bis hier voll dicht (m), Dichte ganz hinten (Anteil).
	public float sichtweite = 70f;

	public float vollDicht = 22f;

	public float dichteHinten = 0.1f;

	private const float ZELLE = 12f;

	private const int STAPEL = 1023;

	// Muss zur Laenge von _ShootoutFiguren im Gras-Shader passen.
	private const int FIGUREN = 8;

	private static readonly int FIGUREN_ID = Shader.PropertyToID("_ShootoutFiguren");

	private static readonly int ANZAHL_ID = Shader.PropertyToID("_ShootoutFigurenAnzahl");

	private static readonly int WEITE_ID = Shader.PropertyToID("_Weite");

	private readonly Vector4[] figuren = new Vector4[FIGUREN];

	private class Zelle
	{
		public Vector3 mitte;

		public int anzahl;

		public readonly List<Matrix4x4[]> stapel = new List<Matrix4x4[]>();
	}

	private List<Zelle> zellen;

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
		figurenWeitergeben(auge);
		material.SetFloat(WEITE_ID, sichtweite);
		float weite = sichtweite + ZELLE;
		foreach (Zelle zelle in zellen)
		{
			float abstand = Vector3.Distance(zelle.mitte, auge);
			if (abstand > weite)
			{
				continue;
			}
			// Wie viele Bueschel dieses Feld zeigt: nah alle, weiter weg weniger (quadratisch).
			float t = Mathf.Clamp01((abstand - vollDicht) / Mathf.Max(1f, sichtweite - vollDicht));
			float anteil = Mathf.Lerp(1f, dichteHinten, Mathf.Sqrt(t));
			int rest = Mathf.Max(1, Mathf.CeilToInt(zelle.anzahl * anteil));
			foreach (Matrix4x4[] stapel in zelle.stapel)
			{
				if (rest <= 0)
				{
					break;
				}
				int zahl = Mathf.Min(rest, stapel.Length);
				Graphics.DrawMeshInstanced(bueschel, 0, material, stapel, zahl, null, ShadowCastingMode.Off, true, base.gameObject.layer);
				rest -= zahl;
			}
		}
	}

	// Fuesse der naechsten Figuren (Spieler und Bots), w = Radius.
	private void figurenWeitergeben(Vector3 auge)
	{
		int n = 0;
		var liste = GrasDruck.Alle;
		// Mehr als 8 Figuren: die naechsten zur Kamera (einfaches Auswaehlen, die Listen sind klein).
		var sortiert = liste.Count > FIGUREN ? new List<Transform>(liste) : liste;
		if (liste.Count > FIGUREN)
		{
			sortiert.Sort((a, b) => (a.position - auge).sqrMagnitude.CompareTo((b.position - auge).sqrMagnitude));
		}
		for (int i = 0; i < sortiert.Count && n < FIGUREN; i++)
		{
			if (sortiert[i] == null)
			{
				continue;
			}
			Vector3 fuss = sortiert[i].position + Vector3.down;
			figuren[n++] = new Vector4(fuss.x, fuss.y, fuss.z, 1.1f);
		}
		for (int i = n; i < FIGUREN; i++)
		{
			figuren[i] = new Vector4(0f, -10000f, 0f, 0f);
		}
		Shader.SetGlobalVectorArray(FIGUREN_ID, figuren);
		Shader.SetGlobalFloat(ANZAHL_ID, n);
	}

	private void aufteilen()
	{
		zellen = new List<Zelle>();
		Vector3[] orte = punkte.vertices;
		Vector3[] normalen = punkte.normals;
		Vector2[] groessen = punkte.uv;
		var gruppen = new Dictionary<Vector2Int, List<Matrix4x4>>();
		var zufall = new System.Random(17);
		for (int i = 0; i < orte.Length; i++)
		{
			Vector3 welt = base.transform.TransformPoint(orte[i]);
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
	}
}
