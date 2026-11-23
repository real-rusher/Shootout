using System.Collections.Generic;
using UnityEngine;

// Markiert eine Figur (Spieler oder Bot), die Gras zur Seite drueckt. GrasFeld gibt die
// Positionen an den Gras-Shader weiter. Haengt playerlooking an jede Figur.
public class GrasDruck : MonoBehaviour
{
	public static readonly List<Transform> Alle = new List<Transform>();

	private void OnEnable()
	{
		Alle.Add(base.transform);
	}

	private void OnDisable()
	{
		Alle.Remove(base.transform);
	}
}
