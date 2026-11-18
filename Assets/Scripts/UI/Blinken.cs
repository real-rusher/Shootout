using TMPro;
using UnityEngine;

// Laesst einen Text sanft pulsieren. Ersatz fuer die Animation-Komponenten des
// Originals ("press escape to continue" usw.), deren Clips nicht erhalten sind.
[RequireComponent(typeof(TMP_Text))]
public class Blinken : MonoBehaviour
{
	public float geschwindigkeit = 2.5f;

	public float minimum = 0.35f;

	private TMP_Text text;

	private void Awake()
	{
		text = GetComponent<TMP_Text>();
	}

	private void Update()
	{
		// unscaledTime, damit es auch bei Pause weiterlaeuft.
		float wert = Mathf.Lerp(minimum, 1f, (Mathf.Sin(Time.unscaledTime * geschwindigkeit) + 1f) * 0.5f);
		Color farbe = text.color;
		farbe.a = wert;
		text.color = farbe;
	}
}
