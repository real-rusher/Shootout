using UnityEngine;

// Laesst eine Wasserflaeche wellen (Punkte des Netzes auf und ab). Nur Optik, ohne Kollision.
[RequireComponent(typeof(MeshFilter))]
public class Wasser : MonoBehaviour
{
	public float hoehe = 0.25f;

	public float wellenlaenge = 14f;

	public float tempo = 1.2f;

	private Mesh netz;

	private Vector3[] ruhe;

	private Vector3[] punkte;

	private void Start()
	{
		// Eigene Kopie, damit das gespeicherte Netz unveraendert bleibt.
		MeshFilter filter = GetComponent<MeshFilter>();
		netz = Instantiate(filter.sharedMesh);
		filter.mesh = netz;
		ruhe = netz.vertices;
		punkte = new Vector3[ruhe.Length];
	}

	private void Update()
	{
		float t = Time.time * tempo;
		float k = 2f * Mathf.PI / wellenlaenge;
		for (int i = 0; i < ruhe.Length; i++)
		{
			Vector3 p = ruhe[i];
			p.y += Mathf.Sin(p.z * k + t) * hoehe + Mathf.Sin((p.x * 0.7f + p.z * 0.3f) * k * 1.7f + t * 1.3f) * hoehe * 0.4f;
			punkte[i] = p;
		}
		netz.vertices = punkte;
		netz.RecalculateNormals();
	}

	private void OnDestroy()
	{
		if (netz != null)
		{
			Destroy(netz);
		}
	}
}
