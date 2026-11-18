using UnityEngine;

// Rote Explosion, wenn jemand stirbt: Partikelknall, kurzer Lichtblitz, Knall. Wird zur Laufzeit
// erzeugt, braucht nur ein Partikelmaterial (traegt das Editor-Skript am Spieler ein).
public static class Todeseffekt
{
	// Zum Einstellen
	private const int PARTIKEL = 90;

	private const float LEBENSDAUER = 2f;

	private static readonly Color FARBE_HELL = new Color(1f, 0.25f, 0.15f);

	private static readonly Color FARBE_DUNKEL = new Color(0.6f, 0f, 0f);

	public static void Zeigen(Vector3 ort, Material material)
	{
		GameObject wurzel = new GameObject("Todeseffekt");
		wurzel.transform.position = ort;

		ParticleSystem knall = wurzel.AddComponent<ParticleSystem>();
		// Ein frisch angelegtes Partikelsystem laeuft schon, vor dem Einstellen anhalten.
		knall.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		ParticleSystemRenderer anzeige = wurzel.GetComponent<ParticleSystemRenderer>();
		anzeige.sharedMaterial = material;
		ParticleSystem.MainModule main = knall.main;
		main.duration = 0.5f;
		main.loop = false;
		main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f);
		main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
		main.startColor = new ParticleSystem.MinMaxGradient(FARBE_HELL, FARBE_DUNKEL);
		main.gravityModifier = 0.8f;
		main.maxParticles = PARTIKEL;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		ParticleSystem.EmissionModule emission = knall.emission;
		emission.rateOverTime = 0f;
		emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)PARTIKEL) });
		ParticleSystem.ShapeModule form = knall.shape;
		form.shapeType = ParticleSystemShapeType.Sphere;
		form.radius = 0.4f;
		ParticleSystem.ColorOverLifetimeModule verlauf = knall.colorOverLifetime;
		verlauf.enabled = true;
		Gradient farben = new Gradient();
		farben.SetKeys(
			new[] { new GradientColorKey(new Color(1f, 0.6f, 0.4f), 0f), new GradientColorKey(FARBE_DUNKEL, 1f) },
			new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
		verlauf.color = new ParticleSystem.MinMaxGradient(farben);
		ParticleSystem.SizeOverLifetimeModule groesse = knall.sizeOverLifetime;
		groesse.enabled = true;
		groesse.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
		knall.Play();

		GameObject blitz = new GameObject("Blitz");
		blitz.transform.SetParent(wurzel.transform, false);
		Light licht = blitz.AddComponent<Light>();
		licht.type = LightType.Point;
		licht.color = FARBE_HELL;
		licht.range = 9f;
		licht.intensity = 6f;
		blitz.AddComponent<Verblassen>();

		AudioSource ton = wurzel.AddComponent<AudioSource>();
		ton.spatialBlend = 1f;
		ton.rolloffMode = AudioRolloffMode.Linear;
		ton.minDistance = 3f;
		ton.maxDistance = 120f;
		ton.PlayOneShot(Toene.Explosion());

		Object.Destroy(wurzel, LEBENSDAUER);
	}
}
