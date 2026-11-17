using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayersManager : NetworkBehaviour
{
	public static PlayersManager Instance;

	public List<Playermanager> players = new List<Playermanager>();

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

	public void addPlayerClientSide(Playermanager player)
	{
		players.Add(player);
		Debug.Log(player.playerNameString + " joined the Game");
		UIGameManager.Instance.addNewPlayer(player);
	}

	public string addPlayerOwnerSide(Playermanager player, string pName)
	{
		string uniqueName = getUniqueName(pName);
		players.Add(player);
		UIGameManager.Instance.addNewPlayer(player);
		Debug.Log(player.playerNameString + "Joined the Game");
		return uniqueName;
	}

	public void removePlayer(Playermanager player)
	{
		players.Remove(player);
		UIGameManager.Instance.removePlayer(player);
		Debug.Log(player.playerNameString + " left the Game");
	}

	private string getUniqueName(string pName)
	{
		Playermanager playerByName = getPlayerByName(pName);
		int num = 0;
		while (playerByName != null)
		{
			num++;
			playerByName = getPlayerByName(pName + " #" + num);
		}
		string result = pName;
		if (num != 0)
		{
			result = pName + " #" + num;
		}
		return result;
	}

	private Playermanager getPlayerByName(string pName)
	{
		for (int i = 0; i < players.Count; i++)
		{
			if (players[i].name == pName)
			{
				return players[i];
			}
		}
		return null;
	}

	public static Playermanager getPlayerByNetworkId(ulong behaviourId)
	{
		for (int i = 0; i < Instance.players.Count; i++)
		{
			if (Instance.players[i].NetworkBehaviourId == behaviourId)
			{
				return Instance.players[i];
			}
		}
		return null;
	}

	internal override string __getTypeName()
	{
		return "PlayersManager";
	}
}
