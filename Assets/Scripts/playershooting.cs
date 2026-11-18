using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Rekonstruiert aus dem dekompilierten Build und um drei Waffen erweitert
// (Werte in Waffen.cs). Treffer, Schaden und Kills entscheidet der Server.
public class playershooting : NetworkBehaviour
{
	// Abklingzeit des Originals (0,7 s im Prefab). Gilt jetzt ueber Waffen.LISTE[GEWEHR].
	[SerializeField]
	private float coolDown;

	private float _timeSinceShot;

	private Playermanager playermanager;

	private playerlooking blick;

	private playermovment bewegung;

	private Transform cam;

	private PlayerInput playerInput;

	private InputAction fireAction;

	private AudioSource tonQuelle;

	public GameObject laser;

	// Aktuelle Waffe (Waffen.GEWEHR, SMG, SNIPER). Nur beim Besitzer gueltig.
	public int waffe { get; private set; }

	// Sniper-Zoomstufe: -1 = aus, sonst Index in Waffen.SNIPER_ZOOM_SICHTFELD.
	public int zoomStufe { get; private set; } = -1;

	public bool _isDead => playermanager.isDead.Value;

	// Wie weit ein Schuss ins Leere gezeichnet wird.
	private const float MAX_REICHWEITE = 600f;

	// Nur auf dem Server: Ist der Tod dieses Spielers schon gezaehlt? Bis er wieder lebt,
	// zaehlen weitere Treffer nicht. Sonst bringt Dauerfeuer mehrere Kills, weil weitere
	// Schuesse ankommen, bevor das Opfer selbst gemerkt hat, dass es tot ist.
	private bool _todGezaehlt;

	private void Awake()
	{
		playermanager = GetComponent<Playermanager>();
		blick = GetComponent<playerlooking>();
		bewegung = GetComponent<playermovment>();
		cam = blick.cameraTransform;
		tonQuelle = base.gameObject.AddComponent<AudioSource>();
		tonQuelle.playOnAwake = false;
		tonQuelle.spatialBlend = 1f;
		tonQuelle.rolloffMode = AudioRolloffMode.Linear;
		tonQuelle.minDistance = 3f;
		tonQuelle.maxDistance = 150f;
	}

	private void Start()
	{
		playerInput = Manager.Instance.playerInput;
		fireAction = playerInput.actions["Fire"];
		_timeSinceShot = 10f;
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		if (playermanager.istLokal)
		{
			// Eigene Schuesse ohne Raumklang, die anderen hoert man aus ihrer Richtung.
			tonQuelle.spatialBlend = 0f;
		}
		if (base.IsServer)
		{
			playermanager.isDead.OnValueChanged += todGeaendert;
		}
		waffeWaehlen(Waffen.GEWEHR);
	}

	public override void OnNetworkDespawn()
	{
		if (base.IsServer)
		{
			playermanager.isDead.OnValueChanged -= todGeaendert;
		}
		base.OnNetworkDespawn();
	}

	private void todGeaendert(bool vorher, bool jetzt)
	{
		// Wieder am Leben (Respawn): voll geheilt, der naechste toedliche Treffer zaehlt wieder.
		if (!jetzt)
		{
			_todGezaehlt = false;
			playermanager.leben.Value = Waffen.MAX_LEBEN;
		}
	}

	private void Update()
	{
		_timeSinceShot += Time.deltaTime;
		if (!playermanager.istLokal)
		{
			return;
		}
		if (Manager.Instance.gamestate != Manager.GameState.Game || _isDead)
		{
			if (zoomStufe >= 0 && _isDead)
			{
				setzeZoom(-1);
			}
			return;
		}
		waffenTasten();
		checkForShoot();
	}

	private void waffenTasten()
	{
		Keyboard tastatur = Keyboard.current;
		if (tastatur != null)
		{
			if (tastatur.digit1Key.wasPressedThisFrame) waffeWaehlen(Waffen.GEWEHR);
			if (tastatur.digit2Key.wasPressedThisFrame) waffeWaehlen(Waffen.SMG);
			if (tastatur.digit3Key.wasPressedThisFrame) waffeWaehlen(Waffen.SNIPER);
		}
		Mouse maus = Mouse.current;
		if (maus == null)
		{
			return;
		}
		float rad = maus.scroll.ReadValue().y;
		if (rad > 0.1f)
		{
			waffeWaehlen((waffe + Waffen.LISTE.Length - 1) % Waffen.LISTE.Length);
		}
		else if (rad < -0.1f)
		{
			waffeWaehlen((waffe + 1) % Waffen.LISTE.Length);
		}
		// Zoom wie bei Counter-Strike: jeder Rechtsklick eine Stufe weiter, dann wieder aus.
		if (waffe == Waffen.SNIPER && maus.rightButton.wasPressedThisFrame)
		{
			int naechste = zoomStufe + 1;
			setzeZoom(naechste >= Waffen.SNIPER_ZOOM_SICHTFELD.Length ? -1 : naechste);
		}
	}

