using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitTrigger : MonoBehaviour
{
  //gm
	private GameObject  _gm;
	public GameObject openDoor_UI;
	
	//Door / exit audio. Unassigned by default so behaviour is unchanged until clips are supplied.
	public AudioSource door_source;
	public AudioClip door_clip;
	[Tooltip("Seconds to wait before loading the next scene, so the door sound can finish. 0 = load immediately.")]
	public float doorSoundDelay = 0f;
    // Start is called before the first frame update
    void Start()
    {
		_gm = GameObject.FindGameObjectWithTag("GameManager");
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
	public void OnTriggerEnter(Collider col){
		if(col.gameObject.tag == "Player"){
			if(_gm.GetComponent<GameManager>().keysfound == 3){
				openDoor_UI.SetActive(true);
			}
		}
	}
	public void OnTriggerExit(Collider col){
		if(col.gameObject.tag == "Player"){
			openDoor_UI.SetActive(false);
		}
	}
	public void GameWin(){
		// Door audio hook. There is no swinging-door mechanic in the project - the only "door"
		// interaction is this exit transition - so the sound plays on the transition itself.
		// While door_clip is null this behaves exactly as before.
		if(door_clip != null){
			if(door_source != null){
				door_source.clip = door_clip;
				door_source.Play();
			}else{
				AudioSource.PlayClipAtPoint(door_clip, transform.position);
			}
			if(doorSoundDelay > 0f){
				StartCoroutine(LoadNextSceneAfterDelay());
				return;
			}
		}
		SceneManager.LoadScene("Bedroom");
	}
	
	IEnumerator LoadNextSceneAfterDelay(){
		yield return new WaitForSeconds(doorSoundDelay);
		SceneManager.LoadScene("Bedroom");
	}
}