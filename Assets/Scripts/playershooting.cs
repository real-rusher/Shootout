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

	private void Start()
	{
		playerInput = Manager.Instance.playerInput;
		fireAction = playerInput.actions["Fire"];
		playermanager = GetComponent<Playermanager>();
		cam = GetComponent<playerlooking>().cameraTransform;
		_timeSinceShot = coolDown;
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
			playermanager.shotOtherPlayer(hitPlayer.name);
			hitPlayer.playerShotServerRPC(base.name, hitPlayer.name);
		}
	}

	[ServerRpc(RequireOwnership = false)]
	public void playerShotServerRPC(string shooter, string victim)
	{
		receiveShotClientRPC(shooter, victim);
	}

	[ClientRpc]
	public void receiveShotClientRPC(string shooter, string victim)
	{
		playermanager.gotHit(shooter, victim);
	}
}