	public void waffeWaehlen(int neueWaffe)
	{
		neueWaffe = Mathf.Clamp(neueWaffe, 0, Waffen.LISTE.Length - 1);
		waffe = neueWaffe;
		setzeZoom(-1);
		// Laserpointer je nach Waffe laenger oder kuerzer (Drehpunkt ist die Spitze).
		if (playermanager.laserPointer != null)
		{
			float laenge = Waffen.Hole(waffe).laenge;
			playermanager.laserPointer.transform.localScale = new Vector3(1f, 1f, laenge);
		}
	}

	private void setzeZoom(int stufe)
	{
		zoomStufe = stufe;
		if (playermanager.istLokal)
		{
			blick.setzeSichtfeld(stufe < 0 ? Waffen.NORMALES_SICHTFELD : Waffen.SNIPER_ZOOM_SICHTFELD[stufe]);
		}
	}

	private float aktuelleAbklingzeit()
	{
		// Dev-Funktion: Namen mit "!ad" schiessen mit jeder Waffe im Dauerfeuer.
		if (Waffen.HatDauerfeuer(playermanager.playerNameString))
		{
			return Waffen.DAUERFEUER_ABKLINGZEIT;
		}
		return Waffen.Hole(waffe).abklingzeit;
	}

	public void checkForShoot()
	{
		float abklingzeit = aktuelleAbklingzeit();
		UIGameManager.Instance.coolDown(_timeSinceShot / abklingzeit);
		if (fireAction.IsPressed() && _timeSinceShot >= abklingzeit)
		{
			_timeSinceShot = 0f;
			shoot();
		}
	}

	// Fuer Bots: schiesst, wenn die Waffe bereit ist.
	public bool botSchuss()
	{
		if (_isDead || _timeSinceShot < aktuelleAbklingzeit())
		{
			return false;
		}
		_timeSinceShot = 0f;
		shoot();
		return true;
	}

	private void shoot()
	{
		Waffen.Werte werte = Waffen.Hole(waffe);
		float streuung = Mathf.Lerp(werte.streuungStehen, werte.streuungLaufen, Mathf.Clamp01(bewegung.laufAnteil));
		// Ueber die Schulter zielt man auf den Punkt unter dem Fadenkreuz der Schulterkamera.
		Vector3 richtung = streuen(blick.zielRichtung(cam.position), streuung);
		Vector3 trefferpunkt = cam.position + richtung * MAX_REICHWEITE;
		RaycastHit treffer;
		if (ersterTreffer(cam.position, richtung, out treffer))
		{
			trefferpunkt = treffer.point;
			playershooting getroffen = treffer.collider.GetComponentInParent<playershooting>();
			if (getroffen != null)
			{
				playerHit(getroffen);
			}
		}
		// Den eigenen Laser sofort zeigen. Die anderen bekommen den Trefferpunkt ueber das Netz.
		zeigeLaser(trefferpunkt, waffe);
		laserEffectServerRPC(trefferpunkt, waffe);
	}

	private Vector3 streuen(Vector3 richtung, float grad)
	{
		if (grad <= 0f)
		{
			return richtung;
		}
		Vector2 zufall = UnityEngine.Random.insideUnitCircle * Mathf.Tan(grad * Mathf.Deg2Rad);
		return (richtung + cam.right * zufall.x + cam.up * zufall.y).normalized;
	}

	// Erster Treffer, der nicht zur eigenen Figur gehoert. Das Original verliess sich darauf,
	// dass der eigene Spieler auf "Ignore Raycast" liegt. Bots gehoeren aber dem Host und
	// muessen auch den Host treffen koennen, deshalb wird die eigene Figur hier ausgelassen.
	public bool ersterTreffer(Vector3 start, Vector3 richtung, out RaycastHit ergebnis)
	{
		int maske = LayerMask.GetMask("Default", "Enemy", "EnemyBody", "Ignore Raycast");
		RaycastHit[] treffer = Physics.RaycastAll(start, richtung, MAX_REICHWEITE, maske, QueryTriggerInteraction.Ignore);
		Array.Sort(treffer, (a, b) => a.distance.CompareTo(b.distance));
		for (int i = 0; i < treffer.Length; i++)
		{
			if (treffer[i].collider.transform.root != base.transform.root)
			{
				ergebnis = treffer[i];
				return true;
			}
		}
		ergebnis = default(RaycastHit);
		return false;
	}

