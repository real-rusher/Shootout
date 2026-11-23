using UnityEngine;

// Alle Zahlen der Waffen an einer Stelle, zum Einstellen.
// Index 0, 1, 2 entspricht den Tasten 1, 2, 3.
public static class Waffen
{
	public const int GEWEHR = 0;

	public const int SMG = 1;

	public const int SNIPER = 2;

	public const int MAX_LEBEN = 100;

	// Sichtfeld ohne Zoom (Grad).
	public const float NORMALES_SICHTFELD = 60f;

	// Sniper-Zoom: Rechtsklick an (erste Stufe) und aus, dazwischen Mausrad durch die Stufen.
	public static readonly float[] SNIPER_ZOOM_SICHTFELD = { 30f, 20f, 12f, 7f };

	// Dev-Funktion: Wer diese Zeichen im Namen hat (Gross- und Kleinschreibung egal),
	// schiesst mit jeder Waffe im Dauerfeuer.
	public const string DAUERFEUER_KENNUNG = "!ad";

	public const float DAUERFEUER_ABKLINGZEIT = 0.01f;

	public struct Werte
	{
		public string name;

		// Sekunden zwischen zwei Schuessen.
		public float abklingzeit;

		// Abzug von MAX_LEBEN pro Treffer.
		public int schaden;

		// Halber Kegelwinkel der Streuung in Grad, im Stand und bei voller Laufgeschwindigkeit.
		public float streuungStehen;

		public float streuungLaufen;

		// Faktor auf LaserEffect.effectWidth.
		public float laserDicke;

		public Color laserFarbe;

		// Tonhoehe des Schusses (1 = Gewehr).
		public float tonHoehe;

		// Laenge des Laserpointers (1 = Original).
		public float laenge;
	}

	public static readonly Werte[] LISTE =
	{
		// Gewehr: die Waffe des Originals (Abklingzeit 0,7 s aus dem Prefab, ein Treffer toetet, keine Streuung).
		new Werte { name = "Gewehr", abklingzeit = 0.7f, schaden = 100, streuungStehen = 0f, streuungLaufen = 0f, laserDicke = 1f, laserFarbe = new Color(0.38f, 0.537f, 1f), tonHoehe = 1f, laenge = 1f },
		// SMG: schnell, aber ungenau, besonders im Laufen. Fuenf Treffer toeten.
		new Werte { name = "SMG", abklingzeit = 0.08f, schaden = 20, streuungStehen = 2.5f, streuungLaufen = 6f, laserDicke = 0.6f, laserFarbe = new Color(1f, 0.78f, 0.25f), tonHoehe = 1.7f, laenge = 0.7f },
		// Sniper: langsam, immer genau (auch im Laufen), mit Zoom auf Rechtsklick.
		new Werte { name = "Sniper", abklingzeit = 1.6f, schaden = 100, streuungStehen = 0f, streuungLaufen = 0f, laserDicke = 1.6f, laserFarbe = new Color(1f, 0.3f, 0.3f), tonHoehe = 0.6f, laenge = 1.4f },
	};

	public static Werte Hole(int waffe)
	{
		return LISTE[Mathf.Clamp(waffe, 0, LISTE.Length - 1)];
	}

	public static bool HatDauerfeuer(string spielerName)
	{
		return spielerName != null && spielerName.IndexOf(DAUERFEUER_KENNUNG, System.StringComparison.OrdinalIgnoreCase) >= 0;
	}
}
