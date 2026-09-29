using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NeKoRoSYS.InputHandling.Mobile;

public class Controller : MonoBehaviour
{
	[Tooltip("Camera degrees per pixel of touch-drag on the look pad. Primary look tuning knob for mobile.")]
	public float cameraSensitivity = 0.15f;
	[Tooltip("Multiplier on the movement stick's analog output. 1 = full stick deflection = full walk speed.")]
	public float movementSensitivity = 1f;
	public float walkspeed = 2.5f;
	public float runspeed = 6f;
	
	public float rotationX;
	public float rotationY;
	
	public AudioSource _audiosource;
	public AudioClip   walkaudio;
	CharacterController characterController;
	
	//NeKONTROLLER touch controls
	public ControlStick movementStick;
	public ControlPad cameraPad;
	
	public Camera cam;
    // Start is called before the first frame update
    void Start()
    {
        characterController = this.GetComponent<CharacterController>();
		_audiosource = this.GetComponent<AudioSource>();
		_audiosource.clip = walkaudio;
    }

    // Update is called once per frame
    void Update()
    {
		// While paused, skip input entirely: with Time.timeScale = 0 the camera Lerp is frozen,
		// but rotationX/rotationY would keep accumulating from the look pad and then snap the view
		// the moment the player resumes.
		if(PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

		FPSCamera();
		PlayerControls();
		// Escape / Android back is handled by PauseManager (it opens the pause menu).
		// This used to call Application.Quit(), so a back-button press quit the game outright.
    }
	void PlayerControls(){
		if(movementStick != null){
			// Analog stick deflection (0..1 magnitude), not a quantised on/off.
			Vector2 stick = movementStick.input;
			if(stick.sqrMagnitude > 1f) stick = stick.normalized;
			float hInput = stick.x * movementSensitivity;
			float vInput = stick.y * movementSensitivity;
			bool isMoving = stick.sqrMagnitude > 0.01f;
			
			Vector3 fwdMovement = Vector3.zero;
			Vector3 rightMovement = Vector3.zero;
			_audiosource.pitch = 0.7f;
			
			float speed = walkspeed;
			if(characterController.isGrounded == true){
				fwdMovement = cam.gameObject.transform.forward * vInput;
				rightMovement = cam.gameObject.transform.right * hInput;
			}
			// Pushing the stick to its outer ring sprints. Replaces the desktop-only "Run" axis,
			// which no touch device can ever press.
			if(movementStick.FullInput){
				speed = runspeed;
				_audiosource.pitch = 1f;
			}
			
			// Footsteps only while actually moving and grounded (upstream played the loop permanently).
			if(isMoving && characterController.isGrounded){
				if(!_audiosource.isPlaying){
					_audiosource.Play();
				}
			} else if(_audiosource.isPlaying){
				_audiosource.Stop();
			}
			
			characterController.SimpleMove(Vector3.ClampMagnitude(fwdMovement + rightMovement, 1f)* speed);
		} else {
			_audiosource.Stop();
		}
	}
	void FPSCamera(){
		if(cameraPad != null){
			// cameraPad.delta is accumulated touch movement in SCREEN PIXELS for this frame.
			rotationX += cameraPad.delta.x * cameraSensitivity;
			rotationY += cameraPad.delta.y * cameraSensitivity;
		}
		
		rotationX = AngleCorrection(rotationX,-360,360);
		rotationY = AngleCorrection(rotationY,-70,90);
		
		Quaternion xQuat = Quaternion.AngleAxis(rotationX,Vector3.up);
		Quaternion yQuat = Quaternion.AngleAxis(rotationY,-Vector3.right);
		
		Quaternion finalQuat = Quaternion.identity * xQuat * yQuat;
		
		cam.transform.localRotation = Quaternion.Lerp(cam.transform.localRotation, finalQuat, Time.deltaTime * 20f);
	}
	float AngleCorrection(float angle,float min,float max){
		if(angle > 360){
			angle -= 360;
		}
		if(angle < -360){
			angle += 360;
		}
		return Mathf.Clamp(angle,min,max);
	}
}
