using TMPro;
using UnityEngine;

public class connectingAnim : MonoBehaviour
{
	public TMP_Text connectingTxt;

	private float speed = 0.3f;

	private float timer;

	private float overallTimer;

	private void OnEnable()
	{
		timer = 0f;
		overallTimer = 0f;
	}

	private void Update()
	{
		timer += Time.deltaTime;
		overallTimer += Time.deltaTime;
		if (timer < speed)
		{
			connectingTxt.text = "connecting";
		}
		else if (timer < speed * 2f)
		{
			connectingTxt.text = "connecting.";
		}
		else if (timer < speed * 3f)
		{
			connectingTxt.text = "connecting..";
		}
		else if (timer < speed * 4f)
		{
			connectingTxt.text = "connecting...";
		}
		else
		{
			timer = 0f;
		}
		if (overallTimer > 10f)
		{
			UIManager.Instance.connectionFailed();
		}
	}
}
