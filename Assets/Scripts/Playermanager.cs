using System;
using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Playermanager : NetworkBehaviour
{
	public delegate void Kill(Playermanager thisManager, string affectedManager);

	public NetworkVariable<FixedString64Bytes> _playerName;

	public NetworkVariable<int> kills;

	public NetworkVariable<int> deaths;

	public NetworkVariable<bool> isDead;

	// Neu: Leben und Team entscheidet der Server.
	public NetworkVariable<int> leben;

	public NetworkVariable<int> team;

	public TMP_Text nameLabel;

	public GameObject body;

	public GameObject laserPointer;

	// Partikelmaterial fuer die rote Explosion beim Tod (Todeseffekt).
	public Material todesMaterial;

	private CharacterController characterController;

	public static Playermanager ownerPlayer;

	public Kill shotPlayer;

	public Kill gotShot;

	// So lange liegt ein Bot tot, bevor er neu erscheint.
	private const float BOT_RESPAWN_ZEIT = 3f;

	public string playerNameString => _playerName.Value.ToString();

	public bool istBot { get; private set; }

	// Der Mensch an diesem PC. Bots gehoeren dem Host (IsOwner), sind aber nicht lokal.
	public bool istLokal => base.IsOwner && !istBot;

	private void Awake()
	{
		_playerName = new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
		kills = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
		deaths = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
		isDead = new NetworkVariable<bool>(value: false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
		leben = new NetworkVariable<int>(Waffen.MAX_LEBEN, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
		team = new NetworkVariable<int>(Teams.KEINS, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
		istBot = GetComponent<BotSteuerung>() != null;
		gotShot += UIGameManager.Instance.gotShot;
		shotPlayer += UIGameManager.Instance.shotPlayer;
		characterController = GetComponent<CharacterController>();
	}

	private void OnEnable()
	{
		_playerName.OnValueChanged += nameArrived;
		team.OnValueChanged += teamGeaendert;
	}

	private void nameArrived(FixedString64Bytes previousName, FixedString64Bytes newName)
	{
		if (previousName.Value.ToString() == "")
		{
			if (base.IsOwner)
			{
				string text = PlayersManager.Instance.addPlayerOwnerSide(this, newName.Value.ToString());
				_playerName.Value = text;
				base.name = text;
				if (nameLabel != null)
				{
					nameLabel.text = text;
				}
			}
			else
			{
				PlayersManager.Instance.addPlayerClientSide(this);
				base.name = newName.Value.ToString();
				nameLabel.text = newName.Value.ToString();
			}
		}
		else if (previousName != newName)
		{
			base.name = newName.Value.ToString();
			if (nameLabel != null)
			{
				nameLabel.text = newName.Value.ToString();
			}
		}
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		if (base.IsServer)
		{
			team.Value = Runde.Instance != null ? Runde.Instance.TeamZuteilen(this) : Teams.KEINS;
		}
		if (!base.IsOwner)
		{
			base.gameObject.layer = 8;
			body.layer = 9;
			if (playerNameString != "")
			{
				nameArrived(new FixedString64Bytes(""), playerNameString);
			}
		}
		else if (istBot)
		{
			_playerName.Value = new FixedString64Bytes(GetComponent<BotSteuerung>().botName);
		}
		else
		{
			ownerPlayer = this;
			_playerName.Value = new FixedString64Bytes(Manager.Instance.currentPlayerName);
			UnityEngine.Object.Destroy(nameLabel.gameObject);
		}
		teamGeaendert(Teams.KEINS, team.Value);
	}

	public override void OnNetworkDespawn()
	{
		PlayersManager.Instance.removePlayer(this);
		base.OnNetworkDespawn();
	}

	private void teamGeaendert(int vorher, int jetzt)
	{
		// Koerper und Namensschild in Teamfarbe. Ohne Team bleibt alles wie im Original.
		Renderer koerper = body != null ? body.GetComponent<Renderer>() : null;
		if (koerper != null)
		{
			MaterialPropertyBlock block = new MaterialPropertyBlock();
			koerper.GetPropertyBlock(block);
			block.SetColor("_Color", Teams.Farbe(jetzt));
			koerper.SetPropertyBlock(block);
		}
		if (nameLabel != null)
		{
			nameLabel.color = Teams.SchriftFarbe(jetzt);
		}
	}

	public void spawn()
	{
		// Im Teammodus im Bereich des eigenen Teams, sonst irgendwo.
		int spawnTeam = Runde.Instance != null && Runde.Instance.teamModus.Value ? team.Value : Teams.KEINS;
		Vector3 location = Spawnlocation.getLocation(spawnTeam);
		// Blick zur Mitte der Map. Im Original stand hier LookRotation(-(0, y, 0)), also
		// senkrecht nach unten, und die Blickrichtung nach dem Respawn war zufaellig.
		Vector3 zurMitte = Spawnlocation.getMitte() - location;
		zurMitte.y = 0f;
		Quaternion drehung = zurMitte.sqrMagnitude > 0.01f ? Quaternion.LookRotation(zurMitte) : base.transform.rotation;
		// CharacterController kurz aus, sonst setzt er die alte Position zurueck.
		bool controllerAn = characterController.enabled;
		characterController.enabled = false;
		base.transform.SetPositionAndRotation(location, drehung);
		characterController.enabled = controllerAn;
		// Ohne Teleport gleiten die anderen die Figur quer durch die Map an den neuen Ort.
		NetworkTransform netzTransform = GetComponent<NetworkTransform>();
		if (netzTransform != null && base.IsSpawned)
		{
			try
			{
				netzTransform.Teleport(location, drehung, base.transform.localScale);
			}
			catch (Exception fehler)
			{
				Debug.LogWarning("Teleport nicht moeglich: " + fehler.Message);
			}
		}
		if (istLokal)
		{
			GetComponent<playerlooking>().blickAusTransform();
		}
	}

	// Erster Spawn nach dem Beitreten: Im Teammodus auf die Teamzuteilung des Servers warten,
	// sonst landet man ohne Team irgendwo, womoeglich beim Gegner.
	public void ersterSpawn()
	{
		StartCoroutine(aufTeamWarten());
	}

	private IEnumerator aufTeamWarten()
	{
		float bis = Time.time + 3f;
		while (Runde.Instance != null && Runde.Instance.teamModus.Value && team.Value == Teams.KEINS && Time.time < bis)
		{
			yield return null;
		}
		spawn();
	}

	public void shotOtherPlayer(string hitPlayer)
	{
		kills.Value++;
		if (istLokal)
		{
			shotPlayer?.Invoke(this, hitPlayer);
		}
	}

	public void gotHit(string shooter, string victim)
	{
		// Fuer alle sichtbar, bevor der Besitzer die Figur zum neuen Spawnpunkt setzt.
		Todeseffekt.Zeigen(base.transform.position + Vector3.up * 0.3f, todesMaterial);
		if (istBot && base.IsOwner)
		{
			StartCoroutine(botTot());
		}
		else if (base.IsOwner)
		{
			isDead.Value = true;
			deaths.Value++;
			gotShot?.Invoke(this, shooter);
			spawn();
			StartCoroutine(ownerPlayerDead());
		}
		else
		{
			StartCoroutine(foreignPlayerDead());
		}
	}

	public void respawn()
	{
		isDead.Value = false;
	}

	private IEnumerator ownerPlayerDead()
	{
		Manager.Instance.stopGame();
		body.SetActive(value: false);
		laserPointer.SetActive(value: false);
		while (isDead.Value)
		{
			yield return null;
		}
		body.SetActive(value: true);
		laserPointer.SetActive(value: true);
		Manager.Instance.continueGame();
	}

	// Wie foreignPlayerDead, nur auf dem Host fuer seine Bots, mit automatischem Respawn.
	private IEnumerator botTot()
	{
		isDead.Value = true;
		deaths.Value++;
		body.SetActive(value: false);
		nameLabel.enabled = false;
		laserPointer.SetActive(value: false);
		characterController.enabled = false;
		yield return new WaitForSeconds(BOT_RESPAWN_ZEIT);
		spawn();
		body.SetActive(value: true);
		nameLabel.enabled = true;
		laserPointer.SetActive(value: true);
		characterController.enabled = true;
		isDead.Value = false;
	}

	private IEnumerator foreignPlayerDead()
	{
		body.SetActive(value: false);
		nameLabel.enabled = false;
		laserPointer.SetActive(value: false);
		characterController.enabled = false;
		if (!isDead.Value)
		{
			while (!isDead.Value)
			{
				yield return null;
			}
		}
		while (isDead.Value)
		{
			yield return null;
		}
		body.SetActive(value: true);
		nameLabel.enabled = true;
		laserPointer.SetActive(value: true);
		characterController.enabled = true;
	}

	private void OnDisable()
	{
		if (istLokal && Manager.Instance.gamestate != Manager.GameState.Lobby)
		{
			Manager.Instance.connectionLost();
		}
	}

}
