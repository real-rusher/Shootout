using System.Text;
using Unity.Netcode;
using UnityEngine;

// Kommt auf dasselbe GameObject wie der NetworkManager.
// Gastgeber und Gast muessen dieselbe Version haben, sonst passen RPCs und
// NetworkVariables nicht zusammen und das Spiel verhaelt sich unvorhersehbar.
// Deshalb lehnt der Host Clients mit anderer Version schon beim Verbinden ab.
// Der Client landet dann nach dem Timeout in ConnectionManager.waitForConnection
// bei "Verbindung fehlgeschlagen".
[RequireComponent(typeof(NetworkManager))]
public class Spielversion : MonoBehaviour
{
	// Bei jeder Aenderung erhoehen und im README in der Versionstabelle eintragen.
	public const string VERSION = "0.1.0";

	private NetworkManager netzwerk;

	private void Awake()
	{
		netzwerk = GetComponent<NetworkManager>();
		netzwerk.NetworkConfig.ConnectionApproval = true;
		// Wird bei StartClient und StartHost als Payload mitgeschickt.
		netzwerk.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(VERSION);
		netzwerk.ConnectionApprovalCallback = pruefeVerbindung;
	}

	private void pruefeVerbindung(NetworkManager.ConnectionApprovalRequest anfrage, NetworkManager.ConnectionApprovalResponse antwort)
	{
		string versionGast = anfrage.Payload == null ? "" : Encoding.UTF8.GetString(anfrage.Payload);
		antwort.Approved = versionGast == VERSION;
		// Mit eingeschalteter ConnectionApproval legt Netcode den Spieler nur an,
		// wenn man es ausdruecklich erlaubt.
		antwort.CreatePlayerObject = antwort.Approved;
		antwort.Pending = false;
		if (!antwort.Approved)
		{
			Debug.Log("Client abgelehnt: Version " + versionGast + ", Host hat " + VERSION);
		}
	}
}
