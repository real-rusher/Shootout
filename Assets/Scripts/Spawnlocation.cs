using System.Collections.Generic;
using UnityEngine;

public class Spawnlocation : MonoBehaviour
{
	public class SpawnBox
	{
		private Vector3 corner1;

		private Vector3 corner2;

		// Team dieses Bereichs (0 = beliebig).
		public int team;

		public SpawnBox(Vector3 corner1, Vector3 corner2)
		{
			this.corner1 = corner1;
			this.corner2 = corner2;
		}

		public Vector3 mitte => (corner1 + corner2) * 0.5f;

		public Vector3 getSpawnPos()
		{
			Vector3 result = default(Vector3);
			result.x = Random.Range(corner1.x, corner2.x);
			result.y = Random.Range(corner1.y, corner2.y);
			result.z = Random.Range(corner1.z, corner2.z);
			return result;
		}
	}

	public static Spawnlocation Instance;

	public List<SpawnBox> spawnBoxes = new List<SpawnBox>();

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Object.Destroy(Instance);
		}
	}

	public static void convertCollToBox(GameObject currentMap)
	{
		Instance.spawnBoxes.Clear();
		BoxCollider[] components = currentMap.GetComponents<BoxCollider>();
		for (int i = 0; i < components.Length; i++)
		{
			Instance.spawnBoxes.Add(new SpawnBox(components[i].bounds.min, components[i].bounds.max));
		}
		// Neu: Bereiche mit Team (TeamSpawnZone), z. B. auf der D-Day-Map.
		bool hatTeamBereiche = false;
		foreach (TeamSpawnZone zone in currentMap.GetComponentsInChildren<TeamSpawnZone>(true))
		{
			Bounds grenzen = zone.GetComponent<BoxCollider>().bounds;
			SpawnBox box = new SpawnBox(grenzen.min, grenzen.max);
			box.team = zone.team;
			Instance.spawnBoxes.Add(box);
			hatTeamBereiche = true;
		}
		if (!hatTeamBereiche)
		{
			teamsAufteilen();
		}
	}

	// Maps ohne Team-Bereiche: Boxen entlang der laengsten Ausdehnung in zwei Haelften
	// teilen, damit die Teams auf verschiedenen Seiten starten.
	private static void teamsAufteilen()
	{
		List<SpawnBox> boxen = Instance.spawnBoxes;
		if (boxen.Count < 2)
		{
			return;
		}
		Bounds alle = new Bounds(boxen[0].mitte, Vector3.zero);
		foreach (SpawnBox box in boxen)
		{
			alle.Encapsulate(box.mitte);
		}
		bool entlangX = alle.size.x >= alle.size.z;
		List<SpawnBox> sortiert = new List<SpawnBox>(boxen);
		sortiert.Sort((a, b) => entlangX ? a.mitte.x.CompareTo(b.mitte.x) : a.mitte.z.CompareTo(b.mitte.z));
		for (int i = 0; i < sortiert.Count; i++)
		{
			sortiert[i].team = i < sortiert.Count / 2 ? Teams.ALLIIERTE : Teams.VERTEIDIGER;
		}
	}

	public static bool hatPunkte()
	{
		return Instance != null && Instance.spawnBoxes.Count > 0;
	}

	public static Vector3 getLocation()
	{
		return getLocation(Teams.KEINS);
	}

	// Ohne Team (0) irgendein Bereich, sonst einer des Teams.
	public static Vector3 getLocation(int team)
	{
		List<SpawnBox> passend = new List<SpawnBox>();
		foreach (SpawnBox box in Instance.spawnBoxes)
		{
			if (team == Teams.KEINS || box.team == team)
			{
				passend.Add(box);
			}
		}
		if (passend.Count == 0)
		{
			passend = Instance.spawnBoxes;
		}
		return passend[Random.Range(0, passend.Count)].getSpawnPos();
	}

	// Mitte aller Spawnbereiche, damit man nach dem Spawn ins Geschehen schaut.
	public static Vector3 getMitte()
	{
		if (!hatPunkte())
		{
			return Vector3.zero;
		}
		Vector3 summe = Vector3.zero;
		foreach (SpawnBox box in Instance.spawnBoxes)
		{
			summe += box.mitte;
		}
		return summe / Instance.spawnBoxes.Count;
	}
}
