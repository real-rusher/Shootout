using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ConnectionManager : NetworkBehaviour
{
	public NetworkManager networkManager;

	public NetworkVariable<int> chosenMap;

	private void Awake()
	{
		chosenMap = new NetworkVariable<int>(0);
	}

	private IEnumerator waitForChosenMap()
	{
		while (chosenMap.Value == 0)
		{
		}
		yield return null;
		Manager.Instance.getMap();
		if (chosenMap.Value == 1)
		{
			Spawnlocation.convertCollToBox(Manager.Instance.oldMap);
		}
		else if (chosenMap.Value == 2)
		{
			Spawnlocation.convertCollToBox(Manager.Instance.newMap);
		}
		while (Playermanager.ownerPlayer == null)
		{
			yield return null;
		}
		Playermanager.ownerPlayer.spawn();
	}

	public string connectAsHost(int pMap, ushort port = 7777)
	{
		if (networkManager.IsClient || networkManager.IsServer || networkManager.IsHost)
		{
			return "";
		}
		networkManager.GetComponent<UnityTransport>().ConnectionData.Address = getLocalIPAdress();
		networkManager.GetComponent<UnityTransport>().ConnectionData.Port = port;
		networkManager.StartHost();
		chosenMap.Value = pMap;
		StartCoroutine(waitForChosenMap());
		return getIPv4(getLocalIPAdress())[1];
	}

	public string connectToHost(string shortIP, ushort port = 7777)
	{
		if (networkManager.IsClient || networkManager.IsServer || networkManager.IsHost)
		{
			return "";
		}
		networkManager.GetComponent<UnityTransport>().ConnectionData.Address = getIPv4(getLocalIPAdress())[0] + shortIP;
		networkManager.GetComponent<UnityTransport>().ConnectionData.Port = port;
		networkManager.StartClient();
		StartCoroutine(waitForConnection());
		return shortIP;
	}

	public string GlobalconnectAsHost(int pMap, ushort port = 7777)
	{
		if (networkManager.IsClient || networkManager.IsServer || networkManager.IsHost)
		{
			return "";
		}
		string globalIPAddress = getGlobalIPAddress();
		if (globalIPAddress != "")
		{
			networkManager.GetComponent<UnityTransport>().ConnectionData.Address = globalIPAddress;
			networkManager.GetComponent<UnityTransport>().ConnectionData.Port = port;
			networkManager.StartHost();
		}
		chosenMap.Value = pMap;
		StartCoroutine(waitForConnection());
		return globalIPAddress;
	}

	public string GlobalconnectToHost(string longIP6, ushort port = 7777)
	{
		if (networkManager.IsClient || networkManager.IsServer || networkManager.IsHost)
		{
			return "";
		}
		networkManager.GetComponent<UnityTransport>().ConnectionData.Address = longIP6;
		networkManager.GetComponent<UnityTransport>().ConnectionData.Port = port;
		networkManager.StartClient();
		StartCoroutine(waitForConnection());
		return longIP6;
	}

	private IEnumerator waitForConnection()
	{
		float timer = 0f;
		while (timer < 5f)
		{
			timer += Time.deltaTime;
			if (networkManager.IsConnectedClient || networkManager.IsHost)
			{
				Manager.Instance.startGame();
				StartCoroutine(waitForChosenMap());
				break;
			}
			yield return null;
		}
		if (!networkManager.IsConnectedClient && !networkManager.IsHost)
		{
			stopNetwork();
			UIManager.Instance.connectionFailed();
		}
	}

	public void stopNetwork()
	{
		networkManager.Shutdown();
		StartCoroutine(resetChosenMap());
	}

	private IEnumerator resetChosenMap()
	{
		while (base.IsClient || base.IsHost)
		{
			yield return null;
		}
		chosenMap.Value = 0;
	}

	private void OnConnectedToServer()
	{
	}

	private void OnFailedToConnect()
	{
	}

	private void OnDisconnectedFromServer()
	{
	}

	public void disconnect()
	{
	}

	private string getLocalIPAdress()
	{
		IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
		foreach (IPAddress iPAddress in addressList)
		{
			if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
			{
				return iPAddress.ToString();
			}
		}
		throw new Exception("No network adapters with an IPv4 address in the system!");
	}

	private string getGlobalIPAddress()
	{
		try
		{
			using StreamReader streamReader = new StreamReader(((HttpWebResponse)WebRequest.Create("https://api64.ipify.org/").GetResponse()).GetResponseStream());
			string result = streamReader.ReadToEnd();
			streamReader.Close();
			return result;
		}
		catch (Exception)
		{
			return "";
		}
	}

	private string[] getIPv4(string ip)
	{
		string text = "";
		string text2 = "";
		int num = 0;
		for (int i = 0; i < ip.Length; i++)
		{
			if (num < 3)
			{
				if (ip[i] == '.')
				{
					num++;
				}
				text += ip[i];
			}
			else
			{
				text2 += ip[i];
			}
		}
		return new string[2] { text, text2 };
	}

}
