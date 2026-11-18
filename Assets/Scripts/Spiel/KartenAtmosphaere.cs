using UnityEngine;

// Eigene Stimmung fuer eine Map: Nebel, Umgebungslicht, Staerke der Sonnen. Gilt, solange die
// Map aktiv ist, danach kommt der alte Zustand zurueck.
public class KartenAtmosphaere : MonoBehaviour
{
	public static KartenAtmosphaere Aktuelle { get; private set; }

	public Color nebelFarbe = new Color(0.62f, 0.66f, 0.7f);

	public float nebelDichte = 0.006f;

	// Faktor auf das Umgebungslicht (1 = unveraendert). Unter der Erde dunkler.
	public float umgebungsFaktor = 1f;

	// Faktor auf die Sonnen der Szene (0 = aus). Im Tunnel kommt sonst Sonnenlicht durch die
	// Decke, weil nicht jede Sonne Schatten wirft.
	public float sonnenFaktor = 1f;

	private bool vorherNebel;

	private Color vorherFarbe;

	private float vorherDichte;

	private FogMode vorherModus;

	private float vorherUmgebung;

	private Light[] sonnen;

	private float[] sonnenStaerke;

	private void OnEnable()
	{
		Aktuelle = this;
		vorherNebel = RenderSettings.fog;
		vorherFarbe = RenderSettings.fogColor;
		vorherDichte = RenderSettings.fogDensity;
		vorherModus = RenderSettings.fogMode;
		vorherUmgebung = RenderSettings.ambientIntensity;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogColor = nebelFarbe;
		RenderSettings.fogDensity = nebelDichte;
		RenderSettings.ambientIntensity = vorherUmgebung * umgebungsFaktor;
		if (sonnenFaktor != 1f)
		{
			sonnen = Object.FindObjectsOfType<Light>();
			sonnenStaerke = new float[sonnen.Length];
			for (int i = 0; i < sonnen.Length; i++)
			{
				sonnenStaerke[i] = sonnen[i].intensity;
				// Nur die Sonnen der Szene, nicht die Lampen der Map selbst.
				if (sonnen[i].type == LightType.Directional && !sonnen[i].transform.IsChildOf(base.transform))
				{
					sonnen[i].intensity *= sonnenFaktor;
				}
			}
		}
	}

	private void OnDisable()
	{
		if (Aktuelle == this)
		{
			Aktuelle = null;
		}
		RenderSettings.fog = vorherNebel;
		RenderSettings.fogColor = vorherFarbe;
		RenderSettings.fogDensity = vorherDichte;
		RenderSettings.fogMode = vorherModus;
		RenderSettings.ambientIntensity = vorherUmgebung;
		if (sonnen != null)
		{
			for (int i = 0; i < sonnen.Length; i++)
			{
				if (sonnen[i] != null)
				{
					sonnen[i].intensity = sonnenStaerke[i];
				}
			}
			sonnen = null;
		}
	}
}
