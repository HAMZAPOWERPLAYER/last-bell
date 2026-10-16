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
		// 2.0 SAVE & CONTINUE: the Menu used to call GameManager.EndRun() here, wiping run
		// progress on arrival. It does not any more - progress now lives in RunSave and is what
		// the menu's CONTINUE button resumes. It is cleared only by PLAY (a deliberate new run)
		// or by escaping (ExitTrigger calls EndRun at the winning door).
		ApplyMusicState();
    }

	void Update(){
		// BUGFIX: this used to rewrite AudioListener.volume and both icon SetActive calls every
		// single frame. That fought the settings screen's master-volume slider (with the music
		// toggle off it pinned the whole game to silence, so the slider looked dead) and did
		// per-frame work for a value that only changes on a toggle. It now reacts to change only.
		if(!appliedMusicState.HasValue || appliedMusicState.Value != isMusicOn) ApplyMusicState();
	}

	bool? appliedMusicState;

	void ApplyMusicState(){
		appliedMusicState = isMusicOn;
		if(music_on != null) music_on.SetActive(isMusicOn);
		if(music_off != null) music_off.SetActive(!isMusicOn);
		AudioListener.volume = isMusicOn ? GameSettings.MasterVolume : 0f;
	}
	public void goToMainMenu(){
		SceneManager.LoadScene("Menu");
	}
	public void goToGameWin(){
		SceneManager.LoadScene("GameWin");
	}
	public void revive(){
		// Ads removed: resume from the last checkpoint instead of showing a rewarded ad.
		// ContinueRun also covers the no-save case by starting a fresh run.
		GameManager.ContinueRun();
		SceneManager.LoadScene("MainLevel");
	}
	public void goToGame(){
		// Same semantics as the MainMenuUI PLAY button: this begins a new run, so stale progress
		// from a previous session must not be inherited.
		GameManager.StartNewRun();
		SceneManager.LoadScene("MainLevel");
	}
	public void Quit(){
		Application.Quit();
	}
	public void ToggleMusic(){
		isMusicOn =  !isMusicOn;
		ApplyMusicState();
	}
	public void goToPrivacy(){
		Application.OpenURL("https://onionkeygames.blogspot.com/2020/10/privacy-policy-tom-felton-built-zabel.html");
	}
}
