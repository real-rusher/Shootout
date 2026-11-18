using Unity.Netcode;
using UnityEngine;

// Einfache, absichtlich schlechte Bots. Laufen nur auf dem Server (Host), benutzen aber
// dieselbe Bewegung (playermovment) und dieselben Waffen (playershooting) wie Menschen.
//
// Verhalten: Sieht der Bot einen Gegner, kaempft er (zielt mit Wackeln, schiesst nach einer
// Reaktionszeit, weicht seitlich aus). Sonst geht er auf den naechsten Gegner zu, auf Maps mit
// Hoehenunterschied ueber die Wege aus BotWege (D-Day: die Schluchten).
public class BotSteuerung : NetworkBehaviour
{
	public string botName = "Bot";

	// Eingaben fuer playermovment.
	internal Vector2 bewegung;

	// Ein paar Zehntelsekunden gedrueckt halten, sonst verpasst playermovment den Sprung je
	// nachdem, welches Update zuerst laeuft.
	internal bool springen => Time.time < springenBis;

	// --- Wie gut die Bots sind (zum Einstellen) ---

	// Wie weit sie Gegner sehen.
	private const float SICHTWEITE = 70f;

	// Zeit vom Entdecken bis zum ersten Schuss (Sekunden, zufaellig dazwischen).
	private const float REAKTION_MIN = 0.6f;

	private const float REAKTION_MAX = 1.2f;

	// Zusaetzliche Reaktionszeit mit Waffen, die mit einem Treffer toeten.
	private const float REAKTION_EINSCHUSS = 0.6f;

	// Wie schnell sie sich drehen (Grad pro Sekunde).
	private const float DREHTEMPO = 140f;

	// Wackeln beim Zielen (Grad): Grundwert plus Zuschlag pro Meter Entfernung.
	private const float ZIELFEHLER = 2.5f;

	private const float ZIELFEHLER_PRO_METER = 0.07f;

	// Faktor auf das Wackeln mit Waffen, die mit einem Treffer toeten.
	private const float ZIELFEHLER_EINSCHUSS = 1.7f;

	// Ab dieser Abweichung vom Ziel (Grad) schiessen sie.
	private const float SCHUSSWINKEL = 6f;

	// Wie oft Bots welche Waffe nehmen (Gewehr, SMG, Sniper).
	private static readonly float[] WAFFEN_GEWICHT = { 0.35f, 0.5f, 0.15f };

	// Ab diesem Hoehenunterschied zum Ziel nehmen sie einen Weg aus BotWege (m).
	private const float HOEHENUNTERSCHIED = 6f;

	// So nah muss ein Wegpunkt sein, damit er als erreicht gilt (m).
	private const float WEGPUNKT_RADIUS = 4f;

	private float springenBis;

	private Playermanager playermanager;

	private playershooting schiessen;

	private playerlooking blick;

	private Playermanager ziel;

	private float reaktionBis;

	private float naechsteZielsuche;

	private Vector3 jagdZiel;

	private float jagdBis;

	private Transform weg;

	private int wegIndex;

