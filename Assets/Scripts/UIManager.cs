using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
	private delegate void UITriggers();

	public static UIManager Instance;

	private Stack<GameObject> uiStack = new Stack<GameObject>();

	public GameObject startingUI;

	private Dictionary<string, UITriggers> triggerForUI = new Dictionary<string, UITriggers>();

	private PlayerInput playerInput;

	private InputAction escapeAction;

	private InputAction anykeyAction;

	private InputAction enterAction;

	public Button enterUsernameButton;

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
		uiStack.Push(startingUI);
		triggerForUI.Add("Enter Username", EnterUsername);
		triggerForUI.Add("Join a LAN Game", JoinAGame);
		triggerForUI.Add("Join a GLOBAL Game", JoinAGame);
		playerInput = Manager.Instance.playerInput;
		escapeAction = playerInput.actions["Escape"];
		anykeyAction = playerInput.actions["AnyKey"];
		enterAction = playerInput.actions["Enter"];
	}

	private void Update()
	{
		if (Manager.Instance.gamestate == Manager.GameState.Lobby)
		{
			if (triggerForUI.ContainsKey(uiStack.Peek().name))
			{
				triggerForUI[uiStack.Peek().name]();
			}
			if (escapeAction.WasPerformedThisFrame() && uiStack.Count > 1)
			{
				revertBranch();
			}
		}
	}

	public void continueBranch(GameObject nextUI)
	{
		if (uiStack.Peek().transform.Find("UI") != null)
		{
			uiStack.Peek().transform.Find("UI").gameObject.SetActive(value: false);
		}
		foreach (Transform item in uiStack.Peek().transform)
		{
			if (item == nextUI.transform)
			{
				item.gameObject.SetActive(value: true);
				uiStack.Push(item.gameObject);
				return;
			}
		}
		Debug.LogWarning("UIManager: UI not available");
	}

	public void continueBranch(string nextUI)
	{
		if (uiStack.Peek().transform.Find("UI") != null)
		{
			uiStack.Peek().transform.Find("UI").gameObject.SetActive(value: false);
		}
		Transform transform = uiStack.Peek().transform.Find(nextUI);
		if (transform != null)
		{
			transform.gameObject.SetActive(value: true);
			uiStack.Push(transform.gameObject);
		}
		else
		{
			Debug.LogWarning("UIManager: UI not available: " + nextUI);
		}
	}

	public void revertBranch()
	{
		uiStack.Peek().SetActive(value: false);
		uiStack.Pop();
		if (uiStack.Peek().transform.Find("UI") != null)
		{
			uiStack.Peek().transform.Find("UI").gameObject.SetActive(value: true);
		}
	}

	private void EnterUsername()
	{
		bool flag = ValidateUserInput.isUsernameValid(base.transform.GetComponentInChildren<TMP_InputField>().text) != "";
		base.transform.GetComponentInChildren<TMP_InputField>().ActivateInputField();
		base.transform.GetComponentInChildren<TMP_InputField>().Select();
		enterUsernameButton.interactable = flag;
		if (enterAction.IsPressed() && flag)
		{
			EnterUsernameBtn();
		}
	}

	public void EnterUsernameBtn()
	{
		string text = ValidateUserInput.isUsernameValid(base.transform.GetComponentInChildren<TMP_InputField>().text);
		if (text != "")
		{
			Manager.Instance.currentPlayerName = text;
			continueBranch("Lobby Screen");
		}
	}

	private void JoinAGame()
	{
		base.transform.GetComponentInChildren<TMP_InputField>().ActivateInputField();
		base.transform.GetComponentInChildren<TMP_InputField>().Select();
		if (enterAction.IsPressed())
		{
			if (uiStack.Peek().name == "Join a LAN Game")
			{
				connectAsClientBtn(isLAN: true);
			}
			else if (uiStack.Peek().name == "Join a GLOBAL Game")
			{
				connectAsClientBtn(isLAN: false);
			}
			else
			{
				Debug.LogError("ERROR Z.141: UIManager");
			}
		}
	}

	public void connectAsClientBtn(bool isLAN)
	{
		string text = base.transform.GetComponentInChildren<TMP_InputField>().text;
		revertBranch();
		continueBranch("Connecting");
		if (isLAN)
		{
			Manager.Instance.connectAsClient(text);
		}
		else
		{
			Manager.Instance.globalconnectAsClient(text);
		}
	}

	public void connectionFailed()
	{
		if (uiStack.Peek().name != "Lobby Screen")
		{
			revertBranch();
		}
		continueBranch("Couldnt connect");
	}

	public void gameStarting()
	{
		while (uiStack.Peek().name != "Lobby Screen")
		{
			revertBranch();
		}
		startingUI.SetActive(value: false);
	}

	public void gameLeft()
	{
		startingUI.SetActive(value: true);
		while (uiStack.Peek().name != "Lobby Screen")
		{
			revertBranch();
		}
	}

	public void connectionLost()
	{
		startingUI.SetActive(value: true);
		if (uiStack.Peek().name != "Lobby Screen")
		{
			revertBranch();
		}
		continueBranch("You Got Disconnected");
	}
}
