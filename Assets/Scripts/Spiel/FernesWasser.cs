using UnityEngine;

// Meer bis zum Horizont: nimmt wie das nahe Wasser (Wasser.cs) in den Stufen "Hoch" und "Ultra"
// das bessere Material, damit am Uebergang keine Kante zu sehen ist.
public class FernesWasser : MonoBehaviour
{
	public Material normalMaterial;

	public Material hochMaterial;

	private void OnEnable()
	{
		Grafik.Geaendert += anwenden;
		anwenden();
	}

	private void OnDisable()
	{
		Grafik.Geaendert -= anwenden;
	}

	private void anwenden()
	{
		Material material = Grafik.Hoch && hochMaterial != null ? hochMaterial : normalMaterial;
		foreach (MeshRenderer anzeige in GetComponentsInChildren<MeshRenderer>(true))
		{
			anzeige.sharedMaterial = material;
		}
	}
}
