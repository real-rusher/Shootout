using UnityEngine;

// Laesst das Licht eines Feuers unruhig flackern.
[RequireComponent(typeof(Light))]
public class Flackern : MonoBehaviour
{
	public float staerke = 0.35f;

	private Light licht;

	private float grund;

	private float versatz;

	private void Awake()
	{
		licht = GetComponent<Light>();
		grund = licht.intensity;
		versatz = Random.value * 100f;
	}

	private void Update()
	{
		float rauschen = Mathf.PerlinNoise(Time.time * 6f + versatz, versatz);
		licht.intensity = grund * (1f - staerke + rauschen * staerke * 2f);
	}
}
