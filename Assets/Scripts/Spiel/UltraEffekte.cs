using UnityEngine;

// Kurze Effekte der Stufe "Ultra", fuer alle Figuren aus einem Satz Partikelsysteme:
// Wasserspritzer (Tropfen, Ring, Gischt), Bugwelle beim Waten und Muendungsfeuer mit Licht.
// Liegt am Game Manager, die Materialien traegt das Editor-Skript ein.
public class UltraEffekte : MonoBehaviour
{
	public Material tropfenMaterial;

	public Material ringMaterial;

	public Material blitzMaterial;

	public Material funkenMaterial;

	private static UltraEffekte instanz;

	private ParticleSystem tropfen;

	private ParticleSystem ringe;

	private ParticleSystem gischt;

	private ParticleSystem blitze;

	private ParticleSystem funken;

	private Light blitzLicht;

	private float lichtAus;

	// Zum Einstellen
	private static readonly Color WASSER_FARBE = new Color(0.85f, 0.93f, 1f, 0.85f);

	private const float BLITZ_DAUER = 0.05f;

	private void Awake()
	{
		instanz = this;
		tropfen = system("Tropfen", tropfenMaterial, ParticleSystemRenderMode.Stretch, 1.4f, 400);
		tropfen.GetComponent<ParticleSystemRenderer>().velocityScale = 0.06f;
		tropfen.GetComponent<ParticleSystemRenderer>().lengthScale = 1.5f;
		ringe = system("Ringe", ringMaterial, ParticleSystemRenderMode.HorizontalBillboard, 0f, 80);
		var wachsen = ringe.sizeOverLifetime;
		wachsen.enabled = true;
		wachsen.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f));
		gischt = system("Gischt", tropfenMaterial, ParticleSystemRenderMode.Billboard, 0.25f, 200);
		var gischtWachsen = gischt.sizeOverLifetime;
		gischtWachsen.enabled = true;
		gischtWachsen.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
		blitze = system("Muendungsfeuer", blitzMaterial, ParticleSystemRenderMode.Billboard, 0f, 40);
		funken = system("Funken", funkenMaterial, ParticleSystemRenderMode.Stretch, 0.6f, 200);
		funken.GetComponent<ParticleSystemRenderer>().velocityScale = 0.04f;
		foreach (ParticleSystem ps in new[] { tropfen, ringe, gischt, funken })
		{
			// Alle verblassen am Ende ihres Lebens.
			var farbe = ps.colorOverLifetime;
			farbe.enabled = true;
			var verlauf = new Gradient();
			verlauf.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
			farbe.color = new ParticleSystem.MinMaxGradient(verlauf);
		}
		GameObject lichtGo = new GameObject("Muendungslicht");
		lichtGo.transform.SetParent(base.transform, false);
		blitzLicht = lichtGo.AddComponent<Light>();
		blitzLicht.type = LightType.Point;
		blitzLicht.range = 7f;
		blitzLicht.intensity = 3f;
		blitzLicht.enabled = false;
	}

	private ParticleSystem system(string name, Material material, ParticleSystemRenderMode art, float schwerkraft, int hoechstens)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(base.transform, false);
		ParticleSystem ps = go.AddComponent<ParticleSystem>();
		ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		var main = ps.main;
		main.playOnAwake = false;
		// Laeuft dauernd, gibt aber selbst nichts aus (emission aus). Die Teilchen kommen nur
		// ueber Emit, mit eigener Geschwindigkeit.
		main.loop = true;
		main.startSpeed = 0f;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.gravityModifier = schwerkraft;
		main.maxParticles = hoechstens;
		var emission = ps.emission;
		emission.enabled = false;
		var form = ps.shape;
		form.enabled = false;
		ParticleSystemRenderer anzeige = go.GetComponent<ParticleSystemRenderer>();
		anzeige.renderMode = art;
		anzeige.sharedMaterial = material;
		anzeige.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		ps.Play();
		return ps;
	}

	private void Update()
	{
		if (blitzLicht.enabled && Time.time >= lichtAus)
		{
			blitzLicht.enabled = false;
		}
	}

	private static bool bereit => instanz != null && Grafik.Ultra && instanz.tropfenMaterial != null;

	// Etwas faellt oder schlaegt ins Wasser. staerke 1 = Figur springt aus geringer Hoehe.
	public static void Spritzer(Vector3 ort, float staerke)
	{
		if (!bereit)
		{
			return;
		}
		var p = new ParticleSystem.EmitParams();
		int anzahl = Mathf.RoundToInt(14f * staerke);
		for (int i = 0; i < anzahl; i++)
		{
			Vector2 seite = Random.insideUnitCircle * 2.2f * Mathf.Sqrt(staerke);
			p.position = ort + new Vector3(seite.x * 0.15f, 0.05f, seite.y * 0.15f);
			p.velocity = new Vector3(seite.x, Random.Range(3f, 6.5f) * Mathf.Sqrt(staerke), seite.y);
			p.startSize = Random.Range(0.05f, 0.12f);
			p.startLifetime = Random.Range(0.6f, 1.1f);
			p.startColor = WASSER_FARBE;
			instanz.tropfen.Emit(p, 1);
		}
		p = new ParticleSystem.EmitParams();
		p.position = ort + Vector3.up * 0.03f;
		p.velocity = Vector3.zero;
		p.startSize = 1.6f * staerke;
		p.startLifetime = 1.2f;
		p.startColor = new Color(1f, 1f, 1f, 0.7f);
		instanz.ringe.Emit(p, 1);
		for (int i = 0; i < Mathf.RoundToInt(5f * staerke); i++)
		{
			Vector2 seite = Random.insideUnitCircle;
			p.position = ort + new Vector3(seite.x * 0.4f, 0.2f, seite.y * 0.4f);
			p.velocity = new Vector3(seite.x, Random.Range(0.8f, 1.8f), seite.y);
			p.startSize = Random.Range(0.5f, 0.9f) * Mathf.Sqrt(staerke);
			p.startLifetime = Random.Range(0.5f, 0.9f);
			p.startColor = new Color(1f, 1f, 1f, 0.45f);
			instanz.gischt.Emit(p, 1);
		}
	}

	// Figur watet durchs Wasser: kleine Bugwelle in Laufrichtung.
	public static void Welle(Vector3 ort, Vector3 tempo)
	{
		if (!bereit)
		{
			return;
		}
		Vector3 vorne = tempo.normalized;
		var p = new ParticleSystem.EmitParams();
		p.position = ort + vorne * 0.4f + Vector3.up * 0.03f;
		p.startSize = 0.9f;
		p.startLifetime = 0.9f;
		p.startColor = new Color(1f, 1f, 1f, 0.5f);
		instanz.ringe.Emit(p, 1);
		for (int i = 0; i < 4; i++)
		{
			Vector3 seite = Vector3.Cross(Vector3.up, vorne) * Random.Range(-1f, 1f);
			p.position = ort + vorne * 0.45f + seite * 0.3f;
			p.velocity = vorne * tempo.magnitude * 0.35f + seite * 1.2f + Vector3.up * Random.Range(1.2f, 2.4f);
			p.startSize = Random.Range(0.04f, 0.09f);
			p.startLifetime = Random.Range(0.4f, 0.7f);
			p.startColor = WASSER_FARBE;
			instanz.tropfen.Emit(p, 1);
		}
	}

	// Schuss: kurzer Blitz an der Muendung, ein paar Funken, ein Lichtblitz.
	public static void Muendungsfeuer(Vector3 ort, Vector3 richtung, Color farbe)
	{
		if (!bereit)
		{
			return;
		}
		Color hell = Color.Lerp(farbe, Color.white, 0.55f);
		var p = new ParticleSystem.EmitParams();
		p.position = ort + richtung * 0.08f;
		p.startSize = Random.Range(0.3f, 0.45f);
		p.startLifetime = BLITZ_DAUER;
		p.rotation = Random.Range(0f, 360f);
		p.startColor = hell;
		instanz.blitze.Emit(p, 1);
		p.position = ort + richtung * 0.22f;
		p.startSize = Random.Range(0.18f, 0.26f);
		instanz.blitze.Emit(p, 1);
		p = new ParticleSystem.EmitParams();
		for (int i = 0; i < 5; i++)
		{
			p.position = ort;
			p.velocity = (richtung + Random.insideUnitSphere * 0.35f).normalized * Random.Range(6f, 12f);
			p.startSize = Random.Range(0.015f, 0.03f);
			p.startLifetime = Random.Range(0.12f, 0.25f);
			p.startColor = new Color(1f, 0.75f, 0.35f);
			instanz.funken.Emit(p, 1);
		}
		instanz.blitzLicht.transform.position = ort + richtung * 0.3f;
		instanz.blitzLicht.color = hell;
		instanz.blitzLicht.enabled = true;
		instanz.lichtAus = Time.time + BLITZ_DAUER;
	}
}
