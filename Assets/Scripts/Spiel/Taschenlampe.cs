using UnityEngine;

// Taschenlampe an der Kamera jeder Figur (Spieler und Bots). Leuchtet nur auf Maps, deren
// KartenRegeln das wollen (Tunnel), und nicht, solange die Figur tot ist. Die Richtung kommt
// von der Kamera, deren Neigung das Netz schon uebertraegt: Man sieht auch die Lampen der anderen.
public class Taschenlampe : MonoBehaviour
{
	// Zum Einstellen
	private const float WINKEL = 55f;

	private const float REICHWEITE = 40f;

	private const float STAERKE = 2.6f;

	private static readonly Color FARBE = new Color(1f, 0.93f, 0.8f);

	// Etwas vor der Kamera, damit die eigene Waffe nicht im Licht steht.
	private static readonly Vector3 VERSATZ = new Vector3(0f, -0.05f, 0.35f);

	public Transform halter;

	public Playermanager figur;

	private Light licht;

	private void LateUpdate()
	{
		KartenRegeln regeln = KartenRegeln.Aktuelle;
		bool an = regeln != null && regeln.taschenlampe && figur != null && figur.IsSpawned && !figur.isDead.Value;
		if (an && licht == null)
		{
			erzeugen();
		}
		if (licht != null)
		{
			licht.enabled = an;
		}
	}

	private void erzeugen()
	{
		GameObject go = new GameObject("Taschenlampe");
		go.transform.SetParent(halter, false);
		go.transform.localPosition = VERSATZ;
		licht = go.AddComponent<Light>();
		licht.type = LightType.Spot;
		licht.spotAngle = WINKEL;
		licht.range = REICHWEITE;
		licht.intensity = STAERKE;
		licht.color = FARBE;
		if (figur.istLokal)
		{
			// Die eigene Lampe immer voll berechnet und mit Schatten. Die eigene Figur liegt auf
			// "Ignore Raycast" (Layer 2) und bleibt unbeleuchtet, sonst wirft die Waffe einen
			// grossen Schatten ins Bild.
			licht.renderMode = LightRenderMode.ForcePixel;
			licht.shadows = LightShadows.Soft;
			licht.cullingMask = ~(1 << 2);
		}
		else
		{
			// Lampen der anderen ohne Schatten, das spart Rechenzeit.
			licht.shadows = LightShadows.None;
		}
	}
}
