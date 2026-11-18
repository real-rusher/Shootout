using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Manager : NetworkBehaviour
{
	public enum GameState
	{
		Lobby,
		Game,
		PausedGame,
		Settings
	}

	public static Manager Instance;

	public GameObject oldMapPrefab;

	public GameObject newMapPrefab;

	public GameObject oldMap;

	public GameObject newMap;

	// Neu: dritte Map (chosenMap 3).
	public GameObject dDayMapPrefab;

	public GameObject dDayMap;

	// Weitere Maps ab Nummer 4 (Wuestenstadt, Tunnel, Himmelsinseln), Reihenfolge = Nummer.
	public GameObject[] weitereKartenPrefabs = new GameObject[0];

	private GameObject[] weitereKarten = new GameObject[0];

	// Namen fuer den Ladebildschirm, Index = Map-Nummer.
	private static readonly string[] KARTEN_NAMEN = { "", "Zimmer", "Info-Raum", "D-Day", "Wüstenstadt", "Tunnel", "Himmelsinseln" };

	// Die Map, die gerade gespielt wird (nach getMap).
	public GameObject aktiveKarte { get; private set; }

	// So lange steht der Ladebildschirm mindestens (Sekunden). In der Zeit werden die
	// Shader der Map vorbereitet, sonst blitzt beim ersten Bild alles blau oder ruckelt.
	private const float LADE_MINDESTZEIT = 1.2f;

	public PlayerInput playerInput;

	public ConnectionManager connectionManager;

	public GameObject uiCam;

	public Action OnLobbyoppened;

	public Action OnGamestarted;

	public Action OnGamepaused;

	public string currentPlayerName = "";

	public GameState gamestate { get; private set; }

	public string ownIP { get; private set; } = "";

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			UnityEngine.Object.Destroy(Instance);
		}
		kartenErzeugen();
	}

	// Legt fehlende Maps an. Das Zimmer bleibt sichtbar, es ist der Hintergrund im Menue.
	private void kartenErzeugen()
	{
		if (oldMap == null)
		{
			oldMap = UnityEngine.Object.Instantiate(oldMapPrefab);
		}
		if (newMap == null)
		{
			newMap = UnityEngine.Object.Instantiate(newMapPrefab);
		}
		if (dDayMap == null && dDayMapPrefab != null)
		{
			dDayMap = UnityEngine.Object.Instantiate(dDayMapPrefab);
		}
		if (weitereKarten.Length != weitereKartenPrefabs.Length)
		{
			weitereKarten = new GameObject[weitereKartenPrefabs.Length];
		}
		for (int i = 0; i < weitereKartenPrefabs.Length; i++)
		{
			if (weitereKarten[i] == null && weitereKartenPrefabs[i] != null)
			{
				weitereKarten[i] = UnityEngine.Object.Instantiate(weitereKartenPrefabs[i]);
			}
			if (weitereKarten[i] != null)
			{
				weitereKarten[i].SetActive(value: false);
			}
		}
		oldMap.SetActive(value: true);
		newMap.SetActive(value: false);
		if (dDayMap != null)
		{
			dDayMap.SetActive(value: false);
		}
		aktiveKarte = null;
	}

	public void connectAsClient(string shortIP)
	{
		ownIP = connectionManager.connectToHost(shortIP, 7777);
	}

	public void startHost(int pMap)
	{
		ownIP = connectionManager.connectAsHost(pMap, 7777);
		UIManager.Instance.gameStarting();
		startGame();
	}

	// Online ueber Unity Relay. Den Lobbycode (Join-Code) setzt ConnectionManager per
	// setzeLobbycode, sobald er da ist. UIManager zeigt "Connecting" schon selbst.
	public void globalconnectAsClient(string longIP)
	{
		connectionManager.OnlineBeitreten(longIP);
	}

	public void globalstartHost(int pMap)
	{
		// Anmeldung und Relay brauchen einen Moment, so lange "Connecting" zeigen.
		// Wie beim Beitreten: erst das Map-Fenster schliessen, dann "Connecting" oeffnen.
		UIManager.Instance.revertBranch();
		UIManager.Instance.continueBranch("Connecting");
		connectionManager.OnlineHosten(pMap);
	}

	public void setzeLobbycode(string code)
	{
		ownIP = code;
	}

	// Aktiviert die gewaehlte Map und entfernt die anderen (wie im Original, spart Speicher).
	public void getMap()
	{
		int wahl = connectionManager.chosenMap.Value;
		GameObject gewaehlt = newMap;
		if (wahl == 1)
		{
			gewaehlt = oldMap;
		}
		else if (wahl == 3 && dDayMap != null)
		{
			gewaehlt = dDayMap;
		}
		else if (wahl >= 4 && wahl - 4 < weitereKarten.Length && weitereKarten[wahl - 4] != null)
		{
			gewaehlt = weitereKarten[wahl - 4];
		}
		var alle = new System.Collections.Generic.List<GameObject> { oldMap, newMap, dDayMap };
		alle.AddRange(weitereKarten);
		foreach (GameObject karte in alle)
		{
			if (karte != null && karte != gewaehlt)
			{
				UnityEngine.Object.Destroy(karte);
			}
		}
		gewaehlt.SetActive(value: true);
		aktiveKarte = gewaehlt;
		UIGameManager.Instance.ladenZeigen(wahl > 0 && wahl < KARTEN_NAMEN.Length ? KARTEN_NAMEN[wahl] : "");
		StartCoroutine(ladenAbschliessen());
	}

	private IEnumerator ladenAbschliessen()
	{
		float bis = Time.unscaledTime + LADE_MINDESTZEIT;
		// Ein Bild abwarten, dann alle geladenen Shader vorbereiten (auch fuer Dinge, die
		// die Kamera noch nicht sieht).
		yield return null;
		Shader.WarmupAllShaders();
		int bilder = 0;
		while (Time.unscaledTime < bis || bilder < 5)
		{
			bilder++;
			yield return null;
		}
		UIGameManager.Instance.ladenVerbergen();
	}

	private void returnMap()
	{
		kartenErzeugen();
	}

	public void startGame()
	{
		gamestate = GameState.Game;
		UIManager.Instance.gameStarting();
		playerInput.SwitchCurrentActionMap("Player");
		Cursor.lockState = CursorLockMode.Locked;
		UIGameManager.Instance.activate();
		uiCam.SetActive(value: false);
	}

	public void stopGame()
	{
		gamestate = GameState.PausedGame;
		playerInput.SwitchCurrentActionMap("UI");
		Cursor.lockState = CursorLockMode.None;
	}

	public void continueGame()
	{
		gamestate = GameState.Game;
		playerInput.SwitchCurrentActionMap("Player");
		Cursor.lockState = CursorLockMode.Locked;
	}

	public void leaveGame()
	{
		gamestate = GameState.Lobby;
		playerInput.SwitchCurrentActionMap("UI");
		Cursor.lockState = CursorLockMode.None;
		UIGameManager.Instance.deactivate();
		UIManager.Instance.gameLeft();
		uiCam.SetActive(value: true);
		connectionManager.stopNetwork();
		returnMap();
	}

	public void connectionLost()
	{
		gamestate = GameState.Lobby;
		playerInput.SwitchCurrentActionMap("UI");
		Cursor.lockState = CursorLockMode.None;
		UIGameManager.Instance.deactivate();
		UIManager.Instance.connectionLost();
		uiCam.SetActive(value: true);
		connectionManager.stopNetwork();
		// Fehlte im Original: Ohne neue Maps fehlte nach einem Verbindungsabbruch die
		// geloeschte Map beim naechsten Spiel.
		returnMap();
	}

	public void openSettings()
	{
		gamestate = GameState.Settings;
	}

	public void closeSettings()
	{
		if (uiCam.activeSelf)
		{
			gamestate = GameState.Lobby;
		}
		else
		{
			gamestate = GameState.PausedGame;
		}
	}

	public void copyIPAdress()
	{
		GUIUtility.systemCopyBuffer = ownIP;
		// Vorher passierte sichtbar nichts, der Knopf wirkte kaputt.
		UIGameManager.Instance.zeigeHinweis("Lobbycode " + ownIP + " kopiert");
	}

	public void QuitApp()
	{
		// Application.Quit tut im Editor nichts, dort deshalb Play beenden.
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

}
