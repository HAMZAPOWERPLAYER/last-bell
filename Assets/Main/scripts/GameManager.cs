using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using NeKoRoSYS.InputHandling.Mobile;

public class GameManager : MonoBehaviour
{
	//UI
	public GameObject pickUp_UI;
	public GameObject outro_UI;
	public Text keyCounter_txt;
	//controllers
	public int keysfound = 0;
	public bool isGamePaused = false;
	//JumpScare Spawn
	public GameObject monsterprefab;
	public Transform[] spawnPositions = new Transform[6];
	//trigger
	public bool isOnKeyTrigger_ = false;
	public bool isOnPageTrigger_ = false;
	public GameObject currentKey = null;
	public GameObject currentPage = null;
	public int currentPageNumber = 0;
	//Counters
	public int amountOfPagesIngame = 0;
	public int amountOfKeysIngame = 0;
	//pages
	public GameObject[] pages = new GameObject[6];
	public ControlButton interactButton;
	
	//Pickup audio. Clips are left unassigned until real assets are supplied - playback is
	//skipped when a clip is null, so behaviour is unchanged in the meantime.
	public AudioSource pickup_source;
	public AudioClip keyPickup_clip;
	public AudioClip pagePickup_clip;
	
	
	//Audio from pages
	
    // Start is called before the first frame update
    void Start()
    {
		amountOfKeysIngame = PlayerPrefs.GetInt("Keys", 0);
		keysfound = PlayerPrefs.GetInt("Keys", 0);
		keyCounter_txt.text = keysfound+" keys found";
        initSpawns();
		spawnJumpScare();
		if(interactButton != null){
			interactButton.OnButtonAction += (pressed) => {
				if(pressed) pick();
			};
		}
    }

    // Update is called once per frame
    void Update()
    {
        pickUp_UI.SetActive(isOnKeyTrigger_ || isOnPageTrigger_);
		if(keysfound == 3){
			outro_UI.SetActive(true);
		}
    }
	void initSpawns(){
		GameObject[] spawns = GameObject.FindGameObjectsWithTag("SpawnItem");
		
		while(amountOfPagesIngame < 6 || amountOfKeysIngame < 3 ){
			
			foreach(GameObject i in spawns){
				int rand = Random.Range(1,5);
				if(amountOfPagesIngame < 6 && rand == 2 && i.GetComponent<SpawnItem>().kindofItem == "none"){
					i.GetComponent<SpawnItem>().kindofItem = "Page";
					i.GetComponent<SpawnItem>().spawnItem();
					amountOfPagesIngame++;
				}
				if(amountOfKeysIngame < 3 && rand == 3 && i.GetComponent<SpawnItem>().kindofItem == "none"){
					i.GetComponent<SpawnItem>().kindofItem = "Key";
					i.GetComponent<SpawnItem>().spawnItem();
					amountOfKeysIngame++;
				}
			}
		}
	}
	void spawnJumpScare(){
		if(monsterprefab == null){
			Debug.LogWarning("[GameManager] monsterprefab is not assigned; skipping jump scare spawn.");
			return;
		}
		// Never index the array blindly: unassigned/blank entries used to throw a
		// NullReferenceException here, which aborted Start() before interactButton was bound.
		List<Transform> valid = new List<Transform>();
		if(spawnPositions != null){
			foreach(Transform t in spawnPositions){
				if(t != null) valid.Add(t);
			}
		}
		if(valid.Count == 0){
			Debug.LogWarning("[GameManager] No spawn positions assigned; skipping jump scare spawn.");
			return;
		}
		int rand = Random.Range(1, valid.Count + 1);
		Instantiate(monsterprefab, valid[rand - 1].position, valid[rand - 1].rotation);
	}
	public void closePage(){
		for(int i = 0; i < pages.Length; i++){
			if(pages[i] != null) pages[i].SetActive(false);
		}
	}
	public void pick(){
		if(currentKey != null){
			keysfound++;
			isOnKeyTrigger_ = false;
			PlayerPrefs.SetInt("Keys",keysfound);
			Destroy(currentKey);
			keyCounter_txt.text = keysfound+" keys found";
			PlayPickup(keyPickup_clip);
		}
		else if(currentPage != null){
			currentPageNumber++;
			isOnPageTrigger_ = false;
			Destroy(currentPage);
			PlayPickup(pagePickup_clip);
			if(currentPageNumber >= 1 && currentPageNumber <= pages.Length && pages[currentPageNumber - 1] != null){
				pages[currentPageNumber - 1].SetActive(true);
				isGamePaused = true;
			}
		}
	}
	
	void PlayPickup(AudioClip clip){
		if(clip == null) return;
		if(pickup_source != null){
			pickup_source.PlayOneShot(clip);
		}else{
			AudioSource.PlayClipAtPoint(clip, transform.position);
		}
	}
}
