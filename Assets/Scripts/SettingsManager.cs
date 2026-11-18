using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
	public static SettingsManager Instance;

	public GameObject settingsScreen;

	public Slider c_SensiSlider;

	public static float c_Sensi = 3f;

	private int currentTab;

	public Button[] tabButtons;

	public TMP_InputField changeUsernameField;

	public Button changeUsernameBtn;

	public TMP_Text changeUsernameBtnTxt;

	// --- Ab hier neu: Im Original hatten Video- und Sound-Tab keine Funktion. ---

	// Inhalte der drei Tabs (Video, Sound, Steuerung), gleiche Reihenfolge wie tabButtons.
	public GameObject[] tabInhalte;

	public ScrollRect scroller;

	public Toggle fpsSchalter;

	public Button fensterKnopf;

	public Button vollbildKnopf;

	public Toggle kopfbilderSchalter;

	public Slider lautstaerkeRegler;

	public Toggle invertierenSchalter;

	public static bool fpsAnzeigen;

	public static bool mausInvertiert;

	public static bool fremdeKopfbilder = true;

	public static float lautstaerke = 0.8f;

	// Wird ausgeloest, wenn sich "Bilder anderer Spieler" aendert (Kopfbild hoert zu).
	public static event System.Action KopfbildEinstellungGeaendert;

	// Fenstergroesse beim Wechsel aus dem Vollbild.
	private const int FENSTER_BREITE = 1280;

	private const int FENSTER_HOEHE = 720;

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
		Laden();
	}

	private void Start()
	{
		// Die neuen Bedienelemente werden hier verdrahtet statt in der Szene, damit ihr
		// Zustand (an/aus, Wert) direkt mitkommt.
		if (fpsSchalter != null)
		{
			fpsSchalter.SetIsOnWithoutNotify(fpsAnzeigen);
			fpsSchalter.onValueChanged.AddListener(wert => { fpsAnzeigen = wert; Speichern(); });
		}
		if (kopfbilderSchalter != null)
		{
			kopfbilderSchalter.SetIsOnWithoutNotify(fremdeKopfbilder);
			kopfbilderSchalter.onValueChanged.AddListener(wert =>
			{
				fremdeKopfbilder = wert;
				Speichern();
				KopfbildEinstellungGeaendert?.Invoke();
			});
		}
		if (invertierenSchalter != null)
		{
			invertierenSchalter.SetIsOnWithoutNotify(mausInvertiert);
			invertierenSchalter.onValueChanged.AddListener(wert => { mausInvertiert = wert; Speichern(); });
		}
		if (lautstaerkeRegler != null)
		{
			lautstaerkeRegler.minValue = 0f;
			lautstaerkeRegler.maxValue = 1f;
			lautstaerkeRegler.SetValueWithoutNotify(lautstaerke);
			lautstaerkeRegler.onValueChanged.AddListener(wert =>
			{
				lautstaerke = wert;
				AudioListener.volume = wert;
				Speichern();
			});
		}
		if (fensterKnopf != null)
		{
			fensterKnopf.onClick.AddListener(() => setzeVollbild(false));
		}
		if (vollbildKnopf != null)
		{
			vollbildKnopf.onClick.AddListener(() => setzeVollbild(true));
		}
		AudioListener.volume = lautstaerke;
		fensterKnoepfeAktualisieren();
	}

	private void Update()
	{
		if (changeUsernameField.isFocused)
		{
			checkUsernameInput();
		}
	}

	public void openSettings()
	{
		Manager.Instance.openSettings();
		settingsScreen.SetActive(value: true);
		updateControlls();
		zeigeTab(currentTab);
		fensterKnoepfeAktualisieren();
	}

	public void closeSettings()
	{
		Manager.Instance.closeSettings();
		settingsScreen.SetActive(value: false);
	}

	public void changeSensi()
	{
		c_Sensi = c_SensiSlider.value;
		Speichern();
	}

	private void updateControlls()
	{
		c_SensiSlider.value = c_Sensi;
	}

	public void setTab(int btn)
	{
		if (btn != currentTab)
		{
			tabButtons[currentTab].interactable = true;
			tabButtons[btn].interactable = false;
			currentTab = btn;
		}
		zeigeTab(btn);
	}

	// Zeigt nur den Inhalt des gewaehlten Tabs. Im Original lagen Sound und Steuerung
	// uebereinander und Video war nie sichtbar.
	private void zeigeTab(int tab)
	{
		for (int i = 0; i < tabButtons.Length; i++)
		{
			tabButtons[i].interactable = i != tab;
		}
		if (tabInhalte == null)
		{
			return;
		}
		for (int i = 0; i < tabInhalte.Length; i++)
		{
			if (tabInhalte[i] != null)
			{
				tabInhalte[i].SetActive(i == tab);
			}
		}
		if (scroller != null && tab < tabInhalte.Length && tabInhalte[tab] != null)
		{
			scroller.content = tabInhalte[tab].GetComponent<RectTransform>();
			scroller.verticalNormalizedPosition = 1f;
		}
	}

	private void setzeVollbild(bool vollbild)
	{
		if (vollbild)
		{
			Resolution aufloesung = Screen.currentResolution;
			Screen.SetResolution(aufloesung.width, aufloesung.height, FullScreenMode.FullScreenWindow);
		}
		else
		{
			Screen.SetResolution(FENSTER_BREITE, FENSTER_HOEHE, FullScreenMode.Windowed);
		}
		// Screen.fullScreen aendert sich erst im naechsten Frame, deshalb den Wunsch anzeigen.
		if (fensterKnopf != null)
		{
			fensterKnopf.interactable = vollbild;
		}
		if (vollbildKnopf != null)
		{
			vollbildKnopf.interactable = !vollbild;
		}
	}

	private void fensterKnoepfeAktualisieren()
	{
		bool vollbild = Screen.fullScreen;
		if (fensterKnopf != null)
		{
			fensterKnopf.interactable = vollbild;
		}
		if (vollbildKnopf != null)
		{
			vollbildKnopf.interactable = !vollbild;
		}
	}

	private void checkUsernameInput()
	{
		string text = ValidateUserInput.isUsernameValid(changeUsernameField.text);
		if (text != "" && text != Manager.Instance.currentPlayerName)
		{
			changeUsernameField.text = text;
			changeUsernameBtn.interactable = true;
			changeUsernameBtnTxt.color = Color.HSVToRGB(0f, 0f, 0.78f);
		}
		else
		{
			changeUsernameBtn.interactable = false;
			changeUsernameBtnTxt.color = Color.HSVToRGB(0f, 0f, 0.38f);
		}
	}

	public void setUserName()
	{
		string text = ValidateUserInput.isUsernameValid(changeUsernameField.text);
		if (text != "" && text != Manager.Instance.currentPlayerName)
		{
			Manager.Instance.currentPlayerName = text;
			if (Manager.Instance.gamestate == Manager.GameState.Game || Manager.Instance.gamestate == Manager.GameState.PausedGame)
			{
				Playermanager.ownerPlayer._playerName.Value = text;
			}
			checkUsernameInput();
		}
	}

	// Einstellungen bleiben ueber Programmstarts erhalten.
	private static void Laden()
	{
		c_Sensi = PlayerPrefs.GetFloat("einstellung.empfindlichkeit", c_Sensi);
		fpsAnzeigen = PlayerPrefs.GetInt("einstellung.fps", 0) == 1;
		mausInvertiert = PlayerPrefs.GetInt("einstellung.invertiert", 0) == 1;
		fremdeKopfbilder = PlayerPrefs.GetInt("einstellung.kopfbilder", 1) == 1;
		lautstaerke = PlayerPrefs.GetFloat("einstellung.lautstaerke", lautstaerke);
	}

	private static void Speichern()
	{
		PlayerPrefs.SetFloat("einstellung.empfindlichkeit", c_Sensi);
		PlayerPrefs.SetInt("einstellung.fps", fpsAnzeigen ? 1 : 0);
		PlayerPrefs.SetInt("einstellung.invertiert", mausInvertiert ? 1 : 0);
		PlayerPrefs.SetInt("einstellung.kopfbilder", fremdeKopfbilder ? 1 : 0);
		PlayerPrefs.SetFloat("einstellung.lautstaerke", lautstaerke);
		PlayerPrefs.Save();
	}
}
