using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
			{
				ServerRpcParams serverRpcParams = default(ServerRpcParams);
				FastBufferWriter bufferWriter = __beginSendServerRpc(441498462u, serverRpcParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in camDistance, default(FastBufferWriter.ForPrimitives));
				__endSendServerRpc(ref bufferWriter, 441498462u, serverRpcParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
			{
				laserEffectClientRPC(camDistance);
			}
		}
	}

	[ClientRpc]
	public void laserEffectClientRPC(float camDistance)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
			{
				ClientRpcParams clientRpcParams = default(ClientRpcParams);
				FastBufferWriter bufferWriter = __beginSendClientRpc(811653730u, clientRpcParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in camDistance, default(FastBufferWriter.ForPrimitives));
				__endSendClientRpc(ref bufferWriter, 811653730u, clientRpcParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
			{
				Object.Instantiate(laser).GetComponent<LaserEffect>().InitializeLaser(cam.position, cam.rotation, camDistance, playermanager.laserPointer.transform.position);
			}
		}
	}

	private void playerHit(playershooting hitPlayer)
	{
		if (!(hitPlayer == playermanager))
		{
			playermanager.shotOtherPlayer(hitPlayer.name);
			hitPlayer.playerShotServerRPC(base.name, hitPlayer.name);
		}
	}

	[ServerRpc(RequireOwnership = false)]
	public void playerShotServerRPC(string shooter, string victim)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
		{
			ServerRpcParams serverRpcParams = default(ServerRpcParams);
			FastBufferWriter bufferWriter = __beginSendServerRpc(1432046681u, serverRpcParams, RpcDelivery.Reliable);
			bool value = shooter != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(shooter);
			}
			bool value2 = victim != null;
			bufferWriter.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
			if (value2)
			{
				bufferWriter.WriteValueSafe(victim);
			}
			__endSendServerRpc(ref bufferWriter, 1432046681u, serverRpcParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
		{
			receiveShotClientRPC(shooter, victim);
		}
	}

	[ClientRpc]
	public void receiveShotClientRPC(string shooter, string victim)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
		{
			ClientRpcParams clientRpcParams = default(ClientRpcParams);
			FastBufferWriter bufferWriter = __beginSendClientRpc(4196204138u, clientRpcParams, RpcDelivery.Reliable);
			bool value = shooter != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(shooter);
			}
			bool value2 = victim != null;
			bufferWriter.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
			if (value2)
			{
				bufferWriter.WriteValueSafe(victim);
			}
			__endSendClientRpc(ref bufferWriter, 4196204138u, clientRpcParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
		{
			playermanager.gotHit(shooter, victim);
		}
	}

	static playershooting()
	{
		NetworkManager.__rpc_func_table.Add(441498462u, __rpc_handler_441498462);
		NetworkManager.__rpc_func_table.Add(811653730u, __rpc_handler_811653730);
		NetworkManager.__rpc_func_table.Add(1432046681u, __rpc_handler_1432046681);
		NetworkManager.__rpc_func_table.Add(4196204138u, __rpc_handler_4196204138);
	}

	private static void __rpc_handler_441498462(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out float value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Server;
			((playershooting)target).laserEffectServerRPC(value);
			target.__rpc_exec_stage = __RpcExecStage.None;
		}
	}

	private static void __rpc_handler_811653730(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out float value, default(FastBufferWriter.ForPrimitives));
			target.__rpc_exec_stage = __RpcExecStage.Client;
			((playershooting)target).laserEffectClientRPC(value);
			target.__rpc_exec_stage = __RpcExecStage.None;
		}
	}

	private static void __rpc_handler_1432046681(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			string s = null;
			if (value)
			{
				reader.ReadValueSafe(out s, oneByteChars: false);
			}
			reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
			string s2 = null;
			if (value2)
			{
				reader.ReadValueSafe(out s2, oneByteChars: false);
			}
			target.__rpc_exec_stage = __RpcExecStage.Server;
			((playershooting)target).playerShotServerRPC(s, s2);
			target.__rpc_exec_stage = __RpcExecStage.None;
		}
	}

	private static void __rpc_handler_4196204138(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			string s = null;
			if (value)
			{
				reader.ReadValueSafe(out s, oneByteChars: false);
			}
			reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
			string s2 = null;
			if (value2)
			{
				reader.ReadValueSafe(out s2, oneByteChars: false);
			}
			target.__rpc_exec_stage = __RpcExecStage.Client;
			((playershooting)target).receiveShotClientRPC(s, s2);
			target.__rpc_exec_stage = __RpcExecStage.None;
		}
	}

	internal override string __getTypeName()
	{
		return "playershooting";
	}
}
