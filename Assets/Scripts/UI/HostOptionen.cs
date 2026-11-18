using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Optionen beim Erstellen einer Lobby (Teams, Bots). Gibt es im LAN- und im Online-Fenster,
// beide schreiben in LobbyWahl. Die Map-Knoepfe starten dann wie bisher die Runde.
public class HostOptionen : MonoBehaviour
{
	public Toggle teamsSchalter;

	public Toggle botsSchalter;

	public Slider spielerRegler;

	public TMP_Text spielerText;

	private void OnEnable()
	{
		// Beide Fenster zeigen dieselbe Wahl.
		teamsSchalter.SetIsOnWithoutNotify(LobbyWahl.teams);
		botsSchalter.SetIsOnWithoutNotify(LobbyWahl.botsAuffuellen);
		spielerRegler.minValue = LobbyWahl.MIN_SPIELER_MIT_BOTS;
		spielerRegler.maxValue = LobbyWahl.MAX_SPIELER_MIT_BOTS;
		spielerRegler.wholeNumbers = true;
		spielerRegler.SetValueWithoutNotify(LobbyWahl.spielerMitBots);
		anzeigen();
	}

	private void Start()
	{
		teamsSchalter.onValueChanged.AddListener(wert => LobbyWahl.teams = wert);
		botsSchalter.onValueChanged.AddListener(wert => { LobbyWahl.botsAuffuellen = wert; anzeigen(); });
		spielerRegler.onValueChanged.AddListener(wert => { LobbyWahl.spielerMitBots = Mathf.RoundToInt(wert); anzeigen(); });
	}

	private void anzeigen()
	{
		spielerRegler.interactable = LobbyWahl.botsAuffuellen;
		if (spielerText != null)
		{
			spielerText.text = "Mit Bots auffüllen auf " + LobbyWahl.spielerMitBots + " Spieler";
		}
	}
}
