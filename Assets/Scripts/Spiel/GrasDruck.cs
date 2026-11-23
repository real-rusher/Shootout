using System.Collections.Generic;
using UnityEngine;

// Markiert eine Figur (Spieler oder Bot), die Gras niederdrueckt. GrasFeld gibt die Positionen
// und Laufrichtungen an die Trampelkarte und den Gras-Shader weiter. Haengt playerlooking an.
public class GrasDruck : MonoBehaviour
{
	public static readonly List<GrasDruck> Alle = new List<GrasDruck>();

	// Geschwindigkeit in der Ebene (m/s), geglaettet. Fuer die Richtung der Trampelpfade.
	public Vector3 tempo { get; private set; }

	// Steht die Figur auf dem Boden (nicht im Sprung)?
	public bool amBoden { get; private set; }

	private Vector3 vorher;

	private void OnEnable()
	{
		Alle.Add(this);
		vorher = base.transform.position;
	}

	private void OnDisable()
	{
		Alle.Remove(this);
	}

	private void Update()
	{
		Vector3 hier = base.transform.position;
		Vector3 jetzt = (hier - vorher) / Mathf.Max(Time.deltaTime, 0.001f);
		jetzt.y = 0f;
		// Teleport (neu erschienen) nicht als Laufen werten
		if (jetzt.sqrMagnitude > 900f)
		{
			jetzt = Vector3.zero;
		}
		tempo = Vector3.Lerp(tempo, jetzt, 1f - Mathf.Exp(-10f * Time.deltaTime));
		vorher = hier;
		// Fuesse 1 m unter der Mitte (CharacterController), Boden hoechstens 0,3 m darunter.
		amBoden = Physics.Raycast(hier, Vector3.down, 1.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
	}
}
