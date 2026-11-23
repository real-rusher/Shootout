using System.Collections.Generic;
using UnityEngine;

// Bereich ohne Wasser, z. B. der Innenraum eines Landungsboots: Dort zeichnet der Wasser-Shader
// nichts, und wer drin steht, gilt nicht als im oder unter Wasser (keine Spritzer, keine
// Unterwassersicht). Quader in lokalen Koordinaten des Objekts, nur um die Hochachse gedreht.
public class WasserAusschnitt : MonoBehaviour
{
	public Vector3 mitte;

	public Vector3 halbeGroesse = Vector3.one;

	private static readonly List<WasserAusschnitt> alle = new List<WasserAusschnitt>();

	// Muss zur Laenge der Felder im Wasser-Shader passen.
	private const int HOECHSTENS = 8;

	private static readonly Vector4[] lageA = new Vector4[HOECHSTENS];

	private static readonly Vector4[] lageB = new Vector4[HOECHSTENS];

	private static int gesetztImBild = -1;

	private void OnEnable()
	{
		alle.Add(this);
	}

	private void OnDisable()
	{
		alle.Remove(this);
	}

	public bool enthaelt(Vector3 welt)
	{
		Vector3 lokal = base.transform.InverseTransformPoint(welt) - mitte;
		return Mathf.Abs(lokal.x) < halbeGroesse.x && Mathf.Abs(lokal.z) < halbeGroesse.z && lokal.y < halbeGroesse.y && lokal.y > -halbeGroesse.y - 3f;
	}

	public static bool Enthaelt(Vector3 welt)
	{
		foreach (WasserAusschnitt ausschnitt in alle)
		{
			if (ausschnitt.enthaelt(welt))
			{
				return true;
			}
		}
		return false;
	}

	// Gibt die Bereiche an den Wasser-Shader (einmal pro Bild, die naechsten zur Kamera zuerst
	// waeren besser, acht Boote reichen aber fuer jede Map).
	public static void ShaderSetzen()
	{
		if (gesetztImBild == Time.frameCount)
		{
			return;
		}
		gesetztImBild = Time.frameCount;
		int n = 0;
		foreach (WasserAusschnitt a in alle)
		{
			if (n >= HOECHSTENS)
			{
				break;
			}
			Vector3 m = a.transform.TransformPoint(a.mitte);
			Vector3 skala = a.transform.lossyScale;
			float gier = a.transform.eulerAngles.y * Mathf.Deg2Rad;
			lageA[n] = new Vector4(m.x, m.z, a.halbeGroesse.x * skala.x, a.halbeGroesse.z * skala.z);
			lageB[n] = new Vector4(Mathf.Cos(gier), Mathf.Sin(gier), 0f, 0f);
			n++;
		}
		for (int i = n; i < HOECHSTENS; i++)
		{
			lageA[i] = new Vector4(0f, 0f, 0f, 0f);
			lageB[i] = new Vector4(1f, 0f, 0f, 0f);
		}
		Shader.SetGlobalVectorArray("_ShootoutAusschnittA", lageA);
		Shader.SetGlobalVectorArray("_ShootoutAusschnittB", lageB);
		Shader.SetGlobalFloat("_ShootoutAusschnitte", n);
	}
}
