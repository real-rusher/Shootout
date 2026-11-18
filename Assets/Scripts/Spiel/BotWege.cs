using UnityEngine;

// Wege fuer Bots auf Maps mit Hoehenunterschieden (D-Day: die drei Schluchten). Jedes Kind ist
// ein Weg, dessen Kinder die Wegpunkte von unten nach oben sind. Liegt an der Map-Wurzel und
// gilt, solange die Map aktiv ist.
public class BotWege : MonoBehaviour
{
	public static BotWege Aktuelle { get; private set; }

	private void OnEnable()
	{
		Aktuelle = this;
	}

	private void OnDisable()
	{
		if (Aktuelle == this)
		{
			Aktuelle = null;
		}
	}

	public int AnzahlWege => base.transform.childCount;

	public Transform Weg(int nummer)
	{
		return base.transform.GetChild(nummer);
	}
}
