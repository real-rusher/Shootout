using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Teil des Editor-Skripts: genauere Modelle fuer D-Day. Landungsboot nach dem Vorbild der
// LCVP, ein Panzerwrack, Panzerigel aus Doppel-T-Traegern, Holzrampen mit Tellerminen,
// Stacheldrahtrollen, einzelne Sandsaecke und begehbare Bunker mit Schiessscharten.
// Aussehen kommt aus zusammengesetzten Netzen (Netzbau), Kollision aus wenigen einfachen
// Kaesten: Das bleibt fuer alle Spieler gleich und verlaesslich.
public static partial class SzeneAufbauen
{
	static int ddModellNummer;

	static GameObject DDNetz(Transform eltern, string name, Netzbau netz, Material[] materialien, Vector3 ort = default(Vector3), Quaternion drehung = default(Quaternion))
	{
		var go = new GameObject(name);
		go.transform.SetParent(eltern, false);
		go.transform.localPosition = ort;
		go.transform.localRotation = drehung.w == 0f && drehung.x == 0f && drehung.y == 0f && drehung.z == 0f ? Quaternion.identity : drehung;
		go.AddComponent<MeshFilter>().sharedMesh = AlsAsset(netz.Fertig(name), DDAY + "/Modell" + (ddModellNummer++) + ".asset");
		go.AddComponent<MeshRenderer>().sharedMaterials = materialien;
		return go;
	}

	// Unsichtbarer Kasten nur fuer Kollision
	static void DDKasten(Transform eltern, Vector3 mitte, Vector3 groesse, Vector3 drehung = default(Vector3))
	{
		var kasten = new GameObject("Kollision");
		kasten.transform.SetParent(eltern, false);
		kasten.transform.localPosition = mitte;
		kasten.transform.localRotation = Quaternion.Euler(drehung);
		kasten.AddComponent<BoxCollider>().size = groesse;
	}

	static Matrix4x4 M(Vector3 ort, Vector3 groesse, Vector3 drehung = default(Vector3))
	{
		return Matrix4x4.TRS(ort, Quaternion.Euler(drehung), groesse);
	}

	// ------------------------------------------------------------------
	// Landungsboot (LCVP): 10,5 m, Sperrholzrumpf grau-gruen, Stahlrampe vorne

