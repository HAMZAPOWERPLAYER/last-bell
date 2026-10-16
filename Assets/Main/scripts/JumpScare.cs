using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JumpScare : MonoBehaviour
{
	public GameObject jumpScare_UI;
	public GameObject Canvas;
	public void Start(){
		// BUGFIX: this dereferenced both the tagged Canvas and its "JumpScare" child without a
		// null check, so a missing tag/child threw a NullReferenceException out of Start().
		GameObject canvasObject = GameObject.FindGameObjectWithTag("Canvas");
		if(canvasObject == null){
			Debug.LogError("[JumpScare] No GameObject tagged 'Canvas' was found; the jump scare cannot be shown.", this);
			return;
		}
		Canvas = canvasObject;
		Transform scareTransform = Canvas.transform.Find("JumpScare");
		if(scareTransform == null){
			Debug.LogError("[JumpScare] The tagged Canvas has no child named 'JumpScare'.", this);
			return;
		}
		jumpScare_UI = scareTransform.gameObject;
	}
    public void OnTriggerEnter(Collider col){
		if(col.gameObject.tag == "Player"){
			if(jumpScare_UI != null) jumpScare_UI.SetActive(true);
			Destroy(this.gameObject);
		}
	}
}
