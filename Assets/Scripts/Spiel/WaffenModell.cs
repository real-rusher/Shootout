using UnityEngine;

// Waffenmodelle der Stufe "Ultra" statt des Laserpointers. Die Muendung jedes Modells liegt an
// der Spitze des Laserpointers, dort beginnt auch der Strahl. Gezeigt wird die Waffe, die die
// Figur zuletzt gewaehlt (eigene) oder abgefeuert hat (andere, das Netz meldet die Waffe nur
// beim Schuss).
public class WaffenModell : MonoBehaviour
{
	// Index = Waffen-Nummer (Waffen.GEWEHR, SMG, SNIPER). Traegt das Editor-Skript ein.
	public GameObject[] modelle = new GameObject[0];

	// Das Rohr des Laserpointers (Modell aus Player.glb).
	public GameObject pointerModell;

	private playershooting schiessen;

	private Playermanager figur;

	private void Awake()
	{
		schiessen = GetComponent<playershooting>();
		figur = GetComponent<Playermanager>();
	}

	private void LateUpdate()
	{
		bool ultra = Grafik.Ultra && modelle.Length > 0;
		// Wie der Laserpointer: Nach dem Tod und vor dem Spawn versteckt (Playermanager).
		bool sichtbar = figur == null || figur.laserPointer == null || figur.laserPointer.activeSelf;
		int waffe = schiessen != null ? schiessen.gezeigteWaffe : 0;
		for (int i = 0; i < modelle.Length; i++)
		{
			bool zeigen = ultra && sichtbar && i == waffe;
			if (modelle[i] != null && modelle[i].activeSelf != zeigen)
			{
				modelle[i].SetActive(zeigen);
			}
		}
		if (pointerModell != null && pointerModell.activeSelf == ultra)
		{
			pointerModell.SetActive(!ultra);
		}
	}
}
