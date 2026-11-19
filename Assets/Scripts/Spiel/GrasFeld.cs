using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Grashalme der Stufe "Ultra". Die Standorte legt das Editor-Skript als Punkte eines Netzes ab
// (Position, Bodennormale, Groesse in uv.x), gezeichnet wird mit DrawMeshInstanced in Feldern
// von ZELLE Metern, nur in der Naehe der Kamera. Ohne Kollider, rein zum Ansehen.
public class GrasFeld : MonoBehaviour
{
	public Mesh punkte;

	public Mesh bueschel;

	public Material material;

	// Zum Einstellen: Sichtweite (m), muss zu _Weite im Material passen.
	public float sichtweite = 70f;

	private const float ZELLE = 16f;

	private const int STAPEL = 1023;

	private class Zelle
	{
		public Vector3 mitte;

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
		float weite = sichtweite + ZELLE;
		foreach (Zelle zelle in zellen)
		{
			if ((zelle.mitte - auge).sqrMagnitude > weite * weite)
			{
				continue;
			}
			foreach (Matrix4x4[] stapel in zelle.stapel)
			{
				Graphics.DrawMeshInstanced(bueschel, 0, material, stapel, stapel.Length, null, ShadowCastingMode.Off, true, base.gameObject.layer);
			}
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
			var zelle = new Zelle { mitte = new Vector3((paar.Key.x + 0.5f) * ZELLE, paar.Value[0].m13, (paar.Key.y + 0.5f) * ZELLE) };
			for (int start = 0; start < paar.Value.Count; start += STAPEL)
			{
				zelle.stapel.Add(paar.Value.GetRange(start, Mathf.Min(STAPEL, paar.Value.Count - start)).ToArray());
			}
			zellen.Add(zelle);
		}
	}
}
