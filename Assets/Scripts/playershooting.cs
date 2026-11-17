using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Rekonstruiert aus dem dekompilierten Build. Die RPC-Methoden hatte der
// Netcode-IL-Postprocessor aufgeblaeht, hier stehen sie wieder in normaler Form.
public class playershooting : NetworkBehaviour
{
	[SerializeField]
	private float coolDown;

	private float _timeSinceShot;

	private Playermanager playermanager;

	private Transform cam;

	private PlayerInput playerInput;

	private InputAction fireAction;

	public GameObject laser;

	public bool _isDead => GetComponent<Playermanager>().isDead.Value;

	// Nur auf dem Server: Ist der Tod dieses Spielers schon gezaehlt? Bis er wieder lebt,
	// zaehlen weitere Treffer nicht. Sonst bringt Dauerfeuer mehrere Kills, weil weitere
	// Schuesse ankommen, bevor das Opfer selbst gemerkt hat, dass es tot ist.
	private bool _todGezaehlt;

	private void Start()
	{
		playerInput = Manager.Instance.playerInput;
		fireAction = playerInput.actions["Fire"];
		playermanager = GetComponent<Playermanager>();
		cam = GetComponent<playerlooking>().cameraTransform;
		_timeSinceShot = coolDown;
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		if (base.IsServer)
		{
			GetComponent<Playermanager>().isDead.OnValueChanged += todGeaendert;
		}
	}

	public override void OnNetworkDespawn()
	{
		if (base.IsServer)
		{
			GetComponent<Playermanager>().isDead.OnValueChanged -= todGeaendert;
		}
		base.OnNetworkDespawn();
	}

	private void todGeaendert(bool vorher, bool jetzt)
	{
		// Wieder am Leben (Respawn): Der naechste Treffer zaehlt wieder.
		if (!jetzt)
		{
			_todGezaehlt = false;
		}
	}

	private void Update()
	{
		checkForShoot();
		_timeSinceShot += Time.deltaTime;
	}

	public void checkForShoot()
	{
		if (Manager.Instance.gamestate == Manager.GameState.Game && !_isDead && base.IsOwner)
		{
			UIGameManager.Instance.coolDown(_timeSinceShot / coolDown);
			// MOD: Hier stand vermutlich "_timeSinceShot >= coolDown" (coolDown = 0,7 s im Prefab).
			// Mit 0.01f feuert der Laser praktisch dauerhaft (Vollautomatik).
			if (fireAction.IsPressed() && _timeSinceShot >= 0.01f)
			{
				_timeSinceShot = 0f;
				shoot();
			}
		}
	}

	private void shoot()
	{
		LayerMask layerMask = LayerMask.GetMask("Default", "Enemy", "EnemyBody");
		if (Physics.Raycast(cam.position, cam.TransformDirection(Vector3.forward), out var hitInfo, float.PositiveInfinity, layerMask))
		{
			if (hitInfo.collider.gameObject.layer == 8)
			{
				playerHit(hitInfo.collider.GetComponent<playershooting>());
			}
			else if (hitInfo.collider.gameObject.layer == 9)
			{
				playerHit(hitInfo.collider.transform.parent.GetComponent<playershooting>());
			}
			laserEffectServerRPC(hitInfo.distance);
		}
	}

	[ServerRpc(RequireOwnership = false)]
	public void laserEffectServerRPC(float camDistance)
	{
		laserEffectClientRPC(camDistance);
	}

	[ClientRpc]
	public void laserEffectClientRPC(float camDistance)
	{
		Instantiate(laser).GetComponent<LaserEffect>().InitializeLaser(cam.position, cam.rotation, camDistance, playermanager.laserPointer.transform.position);
	}

	private void playerHit(playershooting hitPlayer)
	{
		// Original-Vergleich (playershooting == Playermanager) ist nie wahr, Selbsttreffer
		// verhindert in Wahrheit nur die Layer-Maske (eigener Spieler liegt auf Layer 2).
		if (!(hitPlayer == playermanager))
		{
			// Den Kill zaehlt der Schuetze erst, wenn der Server den Treffer annimmt
			// (killBestaetigtClientRPC). Treffer auf schon tote Spieler zaehlen nicht.
			hitPlayer.playerShotServerRPC(base.name, hitPlayer.name);
		}
	}

	// Laeuft am getroffenen Spieler auf dem Server.
	[ServerRpc(RequireOwnership = false)]
	public void playerShotServerRPC(string shooter, string victim, ServerRpcParams rpcParams = default)
	{
		if (_todGezaehlt)
		{
			return;
		}
		_todGezaehlt = true;
		receiveShotClientRPC(shooter, victim);
		// Den Schuetzen ueber seine Client-ID finden, nicht ueber den Namen (der kann sich aendern).
		NetworkClient schuetze;
		if (base.NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out schuetze) && schuetze.PlayerObject != null)
		{
			schuetze.PlayerObject.GetComponent<playershooting>().killBestaetigtClientRPC(victim);
		}
	}

	// Laeuft am Schuetzen. Nur sein Besitzer darf seine Kills hochzaehlen.
	[ClientRpc]
	public void killBestaetigtClientRPC(string victim)
	{
		if (base.IsOwner)
		{
			playermanager.shotOtherPlayer(victim);
		}
	}

	[ClientRpc]
	public void receiveShotClientRPC(string shooter, string victim)
	{
		playermanager.gotHit(shooter, victim);
	}
}
