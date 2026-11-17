using System.Collections.Generic;
using UnityEngine;

public class Spawnlocation : MonoBehaviour
{
	public class SpawnBox
	{
		private Vector3 corner1;

		private Vector3 corner2;

		public SpawnBox(Vector3 corner1, Vector3 corner2)
		{
			this.corner1 = corner1;
			this.corner2 = corner2;
		}

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
	}

	public static Vector3 getLocation()
	{
		return Instance.spawnBoxes[Random.Range(0, Instance.spawnBoxes.Count)].getSpawnPos();
	}
}
