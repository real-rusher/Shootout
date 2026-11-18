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

	// --- Neu: Anzeige fuer Leben, Waffe, Teams, FPS und Zielfernrohr ---

	public TMP_Text lebenText;

	public RectTransform lebenBalken;

	public TMP_Text waffeText;

	public TMP_Text teamText;

	public TMP_Text fpsText;

	public GameObject fadenkreuz;

	public GameObject zielfernrohr;

	private float hitmarkerBis;

	private float fpsGeglaettet = 60f;

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
		anzeigeAktualisieren();
	}

	private void anzeigeAktualisieren()
	{
		// Hitmarker ueber eine Zeit statt pro Treffer eine Coroutine: bei der SMG kommen
		// die Treffer schneller als die Anzeige dauert.
		Hitmarker.SetActive(Time.time < hitmarkerBis);

		if (fpsText != null)
		{
			fpsText.gameObject.SetActive(SettingsManager.fpsAnzeigen);
			if (SettingsManager.fpsAnzeigen && Time.unscaledDeltaTime > 0f)
			{
				fpsGeglaettet = Mathf.Lerp(fpsGeglaettet, 1f / Time.unscaledDeltaTime, 0.05f);
				fpsText.text = Mathf.RoundToInt(fpsGeglaettet) + " FPS";
			}
		}

		Playermanager ich = Playermanager.ownerPlayer;
		if (ich == null || Manager.Instance.gamestate == Manager.GameState.Lobby)
		{
			return;
		}
		int leben = Mathf.Max(0, ich.leben.Value);
		if (lebenText != null)
		{
			lebenText.text = leben + " / " + Waffen.MAX_LEBEN;
		}
		if (lebenBalken != null)
		{
			lebenBalken.anchorMax = new Vector2((float)leben / Waffen.MAX_LEBEN, 1f);
		}
		playershooting schiessen = ich.GetComponent<playershooting>();
		if (waffeText != null && schiessen != null)
		{
			string text = "";
			for (int i = 0; i < Waffen.LISTE.Length; i++)
			{
				string eintrag = "[" + (i + 1) + "] " + Waffen.LISTE[i].name;
				text += i == schiessen.waffe ? "<b><color=#FFFFFF>" + eintrag + "</color></b>   " : "<color=#9A9A9A>" + eintrag + "</color>   ";
			}
			if (Waffen.HatDauerfeuer(ich.playerNameString))
			{
				text += "<color=#FFB040>Dauerfeuer</color>";
			}
			waffeText.text = text;
		}
		bool gezoomt = schiessen != null && schiessen.zoomStufe >= 0 && !ich.isDead.Value;
		if (zielfernrohr != null)
		{
			zielfernrohr.SetActive(gezoomt);
		}
		if (fadenkreuz != null)
		{
			fadenkreuz.SetActive(!gezoomt);
		}
		if (teamText != null)
		{
			Runde runde = Runde.Instance;
			bool teams = runde != null && runde.teamModus.Value;
			teamText.gameObject.SetActive(teams);
			if (teams)
			{
				string farbe1 = ColorUtility.ToHtmlStringRGB(Teams.SchriftFarbe(Teams.ALLIIERTE));
				string farbe2 = ColorUtility.ToHtmlStringRGB(Teams.SchriftFarbe(Teams.VERTEIDIGER));
				string farbeIch = ColorUtility.ToHtmlStringRGB(Teams.SchriftFarbe(ich.team.Value));
				teamText.text = "<color=#" + farbe1 + ">" + Teams.Name(Teams.ALLIIERTE) + " " + runde.punkteAlliierte.Value + "</color>  :  <color=#"
					+ farbe2 + ">" + runde.punkteVerteidiger.Value + " " + Teams.Name(Teams.VERTEIDIGER) + "</color>\n<size=70%>Dein Team: <color=#"
					+ farbeIch + ">" + Teams.Name(ich.team.Value) + "</color></size>";
			}
		}
	}

	public void zeigeHitmarker()
	{
		hitmarkerBis = Time.time + 0.5f;
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
		zeigeHitmarker();
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
		else if (Manager.Instance.gamestate != Manager.GameState.Lobby)
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