	static void DDLandungsbootModell(GameObject boot, Material rumpf, Material stahl, Material innen, Material schwarz)
	{
		const int R = 0, S = 1, I = 2, K = 3;
		var n = new Netzbau(4);
		// Rumpf: Boden, Kimm schraeg, Bordwaende leicht ausgestellt
		n.Quader(M(new Vector3(0f, -0.42f, 0f), new Vector3(2.1f, 0.1f, 9.8f)), R, Color.white);
		foreach (var s in new[] { -1f, 1f })
		{
			n.Quader(M(new Vector3(s * 1.33f, -0.12f, 0f), new Vector3(0.1f, 0.85f, 9.8f), new Vector3(0f, 0f, -s * 48f)), R, Color.white);
			n.Quader(M(new Vector3(s * 1.63f, 1.15f, 0f), new Vector3(0.1f, 2.3f, 9.8f), new Vector3(0f, 0f, s * -4f)), R, Color.white);
			// Scheuerleiste und Dollbord
			n.Quader(M(new Vector3(s * 1.72f, 1.6f, 0f), new Vector3(0.12f, 0.1f, 9.7f)), R, Color.white);
			n.Quader(M(new Vector3(s * 1.66f, 2.33f, 0f), new Vector3(0.22f, 0.09f, 9.9f)), R, Color.white);
			// Spanten innen
			for (float z = -4.2f; z <= 4.3f; z += 0.85f)
			{
				n.Quader(M(new Vector3(s * 1.5f, 1.3f, z), new Vector3(0.08f, 1.9f, 0.1f)), I, Color.white);
			}
			// Rahmen der Bugrampe
			n.Quader(M(new Vector3(s * 1.55f, 1.15f, 4.85f), new Vector3(0.28f, 2.3f, 0.4f)), S, Color.white);
			// Seil zur Rampe
			n.Rohr(new Vector3(s * 1.5f, 2.3f, 4.75f), new Vector3(s * 1.45f, 1.2f, 6.1f), 0.015f, 0.015f, 5, K, Color.white, Color.white, 0f, 1f, false);
			// MG-Lafette am Heck
			n.Rohr(new Vector3(s * 1.35f, 2.3f, -4.35f), new Vector3(s * 1.35f, 2.95f, -4.35f), 0.04f, 0.04f, 8, S, Color.white, Color.white, 0f, 1f, true);
			n.Quader(M(new Vector3(s * 1.35f, 3.03f, -4.25f), new Vector3(0.12f, 0.15f, 0.75f)), K, Color.white);
			n.Rohr(new Vector3(s * 1.35f, 3.05f, -3.9f), new Vector3(s * 1.4f, 3.08f, -3.15f), 0.022f, 0.022f, 8, K, Color.white, Color.white, 0f, 1f, true);
			n.Quader(M(new Vector3(s * 1.35f, 3.15f, -3.95f), new Vector3(0.5f, 0.35f, 0.04f)), S, Color.white);
			// Gerundete Heckecken
			n.Rohr(new Vector3(s * 1.5f, -0.3f, -4.85f), new Vector3(s * 1.5f, 2.3f, -4.85f), 0.2f, 0.2f, 10, R, Color.white, Color.white, 0f, 1f, true);
		}
		// Deck innen, Motorabdeckung, Heckspiegel
		n.Quader(M(new Vector3(0f, 0.32f, 0f), new Vector3(3.1f, 0.08f, 9.6f)), I, Color.white);
		n.Quader(M(new Vector3(0f, 0.6f, -3.95f), new Vector3(2.2f, 0.55f, 1.5f)), I, Color.white);
		n.Quader(M(new Vector3(0f, 1.0f, -4.95f), new Vector3(3.2f, 2.6f, 0.12f)), R, Color.white);
		// Gepanzerter Steuerstand mit Rad
		n.Quader(M(new Vector3(0f, 1.1f, -4.3f), new Vector3(1.2f, 0.4f, 1.0f)), I, Color.white);
		n.Quader(M(new Vector3(0f, 2.55f, -3.75f), new Vector3(1.5f, 0.85f, 0.06f)), S, Color.white);
		n.Quader(M(new Vector3(-0.74f, 2.5f, -4.1f), new Vector3(0.06f, 0.75f, 0.7f)), S, Color.white);
		n.Quader(M(new Vector3(0.74f, 2.5f, -4.1f), new Vector3(0.06f, 0.75f, 0.7f)), S, Color.white);
		n.Rohr(new Vector3(0f, 2.35f, -3.88f), new Vector3(0f, 2.35f, -3.84f), 0.24f, 0.24f, 14, K, Color.white, Color.white, 0f, 1f, true);
		// Bugrampe (Gelenk bei z = 5, 28 Grad heruntergeklappt) mit Querrippen
		var rampe = Matrix4x4.TRS(new Vector3(0f, 0.3f, 5f), Quaternion.Euler(28f, 0f, 0f), Vector3.one);
		n.Quader(rampe * M(new Vector3(0f, 0f, 1.4f), new Vector3(3.2f, 0.12f, 2.8f)), S, Color.white);
		for (int i = 0; i < 6; i++)
		{
			n.Quader(rampe * M(new Vector3(0f, 0.09f, 0.3f + i * 0.45f), new Vector3(3.0f, 0.06f, 0.07f)), S, Color.white);
		}
		foreach (var s in new[] { -1f, 1f })
		{
			n.Quader(rampe * M(new Vector3(s * 1.55f, 0.25f, 1.4f), new Vector3(0.1f, 0.5f, 2.8f)), S, Color.white);
		}
		DDNetz(boot.transform, "Modell", n, new[] { rumpf, stahl, innen, schwarz });
	}

	// ------------------------------------------------------------------
	// Panzerwrack: Wanne mit schraeger Frontplatte, Laufwerk, Ketten, Gussturm, Kanone

