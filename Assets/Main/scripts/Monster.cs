using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Monster : MonoBehaviour
{
	private GameObject  gm;
	
	public GameObject player;
	public Transform player_transform;
	
	public float facePlayerfactor = 20f;
	public bool inView;
	public bool isOnFlashLightTrigger = false;
	public bool nearPlayer;
	public float distancePlayer = 12f;
	
	
	public GameObject cube;
	public Transform cube_transform;
	public Vector3 spawnOrigin;
	public float distanceRayMax;
	public Transform safePlace;
	public bool isSafePlaceavailable;
	
	public float spawnRate =80f;
	public float nextTeleport = 10f;
	public float currentTime;
	
	public float danger = 0f;
	public float dangertemp;
	public float pointInTime_Damage;
	public float pointInTime_health;
	public float lifeDuration = 10f;
	public bool onDanger = false;
	
	public Image deathscreen;
	public float alpha;
	public float deathTime;
	
	[Header("Teleport")]
	[Tooltip("Max random re-rolls when searching for an out-of-view destination. Bounded so the search can never hang.")]
	public int teleportAttempts = 24;
	[Tooltip("Reject destinations closer than this to the player, so it cannot pop in on top of them.")]
	public float teleportMinDistance = 8f;
	[Tooltip("Seconds added to the teleport timer after each teleport (was a hardcoded 10).")]
	public float teleportInterval = 10f;
	[Tooltip("Keeps the destination on the player's floor (was a hardcoded 2.24).")]
	public float floorOffset = 2.24f;
	[Tooltip("Half-angle of the camera's view cone treated as 'in view'. Destinations inside this cone are only accepted if level geometry blocks the line of sight.")]
	public float viewConeAngle = 60f;
	
	[Header("Danger meter")]
	[Tooltip("Value the danger meter ramps toward while the player is caught in view (was a hardcoded 10).")]
	public float dangerCeiling = 10f;
	[Tooltip("Danger value that kills the player. Time to die = lifeDuration * (threshold / ceiling): 7 * (6.9/10) = 4.83s reaction window.")]
	public float dangerDeathThreshold = 6.9f;
	
    // Start is called before the first frame update
    void Start()
    {
		gm = GameObject.FindGameObjectWithTag("GameManager");
        nextTeleport = spawnRate;
    }
	void goToSafePlace(){
		transform.position = safePlace.position;
		nextTeleport += spawnRate;
		isSafePlaceavailable = false;
	}
	IEnumerator SafePlaceTimer()
    {
        yield return new WaitForSeconds(3);
        isSafePlaceavailable = true;
    }
    // Update is called once per frame
    void Update()
    {
		if(gm == null) return;
		if(gm.GetComponent<GameManager>().keysfound > 0){
		if(isSafePlaceavailable && !isOnFlashLightTrigger && !onDanger){
			goToSafePlace();
		}
		FacePlayer();
		Damage();
        currentTime = Time.time;
		
		RaycastHit hit;
		 if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity))
        {
            //Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
            if(hit.transform.gameObject.tag == "Player"){
				inView = true;
			}
			else inView = false;
        }
		

		
		spawnOrigin.Set(cube_transform.position.x,cube_transform.position.y,cube_transform.position.z);
		
		if( !(nearPlayer || inView)){
			if(currentTime > nextTeleport){
				TeleportOutOfView();
				nextTeleport += teleportInterval;
			}
		}
		
		if(Vector3.Distance(transform.position, player_transform.position) <= distancePlayer){
			nearPlayer = true;
			//if(!GetComponent<AudioSource>().isPlaying && inView){
			//	GetComponent<AudioSource>().Play();
			//}
		}else{
			nearPlayer = false;
		}
		}
    }
	/// <summary>
	/// Picks a random destination around the player and moves there, preferring spots the player
	/// cannot see.
	///
	/// BUGFIX: the original code was `do { reroll } while (ableToTeleport)` with
	/// ableToTeleport = (!inView || isOnFlashLightTrigger). That is inverted - it kept re-rolling
	/// while the player was NOT visible and stopped the moment the player WAS visible, so the
	/// monster preferentially teleported into view. It could also never terminate: re-positioning
	/// happens without re-facing the monster, so its forward ray usually never reaches the player,
	/// leaving ableToTeleport true and freezing the game in an infinite loop. This version is
	/// bounded and tests the CAMERA's view instead of the monster's forward ray.
	/// </summary>
	void TeleportOutOfView()
	{
		Vector3 fallback = transform.position;
		float bestAngle = -1f;
		Transform playerRoot = PlayerRoot();

		for (int attempt = 0; attempt < Mathf.Max(1, teleportAttempts); attempt++)
		{
			Vector3 candidate = new Vector3(
				Random.Range(spawnOrigin.x - distanceRayMax, spawnOrigin.x + distanceRayMax),
				player_transform.position.y - floorOffset,
				Random.Range(spawnOrigin.z - distanceRayMax, spawnOrigin.z + distanceRayMax));

			if (playerRoot != null &&
				Vector3.Distance(candidate, player_transform.position) < teleportMinDistance)
				continue;

			if (IsHiddenFromPlayer(candidate))
			{
				transform.position = candidate;
				inView = false;
				return;
			}

			if (cube_transform != null)
			{
				float angle = Vector3.Angle(cube_transform.forward, candidate - cube_transform.position);
				if (angle > bestAngle) { bestAngle = angle; fallback = candidate; }
			}
		}

		// Budget exhausted: use the spot that was furthest outside the view cone.
		transform.position = fallback;
		inView = false;
	}

	Transform PlayerRoot()
	{
		if (cube_transform != null) return cube_transform.root;
		return player_transform != null ? player_transform.root : null;
	}

	/// <summary>True when the player's camera cannot see this world position.</summary>
	bool IsHiddenFromPlayer(Vector3 candidate)
	{
		if (cube_transform == null) return true;

		Vector3 toCandidate = candidate - cube_transform.position;
		// Comfortably outside the view cone - no need to test line of sight.
		if (Vector3.Angle(cube_transform.forward, toCandidate) >= viewConeAngle) return true;

		// Inside the cone: only accept it if level geometry blocks the view.
		RaycastHit hit;
		if (Physics.Linecast(cube_transform.position, candidate, out hit, ~0, QueryTriggerInteraction.Ignore))
		{
			Transform root = PlayerRoot();
			bool hitPlayer = root != null && (hit.transform == root || hit.transform.IsChildOf(root));
			return !hitPlayer;   // blocked by geometry => hidden; blocked by the player => still visible
		}
		return false;            // clear line of sight => visible
	}

	void FacePlayer(){
		Vector3 diretion = (player_transform.position - transform.position).normalized;
		Quaternion lookRotation = Quaternion.LookRotation( new Vector3(diretion.x,0,diretion.z));
		transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * facePlayerfactor);
	}
	void Damage(){
		if(nearPlayer && inView && !onDanger){
			onDanger = true;
			StartCoroutine(SafePlaceTimer());
			dangertemp = danger;
		}
		if(onDanger && (!nearPlayer || ! inView)){
			onDanger = false;
			dangertemp = danger;
		}
		if(onDanger && pointInTime_Damage <= lifeDuration){
			danger = Mathf.Lerp(dangertemp, dangerCeiling, pointInTime_Damage/lifeDuration);
			pointInTime_Damage += Time.deltaTime;
			if(pointInTime_health > 0){
				pointInTime_health -= Time.deltaTime;
			}
		}
		if(!onDanger && pointInTime_health <= lifeDuration){
			danger = Mathf.Lerp(dangertemp,0,pointInTime_health/lifeDuration);
			pointInTime_health += Time.deltaTime;
			if(pointInTime_Damage > 0){
				pointInTime_Damage -= Time.deltaTime;
			}
		}
		if(pointInTime_Damage > lifeDuration){
			Cursor.lockState = CursorLockMode.None;
			SceneManager.LoadScene("GameOver");
			
		}
		if(danger >= dangerDeathThreshold){
			Cursor.lockState = CursorLockMode.None;
			SceneManager.LoadScene("GameOver");
		}
		alpha = 0;
		if(danger/lifeDuration > 0.5 && onDanger){
			alpha = danger/lifeDuration;
		}
		else if(danger/lifeDuration < 0.5 && onDanger){
			alpha = 0.7f;
		}	
		else if(!onDanger){
			alpha = danger/lifeDuration;
		}		
		deathscreen.color = Color.red;
		deathscreen.canvasRenderer.SetAlpha(alpha);
		
	}
}