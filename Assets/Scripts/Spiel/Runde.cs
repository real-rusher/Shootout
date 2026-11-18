using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Regeln der laufenden Runde: Teams, Teampunkte, Bots. Liegt am Game Manager (Szenenobjekt
// mit NetworkObject). Entscheidungen trifft der Server, die Clients lesen die NetworkVariables.
public class Runde : NetworkBehaviour
{
	public static Runde Instance;

	public NetworkVariable<bool> teamModus;

	public NetworkVariable<int> punkteAlliierte;

	public NetworkVariable<int> punkteVerteidiger;

	// Prefab der Bots (Spieler-Prefab mit BotSteuerung), traegt das Editor-Skript ein.
	public GameObject botPrefab;

	// Wie oft der Server die Zahl der Bots prueft (Sekunden).
	private const float BOT_PRUEFINTERVALL = 1f;

	private readonly List<NetworkObject> bots = new List<NetworkObject>();

	private int botZaehler;

	private float naechstePruefung;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		teamModus = new NetworkVariable<bool>(false);
		punkteAlliierte = new NetworkVariable<int>(0);
		punkteVerteidiger = new NetworkVariable<int>(0);
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		if (base.IsServer)
		{
			teamModus.Value = LobbyWahl.teams;
			punkteAlliierte.Value = 0;
			punkteVerteidiger.Value = 0;
			bots.Clear();
			botZaehler = 0;
		}
	}

	public override void OnNetworkDespawn()
	{
		bots.Clear();
		base.OnNetworkDespawn();
	}

	// Team fuer einen neuen Spieler oder Bot: das kleinere der beiden. Ohne Teammodus keins.
	// Liest LobbyWahl direkt, weil die Spieler des Hosts entstehen koennen, bevor teamModus gesetzt ist.
	public int TeamZuteilen(Playermanager neu)
	{
		if (!LobbyWahl.teams)
		{
			return Teams.KEINS;
		}
		int alliierte = 0;
		int verteidiger = 0;
		foreach (Playermanager spieler in Object.FindObjectsOfType<Playermanager>())
		{
			if (spieler == neu)
			{
				continue;
			}
			if (spieler.team.Value == Teams.ALLIIERTE)
			{
				alliierte++;
			}
			else if (spieler.team.Value == Teams.VERTEIDIGER)
			{
				verteidiger++;
			}
		}
		return alliierte <= verteidiger ? Teams.ALLIIERTE : Teams.VERTEIDIGER;
	}

	// Kein Eigenbeschuss im Teammodus.
	public static bool DarfTreffen(Playermanager schuetze, Playermanager opfer)
	{
		if (Instance == null || !Instance.teamModus.Value)
		{
			return true;
		}
		return schuetze.team.Value == Teams.KEINS || schuetze.team.Value != opfer.team.Value;
	}

	public void killGezaehlt(Playermanager schuetze)
	{
		if (!base.IsServer || !teamModus.Value)
		{
			return;
		}
		if (schuetze.team.Value == Teams.ALLIIERTE)
		{
			punkteAlliierte.Value++;
		}
		else if (schuetze.team.Value == Teams.VERTEIDIGER)
		{
			punkteVerteidiger.Value++;
		}
	}

	private void Update()
	{
		if (!base.IsServer || !base.IsSpawned || Time.time < naechstePruefung)
		{
			return;
		}
		naechstePruefung = Time.time + BOT_PRUEFINTERVALL;
		botsAbgleichen();
	}

	// Fuellt die Runde mit Bots auf LobbyWahl.spielerMitBots auf. Kommen echte Spieler dazu,
	// gehen Bots wieder.
	private void botsAbgleichen()
	{
		bots.RemoveAll(bot => bot == null);
		int soll = 0;
		// Erst wenn eine Map laeuft (Spawnpunkte da), sonst haetten Bots keinen Platz.
		if (LobbyWahl.botsAuffuellen && botPrefab != null && Manager.Instance.gamestate != Manager.GameState.Lobby && Spawnlocation.hatPunkte())
		{
			int echte = base.NetworkManager.ConnectedClients.Count;
			soll = Mathf.Max(0, LobbyWahl.spielerMitBots - echte);
		}
		while (bots.Count < soll)
		{
			botErzeugen();
		}
		while (bots.Count > soll)
		{
			NetworkObject letzter = bots[bots.Count - 1];
			bots.RemoveAt(bots.Count - 1);
			if (letzter != null && letzter.IsSpawned)
			{
				letzter.Despawn(true);
			}
		}
	}

	private void botErzeugen()
	{
		GameObject bot = Object.Instantiate(botPrefab);
		botZaehler++;
		bot.GetComponent<BotSteuerung>().botName = "Bot " + botZaehler;
		NetworkObject netz = bot.GetComponent<NetworkObject>();
		netz.Spawn(true);
		bots.Add(netz);
	}
}
