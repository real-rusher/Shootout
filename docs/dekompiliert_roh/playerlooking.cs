using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class playerlooking : NetworkBehaviour
{
	public Transform cameraTransform;

	public float zoomOutSpeed = 1f;

	public float zoomOutDistance = 1f;

	private PlayerInput playerInput;

	private InputAction lookAction;

	private Vector2 look;

	public bool _isDead => Playermanager.ownerPlayer.isDead.Value;

	private void Start()
	{
		playerInput = Manager.Instance.playerInput;
		lookAction = playerInput.actions["Look"];
		if (base.IsOwner)
		{
			switchCameraTo(cameraTransform.gameObject);
			Playermanager ownerPlayer = Playermanager.ownerPlayer;
			ownerPlayer.gotShot = (Playermanager.Kill)Delegate.Combine(ownerPlayer.gotShot, new Playermanager.Kill(cameraStartZoom));
		}
	}

	private void Update()
	{
		if (base.IsOwner && !_isDead)
		{
			updateLook();
		}
	}

	private void LateUpdate()
	{
		if (base.IsOwner)
		{
			updateLabels();
		}
	}

	private void cameraStartZoom(Playermanager pm, string otherPlayer)
	{
		StartCoroutine(cameraZoomAnim(pm, otherPlayer));
	}

	private IEnumerator cameraZoomAnim(Playermanager pm, string otherPlayer)
	{
		Vector3 cameraGlobalPos = cameraTransform.position;
		Quaternion cameraGlobalRot = cameraTransform.rotation;
		float timeSinceDeath = 0f;
		while (_isDead)
		{
			timeSinceDeath += Time.deltaTime;
			float num = (0f - Mathf.Exp((0f - timeSinceDeath) * zoomOutSpeed) + 1f) * zoomOutDistance;
			cameraTransform.position = cameraGlobalPos - num * cameraTransform.forward;
			cameraTransform.rotation = cameraGlobalRot;
			yield return null;
		}
		cameraTransform.position = base.transform.position + new Vector3(0f, 0.88f, 0f);
		lookTowardsCenter();
	}

	private void lookTowardsCenter()
	{
		look.x = base.transform.localRotation.eulerAngles.y;
		look.y = 0f;
	}

	private void updateLook()
	{
		Vector2 vector = lookAction.ReadValue<Vector2>();
		look.x += vector.x * SettingsManager.c_Sensi;
		look.y += vector.y * SettingsManager.c_Sensi;
		look.y = Mathf.Clamp(look.y, -89f, 89f);
		base.transform.localRotation = Quaternion.Euler(0f, look.x, 0f);
		cameraTransform.localRotation = Quaternion.Euler(0f - look.y, 0f, 0f);
	}

	private void switchCameraTo(GameObject go)
	{
		go.GetComponent<Camera>().enabled = true;
	}

	private void updateLabels()
	{
		for (int i = 0; i < PlayersManager.Instance.players.Count; i++)
		{
			if (PlayersManager.Instance.players[i].gameObject != base.gameObject)
			{
				PlayersManager.Instance.players[i].nameLabel.transform.LookAt(cameraTransform);
			}
		}
	}

	internal override string __getTypeName()
	{
		return "playerlooking";
	}
}
