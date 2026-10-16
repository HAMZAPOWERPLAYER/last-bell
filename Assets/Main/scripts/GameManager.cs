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
	public Text interactionPromptText;
	public Text missionObjectiveText;
	public ProgressionGate currentGate;
	public Monster monster;
	public AudioClip escapeCue;
	[Min(15f)] public float escapeWarningDuration = 30f;
	public bool escapeStarted;
	public bool classroomWingUnlocked;
	public bool upperCorridorUnlocked;
	float interactionMessageUntil;
	float missionMessageUntil;
	float escapeTimeRemaining;
	string interactionMessage;
	string missionMessage;
	string lastInteractionPrompt;
	string cachedGatePrompt;
	string lastObjectiveText;
	Text outroObjectiveText;
	int lastEscapeSecondDisplayed = -1;
	bool escapeFailureTriggered;
	
	//Pickup audio. Clips are left unassigned until real assets are supplied - playback is
	//skipped when a clip is null, so behaviour is unchanged in the meantime.
	public AudioSource pickup_source;
	public AudioClip keyPickup_clip;
	public AudioClip pagePickup_clip;
	
	
	//Audio from pages
	
	// ---- run lifecycle ----
	// "Keys" is RUN progress, not a permanent save: it exists so re-entering MainLevel mid-run
	// (revive, scene reload) keeps what was already collected. "RunActive" records whether a run is
	// genuinely in progress. Before this, the only thing that ever cleared "Keys" was MenuAction
	// when the Menu scene loaded - so launching straight into MainLevel (pressing Play in the editor
	// with MainLevel open, or any entry point that skips the Menu) inherited the previous session's
	// keys: the HUD already showed "1 keys found" and initSpawns() only spawned 2 key pickups.
	public const string KeyProgress = "Keys";
	public const string KeyRunActive = "RunActive";

	/// <summary>Starts a genuinely new run: clears collected keys and marks the run active.</summary>
	public static void StartNewRun()
	{
		PlayerPrefs.SetInt(KeyProgress, 0);
		PlayerPrefs.SetInt(KeyRunActive, 1);
		PlayerPrefs.Save();
	}

	/// <summary>Ends the run so the next MainLevel entry starts with zero keys.</summary>
	public static void EndRun()
	{
		PlayerPrefs.SetInt(KeyProgress, 0);
		PlayerPrefs.SetInt(KeyRunActive, 0);
		PlayerPrefs.Save();
	}

	/// <summary>
	/// Keeps run progress only while a run is actually in progress; otherwise discards progress
	/// left behind by a previous session instead of silently resuming it.
	/// </summary>
	static void BeginOrResumeRun()
	{
		if(PlayerPrefs.GetInt(KeyRunActive, 0) == 1) return;
		PlayerPrefs.SetInt(KeyProgress, 0);
		PlayerPrefs.SetInt(KeyRunActive, 1);
		PlayerPrefs.Save();
	}

    // Start is called before the first frame update
    void Start()
    {
        GameSettings.ApplyGraphicsTier();
		BeginOrResumeRun();
		keysfound = Mathf.Clamp(PlayerPrefs.GetInt(KeyProgress, 0), 0, 3);
		amountOfKeysIngame = keysfound;
		if(keyCounter_txt != null) keyCounter_txt.text = keysfound+" keys found";
		interactionPromptText = pickUp_UI != null ? pickUp_UI.GetComponentInChildren<Text>(true) : null;
		missionObjectiveText = GameObject.Find("Intro")?.GetComponent<Text>();
		outroObjectiveText = outro_UI != null ? outro_UI.GetComponentInChildren<Text>(true) : null;
		if(missionObjectiveText != null){
			Animator introAnimator = missionObjectiveText.GetComponent<Animator>();
			if(introAnimator != null) introAnimator.enabled = false;
			Color objectiveColor = missionObjectiveText.color;
			objectiveColor.a = 1f;
			missionObjectiveText.color = objectiveColor;
			RectTransform objectiveRect = missionObjectiveText.rectTransform;
			objectiveRect.anchoredPosition = new Vector2(0f, -55f);
			objectiveRect.sizeDelta = new Vector2(600f, 54f);
			missionObjectiveText.fontSize = 16;
			missionObjectiveText.alignment = TextAnchor.MiddleCenter;
		}
		monster = GameObject.FindGameObjectWithTag("Monster")?.GetComponent<Monster>();
		escapeStarted = keysfound >= 3;
		if(escapeStarted) StartEscapeSequence(false);
		else if(monster != null) monster.SetThreatProgress(keysfound, false);
		UpdateMissionObjective();
		ConfigureTransparentDeathOverlay();
		initSpawns();
		spawnJumpScare();
		if(interactButton != null){
			interactButton.OnButtonAction += (pressed) => {
				Diag.Log("OnButtonAction fired: pressed=" + pressed);
				if(pressed) pick();
			};
		}else{
			Debug.LogError("[GameManager] interactButton is not assigned; the PICK button will not respond.", this);
		}
    }

    void ConfigureTransparentDeathOverlay()
    {
        GameObject deathOverlay = GameObject.Find("DeathImage");
        if (deathOverlay == null) return;

        Image overlayImage = deathOverlay.GetComponent<Image>();
        if (overlayImage != null)
        {
            // The full-screen image starts transparent; cull it until its alpha becomes visible.
            overlayImage.canvasRenderer.cullTransparentMesh = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Direct touch close: if a page panel is open, any tap closes it.
        if(isGamePaused){
            if(UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled){
                var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
                for(int i = 0; i < touches.Count; i++){
                    if(touches[i].phase == UnityEngine.InputSystem.TouchPhase.Began){
                        Diag.Log("Page close tap detected");
                        closePage();
                        break;
                    }
                }
            }
            // Editor/desktop only. On device Input.GetMouseButtonDown(0) is an alias for the
            // first touch, so this fired in addition to the EnhancedTouch loop above and every
            // tap was processed twice (visible as "tap detected" + "click detected" together).
            if(!Application.isMobilePlatform && Input.GetMouseButtonDown(0)){
                Diag.Log("Page close click detected");
                closePage();
            }
        }

        bool hasPickup = isOnKeyTrigger_ || isOnPageTrigger_;
        bool shouldShowPickup = hasPickup || currentGate != null;
        if(pickUp_UI != null && pickUp_UI.activeSelf != shouldShowPickup) pickUp_UI.SetActive(shouldShowPickup);

        if(interactionPromptText != null){
            string prompt = !string.IsNullOrEmpty(interactionMessage) && Time.unscaledTime < interactionMessageUntil
                ? interactionMessage
                : currentGate != null ? cachedGatePrompt
                : hasPickup ? "PICK" : null;
            if(prompt != null && prompt != lastInteractionPrompt){
                interactionPromptText.text = prompt;
                lastInteractionPrompt = prompt;
            }
        }

        if(missionObjectiveText != null){
            if(!string.IsNullOrEmpty(missionMessage) && Time.unscaledTime < missionMessageUntil)
                SetMissionObjective(missionMessage);
            else
                UpdateMissionObjective();
        }

        if(escapeStarted){
            escapeTimeRemaining = Mathf.Max(0f, escapeTimeRemaining - Time.deltaTime);
            int secondsRemaining = Mathf.CeilToInt(escapeTimeRemaining);
            if(keyCounter_txt != null && secondsRemaining != lastEscapeSecondDisplayed){
                keyCounter_txt.text = "RUN! " + secondsRemaining + "s";
                lastEscapeSecondDisplayed = secondsRemaining;
            }
            if(outro_UI != null && !outro_UI.activeSelf) outro_UI.SetActive(true);
            if(outroObjectiveText != null && outroObjectiveText.text != "RUN TO THE MAIN ENTRANCE!")
                outroObjectiveText.text = "RUN TO THE MAIN ENTRANCE!";
            if(escapeTimeRemaining <= 0f && !escapeFailureTriggered){
                escapeFailureTriggered = true;
                PlayerPrefs.Save();
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
            }
        }else if(keysfound >= 3 && outro_UI != null && !outro_UI.activeSelf){
            outro_UI.SetActive(true);
        }
    }

    /// <summary>Updates the persistent story objective as keys and gates advance.</summary>
    void UpdateMissionObjective()
    {
        if(escapeStarted){
            SetMissionObjective("FINAL RUN: REACH THE MAIN ENTRANCE");
        }else if(keysfound <= 0){
            SetMissionObjective("OBJECTIVE: FIND THE CLASSROOM KEY (0/3)");
        }else if(keysfound == 1 && !classroomWingUnlocked){
            SetMissionObjective("OBJECTIVE: UNLOCK THE CLASSROOM WING");
        }else if(keysfound == 1){
            SetMissionObjective("OBJECTIVE: FIND THE STAIRWELL KEY (1/3)");
        }else if(keysfound == 2 && !upperCorridorUnlocked){
            SetMissionObjective("OBJECTIVE: UNLOCK THE UPPER CORRIDOR");
        }else if(keysfound == 2){
            SetMissionObjective("OBJECTIVE: FIND THE MAIN GATE KEY (2/3)");
        }else{
            SetMissionObjective("OBJECTIVE: REACH THE MAIN ENTRANCE");
        }
    }

    void SetMissionObjective(string text)
    {
        if(missionObjectiveText == null || lastObjectiveText == text) return;
        lastObjectiveText = text;
        missionObjectiveText.text = text;
    }

    /// <summary>Temporarily displays a narrative beat without replacing the current objective permanently.</summary>
    public void ShowMissionMessage(string message, float duration)
    {
        missionMessage = message;
        missionMessageUntil = Time.unscaledTime + Mathf.Max(0.1f, duration);
        SetMissionObjective(message);
    }

    /// <summary>Records gate unlock progression and updates the next story objective.</summary>
    public void OnGateUnlocked(ProgressionGate gate)
    {
        if(gate == null) return;
        if(gate.requiredKeys <= 1) classroomWingUnlocked = true;
        else upperCorridorUnlocked = true;
        ShowMissionMessage(gate.gateName.ToUpperInvariant() + " UNLOCKED", 3.5f);
    }

    /// <summary>Sets the gate currently inside the player's interaction radius.</summary>
    public void SetCurrentGate(ProgressionGate gate)
    {
        currentGate = gate;
        cachedGatePrompt = currentGate != null ? "UNLOCK " + currentGate.gateName.ToUpperInvariant() : null;
        interactionMessage = null;
        if(interactionPromptText != null && currentGate != null){
            interactionPromptText.text = cachedGatePrompt;
            lastInteractionPrompt = cachedGatePrompt;
        }
    }

    /// <summary>Displays a short response on the existing pickup interaction button.</summary>
    public void ShowInteractionMessage(string message, float duration)
    {
        interactionMessage = message;
        interactionMessageUntil = Time.unscaledTime + Mathf.Max(0.1f, duration);
        if(interactionPromptText != null){
            interactionPromptText.text = message;
            lastInteractionPrompt = message;
        }
    }

    void StartEscapeSequence(bool playCue)
    {
        escapeStarted = true;
        escapeFailureTriggered = false;
        escapeTimeRemaining = escapeWarningDuration;
        lastEscapeSecondDisplayed = -1;
        if(monster != null) monster.SetThreatProgress(keysfound, true);
        if(keyCounter_txt != null){
            int secondsRemaining = Mathf.CeilToInt(escapeTimeRemaining);
            keyCounter_txt.text = "RUN! " + secondsRemaining + "s";
            lastEscapeSecondDisplayed = secondsRemaining;
        }
        if(outro_UI != null && !outro_UI.activeSelf) outro_UI.SetActive(true);
        if(outroObjectiveText != null) outroObjectiveText.text = "RUN TO THE MAIN ENTRANCE!";
        if(playCue) PlayPickup(escapeCue);
    }

    void initSpawns(){
        int pagesNeeded = Mathf.Max(0, 6 - amountOfPagesIngame);
        int keysNeeded = Mathf.Max(0, 3 - amountOfKeysIngame);
        if(pagesNeeded + keysNeeded == 0) return;

        List<SpawnItem> available = new List<SpawnItem>();
        GameObject[] taggedSpawns = GameObject.FindGameObjectsWithTag("SpawnItem");
        foreach(GameObject spawnObject in taggedSpawns){
            if(spawnObject == null) continue;
            SpawnItem spawn = spawnObject.GetComponent<SpawnItem>();
            if(spawn == null || spawn.kindofItem != "none") continue;
            if(spawn.pageprefab == null || spawn.keyprefab == null){
                Debug.LogWarning("[GameManager] Spawn point is missing a key or page prefab: " + spawnObject.name);
                continue;
            }
            available.Add(spawn);
        }

        HashSet<SpawnItem> used = new HashSet<SpawnItem>();
        for(int stage = keysfound; stage < 3; stage++){
            if(keysNeeded <= 0) break;
            List<SpawnItem> stageCandidates = available.FindAll(point => point.progressionStage == stage && !used.Contains(point));
            if(stageCandidates.Count == 0){
                stageCandidates = available.FindAll(point => !used.Contains(point));
                Debug.LogWarning("[GameManager] No key spawn configured for progression stage " + stage + "; using a remaining fallback point.");
            }
            if(stageCandidates.Count == 0){
                Debug.LogError("[GameManager] No free spawn point remains for progression key " + (stage + 1) + ".");
                return;
            }
            SpawnItem keySpawn = stageCandidates[Random.Range(0, stageCandidates.Count)];
            if(!keySpawn.TrySpawnItem("Key")) return;
            used.Add(keySpawn);
            keysNeeded--;
            amountOfKeysIngame++;
        }

        List<SpawnItem> pageCandidates = available.FindAll(point => !used.Contains(point));
        for(int i = pageCandidates.Count - 1; i > 0; i--){
            int randomIndex = Random.Range(0, i + 1);
            SpawnItem temporary = pageCandidates[i];
            pageCandidates[i] = pageCandidates[randomIndex];
            pageCandidates[randomIndex] = temporary;
        }
        if(pageCandidates.Count < pagesNeeded){
            Debug.LogWarning("[GameManager] Not enough remaining spawn points for all optional pages.");
            pagesNeeded = pageCandidates.Count;
        }
        for(int i = 0; i < pagesNeeded; i++){
            if(!pageCandidates[i].TrySpawnItem("Page", amountOfPagesIngame)) return;
            amountOfPagesIngame++;
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
		isGamePaused = false;
	}
	
	public void pick(){
		Diag.Log("pick() called. currentGate=" + (currentGate != null) + " currentKey=" + (currentKey != null) + " currentPage=" + (currentPage != null));
		if(currentGate != null){
			Diag.Log("Opening gate: " + currentGate.gateName);
			currentGate.TryOpen(keysfound);
			return;
		}
		if(currentKey != null){
			Diag.Log("Picking up key: " + currentKey.name);
			GameObject collectedKey = currentKey;
			KeyTrigger keyStory = collectedKey.GetComponent<KeyTrigger>();
			string keyTitle = keyStory != null ? keyStory.keyTitle : "School Key";
			string keyClue = keyStory != null ? keyStory.storyClue : "A key from the school.";
			keysfound = Mathf.Min(3, keysfound + 1);
			isOnKeyTrigger_ = false;
			currentKey = null;
			PlayerPrefs.SetInt(KeyProgress, keysfound);
			PlayerPrefs.Save();
			Destroy(collectedKey);
			if(keyCounter_txt != null) keyCounter_txt.text = keysfound+" keys found";
			PlayPickup(keyPickup_clip);
			if(monster == null){
				GameObject monsterObject = GameObject.FindGameObjectWithTag("Monster");
				if(monsterObject != null) monster = monsterObject.GetComponent<Monster>();
			}
			if(monster != null) monster.SetThreatProgress(keysfound, keysfound >= 3);
			ShowMissionMessage(keyTitle.ToUpperInvariant() + " FOUND — " + keyClue, 4.5f);
			if(keysfound >= 3) StartEscapeSequence(true);
		}
		else if(currentPage != null){
			currentPageNumber++;
			isOnPageTrigger_ = false;
			GameObject collectedPage = currentPage;
			PageTrigger pageStory = collectedPage.GetComponent<PageTrigger>();
			currentPage = null;
			if(collectedPage != null) Destroy(collectedPage);
			PlayPickup(pagePickup_clip);
			if(currentPageNumber >= 1 && currentPageNumber <= pages.Length && pages[currentPageNumber - 1] != null){
				GameObject pagePanel = pages[currentPageNumber - 1];
				SetPageClue(pagePanel, pageStory != null ? pageStory.storyClue : "A torn note from the night watch.");
				pagePanel.SetActive(true);
				isGamePaused = true;
			}
		}
	}

	void SetPageClue(GameObject pagePanel, string clue)
	{
		if(pagePanel == null || string.IsNullOrEmpty(clue)) return;
		Transform existingPanel = pagePanel.transform.Find("StoryCluePanel");
		GameObject cluePanel = existingPanel != null ? existingPanel.gameObject : new GameObject("StoryCluePanel", typeof(RectTransform), typeof(Image));
		if(existingPanel == null) cluePanel.transform.SetParent(pagePanel.transform, false);

		RectTransform panelRect = cluePanel.GetComponent<RectTransform>();
		panelRect.anchorMin = new Vector2(0.04f, 0.03f);
		panelRect.anchorMax = new Vector2(0.96f, 0.26f);
		panelRect.offsetMin = Vector2.zero;
		panelRect.offsetMax = Vector2.zero;
		Image panelImage = cluePanel.GetComponent<Image>();
		panelImage.color = new Color(0f, 0f, 0f, 0.78f);
		panelImage.raycastTarget = false;

		Transform existingText = cluePanel.transform.Find("StoryClue");
		GameObject clueTextObject = existingText != null ? existingText.gameObject : new GameObject("StoryClue", typeof(RectTransform), typeof(Text));
		if(existingText == null) clueTextObject.transform.SetParent(cluePanel.transform, false);
		RectTransform textRect = clueTextObject.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(8f, 5f);
		textRect.offsetMax = new Vector2(-8f, -5f);
		Text clueLabel = clueTextObject.GetComponent<Text>();
		Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if(font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		clueLabel.font = font;
		clueLabel.text = clue;
		clueLabel.fontSize = 14;
		clueLabel.color = Color.white;
		clueLabel.alignment = TextAnchor.MiddleCenter;
		clueLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
		clueLabel.verticalOverflow = VerticalWrapMode.Overflow;
		clueLabel.raycastTarget = false;
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
