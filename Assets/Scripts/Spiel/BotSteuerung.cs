using Unity.Netcode;
using UnityEngine;

// Einfache, absichtlich schlechte Bots. Laufen nur auf dem Server (Host), benutzen aber
// dieselbe Bewegung (playermovment) und dieselben Waffen (playershooting) wie Menschen.
public class BotSteuerung : NetworkBehaviour
{
	public string botName = "Bot";

	// Eingaben fuer playermovment.
	internal Vector2 bewegung;

	// Ein paar Zehntelsekunden gedrueckt halten, sonst verpasst playermovment den Sprung je
	// nachdem, welches Update zuerst laeuft.
	internal bool springen => Time.time < springenBis;

	private float springenBis;

	// --- Wie schlecht die Bots sind (zum Einstellen) ---

	// Wie weit sie Gegner sehen.
	private const float SICHTWEITE = 70f;

	// Zeit vom Entdecken bis zum ersten Schuss (Sekunden, zufaellig dazwischen).
	private const float REAKTION_MIN = 0.6f;

	private const float REAKTION_MAX = 1.2f;

	// Wie schnell sie sich drehen (Grad pro Sekunde).
	private const float DREHTEMPO = 140f;

	// Wackeln beim Zielen (Grad).
	private const float ZIELFEHLER = 4f;

	// Ab dieser Abweichung vom Ziel (Grad) schiessen sie.
	private const float SCHUSSWINKEL = 7f;

	private Playermanager playermanager;

	private playershooting schiessen;

	private playerlooking blick;

	private Playermanager ziel;

	private float reaktionBis;

	private float naechsteZielsuche;

	private Vector3 wanderZiel;

	private float wanderBis;

	private float seitwaerts;

	private float seitwaertsBis;

	private Vector3 letztePosition;

	private float steckPruefung;

	private float gier;

	private float nick;

	private bool gespawnt;

	private float rauschenStart;

	private void Awake()
	{
		playermanager = GetComponent<Playermanager>();
		schiessen = GetComponent<playershooting>();
		blick = GetComponent<playerlooking>();
		rauschenStart = Random.value * 100f;
	}

	private void Update()
	{
		if (!base.IsServer || !base.IsSpawned)
		{
			return;
		}
		if (!gespawnt)
		{
			// Erst im ersten Update: Dann hat der Server das Team schon zugeteilt.
			gespawnt = true;
			playermanager.spawn();
			gier = base.transform.eulerAngles.y;
			schiessen.waffeWaehlen(Random.Range(0, Waffen.LISTE.Length));
			letztePosition = base.transform.position;
		}
		if (playermanager.isDead.Value)
		{
			bewegung = Vector2.zero;
			ziel = null;
			return;
		}
		if (Time.time >= naechsteZielsuche)
		{
			naechsteZielsuche = Time.time + 0.3f;
			zielSuchen();
		}
		if (ziel != null)
		{
			kaempfen();
		}
		else
		{
			wandern();
		}
		steckenPruefen();
		base.transform.rotation = Quaternion.Euler(0f, gier, 0f);
		blick.cameraTransform.localRotation = Quaternion.Euler(0f - nick, 0f, 0f);
	}

	private void zielSuchen()
	{
		Playermanager bisher = ziel;
		ziel = null;
		float besteEntfernung = SICHTWEITE;
		foreach (Playermanager anderer in PlayersManager.Instance.players)
		{
			if (anderer == null || anderer == playermanager || anderer.isDead.Value || !Runde.DarfTreffen(playermanager, anderer))
			{
				continue;
			}
			float entfernung = Vector3.Distance(base.transform.position, anderer.transform.position);
			if (entfernung < besteEntfernung && sichtbar(anderer))
			{
				besteEntfernung = entfernung;
				ziel = anderer;
			}
		}
		if (ziel != null && ziel != bisher)
		{
			reaktionBis = Time.time + Random.Range(REAKTION_MIN, REAKTION_MAX);
		}
	}

