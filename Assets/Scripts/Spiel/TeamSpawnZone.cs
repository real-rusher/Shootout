using UnityEngine;

// Markiert einen BoxCollider (Trigger) als Spawn-Bereich eines Teams.
// Spawnlocation sammelt diese Bereiche zusaetzlich zu den BoxCollidern an der Map-Wurzel.
[RequireComponent(typeof(BoxCollider))]
public class TeamSpawnZone : MonoBehaviour
{
	// 1 = Alliierte, 2 = Verteidiger (siehe Teams).
	public int team = 1;
}
