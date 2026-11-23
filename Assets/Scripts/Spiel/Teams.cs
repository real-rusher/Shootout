using UnityEngine;

// Namen und Farben der beiden Teams. 0 heisst: kein Team (jeder gegen jeden).
public static class Teams
{
	public const int KEINS = 0;

	public const int ALLIIERTE = 1;

	public const int VERTEIDIGER = 2;

	// Auf D-Day heissen die Teams nach der Geschichte, sonst nach ihrer Farbe. Setzt
	// Manager.getMap, auf Host und Client gleich (beide kennen die gewaehlte Map).
	public static bool dDayNamen;

	public static string Name(int team)
	{
		switch (team)
		{
			case ALLIIERTE: return dDayNamen ? "Alliierte" : "Grün";
			case VERTEIDIGER: return dDayNamen ? "Verteidiger" : "Blau";
			default: return "";
		}
	}

	// Koerperfarbe: Oliv gegen Graublau, gut zu unterscheiden.
	public static Color Farbe(int team)
	{
		switch (team)
		{
			case ALLIIERTE: return new Color(0.42f, 0.5f, 0.24f);
			case VERTEIDIGER: return new Color(0.38f, 0.42f, 0.52f);
			default: return Color.white;
		}
	}

	// Hellere Farbe fuer Schrift (Namensschild, Anzeige).
	public static Color SchriftFarbe(int team)
	{
		switch (team)
		{
			case ALLIIERTE: return new Color(0.7f, 0.9f, 0.45f);
			case VERTEIDIGER: return new Color(0.65f, 0.75f, 1f);
			default: return Color.white;
		}
	}
}
