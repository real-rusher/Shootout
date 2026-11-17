using System;
using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Playermanager : NetworkBehaviour
{
	public delegate void Kill(Playermanager thisManager, string affectedManager);

	public NetworkVariable<FixedString64Bytes> _playerName;

	public NetworkVariable<int> kills;

	public NetworkVariable<int> deaths;

	public NetworkVariable<bool> isDead;

	public TMP_Text nameLabel;

	public GameObject body;

	public GameObject laserPointer;

	private CharacterController characterController;

	public static Playermanager ownerPlayer;

	public Kill shotPlayer;

	public Kill gotShot;

	public string playerNameString => _playerName.Value.ToString();

	private void Awake()
	{
		_playerName = new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
		kills = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
		deaths = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
		isDead = new NetworkVariable<bool>(value: false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
		gotShot = (Kill)Delegate.Combine(gotShot, new Kill(UIGameManager.Instance.gotShot));
		shotPlayer = (Kill)Delegate.Combine(shotPlayer, new Kill(UIGameManager.Instance.shotPlayer));
		characterController = GetComponent<CharacterController>();
	}

	private void OnEnable()
	{
		NetworkVariable<FixedString64Bytes> playerName = _playerName;
		playerName.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(playerName.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(nameArrived));
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
			Debug.Log("f");
			base.name = newName.Value.ToString();
			if (!base.IsOwner)
			{
				nameLabel.text = newName.Value.ToString();
			}
		}
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		if (!base.IsOwner)
		{
			base.gameObject.layer = 8;
			body.layer = 9;
			if (playerNameString != "")
			{
				nameArrived(new FixedString64Bytes(""), playerNameString);
			}
		}
		else
		{
			ownerPlayer = this;
			_playerName.Value = new FixedString64Bytes(Manager.Instance.currentPlayerName);
			UnityEngine.Object.Destroy(nameLabel.gameObject);
		}
	}

	public override void OnNetworkDespawn()
	{
		PlayersManager.Instance.removePlayer(this);
		base.OnNetworkDespawn();
	}

	public void spawn()
	{
		Vector3 location = Spawnlocation.getLocation();
		base.transform.position = location;
		base.transform.rotation = Quaternion.LookRotation(-new Vector3(0f, location.y, 0f));
	}

	public void shotOtherPlayer(string hitPlayer)
	{
		kills.Value++;
		shotPlayer?.Invoke(this, hitPlayer);
	}

	public void gotHit(string shooter, string victim)
	{
		if (base.IsOwner)
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
		if (base.IsOwner && Manager.Instance.gamestate != 0)
		{
			Manager.Instance.connectionLost();
		}
	}

	internal override string __getTypeName()
	{
		return "Playermanager";
	}
}
