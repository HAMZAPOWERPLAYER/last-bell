using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Monster : MonoBehaviour
{
	private GameManager gameManager;
	
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

	[Header("State (read-only debug)")]
	[Tooltip("High-level behaviour state derived from the existing timers each frame. Exposed for debugging and tests; movement is still driven entirely by the timers.")]
	public BehaviorState state = BehaviorState.Dormant;

	public enum BehaviorState { Dormant, Roaming, Stalking, Attacking, Retreating }
	
	[Header("Danger meter")]
	[Tooltip("Value the danger meter ramps toward while the player is caught in view (was a hardcoded 10).")]
	public float dangerCeiling = 10f;
	[Tooltip("Danger value that kills the player. Time to die = lifeDuration * (threshold / ceiling): 7 * (6.9/10) = 4.83s reaction window.")]
	public float dangerDeathThreshold = 6.9f;
	float baseDistancePlayer;
	float baseTeleportInterval;
	float baseFacePlayerFactor;
	int threatKeyCount;
	bool escapeThreatActive;
	bool initialized;
	bool deathTriggered;
	float nextViewCheckTime;
	float retreatUntil;
	[Min(0.05f)] public float viewCheckInterval = 0.12f;
	
	void Awake()
	{
		baseDistancePlayer = distancePlayer;
		baseTeleportInterval = teleportInterval;
		baseFacePlayerFactor = facePlayerfactor;
	}

	// Start is called before the first frame update
    void Start()
    {
        GameObject managerObject = GameObject.FindGameObjectWithTag("GameManager");
        gameManager = managerObject != null ? managerObject.GetComponent<GameManager>() : null;
        nextTeleport = spawnRate;
        initialized = true;
        ApplyThreatProgress();
    }

    /// <summary>Escalates monster pursuit as keys are collected and during the final escape.</summary>
    public void SetThreatProgress(int keysFound, bool escapeActive)
    {
        threatKeyCount = Mathf.Clamp(keysFound, 0, 3);
        escapeThreatActive = escapeActive;
        if (initialized) ApplyThreatProgress();
    }

    void ApplyThreatProgress()
    {
        int stage = threatKeyCount;
        float escalation = Mathf.Max(0, stage - 1);
        distancePlayer = baseDistancePlayer + escalation * 2.5f + (escapeThreatActive ? 3f : 0f);
        facePlayerfactor = baseFacePlayerFactor * (1f + stage * 0.10f + (escapeThreatActive ? 0.2f : 0f));
        teleportInterval = Mathf.Max(3.5f, baseTeleportInterval - escalation * 2f - (escapeThreatActive ? 2f : 0f));
        if (stage > 0)
            nextTeleport = Mathf.Min(nextTeleport, Time.time + teleportInterval);
    }
    void goToSafePlace(){
        if(safePlace == null) return;
		// Fairness: the fixed safe spot can be visible from wherever the player is standing -
		// retreating there used to pop the monster into view (or next to the player). If the spot
		// is not safely hidden and far enough away, use a validated hidden teleport instead.
		bool tooClose = player_transform != null &&
			Vector3.Distance(safePlace.position, player_transform.position) < teleportMinDistance;
		if(tooClose || !IsHiddenFromPlayer(safePlace.position) || !IsValidDestination(safePlace.position)){
			TeleportOutOfView();
			nextTeleport = Time.time + teleportInterval;
			isSafePlaceavailable = false;
			retreatUntil = Time.time + 1.2f;
			return;
		}
		transform.position = safePlace.position;
		// BUGFIX: this added spawnRate (the one-off *initial* delay, 80s) instead of
		// teleportInterval, so retreating to the safe place silenced the teleport schedule for
		// far longer than any authored interval.
		nextTeleport = Time.time + teleportInterval;
		isSafePlaceavailable = false;
		retreatUntil = Time.time + 1.2f;
	}
	IEnumerator SafePlaceTimer()
    {
        yield return new WaitForSeconds(3);
        isSafePlaceavailable = true;
    }
    // Update is called once per frame
    void Update()
    {
		if(gameManager == null || gameManager.keysfound <= 0 || player_transform == null){ state = BehaviorState.Dormant; return; }
		if(isSafePlaceavailable && !isOnFlashLightTrigger && !onDanger){
			goToSafePlace();
		}
		FacePlayer();
		Damage();
        currentTime = Time.time;
		
		RaycastHit hit;
		if(currentTime >= nextViewCheckTime){
			nextViewCheckTime = currentTime + Mathf.Max(0.05f, viewCheckInterval);
			float viewDistance = Mathf.Max(distancePlayer + 4f, 8f);
			inView = Physics.Raycast(transform.position, transform.forward, out hit, viewDistance,
				Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.transform.CompareTag("Player");
		}
		

		
		spawnOrigin = cube_transform != null ? cube_transform.position : player_transform.position;
		
		if( !(nearPlayer || inView)){
			if(currentTime > nextTeleport){
				TeleportOutOfView();
				nextTeleport += teleportInterval;
			}
		}
		
		nearPlayer = (transform.position - player_transform.position).sqrMagnitude <= distancePlayer * distancePlayer;

		// Derived behaviour state (debug/testing read-out; behaviour is still driven entirely by
		// the timers above): Dormant before the first key, Attacking while the danger meter runs,
		// Retreating briefly after a withdrawal, Stalking when near but unseen, Roaming otherwise.
		if(onDanger) state = BehaviorState.Attacking;
		else if(Time.time < retreatUntil) state = BehaviorState.Retreating;
		else if(nearPlayer && !inView) state = BehaviorState.Stalking;
		else state = BehaviorState.Roaming;
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

			// Fairness/robustness: never land inside geometry, in mid-air, or on a different
			// floor than the player (doorways and stairwells used to allow all three).
			if (!IsValidDestination(candidate)) continue;

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

		// Budget exhausted: use the furthest-outside-the-cone spot that was validated; if none
		// passed validation the monster stays put instead of jumping into a wall or a gap.
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

	/// <summary>True when the spot has solid floor at the expected height and no geometry
	/// occupying the monster's body space.</summary>
	bool IsValidDestination(Vector3 candidate)
	{
		RaycastHit ground;
		if (!Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out ground, 4f,
			Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
			return false;
		if (Mathf.Abs(ground.point.y - candidate.y) > 1.2f) return false;

		return !Physics.CheckSphere(candidate + Vector3.up * 0.9f, 0.4f,
			Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
	}

	void FacePlayer(){
		if(player_transform == null) return;
		// Flatten first, then guard: if the player is directly above/below the monster the
		// flattened vector is zero and Quaternion.LookRotation logs "Look rotation viewing
		// vector is zero" every frame.
		Vector3 direction = player_transform.position - transform.position;
		direction.y = 0f;
		if(direction.sqrMagnitude < 0.0001f) return;
		Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);
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
		if(pointInTime_Damage > lifeDuration || danger >= dangerDeathThreshold){
			if(!deathTriggered){
				deathTriggered = true;
				Cursor.lockState = CursorLockMode.None;
				SceneManager.LoadScene("GameOver");
			}
			return;
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
		if(deathscreen != null){
			deathscreen.color = Color.red;
			deathscreen.canvasRenderer.SetAlpha(alpha);
		}
		
	}
}