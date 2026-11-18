using UnityEngine;

// Sprungfeld: schleudert Spieler (und Bots) nach oben und in Blickrichtung des Feldes.
// Braucht einen BoxCollider als Trigger.
[RequireComponent(typeof(BoxCollider))]
public class Sprungfeld : MonoBehaviour
{
	// Geschwindigkeit nach oben und nach vorne (m/s). Mit der Schwerkraft des Spiels
	// (9,81 x 4) reichen 30 nach oben fuer etwa 11 m Hoehe.
	public float hoch = 30f;

	public float vor = 18f;

	private void OnTriggerEnter(Collider anderer)
	{
		playermovment bewegung = anderer.GetComponent<playermovment>();
		// Nur beim Besitzer, der bewegt die Figur. Fremde Figuren setzt das Netz.
		if (bewegung == null || !bewegung.IsOwner)
		{
			return;
		}
		Vector3 richtung = base.transform.forward;
		richtung.y = 0f;
		bewegung.schleudern(richtung.normalized * vor + Vector3.up * hoch);
	}
}
