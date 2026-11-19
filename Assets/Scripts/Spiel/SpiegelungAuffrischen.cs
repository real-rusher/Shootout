using System.Collections;
using UnityEngine;

// Spiegelungen der Stufe "Ultra": Die Map wird einmal in alle Richtungen aufgenommen, sobald
// die Sonde eingeschaltet wird (ein Bild spaeter, dann steht alles). Danach nicht mehr, das
// waere zu teuer. Metall, Wasser und glatte Flaechen spiegeln dann die Umgebung.
[RequireComponent(typeof(ReflectionProbe))]
public class SpiegelungAuffrischen : MonoBehaviour
{
	private void OnEnable()
	{
		StartCoroutine(aufnehmen());
	}

	private IEnumerator aufnehmen()
	{
		yield return null;
		GetComponent<ReflectionProbe>().RenderProbe();
	}
}
