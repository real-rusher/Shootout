using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class ConnectionManager : NetworkBehaviour
{
	public NetworkManager networkManager;

	public NetworkVariable<int> chosenMap;

	// Wie viele Gaeste ein Online-Host aufnimmt (ohne den Host selbst).
	private const int ONLINE_MAX_GAESTE = 9;

	// Gerade eine Online-Runde ueber Relay? Dann muss der Transport danach zurueck auf
	// direktes UDP, damit LAN wieder genau so laeuft wie vorher.
	private bool onlineAktiv;

	// Verhindert doppelte Klicks, waehrend Anmeldung und Relay noch laufen.
	private bool onlineStartLaeuft;

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
		// Spawnbereiche der gewaehlten Map (1 Zimmer, 2 Info-Raum, 3 D-Day).
		Spawnlocation.convertCollToBox(Manager.Instance.aktiveKarte);
		while (Playermanager.ownerPlayer == null)
		{
			yield return null;
		}
		Playermanager.ownerPlayer.ersterSpawn();
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

	// Online-Runden laufen ueber Unity Relay: Host und Gaeste verbinden sich mit einem
	// Relay-Server von Unity, niemand braucht eine Portfreigabe. Der Lobbycode ist der
	// Join-Code von Relay. Die LAN-Methoden oben bleiben davon unberuehrt.
	public async void OnlineHosten(int pMap)
	{
		if (onlineStartLaeuft || networkManager.IsClient || networkManager.IsServer || networkManager.IsHost)
		{
			return;
		}
		onlineStartLaeuft = true;
		try
		{
			await Anmelden();
			Allocation zuteilung = await RelayService.Instance.CreateAllocationAsync(ONLINE_MAX_GAESTE);
			string joinCode = await RelayService.Instance.GetJoinCodeAsync(zuteilung.AllocationId);
			onlineAktiv = true;
			// "dtls": verschluesselte Verbindung zum Relay-Server
			networkManager.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(zuteilung, "dtls"));
			Manager.Instance.setzeLobbycode(joinCode);
			networkManager.StartHost();
			chosenMap.Value = pMap;
			StartCoroutine(waitForConnection());
		}
		catch (Exception fehler)
		{
			OnlineFehlgeschlagen(fehler);
		}
		finally
		{
			onlineStartLaeuft = false;
		}
	}

	public async void OnlineBeitreten(string joinCode)
	{
		if (onlineStartLaeuft || networkManager.IsClient || networkManager.IsServer || networkManager.IsHost)
		{
			return;
		}
		onlineStartLaeuft = true;
		try
		{
			// Join-Codes sind Grossbuchstaben. Das Eingabefeld kann ein unsichtbares
			// Leerzeichen (U+200B) enthalten.
			string code = (joinCode ?? "").Replace("​", "").Trim().ToUpperInvariant();
			await Anmelden();
			JoinAllocation zuteilung = await RelayService.Instance.JoinAllocationAsync(code);
			onlineAktiv = true;
			networkManager.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(zuteilung, "dtls"));
			Manager.Instance.setzeLobbycode(code);
			networkManager.StartClient();
			StartCoroutine(waitForConnection());
		}
		catch (Exception fehler)
		{
			OnlineFehlgeschlagen(fehler);
		}
		finally
		{
			onlineStartLaeuft = false;
		}
	}

	private static async Task Anmelden()
	{
		if (UnityServices.State == ServicesInitializationState.Uninitialized)
		{
			// Eigenes Profil fuer jeden Programmstart: Zwei Instanzen auf einem PC (ParrelSync,
			// zwei Builds) teilen sonst dieselbe anonyme Anmeldung und stoeren sich.
			InitializationOptions optionen = new InitializationOptions().SetProfile("s" + UnityEngine.Random.Range(0, int.MaxValue));
			await UnityServices.InitializeAsync(optionen);
		}
		if (!AuthenticationService.Instance.IsSignedIn)
		{
			await AuthenticationService.Instance.SignInAnonymouslyAsync();
		}
	}

	private void OnlineFehlgeschlagen(Exception fehler)
	{
		// Haeufigste Ursachen: Projekt nicht mit Unity Cloud verknuepft, Relay im Dashboard
		// nicht aktiviert, kein Internet, falscher Join-Code.
		Debug.LogWarning("Online-Verbindung fehlgeschlagen: " + fehler.Message);
		if (networkManager.IsClient || networkManager.IsServer || networkManager.IsHost)
		{
			stopNetwork();
		}
		else
		{
			OnlineZuruecksetzen();
		}
		UIManager.Instance.connectionFailed();
	}

	private void OnlineZuruecksetzen()
	{
		if (!onlineAktiv)
		{
			return;
		}
		onlineAktiv = false;
		// SetConnectionData schaltet das Protokoll zurueck auf direktes UDP. Die Adressdaten
		// bleiben dabei genau so, wie sie waren.
		UnityTransport transport = networkManager.GetComponent<UnityTransport>();
		UnityTransport.ConnectionAddressData daten = transport.ConnectionData;
		transport.SetConnectionData(daten.Address, daten.Port, daten.ServerListenAddress);
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
		// Erst nach dem Herunterfahren: Bei LAN-Runden tut das nichts.
		OnlineZuruecksetzen();
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
