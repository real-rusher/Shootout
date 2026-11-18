// Was der Host beim Erstellen der Lobby gewaehlt hat. Gilt nur auf dem Host, der
// Server liest es beim Start der Runde (Runde.OnNetworkSpawn) und verteilt den
// Teammodus ueber eine NetworkVariable an alle.
public static class LobbyWahl
{
	public static bool teams;

	public static bool botsAuffuellen;

	// So viele Spieler soll eine Runde mit Bots mindestens haben (echte Spieler zaehlen mit).
	public static int spielerMitBots = 6;

	public const int MIN_SPIELER_MIT_BOTS = 2;

	public const int MAX_SPIELER_MIT_BOTS = 10;
}
