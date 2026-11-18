using UnityEngine;

// Laesst das Licht des Blitzes schnell ausgehen.
public class Verblassen : MonoBehaviour
{
	public float dauer = 0.25f;

	private Light licht;

	private float start;

	private float anfang;

	private void Awake()
	{
		licht = GetComponent<Light>();
		anfang = licht.intensity;
		start = Time.time;
	}

	private void Update()
	{
		licht.intensity = Mathf.Lerp(anfang, 0f, (Time.time - start) / dauer);
	}
}
