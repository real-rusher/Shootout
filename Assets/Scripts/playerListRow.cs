using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class playerListRow : MonoBehaviour
{
	public RectTransform rect;

	public Image img;

	public TMP_Text indexTxt;

	public TMP_Text nameTxt;

	public TMP_Text killsTxt;

	public TMP_Text deathsTxt;

	public TMP_Text kdTxt;

	private static List<playerListRow> rows = new List<playerListRow>();

	private Playermanager player;

	private bool ownerPlayer;

	private bool hostPlayer;

	private Color32 lightColor = new Color32(180, 180, 180, 100);

	private Color32 darkColor = new Color32(100, 100, 100, 100);

	private Color32 ownerColor = new Color32(96, 104, 211, 100);

	private float timer = 1f;

	public void setPlayer(Playermanager pPlayer)
	{
		player = pPlayer;
		// Bots gehoeren dem Host, sind aber nicht "du".
		ownerPlayer = player.istLokal;
		hostPlayer = player.IsOwnedByServer;
		rows.Add(this);
		recalc();
	}

	public static void removePlayer(Playermanager pPlayer)
	{
		for (int i = 0; i < rows.Count; i++)
		{
			if (rows[i].player == pPlayer)
			{
				Object.Destroy(rows[i].gameObject);
				rows.RemoveAt(i);
			}
		}
	}

	private void Update()
	{
		if (ownerPlayer)
		{
			if (timer >= 1f)
			{
				recalc();
			}
			timer += Time.deltaTime;
		}
	}

	private void recalc()
	{
		timer = 0f;
		for (int i = 0; i < rows.Count; i++)
		{
			rows[i].updateData();
		}
		sortList();
		for (int j = 0; j < rows.Count; j++)
		{
			rows[j].setIndex(j);
		}
	}

	private void sortList()
	{
		List<playerListRow> list = new List<playerListRow>();
		while (rows.Count > 0)
		{
			playerListRow playerListRow2 = rows[0];
			for (int i = 1; i < rows.Count; i++)
			{
				if (rows[i].player.kills.Value > playerListRow2.player.kills.Value)
				{
					playerListRow2 = rows[i];
				}
				else if (rows[i].player.kills.Value == playerListRow2.player.kills.Value && rows[i].player.deaths.Value < playerListRow2.player.deaths.Value)
				{
					playerListRow2 = rows[i];
				}
			}
			rows.Remove(playerListRow2);
			list.Add(playerListRow2);
		}
		rows = list;
	}

	private void updateData()
	{
		int value = player.kills.Value;
		int value2 = player.deaths.Value;
		float num = ((value2 == 0) ? ((float)value) : ((float)value / (float)value2));
		string zusatz = player.istBot ? "(Bot) " : (hostPlayer ? "(host) " : "");
		nameTxt.text = zusatz + player.name;
		// Im Teammodus der Name in Teamfarbe.
		nameTxt.color = Teams.SchriftFarbe(player.team.Value);
		killsTxt.text = value.ToString();
		deathsTxt.text = value2.ToString();
		// Original zeigte die K/D ungerundet (z. B. 0.3333333).
		kdTxt.text = num.ToString("0.00");
	}

	private void setIndex(int pIndex)
	{
		rect.anchoredPosition = new Vector2(0f, -75 - 50 * pIndex);
		indexTxt.text = (pIndex + 1).ToString();
		if (ownerPlayer)
		{
			img.color = ownerColor;
		}
		else
		{
			img.color = ((pIndex % 2 == 0) ? lightColor : darkColor);
		}
	}
}
