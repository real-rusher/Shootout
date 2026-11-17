using System.Collections;
using UnityEngine;

public class LaserEffect : MonoBehaviour
{
	public float effectTime = 0.3f;

	public int effectWidth = 40;

	private void Start()
	{
		StartCoroutine(destroySelf());
	}

	public void InitializeLaser(Vector3 camPos, Quaternion camRotation, float camDistance, Vector3 laserPointerPos)
	{
		Vector3 vector = (camRotation * Vector3.forward).normalized * camDistance + camPos;
		Quaternion rotation = Quaternion.LookRotation(laserPointerPos - vector, Vector3.up);
		float num = Vector3.Distance(laserPointerPos, vector);
		base.transform.position = laserPointerPos;
		base.transform.rotation = rotation;
		base.transform.localScale = new Vector3(effectWidth, effectWidth, 100f * num);
	}

	private IEnumerator destroySelf()
	{
		yield return new WaitForSeconds(effectTime);
		Object.Destroy(base.gameObject);
	}
}
