using UnityEngine;

// Tauscht einfache gegen aufwendige Teile, je nach Grafikstufe "Ultra" (z. B. Kugel-Baum gegen
// Baum aus Blaettern). Teile mit Kollider bleiben aktiv, nur ihre Anzeige geht aus: Was man
// treffen oder worauf man stehen kann, muss fuer alle Spieler gleich sein, egal welche Grafik.
public class UltraSchalter : MonoBehaviour
{
	public GameObject[] einfach = new GameObject[0];

	public GameObject[] ultra = new GameObject[0];

	private void OnEnable()
	{
		Grafik.Geaendert += anwenden;
		anwenden();
	}

	private void OnDisable()
	{
		Grafik.Geaendert -= anwenden;
	}

	private void anwenden()
	{
		bool an = Grafik.Ultra;
		foreach (GameObject teil in einfach)
		{
			if (teil == null)
			{
				continue;
			}
			if (teil.GetComponentInChildren<Collider>(true) != null)
			{
				foreach (Renderer anzeige in teil.GetComponentsInChildren<Renderer>(true))
				{
					anzeige.enabled = !an;
				}
			}
			else
			{
				teil.SetActive(!an);
			}
		}
		foreach (GameObject teil in ultra)
		{
			if (teil != null)
			{
				teil.SetActive(an);
			}
		}
	}
}
