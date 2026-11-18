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

	private Playermanager playermanager;

	private Camera kamera;

	// Original las hier immer den Zustand des eigenen Spielers, auch auf fremden Objekten.
	public bool _isDead => playermanager.isDead.Value;

	private void Awake()
	{
		playermanager = GetComponent<Playermanager>();
		kamera = cameraTransform.GetComponent<Camera>();
	}

	private void Start()
	{
		playerInput = Manager.Instance.playerInput;
		lookAction = playerInput.actions["Look"];
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		if (playermanager.istLokal)
		{
			switchCameraTo(cameraTransform.gameObject);
			playermanager.gotShot += cameraStartZoom;
			blickAusTransform();
		}
		else
		{
			// Nur der eigene Spieler hoert zu. Sonst gaebe es pro Spieler einen AudioListener.
			AudioListener zuhoerer = cameraTransform.GetComponent<AudioListener>();
			if (zuhoerer != null)
			{
				zuhoerer.enabled = false;
			}
		}
	}

	private void Update()
	{
		if (playermanager.istLokal && !_isDead)
		{
			updateLook();
		}
	}

	private void LateUpdate()
	{
		if (playermanager.istLokal)
		{
			updateLabels();
		}
	}

	private void cameraStartZoom(Playermanager pm, string otherPlayer)
	{
		setzeSichtfeld(Waffen.NORMALES_SICHTFELD);
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
		blickAusTransform();
	}

	// Uebernimmt die Drehung der Spielfigur als Blickrichtung (nach dem Spawn).
	public void blickAusTransform()
	{
		look.x = base.transform.eulerAngles.y;
		look.y = 0f;
		cameraTransform.localRotation = Quaternion.identity;
	}

	private void updateLook()
	{
		Vector2 vector = lookAction.ReadValue<Vector2>();
		// Beim Zoomen langsamer drehen, damit das Zielen gleich anfuehlt.
		float faktor = SettingsManager.c_Sensi * aktuellesSichtfeld() / Waffen.NORMALES_SICHTFELD;
		look.x += vector.x * faktor;
		look.y += vector.y * faktor * (SettingsManager.mausInvertiert ? -1f : 1f);
		look.y = Mathf.Clamp(look.y, -89f, 89f);
		base.transform.localRotation = Quaternion.Euler(0f, look.x, 0f);
		cameraTransform.localRotation = Quaternion.Euler(0f - look.y, 0f, 0f);
	}

	public void setzeSichtfeld(float grad)
	{
		if (kamera != null)
		{
			kamera.fieldOfView = grad;
		}
	}

	public float aktuellesSichtfeld()
	{
		return kamera != null ? kamera.fieldOfView : Waffen.NORMALES_SICHTFELD;
	}

	private void switchCameraTo(GameObject go)
	{
		go.GetComponent<Camera>().enabled = true;
	}

	private void updateLabels()
	{
		for (int i = 0; i < PlayersManager.Instance.players.Count; i++)
		{
			Playermanager anderer = PlayersManager.Instance.players[i];
			if (anderer != null && anderer.gameObject != base.gameObject && anderer.nameLabel != null)
			{
				anderer.nameLabel.transform.LookAt(cameraTransform);
			}
		}
	}

}
