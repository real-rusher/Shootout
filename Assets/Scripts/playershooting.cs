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

	// Dev-Funktion: Wer diese Zeichen im Namen hat (Gross- und Kleinschreibung egal),
	// schiesst im Dauerfeuer. Alle anderen haben die normale Abklingzeit (coolDown).
	private const string DAUERFEUER_KENNUNG = "!ad";

	private const float DAUERFEUER_ABKLINGZEIT = 0.01f;

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
			// Im Mod stand hier fuer alle 0.01f (Dauerfeuer). Jetzt nur noch fuer Namen mit
			// DAUERFEUER_KENNUNG, sonst gilt coolDown aus dem Prefab (0,7 s).
			float abklingzeit = hatDauerfeuer() ? DAUERFEUER_ABKLINGZEIT : coolDown;
			UIGameManager.Instance.coolDown(_timeSinceShot / abklingzeit);
			if (fireAction.IsPressed() && _timeSinceShot >= abklingzeit)
			{
				_timeSinceShot = 0f;
				shoot();
			}
		}
	}

	private bool hatDauerfeuer()
	{
		return playermanager.playerNameString.IndexOf(DAUERFEUER_KENNUNG, System.StringComparison.OrdinalIgnoreCase) >= 0;
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
			// Den eigenen Laser sofort zeigen. Die anderen bekommen den Trefferpunkt ueber das Netz.
			zeigeLaser(hitInfo.point);
			laserEffectServerRPC(hitInfo.point);
		}
	}

	// Frueher wurde nur die Entfernung geschickt, und jeder PC rechnete den Strahl aus seiner
	// Kopie der Kamera des Schuetzen nach. Die wird ueber das Netz nachgefuehrt und weicht ab,
	// dann zeigte der Strahl woandershin. Jetzt geht der genaue Trefferpunkt mit.
	[ServerRpc(RequireOwnership = false)]
	public void laserEffectServerRPC(Vector3 trefferpunkt)
	{
		laserEffectClientRPC(trefferpunkt);
	}

	[ClientRpc]
	public void laserEffectClientRPC(Vector3 trefferpunkt)
	{
		// Der Schuetze hat seinen Laser schon selbst gezeigt.
		if (base.IsOwner)
		{
			return;
		}
		zeigeLaser(trefferpunkt);
	}

	private void zeigeLaser(Vector3 trefferpunkt)
	{
		Instantiate(laser).GetComponent<LaserEffect>().zeigeStrahl(playermanager.laserPointer.transform.position, trefferpunkt);
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
