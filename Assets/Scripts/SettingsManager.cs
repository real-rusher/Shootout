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
	}

	public void closeSettings()
	{
		Manager.Instance.closeSettings();
		settingsScreen.SetActive(value: false);
	}

	public void changeSensi()
	{
		c_Sensi = c_SensiSlider.value;
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
}
