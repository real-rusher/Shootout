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

	// Zweite Kamera fuer die Sicht ueber die rechte Schulter (Einstellung "Third Person").
	private Camera schulterKamera;

	private playershooting schiessen;

	// Lage der Schulterkamera hinter dem Kopf: rechts, etwas hoeher, nach hinten (zum Einstellen).
	private static readonly Vector3 SCHULTER_VERSATZ = new Vector3(0.75f, 0.3f, -2.6f);

	// Abstand der Kamera zu Waenden, damit sie nicht hineinschaut.
	private const float SCHULTER_RADIUS = 0.2f;

	private const float ZIEL_REICHWEITE = 600f;

	// Fuer die Grafikstufe "Hoch": Ressourcen der Nachbearbeitung und Layer ihres Volumens
	// (traegt das Editor-Skript ein), damit auch die Schulterkamera sie bekommt.
	public UnityEngine.Rendering.PostProcessing.PostProcessResources nachbearbeitungRessourcen;

	public LayerMask nachbearbeitungLayer;

	// Original las hier immer den Zustand des eigenen Spielers, auch auf fremden Objekten.
	public bool _isDead => playermanager.isDead.Value;

	private void Awake()
	{
		playermanager = GetComponent<Playermanager>();
		schiessen = GetComponent<playershooting>();
		kamera = cameraTransform.GetComponent<Camera>();
	}

	// Third Person ist an, wenn eingestellt und nicht gerade mit dem Sniper gezoomt wird.
	public bool drittePersonAktiv => schulterKamera != null && SettingsManager.drittePerson && (schiessen == null || schiessen.zoomStufe < 0);

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
			GameObject schulter = new GameObject("Schulterkamera");
			schulter.transform.SetParent(cameraTransform, false);
			schulter.transform.localPosition = SCHULTER_VERSATZ;
			schulterKamera = schulter.AddComponent<Camera>();
			schulterKamera.CopyFrom(kamera);
			schulterKamera.enabled = false;
			Grafik.KameraAusstatten(schulterKamera, nachbearbeitungRessourcen, nachbearbeitungLayer);
			Grafik.Anwenden();
			// Unterwassersicht am Objekt mit dem AudioListener, damit der Tondaempfer wirkt.
			cameraTransform.gameObject.AddComponent<UnterwasserSicht>().blick = this;
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
			kameraModusAktualisieren();
			updateLabels();
		}
	}

	private void kameraModusAktualisieren()
	{
		if (schulterKamera == null)
		{
			return;
		}
		bool schulter = drittePersonAktiv;
		kamera.enabled = !schulter;
		schulterKamera.enabled = schulter;
		if (!schulter)
		{
			return;
		}
		schulterKamera.fieldOfView = kamera.fieldOfView;
		// Hinter Waenden naeher heran, sonst saehe man durch die Wand.
		Vector3 drehpunkt = cameraTransform.position;
		Vector3 wunsch = cameraTransform.TransformPoint(SCHULTER_VERSATZ);
		Vector3 richtung = wunsch - drehpunkt;
		float laenge = richtung.magnitude;
		float frei = laenge;
		foreach (RaycastHit treffer in Physics.SphereCastAll(drehpunkt, SCHULTER_RADIUS, richtung / laenge, laenge, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
		{
			if (treffer.collider.transform.root != base.transform.root && treffer.distance < frei)
			{
				frei = treffer.distance;
			}
		}
		schulterKamera.transform.position = drehpunkt + richtung / laenge * Mathf.Max(0f, frei - 0.05f);
		schulterKamera.transform.rotation = cameraTransform.rotation;
	}

	// Position der Kamera, durch die man gerade schaut.
	public Vector3 aktiveKameraPosition()
	{
		return drittePersonAktiv ? schulterKamera.transform.position : cameraTransform.position;
	}

	// Wohin ein Schuss von start aus gehen muss, um das Fadenkreuz zu treffen. In der Ich-Sicht
	// einfach geradeaus, ueber die Schulter zum Punkt, auf den die Schulterkamera zeigt.
	public Vector3 zielRichtung(Vector3 start)
	{
		if (!drittePersonAktiv)
		{
			return cameraTransform.forward;
		}
		Transform blickpunkt = schulterKamera.transform;
		Vector3 ziel = blickpunkt.position + blickpunkt.forward * ZIEL_REICHWEITE;
		RaycastHit[] treffer = Physics.RaycastAll(blickpunkt.position, blickpunkt.forward, ZIEL_REICHWEITE, LayerMask.GetMask("Default", "Enemy", "EnemyBody", "Ignore Raycast"), QueryTriggerInteraction.Ignore);
		float naechster = float.MaxValue;
		foreach (RaycastHit t in treffer)
		{
			if (t.collider.transform.root != base.transform.root && t.distance < naechster)
			{
				naechster = t.distance;
				ziel = t.point;
			}
		}
		return (ziel - start).normalized;
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