	// Frueher wurde nur die Entfernung geschickt, und jeder PC rechnete den Strahl aus seiner
	// Kopie der Kamera des Schuetzen nach. Die wird ueber das Netz nachgefuehrt und weicht ab,
	// dann zeigte der Strahl woandershin. Jetzt geht der genaue Trefferpunkt mit.
	[ServerRpc(RequireOwnership = false)]
	public void laserEffectServerRPC(Vector3 trefferpunkt, int laserWaffe)
	{
		laserEffectClientRPC(trefferpunkt, laserWaffe);
	}

	[ClientRpc]
	public void laserEffectClientRPC(Vector3 trefferpunkt, int laserWaffe)
	{
		// Der Besitzer (Spieler oder Host bei Bots) hat den Laser schon selbst gezeigt.
		if (base.IsOwner)
		{
			return;
		}
		zeigeLaser(trefferpunkt, laserWaffe);
	}

	private void zeigeLaser(Vector3 trefferpunkt, int laserWaffe)
	{
		Waffen.Werte werte = Waffen.Hole(laserWaffe);
		Instantiate(laser).GetComponent<LaserEffect>().zeigeStrahl(playermanager.laserPointer.transform.position, trefferpunkt, werte.laserFarbe, werte.laserDicke);
		tonQuelle.PlayOneShot(Toene.Schuss(laserWaffe));
	}

	private void playerHit(playershooting hitPlayer)
	{
		// Den Treffer prueft der Server (Team, schon tot, Schaden). Der Schuetze wird ueber
		// seine Objekt-ID mitgeschickt, damit auch Bots (die dem Host gehoeren) richtig zaehlen.
		hitPlayer.playerShotServerRPC(base.NetworkObjectId, waffe);
	}

	// Laeuft am getroffenen Spieler auf dem Server.
	[ServerRpc(RequireOwnership = false)]
	public void playerShotServerRPC(ulong schuetzeId, int schussWaffe, ServerRpcParams rpcParams = default)
	{
		NetworkObject schuetzeObjekt;
		if (!base.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(schuetzeId, out schuetzeObjekt))
		{
			return;
		}
		// Nur wer den Schuetzen besitzt, darf in seinem Namen schiessen (Bots besitzt der Host).
		if (schuetzeObjekt.OwnerClientId != rpcParams.Receive.SenderClientId)
		{
			return;
		}
		Playermanager schuetze = schuetzeObjekt.GetComponent<Playermanager>();
		if (schuetze == null || schuetze == playermanager || schuetze.isDead.Value || _todGezaehlt || playermanager.isDead.Value)
		{
			return;
		}
		if (!Runde.DarfTreffen(schuetze, playermanager))
		{
			return;
		}
		playershooting schuetzeWaffe = schuetze.GetComponent<playershooting>();
		int rest = playermanager.leben.Value - Waffen.Hole(schussWaffe).schaden;
		if (rest > 0)
		{
			playermanager.leben.Value = rest;
			schuetzeWaffe.trefferBestaetigtClientRPC();
			return;
		}
		playermanager.leben.Value = 0;
		_todGezaehlt = true;
		receiveShotClientRPC(schuetze.playerNameString, playermanager.playerNameString);
		schuetzeWaffe.killBestaetigtClientRPC(playermanager.playerNameString);
		if (Runde.Instance != null)
		{
			Runde.Instance.killGezaehlt(schuetze);
		}
	}

	// Laeuft am Schuetzen: Treffer ohne Kill, nur fuer den Hitmarker.
	[ClientRpc]
	public void trefferBestaetigtClientRPC()
	{
		if (playermanager.istLokal)
		{
			UIGameManager.Instance.zeigeHitmarker();
			tonQuelle.PlayOneShot(Toene.Treffer());
		}
	}

	// Laeuft am Schuetzen. Nur sein Besitzer darf seine Kills hochzaehlen.
	[ClientRpc]
	public void killBestaetigtClientRPC(string victim)
	{
		if (base.IsOwner)
		{
			playermanager.shotOtherPlayer(victim);
			if (playermanager.istLokal)
			{
				tonQuelle.PlayOneShot(Toene.Kill());
			}
		}
	}

	[ClientRpc]
	public void receiveShotClientRPC(string shooter, string victim)
	{
		playermanager.gotHit(shooter, victim);
	}
}
