using UnityEngine;

// Das Original hatte keine Tonspur. Diese Toene werden beim ersten Gebrauch aus
// Sinus und Rauschen erzeugt, damit es ohne Audiodateien etwas zu hoeren gibt.
public static class Toene
{
	private const int RATE = 44100;

	private static AudioClip[] schuesse;

	private static AudioClip treffer;

	public static AudioClip Schuss(int waffe)
	{
		if (schuesse == null)
		{
			schuesse = new AudioClip[Waffen.LISTE.Length];
		}
		waffe = Mathf.Clamp(waffe, 0, schuesse.Length - 1);
		if (schuesse[waffe] == null)
		{
			float hoehe = Waffen.Hole(waffe).tonHoehe;
			// Laser-"Pew": Ton faellt von hoch nach tief, dazu etwas Rauschen.
			schuesse[waffe] = Erzeugen("Schuss" + waffe, 0.22f / Mathf.Sqrt(hoehe), 1500f * hoehe, 250f * hoehe, 0.25f, 14f);
		}
		return schuesse[waffe];
	}

	public static AudioClip Treffer()
	{
		if (treffer == null)
		{
			treffer = Erzeugen("Treffer", 0.06f, 2200f, 1800f, 0.05f, 40f);
		}
		return treffer;
	}

	private static AudioClip Erzeugen(string name, float dauer, float startFrequenz, float endFrequenz, float rauschAnteil, float abfall)
	{
		int anzahl = Mathf.Max(1, (int)(RATE * dauer));
		float[] daten = new float[anzahl];
		System.Random zufall = new System.Random(name.GetHashCode());
		float phase = 0f;
		for (int i = 0; i < anzahl; i++)
		{
			float t = (float)i / RATE;
			float anteil = (float)i / anzahl;
			float frequenz = Mathf.Lerp(startFrequenz, endFrequenz, anteil);
			phase += 2f * Mathf.PI * frequenz / RATE;
			float huelle = Mathf.Exp(-t * abfall) * Mathf.Clamp01(t * 400f);
			float rauschen = (float)(zufall.NextDouble() * 2.0 - 1.0);
			daten[i] = (Mathf.Sin(phase) * (1f - rauschAnteil) + rauschen * rauschAnteil) * huelle * 0.5f;
		}
		AudioClip clip = AudioClip.Create(name, anzahl, 1, RATE, false);
		clip.SetData(daten, 0);
		return clip;
	}
}
