using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIGameManager : MonoBehaviour
{
	public static UIGameManager Instance;

	private PlayerInput playerInput;

	private InputAction escapeAction;

	private InputAction spaceAction;

	public GameObject gameScreen;

	public GameObject gameUI;

	public GameObject pauseUI;

	public GameObject deathUI;

	public GameObject playerListUI;

	public TMP_Text lobbyCode;

	public Button copyIP;

	public Transform playerListPanel;

	public GameObject playerRowPrefab;

	public TMP_Text killedByField;

	public TMP_Text killsText;

	public TMP_Text DeathsText;

	public GameObject Hitmarker;

	public Slider fireCoolDown;

	public bool _isDead => Playermanager.ownerPlayer.isDead.Value;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Object.Destroy(Instance);
		}
	}

	private void Start()
	{
		playerInput = Manager.Instance.playerInput;
		Hitmarker.SetActive(value: false);
	}

	private void Update()
	{
		checkEscape();
	}

	private void checkEscape()
	{
		if (Manager.Instance.gamestate == Manager.GameState.Game)
		{
			if (escapeAction.WasPerformedThisFrame())
			{
				Manager.Instance.stopGame();
				pauseUIOn();
			}
		}
		else if (Manager.Instance.gamestate == Manager.GameState.PausedGame)
		{
			if (escapeAction.WasPerformedThisFrame() && !_isDead)
			{
				Manager.Instance.continueGame();
				gameUIOn();
			}
			else if (_isDead && spaceAction.WasPerformedThisFrame())
			{
				Manager.Instance.continueGame();
				gameUIOn();
				Playermanager.ownerPlayer.respawn();
			}
		}
	}

	private void gameUIOn()
	{
		gameUI.SetActive(value: true);
		pauseUI.SetActive(value: false);
		playerListUI.SetActive(value: false);
		deathUI.SetActive(value: false);
		escapeAction = playerInput.actions["Escape"];
	}

	private void pauseUIOn()
	{
		gameUI.SetActive(value: true);
		pauseUI.SetActive(value: true);
		playerListUI.SetActive(value: true);
		deathUI.SetActive(value: false);
		escapeAction = playerInput.actions["Escape"];
	}

	private void deathUIOn()
	{
		gameUI.SetActive(value: true);
		pauseUI.SetActive(value: false);
		playerListUI.SetActive(value: true);
		deathUI.SetActive(value: true);
		escapeAction = playerInput.actions["Escape"];
		spaceAction = playerInput.actions["Space"];
	}

	public void activate()
	{
		gameScreen.SetActive(value: true);
		gameUIOn();
		copyIP.gameObject.SetActive(value: true);
		lobbyCode.gameObject.SetActive(value: true);
		lobbyCode.text = "Your Lobbycode is: " + Manager.Instance.ownIP;
		resetUI();
	}

	public void deactivate()
	{
		gameScreen.SetActive(value: false);
	}

	private void resetUI()
	{
		killsText.text = "Kills: 0";
		DeathsText.text = "Deaths: 0";
	}

	public void shotPlayer(Playermanager ownerPlayer, string affectedPlayer)
	{
		killsText.text = "Kills: " + ownerPlayer.kills.Value;
		StartCoroutine(animateHitmarker());
	}

	private IEnumerator animateHitmarker()
	{
		Hitmarker.SetActive(value: true);
		yield return new WaitForSeconds(0.5f);
		Hitmarker.SetActive(value: false);
	}

	public void gotShot(Playermanager ownerPlayer, string shooterPlayer)
	{
		DeathsText.text = "Deaths: " + ownerPlayer.deaths.Value;
		killedByField.text = shooterPlayer;
		deathUIOn();
	}

	public void coolDown(float value)
	{
		if (value >= 1f)
		{
			fireCoolDown.gameObject.SetActive(value: false);
		}
		else if (Manager.Instance.gamestate != 0)
		{
			if (fireCoolDown.gameObject.activeSelf)
			{
				fireCoolDown.value = value;
				return;
			}
			fireCoolDown.gameObject.SetActive(value: true);
			fireCoolDown.value = value;
		}
	}

	public void addNewPlayer(Playermanager pM)
	{
		Object.Instantiate(playerRowPrefab, playerListPanel).GetComponent<playerListRow>().setPlayer(pM);
	}

	public void removePlayer(Playermanager pM)
	{
		playerListRow.removePlayer(pM);
	}
}
