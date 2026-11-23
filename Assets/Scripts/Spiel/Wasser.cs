using System.Collections.Generic;
using UnityEngine;

// Laesst eine Wasserflaeche wellen (Punkte des Netzes auf und ab). Nur Optik, ohne Kollision.
// Dazu eine Unterseite, damit man die Oberflaeche von unten sieht (sonst ist sie von unten
// unsichtbar), und das bessere Wasser der Grafikstufe "Hoch".
[RequireComponent(typeof(MeshFilter))]
public class Wasser : MonoBehaviour
{
	public float hoehe = 0.35f;

	public float wellenlaenge = 14f;

	public float tempo = 1.2f;

	// Material der Grafikstufe "Hoch" (eigener Shader) und der Unterseite, traegt das Editor-Skript ein.
	public Material hochMaterial;

	public Material unterseiteMaterial;

	// Wo sich die Punkte heben (lokal: x min, z min, x max, z max). Draussen ist das Meer flach,
	// nur der Shader kraeuselt es. Leer (0) heisst: die ganze Flaeche.
	public Vector4 wellenBereich;

	// Indizes der Punkte, die sich bewegen (die anderen bleiben, das spart Rechenzeit).
	private int[] bewegte;

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

	private bool imWellenBereich(float x, float z, float rand)
	{
		if (wellenBereich == Vector4.zero)
		{
			return true;
		}
		return x >= wellenBereich.x - rand && x <= wellenBereich.z + rand && z >= wellenBereich.y - rand && z <= wellenBereich.w + rand;
	}

	private void Start()
	{
		// Eigene Kopie, damit das gespeicherte Netz unveraendert bleibt.
		MeshFilter filter = GetComponent<MeshFilter>();
		netz = Instantiate(filter.sharedMesh);
		filter.mesh = netz;
		ruhe = netz.vertices;
		punkte = (Vector3[])ruhe.Clone();
		flaeche = netz.bounds;
		var liste = new List<int>();
		for (int i = 0; i < ruhe.Length; i++)
		{
			if (imWellenBereich(ruhe[i].x, ruhe[i].z, 1f))
			{
				liste.Add(i);
			}
		}
		bewegte = liste.ToArray();

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
		WasserAusschnitt.ShaderSetzen();
		foreach (int i in bewegte)
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
		// Zum Rand des Wellenbereichs hin flacher, draussen ist das Meer ruhig.
		Vector4 b = wellenBereich == Vector4.zero ? new Vector4(flaeche.min.x, flaeche.min.z, flaeche.max.x, flaeche.max.z) : wellenBereich;
		float rand = Mathf.Clamp01(Mathf.Min(Mathf.Min(x - b.x, b.z - x), Mathf.Min(z - b.y, b.w - z)) / 30f);
		return rand * (Mathf.Sin(z * k + t) * hoehe + Mathf.Sin((x * 0.7f + z * 0.3f) * k * 1.7f + t * 1.3f) * hoehe * 0.4f);
	}

	// Liegt der Punkt unter dieser Wasseroberflaeche?
	public bool unterWasser(Vector3 weltPunkt)
	{
		// Im Boot steht kein Wasser
		if (WasserAusschnitt.Enthaelt(weltPunkt))
		{
			return false;
		}
		Vector3 lokal = base.transform.InverseTransformPoint(weltPunkt);
		if (lokal.x < flaeche.min.x || lokal.x > flaeche.max.x || lokal.z < flaeche.min.z || lokal.z > flaeche.max.z)
		{
			return false;
		}
		return lokal.y < welle(lokal.x, lokal.z);
	}

	// Hoehe der Oberflaeche (Welt) ueber einem Punkt, falls der Punkt ueber oder unter dem Wasser liegt.
	public bool oberflaeche(Vector3 weltPunkt, out float hoeheWelt)
	{
		Vector3 lokal = base.transform.InverseTransformPoint(weltPunkt);
		hoeheWelt = 0f;
		if (WasserAusschnitt.Enthaelt(weltPunkt))
		{
			return false;
		}
		if (lokal.x < flaeche.min.x || lokal.x > flaeche.max.x || lokal.z < flaeche.min.z || lokal.z > flaeche.max.z)
		{
			return false;
		}
		hoeheWelt = base.transform.TransformPoint(new Vector3(lokal.x, welle(lokal.x, lokal.z), lokal.z)).y;
		return true;
	}

	// Wo die Strecke von a nach b die Wasseroberflaeche schneidet (Schuss ins Wasser).
	// Die Wellen zaehlen dabei nicht, sie sind nur eine Handbreit hoch.
	public bool schnitt(Vector3 a, Vector3 b, out Vector3 punkt)
	{
		punkt = Vector3.zero;
		float ebene = base.transform.position.y;
		if ((a.y - ebene) * (b.y - ebene) >= 0f)
		{
			return false;
		}
		punkt = Vector3.Lerp(a, b, (a.y - ebene) / (a.y - b.y));
		if (!oberflaeche(punkt, out float hoeheWelt))
		{
			return false;
		}
		punkt.y = hoeheWelt;
		return true;
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