	private bool sichtbar(Playermanager anderer)
	{
		Vector3 start = blick.cameraTransform.position;
		Vector3 richtung = anderer.transform.position + Vector3.up * 0.5f - start;
		RaycastHit treffer;
		return schiessen.ersterTreffer(start, richtung.normalized, out treffer) && treffer.collider.transform.root == anderer.transform.root;
	}

	private void kaempfen()
	{
		Vector3 start = blick.cameraTransform.position;
		Vector3 zielPunkt = ziel.transform.position + Vector3.up * 0.4f;
		Vector3 richtung = zielPunkt - start;
		// Langsames Wackeln, damit sie nicht perfekt treffen.
		float zeit = Time.time * 0.8f + rauschenStart;
		float fehlerGier = (Mathf.PerlinNoise(zeit, 0f) - 0.5f) * 2f * ZIELFEHLER;
		float fehlerNick = (Mathf.PerlinNoise(0f, zeit) - 0.5f) * 2f * ZIELFEHLER;
		float sollGier = Mathf.Atan2(richtung.x, richtung.z) * Mathf.Rad2Deg + fehlerGier;
		float sollNick = Mathf.Atan2(richtung.y, new Vector2(richtung.x, richtung.z).magnitude) * Mathf.Rad2Deg + fehlerNick;
		gier = Mathf.MoveTowardsAngle(gier, sollGier, DREHTEMPO * Time.deltaTime);
		nick = Mathf.MoveTowards(nick, Mathf.Clamp(sollNick, -80f, 80f), DREHTEMPO * Time.deltaTime);

		// Seitlich ausweichen, ab und zu die Richtung wechseln. Weit weg: naeher ran.
		if (Time.time >= seitwaertsBis)
		{
			seitwaertsBis = Time.time + Random.Range(0.8f, 2f);
			seitwaerts = Random.Range(-1, 2);
		}
		float vorwaerts = richtung.magnitude > 25f ? 1f : (richtung.magnitude < 8f ? -0.5f : 0f);
		bewegung = new Vector2(seitwaerts, vorwaerts);

		float abweichung = Mathf.Abs(Mathf.DeltaAngle(gier, sollGier - fehlerGier)) + Mathf.Abs(nick - (sollNick - fehlerNick));
		if (Time.time >= reaktionBis && abweichung < SCHUSSWINKEL + ZIELFEHLER)
		{
			schiessen.botSchuss();
		}
	}

	private void wandern()
	{
		if (Time.time >= wanderBis || flach(wanderZiel - base.transform.position).magnitude < 3f)
		{
			// Irgendein Spawnpunkt als Ziel: Die liegen ueber die ganze Map verteilt.
			wanderZiel = Spawnlocation.getLocation(0);
			wanderBis = Time.time + Random.Range(8f, 15f);
		}
		Vector3 richtung = flach(wanderZiel - base.transform.position);
		if (richtung.sqrMagnitude > 0.01f)
		{
			float sollGier = Mathf.Atan2(richtung.x, richtung.z) * Mathf.Rad2Deg;
			gier = Mathf.MoveTowardsAngle(gier, sollGier, DREHTEMPO * Time.deltaTime);
		}
		nick = Mathf.MoveTowards(nick, 0f, DREHTEMPO * Time.deltaTime);
		bewegung = new Vector2(0f, 1f);
	}

	// Steckt der Bot fest, springt er und sucht sich ein neues Ziel.
	private void steckenPruefen()
	{
		if (Time.time < steckPruefung)
		{
			return;
		}
		steckPruefung = Time.time + 1f;
		bool willLaufen = bewegung.sqrMagnitude > 0.1f;
		if (willLaufen && flach(base.transform.position - letztePosition).magnitude < 1f)
		{
			springenBis = Time.time + 0.25f;
			wanderBis = 0f;
			gier += Random.Range(-120f, 120f);
		}
		letztePosition = base.transform.position;
	}

	private static Vector3 flach(Vector3 v)
	{
		v.y = 0f;
		return v;
	}
}
