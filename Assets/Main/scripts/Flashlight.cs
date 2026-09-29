using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using NeKoRoSYS.InputHandling.Mobile;

public class Flashlight : MonoBehaviour
{
	public Light f_Light;
	public bool isFlashlightOn = true;
	public float battery = 100f;
	public Text battery_UI;
	
	public float starttime;
	public float timeOfBatteryLeft;
	
	public AudioSource flash_source;
	public AudioClip flash_clip;
	public ControlButton flashlightButton;
	
	[Header("Battery drain")]
	[Tooltip("Seconds of light the battery holds when drain is enabled (the 'battery' field above is the same value used as 100%).")]
	public bool batteryDrainEnabled = false;
	
    // Start is called before the first frame update
    void Start()
    {
        starttime = Time.time;
		timeOfBatteryLeft = ((starttime + battery) - Time.time);
		if(flashlightButton != null){
			flashlightButton.OnButtonAction += (pressed) => {
				if(pressed) on_Off();
			};
		}
		UpdateBatteryUI();
    }

    // Update is called once per frame
    void Update()
    {
        FlashControl();
		// Drain is opt-in: the flashlight is the player's only light source and no battery
		// pickups exist yet, so shipping with a drain would make the game unwinnable.
		// Flip batteryDrainEnabled (and add pickups - see Addbattery) to enable it.
		if(batteryDrainEnabled) batteryControl();
		UpdateBatteryUI();
    }
	
	void FlashControl(){
		if(isFlashlightOn && timeOfBatteryLeft > 0){
			f_Light.intensity = 7;
		}else{
			f_Light.intensity = 0;
		}
	}
	void batteryControl(){
		if((Time.time < starttime + battery) && isFlashlightOn){
			timeOfBatteryLeft = ((starttime + battery) - Time.time);
		}else{
			timeOfBatteryLeft = 0f;
			isFlashlightOn = false;
		}
		
	}
	void UpdateBatteryUI(){
		if(battery_UI == null) return;
		if(!batteryDrainEnabled){
			// No drain: a countdown would be a lie, so show an explicit "no limit" marker.
			battery_UI.text = "Battery: --";
			return;
		}
		int percent = Mathf.Clamp(Mathf.RoundToInt((timeOfBatteryLeft / Mathf.Max(1f, battery)) * 100f), 0, 100);
		battery_UI.text = "Battery: " + percent + "%";
	}
	public void on_Off(){
		isFlashlightOn = !isFlashlightOn;
		if(flash_source != null && flash_clip != null){
			flash_source.clip = flash_clip;
			flash_source.Play();
		}
	}
	public void Addbattery(float n){
		if(n <= 0f) return;
		// BUGFIX: upstream rewrote starttime to ((starttime + battery) - Time.time) + n, which
		// granted (n + battery) seconds - i.e. every pickup refilled a whole extra battery (100s).
		// Granting n seconds is simply advancing the drain start by n.
		starttime += n;
		timeOfBatteryLeft = Mathf.Clamp(timeOfBatteryLeft + n, 0f, battery);
		// A fresh battery must also re-ignite a light that the drain switched off.
		if(timeOfBatteryLeft > 0f) isFlashlightOn = true;
		UpdateBatteryUI();
	}
}
