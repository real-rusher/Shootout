using UnityEngine;

// Besondere Regeln einer Map. Liegt an der Map-Wurzel und gilt, solange die Map aktiv ist.
public class KartenRegeln : MonoBehaviour
{
	public static KartenRegeln Aktuelle { get; private set; }

	// Wer tiefer faellt (Himmelsinseln), erscheint neu an seinem Spawnpunkt.
	public float absturzHoehe = -1000f;

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
}
