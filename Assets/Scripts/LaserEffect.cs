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
		zeigeStrahl(laserPointerPos, vector);
	}

	// Strahl vom Laserpointer bis zum Trefferpunkt. Rechnung wie im Original.
	public void zeigeStrahl(Vector3 laserPointerPos, Vector3 trefferpunkt)
	{
		Quaternion rotation = Quaternion.LookRotation(laserPointerPos - trefferpunkt, Vector3.up);
		float num = Vector3.Distance(laserPointerPos, trefferpunkt);
		base.transform.position = laserPointerPos;
		base.transform.rotation = rotation;
		base.transform.localScale = new Vector3(effectWidth, effectWidth, 100f * num);
	}

	// Mit Farbe und Dicke je Waffe (Waffen.cs).
	public void zeigeStrahl(Vector3 laserPointerPos, Vector3 trefferpunkt, Color farbe, float dicke)
	{
		zeigeStrahl(laserPointerPos, trefferpunkt);
		Vector3 skala = base.transform.localScale;
		base.transform.localScale = new Vector3(skala.x * dicke, skala.y * dicke, skala.z);
		// PropertyBlock statt eigenem Material: kein Material pro Schuss, das man aufraeumen muesste.
		Renderer anzeige = GetComponent<Renderer>();
		if (anzeige != null)
		{
			MaterialPropertyBlock block = new MaterialPropertyBlock();
			block.SetColor("_Color", farbe);
			block.SetColor("_EmissionColor", farbe * 0.6f);
			anzeige.SetPropertyBlock(block);
		}
	}

	private IEnumerator destroySelf()
	{
		yield return new WaitForSeconds(effectTime);
		Object.Destroy(base.gameObject);
	}
}
