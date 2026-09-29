using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuAction : MonoBehaviour
{
	public GameObject music_on;
	public GameObject music_off;
	public bool isMusicOn = true;

    // Start is called before the first frame update
    void Start()
    {
		if(SceneManager.GetActiveScene().name == "Menu"){
			PlayerPrefs.SetInt("Keys",0);
		}
    }

	void Update(){
		if(isMusicOn && music_on != null){
			music_on.SetActive(true);
			music_off.SetActive(false);
			AudioListener.volume = 1;
		}
		if(!isMusicOn && music_off != null){
			music_on.SetActive(false);
			music_off.SetActive(true);
			AudioListener.volume = 0;
		}
	}
	public void goToMainMenu(){
		SceneManager.LoadScene("Menu");
	}
	public void goToGameWin(){
		SceneManager.LoadScene("GameWin");
	}
	public void revive(){
		// Ads removed: restart the level fresh instead of showing a rewarded ad.
		PlayerPrefs.SetInt("Keys",0);
		PlayerPrefs.Save();
		SceneManager.LoadScene("MainLevel");
	}
	public void goToGame(){
		SceneManager.LoadScene("MainLevel");
	}
	public void Quit(){
		Application.Quit();
	}
	public void ToggleMusic(){
		isMusicOn =  !isMusicOn;
	}
	public void goToPrivacy(){
		Application.OpenURL("http://onionkeygames.blogspot.com/2020/10/privacy-policy-tom-felton-built-zabel.html");
	}
}
