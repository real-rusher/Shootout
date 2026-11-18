using System.Collections.Generic;
using UnityEngine;

// Laesst eine Wasserflaeche wellen (Punkte des Netzes auf und ab). Nur Optik, ohne Kollision.
// Dazu eine Unterseite, damit man die Oberflaeche von unten sieht (sonst ist sie von unten
// unsichtbar), und das bessere Wasser der Grafikstufe "Hoch".
[RequireComponent(typeof(MeshFilter))]
public class Wasser : MonoBehaviour
{
	public float hoehe = 0.25f;

	public float wellenlaenge = 14f;

	public float tempo = 1.2f;

	// Material der Grafikstufe "Hoch" (eigener Shader) und der Unterseite, traegt das Editor-Skript ein.
	public Material hochMaterial;

	public Material unterseiteMaterial;

	// Alle aktiven Wasserflaechen, fuer die Unterwassersicht.
	public static readonly List<Wasser> Alle = new List<Wasser>();

	private Mesh netz;

	private Mesh unterseite;

	private Vector3[] ruhe;

	private Vector3[] punkte;

	private Material normalMaterial;

	private MeshRenderer anzeige;

	private Bounds flaeche;

	private void Awake()
	{
		anzeige = GetComponent<MeshRenderer>();
		normalMaterial = anzeige.sharedMaterial;
	}

	private void OnEnable()
	{
		Alle.Add(this);
		Grafik.Geaendert += materialWaehlen;
		materialWaehlen();
	}

	private void OnDisable()
	{
		Alle.Remove(this);
		Grafik.Geaendert -= materialWaehlen;
	}

	private void Start()
	{
		// Eigene Kopie, damit das gespeicherte Netz unveraendert bleibt.
		MeshFilter filter = GetComponent<MeshFilter>();
		netz = Instantiate(filter.sharedMesh);
		filter.mesh = netz;
		ruhe = netz.vertices;
		punkte = new Vector3[ruhe.Length];
		flaeche = netz.bounds;

		// Unterseite: gleiche Punkte, Dreiecke umgedreht.
		int[] dreiecke = netz.triangles;
		for (int i = 0; i < dreiecke.Length; i += 3)
		{
			int tausch = dreiecke[i + 1];
			dreiecke[i + 1] = dreiecke[i + 2];
			dreiecke[i + 2] = tausch;
		}
		unterseite = new Mesh();
		unterseite.vertices = ruhe;
		unterseite.triangles = dreiecke;
		unterseite.bounds = flaeche;
		GameObject unten = new GameObject("Unterseite");
		unten.transform.SetParent(base.transform, false);
		unten.AddComponent<MeshFilter>().sharedMesh = unterseite;
		MeshRenderer untenAnzeige = unten.AddComponent<MeshRenderer>();
		untenAnzeige.sharedMaterial = unterseiteMaterial != null ? unterseiteMaterial : normalMaterial;
		untenAnzeige.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
	}

	private void materialWaehlen()
	{
		if (anzeige != null)
		{
			anzeige.sharedMaterial = Grafik.Hoch && hochMaterial != null ? hochMaterial : normalMaterial;
		}
	}

	private void Update()
	{
		for (int i = 0; i < ruhe.Length; i++)
		{
			Vector3 p = ruhe[i];
			p.y += welle(p.x, p.z);
			punkte[i] = p;
		}
		netz.vertices = punkte;
		netz.RecalculateNormals();
		unterseite.vertices = punkte;
		unterseite.RecalculateNormals();
	}

	// Hoehe der Welle an einem Punkt (lokale Koordinaten).
	private float welle(float x, float z)
	{
		float t = Time.time * tempo;
		float k = 2f * Mathf.PI / wellenlaenge;
		return Mathf.Sin(z * k + t) * hoehe + Mathf.Sin((x * 0.7f + z * 0.3f) * k * 1.7f + t * 1.3f) * hoehe * 0.4f;
	}

	// Liegt der Punkt unter dieser Wasseroberflaeche?
	public bool unterWasser(Vector3 weltPunkt)
	{
		Vector3 lokal = base.transform.InverseTransformPoint(weltPunkt);
		if (lokal.x < flaeche.min.x || lokal.x > flaeche.max.x || lokal.z < flaeche.min.z || lokal.z > flaeche.max.z)
		{
			return false;
		}
		return lokal.y < welle(lokal.x, lokal.z);
	}

	private void OnDestroy()
	{
		if (netz != null)
		{
			Destroy(netz);
		}
		if (unterseite != null)
		{
			Destroy(unterseite);
		}
	}
}