	private int wegRichtung;

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
			schiessen.waffeWaehlen(waffeAuslosen());
			letztePosition = base.transform.position;
		}
		if (playermanager.isDead.Value)
		{
			bewegung = Vector2.zero;
			ziel = null;
			weg = null;
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
			jagen();
		}
		steckenPruefen();
		base.transform.rotation = Quaternion.Euler(0f, gier, 0f);
		blick.cameraTransform.localRotation = Quaternion.Euler(0f - nick, 0f, 0f);
	}

	private static int waffeAuslosen()
	{
		float wurf = Random.value;
		for (int i = 0; i < WAFFEN_GEWICHT.Length; i++)
		{
			wurf -= WAFFEN_GEWICHT[i];
			if (wurf <= 0f)
			{
				return i;
			}
		}
		return Waffen.SMG;
	}

	private bool einschussWaffe => Waffen.Hole(schiessen.waffe).schaden >= Waffen.MAX_LEBEN;

	private bool istGegner(Playermanager anderer)
	{
		return anderer != null && anderer != playermanager && !anderer.isDead.Value && Runde.DarfTreffen(playermanager, anderer);
	}

	private void zielSuchen()
	{
		Playermanager bisher = ziel;
		ziel = null;
		float besteEntfernung = SICHTWEITE;
		foreach (Playermanager anderer in PlayersManager.Instance.players)
		{
			if (!istGegner(anderer))
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
			reaktionBis = Time.time + Random.Range(REAKTION_MIN, REAKTION_MAX) + (einschussWaffe ? REAKTION_EINSCHUSS : 0f);
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
		// Langsames Wackeln, auf Entfernung staerker. Mit Einschuss-Waffen noch mehr,
		// sonst waeren sie damit viel zu stark.
		float fehler = (ZIELFEHLER + ZIELFEHLER_PRO_METER * richtung.magnitude) * (einschussWaffe ? ZIELFEHLER_EINSCHUSS : 1f);
		float zeit = Time.time * 0.8f + rauschenStart;
		float fehlerGier = (Mathf.PerlinNoise(zeit, 0f) - 0.5f) * 2f * fehler;
		float fehlerNick = (Mathf.PerlinNoise(0f, zeit) - 0.5f) * 2f * fehler;
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
		if (Time.time >= reaktionBis && abweichung < SCHUSSWINKEL + fehler)
		{
			schiessen.botSchuss();
		}
	}

	// Kein Gegner in Sicht: zum naechsten Gegner gehen (auch wenn man ihn nicht sieht).
	// Vorher wanderten Bots ziellos und Verteidiger blieben oben, wo sie niemand traf.
	private void jagen()
	{
		if (Time.time >= jagdBis)
		{
			jagdBis = Time.time + Random.Range(2.5f, 4f);
			jagdZiel = naechsterGegnerOrt();
		}
		Vector3 naechster = wegpunktZu(jagdZiel);
		Vector3 richtung = flach(naechster - base.transform.position);
		if (richtung.sqrMagnitude > 0.01f)
		{
			float sollGier = Mathf.Atan2(richtung.x, richtung.z) * Mathf.Rad2Deg;
			gier = Mathf.MoveTowardsAngle(gier, sollGier, DREHTEMPO * Time.deltaTime);
		}
		nick = Mathf.MoveTowards(nick, 0f, DREHTEMPO * Time.deltaTime);
		bewegung = new Vector2(0f, flach(jagdZiel - base.transform.position).magnitude > 3f ? 1f : 0f);
	}

	private Vector3 naechsterGegnerOrt()
	{
		Playermanager bester = null;
		float besteEntfernung = float.MaxValue;
		foreach (Playermanager anderer in PlayersManager.Instance.players)
		{
			if (!istGegner(anderer))
			{
				continue;
			}
			float entfernung = Vector3.Distance(base.transform.position, anderer.transform.position);
			if (entfernung < besteEntfernung)
			{
				besteEntfernung = entfernung;
				bester = anderer;
			}
		}
		if (bester != null)
		{
			// Etwas daneben, damit nicht alle Bots auf denselben Punkt laufen.
			Vector2 versatz = Random.insideUnitCircle * 6f;
			return bester.transform.position + new Vector3(versatz.x, 0f, versatz.y);
		}
		// Niemand da: im Teammodus zur Gegenseite, sonst irgendwohin.
		int gegnerTeam = playermanager.team.Value == Teams.ALLIIERTE ? Teams.VERTEIDIGER : (playermanager.team.Value == Teams.VERTEIDIGER ? Teams.ALLIIERTE : Teams.KEINS);
		return Spawnlocation.getLocation(gegnerTeam);
	}

	// Liegt das Ziel deutlich hoeher oder tiefer, erst einen Weg aus BotWege entlang.
	private Vector3 wegpunktZu(Vector3 zielOrt)
	{
		BotWege wege = BotWege.Aktuelle;
		Vector3 hier = base.transform.position;
		if (wege == null || wege.AnzahlWege == 0)
		{
			return zielOrt;
		}
		if (weg == null)
		{
			if (Mathf.Abs(zielOrt.y - hier.y) < HOEHENUNTERSCHIED)
			{
				return zielOrt;
			}
			wegWaehlen(wege, hier, zielOrt);
		}
		Vector3 punkt = weg.GetChild(wegIndex).position;
		if (flach(punkt - hier).magnitude < WEGPUNKT_RADIUS)
		{
			wegIndex += wegRichtung;
			if (wegIndex < 0 || wegIndex >= weg.childCount)
			{
				weg = null;
				return zielOrt;
			}
			punkt = weg.GetChild(wegIndex).position;
		}
		return punkt;
	}

	// Den Weg nehmen, dessen naechster Punkt am naechsten liegt, und von dort Richtung Ziel.
	private void wegWaehlen(BotWege wege, Vector3 hier, Vector3 zielOrt)
	{
		float beste = float.MaxValue;
		for (int w = 0; w < wege.AnzahlWege; w++)
		{
			Transform kandidat = wege.Weg(w);
			for (int i = 0; i < kandidat.childCount; i++)
			{
				float entfernung = flach(kandidat.GetChild(i).position - hier).magnitude;
				if (entfernung < beste)
				{
					beste = entfernung;
					weg = kandidat;
					wegIndex = i;
				}
			}
		}
		// Wegpunkte liegen von unten nach oben.
		wegRichtung = zielOrt.y > hier.y ? 1 : -1;
	}

	// Steckt der Bot fest, springt er, dreht sich und plant neu.
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
			jagdBis = 0f;
			weg = null;
			gier += Random.Range(-90f, 90f);
		}
		letztePosition = base.transform.position;
	}

	private static Vector3 flach(Vector3 v)
	{
		v.y = 0f;
		return v;
	}
}
