using UnityEngine;

// Eigener Nebel fuer eine Map. Gilt, solange die Map aktiv ist, danach kommt der alte zurueck.
public class KartenAtmosphaere : MonoBehaviour
{
	public Color nebelFarbe = new Color(0.62f, 0.66f, 0.7f);

	public float nebelDichte = 0.006f;

	private bool vorherNebel;

	private Color vorherFarbe;

	private float vorherDichte;

	private FogMode vorherModus;

	private void OnEnable()
	{
		vorherNebel = RenderSettings.fog;
		vorherFarbe = RenderSettings.fogColor;
		vorherDichte = RenderSettings.fogDensity;
		vorherModus = RenderSettings.fogMode;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogColor = nebelFarbe;
		RenderSettings.fogDensity = nebelDichte;
	}

	private void OnDisable()
	{
		RenderSettings.fog = vorherNebel;
		RenderSettings.fogColor = vorherFarbe;
		RenderSettings.fogDensity = vorherDichte;
		RenderSettings.fogMode = vorherModus;
	}
}
