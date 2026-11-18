using UnityEngine;

// Unter Wasser: blaugruener Schleier, dichter Nebel, gedaempfter Ton. Haengt an der Kamera
// des eigenen Spielers (playerlooking legt ihn an) und prueft jedes Bild die aktive Kamera.
public class UnterwasserSicht : MonoBehaviour
{
	// Zum Einstellen
	private static readonly Color NEBEL_FARBE = new Color(0.04f, 0.22f, 0.27f);

	private const float NEBEL_DICHTE = 0.09f;

	private const float TON_GRENZE = 700f;

	public playerlooking blick;

	private bool unten;

	private bool vorherNebel;

	private Color vorherFarbe;

	private float vorherDichte;

	private FogMode vorherModus;

	private AudioLowPassFilter daempfer;

	private void Awake()
	{
		daempfer = base.gameObject.AddComponent<AudioLowPassFilter>();
		daempfer.cutoffFrequency = TON_GRENZE;
		daempfer.enabled = false;
	}

	private void LateUpdate()
	{
		Vector3 auge = blick != null ? blick.aktiveKameraPosition() : base.transform.position;
		bool jetzt = false;
		foreach (Wasser wasser in Wasser.Alle)
		{
			if (wasser.unterWasser(auge))
			{
				jetzt = true;
				break;
			}
		}
		if (jetzt != unten)
		{
			umschalten(jetzt);
		}
	}

	private void umschalten(bool rein)
	{
		unten = rein;
		if (rein)
		{
			vorherNebel = RenderSettings.fog;
			vorherFarbe = RenderSettings.fogColor;
			vorherDichte = RenderSettings.fogDensity;
			vorherModus = RenderSettings.fogMode;
			RenderSettings.fog = true;
			RenderSettings.fogMode = FogMode.Exponential;
			RenderSettings.fogColor = NEBEL_FARBE;
			RenderSettings.fogDensity = NEBEL_DICHTE;
		}
		else
		{
			RenderSettings.fog = vorherNebel;
			RenderSettings.fogColor = vorherFarbe;
			RenderSettings.fogDensity = vorherDichte;
			RenderSettings.fogMode = vorherModus;
			// Ist die Map schon weg (Spiel verlassen), gehoert ihr Nebel nicht ins Menue.
			if (KartenAtmosphaere.Aktuelle == null)
			{
				RenderSettings.fog = false;
			}
		}
		daempfer.enabled = rein;
		if (UIGameManager.Instance != null)
		{
			UIGameManager.Instance.zeigeUnterwasser(rein);
		}
	}

	private void OnDisable()
	{
		if (unten)
		{
			umschalten(false);
		}
	}
}
