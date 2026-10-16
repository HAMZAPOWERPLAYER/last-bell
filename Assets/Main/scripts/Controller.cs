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
        characterController = GetComponent<CharacterController>();
		if(characterController == null){
			Debug.LogError("[Controller] A CharacterController component is required.", this);
			enabled = false;
			return;
		}
		if(_audiosource == null) _audiosource = GetComponent<AudioSource>();
		if(_audiosource != null) _audiosource.clip = walkaudio;
		GameSettings.ApplyLook(this);
    }

    /// <summary>Clears mobile input so a touch held across pause cannot move the player after resume.</summary>
    public void ResetTouchInput()
    {
        if (movementStick != null) movementStick.ForceStopTouch();
        if (cameraPad != null) cameraPad.ForceStopTouch();
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
			if(_audiosource != null) _audiosource.pitch = 0.7f;
			
			float speed = walkspeed;
			Transform movementCamera = cam != null ? cam.transform : transform;
			Vector3 forward = Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up);
			if(forward.sqrMagnitude < 0.0001f)
				forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
			forward.Normalize();
			Vector3 right = Vector3.Cross(Vector3.up, forward);

			// Keep movement horizontal and responsive even on the first frame after a pause,
			// on stairs, or while the CharacterController is briefly airborne.
			fwdMovement = forward * vInput;
			rightMovement = right * hInput;

			// Pushing the stick to its outer ring sprints. Replaces the desktop-only "Run" axis,
			// which no touch device can ever press.
			if(movementStick.FullInput){
				speed = runspeed;
				if(_audiosource != null) _audiosource.pitch = 1f;
			}
			
			// Footsteps only while actually moving and grounded (upstream played the loop permanently).
			if(_audiosource != null){
				if(isMoving && characterController.isGrounded){
					if(!_audiosource.isPlaying) _audiosource.Play();
				}else if(_audiosource.isPlaying){
					_audiosource.Stop();
				}
			}
			
			characterController.SimpleMove(Vector3.ClampMagnitude(fwdMovement + rightMovement, 1f)* speed);
		} else if(_audiosource != null){
			_audiosource.Stop();
		}
	}
	void FPSCamera(){
		if(cam == null) return;
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
		// BUGFIX: the old version subtracted 360 at most once and then clamped, so yaw stuck at
		// +/-360 degrees of accumulated drag until the player dragged back the other way. A
		// continuous range that already spans a full turn is wrapped into (-180,180] instead;
		// a genuinely limited range (pitch) is still clamped.
		if(max - min >= 360f) angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
		return Mathf.Clamp(angle,min,max);
	}
}
