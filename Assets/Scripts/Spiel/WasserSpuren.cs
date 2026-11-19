using UnityEngine;

// Spritzer, wenn eine Figur ins Wasser springt oder hindurch laeuft (Stufe "Ultra"). Laeuft auf
// jedem Rechner fuer jede Figur, die Geschwindigkeit kommt aus der Bewegung zwischen zwei Bildern.
public class WasserSpuren : MonoBehaviour
{
	// Fuesse und Kopf relativ zur Figurmitte (CharacterController: Hoehe 2, Mitte 0).
	private const float FUSS = -1f;

	private const float KOPF = 0.9f;

	private Vector3 vorher;

	private bool warImWasser;

	private float naechsteWelle;

	private void OnEnable()
	{
		vorher = base.transform.position;
	}

	private void LateUpdate()
	{
		Vector3 hier = base.transform.position;
		Vector3 tempo = (hier - vorher) / Mathf.Max(Time.deltaTime, 0.001f);
		vorher = hier;
		if (!Grafik.Ultra || Wasser.Alle.Count == 0 || tempo.sqrMagnitude > 2500f)
		{
			// Sehr schnell heisst: neu erschienen (Teleport), kein Spritzer.
			warImWasser = false;
			return;
		}
		bool drin = false;
		float flaeche = 0f;
		foreach (Wasser wasser in Wasser.Alle)
		{
			if (wasser.oberflaeche(hier, out flaeche) && hier.y + FUSS < flaeche && hier.y + KOPF > flaeche - 0.6f)
			{
				drin = true;
				break;
			}
		}
		if (drin)
		{
			Vector3 ort = new Vector3(hier.x, flaeche, hier.z);
			if (!warImWasser && tempo.y < -4f)
			{
				UltraEffekte.Spritzer(ort, Mathf.Clamp(-tempo.y / 8f, 1f, 3f));
			}
			float seitlich = new Vector2(tempo.x, tempo.z).magnitude;
			if (seitlich > 1.5f && Time.time >= naechsteWelle)
			{
				naechsteWelle = Time.time + 0.11f;
				UltraEffekte.Welle(ort, new Vector3(tempo.x, 0f, tempo.z));
			}
		}
		warImWasser = drin;
	}
}