	static void DDPanzerModell(GameObject wrack, Material lack, Material verbrannt, Material kette, Material rost)
	{
		const int L = 0, V = 1, K = 2, R = 3;
		var n = new Netzbau(4);
		// Wanne
		n.Quader(M(new Vector3(0f, 0.75f, -0.1f), new Vector3(1.9f, 0.9f, 5.3f)), L, Color.white);
		n.Quader(M(new Vector3(0f, 1.47f, -0.55f), new Vector3(2.62f, 0.72f, 4.1f)), L, Color.white);
		n.Quader(M(new Vector3(0f, 1.33f, 2.25f), new Vector3(2.45f, 0.1f, 1.7f), new Vector3(39f, 0f, 0f)), L, Color.white);
		n.Quader(M(new Vector3(0f, 0.75f, 2.65f), new Vector3(2.0f, 0.7f, 0.12f), new Vector3(30f, 0f, 0f)), L, Color.white);
		n.Quader(M(new Vector3(0f, 1.2f, -2.65f), new Vector3(2.5f, 1.2f, 0.1f), new Vector3(-10f, 0f, 0f)), L, Color.white);
		// Motordeck, verbrannt, mit Lueftungsgittern
		n.Quader(M(new Vector3(0f, 1.86f, -1.8f), new Vector3(2.5f, 0.06f, 1.7f)), V, Color.white);
		for (int i = 0; i < 5; i++)
		{
			n.Quader(M(new Vector3(-0.6f + i * 0.3f, 1.9f, -1.9f), new Vector3(0.06f, 0.04f, 1.1f)), K, Color.white);
		}
		// Auspuff-Abweiser hinten, Scheinwerfer, Werkzeug, Ersatzkettenglieder
		n.Quader(M(new Vector3(0f, 0.95f, -2.85f), new Vector3(2.0f, 0.35f, 0.25f), new Vector3(20f, 0f, 0f)), R, Color.white);
		foreach (var s in new[] { -1f, 1f })
		{
			n.Rohr(new Vector3(s * 0.85f, 1.62f, 2.72f), new Vector3(s * 0.85f, 1.62f, 2.85f), 0.1f, 0.1f, 10, K, Color.white, Color.white, 0f, 1f, true);
			n.Quader(M(new Vector3(s * 1.32f, 1.6f, -0.3f), new Vector3(0.05f, 0.12f, 1.4f)), R, Color.white);
		}
		for (int i = 0; i < 4; i++)
		{
			n.Quader(M(new Vector3(-0.75f + i * 0.5f, 1.42f, 2.15f), new Vector3(0.38f, 0.06f, 0.18f), new Vector3(39f, 0f, 0f)), K, Color.white);
		}
		// Laufwerk: Treibrad vorn, Leitrad hinten, sechs Laufrollen in drei Drehgestellen, Stuetzrollen
		foreach (var s in new[] { -1f, 1f })
		{
			float x = s * 1.12f;
			n.Rohr(new Vector3(x - 0.17f, 0.85f, 2.55f), new Vector3(x + 0.17f, 0.85f, 2.55f), 0.38f, 0.38f, 14, K, Color.white, Color.white, 0f, 1f, true);
			n.Rohr(new Vector3(x - 0.15f, 0.55f, -2.55f), new Vector3(x + 0.15f, 0.55f, -2.55f), 0.32f, 0.32f, 14, K, Color.white, Color.white, 0f, 1f, true);
			for (int i = 0; i < 6; i++)
			{
				float z = -1.95f + i * 0.78f;
				n.Rohr(new Vector3(x - 0.14f, 0.38f, z), new Vector3(x + 0.14f, 0.38f, z), 0.31f, 0.31f, 14, K, Color.white, Color.white, 0f, 1f, true);
			}
			for (int b = 0; b < 3; b++)
			{
				n.Quader(M(new Vector3(x - s * 0.18f, 0.62f, -1.56f + b * 1.56f), new Vector3(0.16f, 0.45f, 0.95f)), L, Color.white);
				n.Rohr(new Vector3(x - 0.08f, 1.08f, -1.56f + b * 1.56f), new Vector3(x + 0.08f, 1.08f, -1.56f + b * 1.56f), 0.1f, 0.1f, 8, K, Color.white, Color.white, 0f, 1f, true);
			}
			// Kette: oben und unten mit Gliedern. Rechts gerissen: Das untere Stueck fehlt hinten,
			// ein Stueck liegt hinter dem Panzer im Sand.
			bool gerissen = s > 0f;
			n.Quader(M(new Vector3(x, 1.2f, 0f), new Vector3(0.44f, 0.06f, 5.0f)), K, Color.white);
			float untenVon = gerissen ? -0.6f : -2.4f;
			n.Quader(M(new Vector3(x, 0.05f, (untenVon + 2.4f) * 0.5f), new Vector3(0.44f, 0.06f, 2.4f - untenVon)), K, Color.white);
			for (float z = -2.4f; z <= 2.45f; z += 0.17f)
			{
				n.Quader(M(new Vector3(x, 1.24f, z), new Vector3(0.46f, 0.04f, 0.06f)), K, Color.white);
				if (z > untenVon)
				{
					n.Quader(M(new Vector3(x, 0.01f, z), new Vector3(0.46f, 0.04f, 0.06f)), K, Color.white);
				}
			}
			n.Quader(M(new Vector3(x, 0.55f, 2.95f), new Vector3(0.44f, 0.06f, 1.1f), new Vector3(-62f, 0f, 0f)), K, Color.white);
			if (gerissen)
			{
				n.Quader(M(new Vector3(x + 0.2f, -0.05f, -3.6f), new Vector3(0.44f, 0.05f, 2.2f), new Vector3(0f, 15f, 0f)), K, Color.white);
			}
			else
			{
				n.Quader(M(new Vector3(x, 0.32f, -2.85f), new Vector3(0.44f, 0.06f, 0.7f), new Vector3(50f, 0f, 0f)), K, Color.white);
			}
		}
		DDNetz(wrack.transform, "Wanne", n, new[] { lack, verbrannt, kette, rost });

		// Turm (eigenes Netz, gedreht), Kanone gesenkt
		var t = new Netzbau(3);
		t.Rohr(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.72f, 0f), 1.02f, 0.86f, 18, 0, Color.white, Color.white, 0f, 1f, true);
		t.Quader(M(new Vector3(0f, 0.35f, -0.95f), new Vector3(1.4f, 0.58f, 0.75f)), 0, Color.white);
		t.Quader(M(new Vector3(0f, 0.75f, -0.1f), new Vector3(1.5f, 0.04f, 1.5f)), 1, Color.white);
		t.Quader(M(new Vector3(0f, 0.36f, 1.02f), new Vector3(1.05f, 0.6f, 0.36f)), 0, Color.white);
		var kanone = Matrix4x4.TRS(new Vector3(0f, 0.36f, 1.15f), Quaternion.Euler(6f, 0f, 0f), Vector3.one);
		t.Rohr(kanone.MultiplyPoint3x4(Vector3.zero), kanone.MultiplyPoint3x4(new Vector3(0f, 0f, 2.9f)), 0.075f, 0.06f, 12, 2, Color.white, Color.white, 0f, 1f, true);
		t.Rohr(kanone.MultiplyPoint3x4(new Vector3(0f, 0f, 2.85f)), kanone.MultiplyPoint3x4(new Vector3(0f, 0f, 3.1f)), 0.09f, 0.09f, 12, 2, Color.white, Color.white, 0f, 1f, true);
		// Kommandantenkuppel mit offener Luke, MG
		t.Rohr(new Vector3(0.38f, 0.72f, -0.25f), new Vector3(0.38f, 0.92f, -0.25f), 0.34f, 0.32f, 14, 0, Color.white, Color.white, 0f, 1f, true);
		t.Quader(M(new Vector3(0.38f, 1.2f, -0.58f), new Vector3(0.55f, 0.55f, 0.05f), new Vector3(-15f, 0f, 0f)), 1, Color.white);
		t.Rohr(new Vector3(0.38f, 1.02f, 0.0f), new Vector3(0.38f, 1.06f, 0.7f), 0.025f, 0.025f, 8, 2, Color.white, Color.white, 0f, 1f, true);
		// Antenne
		t.Rohr(new Vector3(-0.55f, 0.72f, -0.95f), new Vector3(-0.65f, 2.4f, -1.25f), 0.012f, 0.006f, 5, 2, Color.white, Color.white, 0f, 1f, false);
		DDNetz(wrack.transform, "Turm", t, new[] { lack, verbrannt, kette }, new Vector3(0f, 1.88f, -0.3f), Quaternion.Euler(0f, 25f, 0f));

