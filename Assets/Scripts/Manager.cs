using System;
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
		oldMap = UnityEngine.Object.Instantiate(oldMapPrefab);
		oldMap.SetActive(value: true);
		newMap = UnityEngine.Object.Instantiate(newMapPrefab);
		newMap.SetActive(value: false);
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

	public void globalconnectAsClient(string longIP)
	{
		ownIP = connectionManager.GlobalconnectToHost(longIP, 7777);
	}

	public void globalstartHost(int pMap)
	{
		ownIP = connectionManager.GlobalconnectAsHost(pMap, 7777);
	}

	public void getMap()
	{
		if (connectionManager.chosenMap.Value == 1)
		{
			Debug.Log("getMap1");
			oldMap.SetActive(value: true);
			UnityEngine.Object.Destroy(newMap);
		}
		else
		{
			Debug.Log("getMap2");
			newMap.SetActive(value: true);
			UnityEngine.Object.Destroy(oldMap);
		}
	}

	private void returnMap()
	{
		if (oldMap != null)
		{
			oldMap.SetActive(value: true);
			newMap = UnityEngine.Object.Instantiate(newMapPrefab);
			newMap.SetActive(value: false);
		}
		else
		{
			newMap.SetActive(value: false);
			oldMap = UnityEngine.Object.Instantiate(oldMapPrefab);
			oldMap.SetActive(value: true);
		}
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
	}

	public void QuitApp()
	{
		Application.Quit();
	}

}