		DDKasten(wrack.transform, new Vector3(0f, 0.95f, -0.1f), new Vector3(2.7f, 1.9f, 5.8f));
		DDKasten(wrack.transform, new Vector3(0f, 2.25f, -0.3f), new Vector3(1.9f, 0.75f, 2.0f), new Vector3(0f, 25f, 0f));
	}

	// ------------------------------------------------------------------
	// Strandsperren

	// Panzerigel: drei Doppel-T-Traeger, rechtwinklig verschweisst, auf drei Enden stehend.
	static void DDPanzerigelModell(Transform eltern, Vector3 ort, float gier, Material rost)
	{
		var igel = new GameObject("Panzerigel");
		igel.transform.SetParent(eltern, false);
		igel.transform.localPosition = ort;
		igel.transform.localRotation = Quaternion.Euler(0f, gier, 0f);
		var stehen = Quaternion.FromToRotation(new Vector3(1f, 1f, 1f).normalized, Vector3.up);
		const float laenge = 2.2f;
		float mitte = laenge * 0.5f * 0.577f;
		var n = new Netzbau(1);
		var achsen = new[] { Vector3.right, Vector3.up, Vector3.forward };
		for (int i = 0; i < 3; i++)
		{
			var richtung = stehen * achsen[i];
			var lage = Quaternion.LookRotation(richtung, stehen * achsen[(i + 1) % 3]);
			var basis = Matrix4x4.TRS(new Vector3(0f, mitte, 0f), lage, Vector3.one);
			// Gurte und Steg
			n.Quader(basis * M(new Vector3(0f, 0.065f, 0f), new Vector3(0.14f, 0.014f, laenge)), 0, Color.white);
			n.Quader(basis * M(new Vector3(0f, -0.065f, 0f), new Vector3(0.14f, 0.014f, laenge)), 0, Color.white);
			n.Quader(basis * M(Vector3.zero, new Vector3(0.012f, 0.13f, laenge)), 0, Color.white);
			var kasten = new GameObject("Traeger");
			kasten.transform.SetParent(igel.transform, false);
			kasten.transform.localPosition = new Vector3(0f, mitte, 0f);
			kasten.transform.localRotation = lage;
			kasten.AddComponent<BoxCollider>().size = new Vector3(0.16f, 0.16f, laenge);
		}
		// Knotenblech in der Mitte
		n.Quader(M(new Vector3(0f, mitte, 0f), new Vector3(0.24f, 0.24f, 0.24f)), 0, Color.white);
		DDNetz(igel.transform, "Modell", n, new[] { rost });
	}

	// Holzrampe: schraeger Stamm auf einer Stuetze, oben eine Tellermine (zur Kueste hin ansteigend).
	static void DDHolzrampe(Transform eltern, Vector3 ort, float gier, Material holz, Material mine)
	{
		var rampe = new GameObject("Holzrampe");
		rampe.transform.SetParent(eltern, false);
		rampe.transform.localPosition = ort;
		rampe.transform.localRotation = Quaternion.Euler(0f, gier, 0f);
		var n = new Netzbau(2);
		var unten = new Vector3(0f, -0.1f, -2.6f);
		var oben = new Vector3(0f, 2.0f, 1.0f);
		n.Rohr(unten, oben, 0.15f, 0.12f, 9, 0, Color.white, Color.white, 0f, 1f, true);
		n.Rohr(new Vector3(0f, -0.2f, 0.75f), new Vector3(0f, 1.9f, 0.8f), 0.12f, 0.11f, 9, 0, Color.white, Color.white, 0f, 1f, true);
		n.Rohr(new Vector3(-0.5f, -0.2f, 1.3f), new Vector3(0f, 1.6f, 0.75f), 0.08f, 0.07f, 7, 0, Color.white, Color.white, 0f, 1f, true);
		n.Rohr(oben + new Vector3(0f, 0.08f, 0f), oben + new Vector3(0f, 0.2f, 0.02f), 0.17f, 0.15f, 14, 1, Color.white, Color.white, 0f, 1f, true);
		DDNetz(rampe.transform, "Modell", n, new[] { holz, mine });
		var richtung = oben - unten;
		DDKasten(rampe.transform, (unten + oben) * 0.5f, new Vector3(0.3f, 0.3f, richtung.magnitude), Quaternion.LookRotation(richtung).eulerAngles);
		DDKasten(rampe.transform, new Vector3(0f, 0.85f, 0.77f), new Vector3(0.25f, 2.1f, 0.25f));
	}

	// Stacheldrahtrolle (Spirale) mit Pfosten. Ohne Kollider: Schuesse sollen hindurchgehen.
	static void DDStacheldraht(Transform eltern, Vector3 ort, float gier, float laenge, Material draht, Material holz)
	{
		var rolle = new GameObject("Stacheldraht");
		rolle.transform.SetParent(eltern, false);
		rolle.transform.localPosition = ort;
		rolle.transform.localRotation = Quaternion.Euler(0f, gier, 0f);
		var n = new Netzbau(2);
		const float radius = 0.42f;
		const int proWindung = 12;
		int windungen = Mathf.RoundToInt(laenge / 0.22f);
		Vector3 vorher = Vector3.zero;
		for (int i = 0; i <= windungen * proWindung; i++)
		{
			float w = i * Mathf.PI * 2f / proWindung;
			float x = -laenge * 0.5f + laenge * i / (windungen * proWindung);
			var punkt = new Vector3(x + Mathf.Sin(w * 0.5f) * 0.03f, radius + Mathf.Cos(w) * radius, Mathf.Sin(w) * radius);
			if (i > 0)
			{
				n.Rohr(vorher, punkt, 0.008f, 0.008f, 4, 0, Color.white, Color.white, 0f, 1f, false);
			}
			vorher = punkt;
		}
		for (float x = -laenge * 0.5f; x <= laenge * 0.5f + 0.01f; x += 2f)
		{
			n.Rohr(new Vector3(x, 0f, 0f), new Vector3(x, 1.05f, 0f), 0.045f, 0.04f, 6, 1, Color.white, Color.white, 0f, 1f, true);
		}
		DDNetz(rolle.transform, "Modell", n, new[] { draht, holz });
	}

	// Einzelne Sandsaecke statt grosser Kaesten (Aussehen). Reihe entlang a nach b, mehrere Lagen.
	static void DDSandsackReihe(Netzbau n, Vector3 a, Vector3 b, int lagen, System.Random zufall)
	{
		var richtung = b - a;
		float laenge = richtung.magnitude;
		var lage = Quaternion.LookRotation(richtung.normalized);
		int proLage = Mathf.Max(1, Mathf.RoundToInt(laenge / 0.62f));
		for (int l = 0; l < lagen; l++)
		{
			float versatz = (l % 2) * 0.31f;
			for (int i = 0; i < proLage; i++)
			{
				float t = Mathf.Clamp((i + 0.5f) * 0.62f + versatz, 0.2f, laenge - 0.2f);
				var ort = a + richtung.normalized * t + Vector3.up * (0.11f + l * 0.2f);
				var drehung = lage * Quaternion.Euler(Zufall(zufall, -4f, 4f), 90f + Zufall(zufall, -6f, 6f), Zufall(zufall, -3f, 3f));
				// Sack: Kern plus abgerundete Enden (kleinere Kaesten)
				n.Quader(Matrix4x4.TRS(ort, drehung, new Vector3(0.36f, 0.2f, 0.52f)), 0, Color.white);
				n.Quader(Matrix4x4.TRS(ort, drehung, new Vector3(0.3f, 0.23f, 0.44f)), 0, Color.white);
				n.Quader(Matrix4x4.TRS(ort, drehung, new Vector3(0.4f, 0.14f, 0.6f)), 0, Color.white);
			}
		}
	}

	// ------------------------------------------------------------------
	// Bunker (Kasematte): dicke Waende, zwei Schiessscharten zur See, abgewinkelter Eingang hinten
	// mit Splitterschutzwand, ebener Zugang (Boden auf Hoehe des Gelaendes), innen MG-Tische,
	// Munitionskisten, eine Lampe. Front zeigt nach -z (zum Strand).

	const float BU_BREITE = 12f;
	const float BU_TIEFE = 9f;
	const float BU_HOEHE = 3.6f;
	const float BU_WAND = 1.2f;

	static void DDBunkerNeu(Transform eltern, Vector3 position, Material beton, Material holz, Material stahl, Material erde, Material sandsack, Material licht)
	{
		var bunker = new GameObject("Bunker");
		bunker.transform.SetParent(eltern, false);
		bunker.transform.localPosition = position;
		var t = bunker.transform;
		float hinten = (BU_TIEFE - BU_WAND) * 0.5f;
		float seite = (BU_BREITE - BU_WAND) * 0.5f;
		// Boden buendig mit dem Gelaende (kein Absatz am Eingang)
		Klotz(t, "Boden", new Vector3(0f, -0.05f, 0f), new Vector3(BU_BREITE, 0.12f, BU_TIEFE), beton);
		Klotz(t, "WandLinks", new Vector3(-seite, BU_HOEHE * 0.5f, 0f), new Vector3(BU_WAND, BU_HOEHE, BU_TIEFE), beton);
		Klotz(t, "WandRechts", new Vector3(seite, BU_HOEHE * 0.5f, 0f), new Vector3(BU_WAND, BU_HOEHE, BU_TIEFE), beton);
		// Front mit zwei Scharten (je 2,2 m breit, 1,1 bis 2,3 m hoch)
		// Die Augen der Figur liegen 1,88 m ueber dem Boden: Die Scharte muss darum herum liegen,
		// und weit genug hinunter reichen, um den Strand unter dem Hang zu sehen (etwa 25 Grad).
		const float schartenUnten = 1.1f;
		const float schartenOben = 2.3f;
		Klotz(t, "FrontUnten", new Vector3(0f, schartenUnten * 0.5f, -hinten), new Vector3(BU_BREITE, schartenUnten, BU_WAND), beton);
		Klotz(t, "FrontOben", new Vector3(0f, (schartenOben + BU_HOEHE) * 0.5f, -hinten), new Vector3(BU_BREITE, BU_HOEHE - schartenOben, BU_WAND), beton);
		// Pfeiler zwischen den Scharten: links, Mitte, rechts
		foreach (var p in new[] { new Vector2(-4.4f, 1.6f), new Vector2(0f, 2.8f), new Vector2(4.4f, 1.6f) })
		{
			Klotz(t, "Schartenpfeiler", new Vector3(p.x, (schartenUnten + schartenOben) * 0.5f, -hinten), new Vector3(p.y, schartenOben - schartenUnten, BU_WAND), beton);
		}
		// Rueckwand mit Tuer rechts (1,4 m breit, 2,2 m hoch)
		const float tuerX = 3.4f;
		const float tuerBreite = 1.4f;
		const float tuerHoehe = 2.2f;
		float linksBis = tuerX - tuerBreite * 0.5f;
		float rechtsAb = tuerX + tuerBreite * 0.5f;
		Klotz(t, "Rueckwand", new Vector3((-BU_BREITE * 0.5f + linksBis) * 0.5f, BU_HOEHE * 0.5f, hinten), new Vector3(linksBis + BU_BREITE * 0.5f, BU_HOEHE, BU_WAND), beton);
		Klotz(t, "Rueckwand", new Vector3((rechtsAb + BU_BREITE * 0.5f) * 0.5f, BU_HOEHE * 0.5f, hinten), new Vector3(BU_BREITE * 0.5f - rechtsAb, BU_HOEHE, BU_WAND), beton);
		Klotz(t, "Tuersturz", new Vector3(tuerX, (tuerHoehe + BU_HOEHE) * 0.5f, hinten), new Vector3(tuerBreite, BU_HOEHE - tuerHoehe, BU_WAND), beton);
		// Decke 1 m dick, mit Erde und Grasnarbe obendrauf (Tarnung)
		Klotz(t, "Decke", new Vector3(0f, BU_HOEHE + 0.5f, 0f), new Vector3(BU_BREITE + 0.6f, 1f, BU_TIEFE + 0.6f), beton);
		var narbe = Klotz(t, "Erdabdeckung", new Vector3(0f, BU_HOEHE + 1.15f, 0.3f), new Vector3(BU_BREITE - 0.6f, 0.35f, BU_TIEFE - 0.4f), erde);
		Object.DestroyImmediate(narbe.GetComponent<Collider>());
		// Splitterschutzwand vor der Tuer: Man kommt seitlich hinein, nicht gerade durch.
		Klotz(t, "Schutzwand", new Vector3(tuerX - 0.4f, 1.25f, hinten + 2.3f), new Vector3(3.6f, 2.5f, 0.8f), beton);
		Klotz(t, "Schutzwand", new Vector3(tuerX + 1.75f, 1.25f, hinten + 1.4f), new Vector3(0.7f, 2.5f, 1.8f), beton);
		// Innen MG-Tische und Munition
		var n = new Netzbau(3);
		foreach (var x in new[] { -2.6f, 2.6f })
		{
			n.Quader(M(new Vector3(x, 0.95f, -hinten + 1.1f), new Vector3(1.6f, 0.08f, 0.8f)), 1, Color.white);
			foreach (var bx in new[] { -0.7f, 0.7f })
			{
				n.Quader(M(new Vector3(x + bx, 0.47f, -hinten + 1.1f), new Vector3(0.08f, 0.9f, 0.7f)), 1, Color.white);
			}
			// MG auf Lafette
			n.Quader(M(new Vector3(x, 1.12f, -hinten + 0.95f), new Vector3(0.14f, 0.18f, 0.9f)), 2, Color.white);
			n.Rohr(new Vector3(x, 1.15f, -hinten + 0.5f), new Vector3(x, 1.15f, -hinten - 0.2f), 0.025f, 0.025f, 8, 2, Color.white, Color.white, 0f, 1f, true);
			n.Quader(M(new Vector3(x - 0.4f, 0.2f, -hinten + 2.2f), new Vector3(0.6f, 0.4f, 0.35f), new Vector3(0f, 15f, 0f)), 1, Color.white);
			n.Quader(M(new Vector3(x - 0.35f, 0.6f, -hinten + 2.25f), new Vector3(0.6f, 0.4f, 0.35f), new Vector3(0f, -8f, 0f)), 1, Color.white);
		}
		// Bank an der Rueckwand, Lampe an der Decke
		n.Quader(M(new Vector3(-2.5f, 0.45f, hinten - 1.0f), new Vector3(3.5f, 0.08f, 0.5f)), 1, Color.white);
		n.Quader(M(new Vector3(-2.5f, 0.22f, hinten - 1.0f), new Vector3(3.3f, 0.44f, 0.08f)), 1, Color.white);
		DDNetz(t, "Ausstattung", n, new[] { beton, holz, stahl });
		var lampe = Klotz(t, "Lampe", new Vector3(0f, BU_HOEHE - 0.12f, 0.5f), new Vector3(0.35f, 0.15f, 0.35f), licht);
		Object.DestroyImmediate(lampe.GetComponent<Collider>());
		var lichtGo = KGruppe(t, "Bunkerlicht");
		lichtGo.transform.localPosition = new Vector3(0f, BU_HOEHE - 0.5f, 0.5f);
		var l = lichtGo.AddComponent<Light>();
		l.type = LightType.Point;
		l.color = new Color(1f, 0.85f, 0.6f);
		l.intensity = 1.4f;
		l.range = 9f;
		// Sandsaecke an den Ecken der Schutzwand
		var saecke = new Netzbau(1);
		var z = new System.Random(77 + (int)position.x);
		DDSandsackReihe(saecke, new Vector3(-BU_BREITE * 0.5f + 0.3f, 0f, hinten + 1.2f), new Vector3(-1.2f, 0f, hinten + 1.2f), 3, z);
		DDNetz(t, "Sandsaecke", saecke, new[] { sandsack });
		DDKasten(t, new Vector3((-BU_BREITE * 0.5f + 0.3f - 1.2f) * 0.5f, 0.3f, hinten + 1.2f), new Vector3(BU_BREITE * 0.5f - 1.5f, 0.6f, 0.6f));
	}
}
