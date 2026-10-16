using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using UnityEngine.SceneManagement;

/// <summary>Shows the main exit prompt when the player reaches the door with all three keys.</summary>
public class ExitTrigger : MonoBehaviour
{
    GameManager gameManager;
    public GameObject openDoor_UI;
    public AudioSource door_source;
    public AudioClip door_clip;
    [Tooltip("Seconds to wait before loading the next scene, so the door sound can finish. 0 = load immediately.")]
    public float doorSoundDelay = 0f;

    readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    bool playerInside;
    bool isLoadingNextScene;
    float lastTapTime = -999f;
    float tapCooldown = 1f;

    void Start()
    {
        if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        GameObject managerObject = GameObject.FindGameObjectWithTag("GameManager");
        if (managerObject != null) gameManager = managerObject.GetComponent<GameManager>();
        RefreshExitPrompt();
    }

    void OnEnable()
    {
        if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
    }

    void Update()
    {
        if (playerInside) RefreshExitPrompt();

        // Direct touch: if the open door button is visible and player taps, trigger GameWin.
        if (playerInside && openDoor_UI != null && openDoor_UI.activeSelf && gameManager != null && gameManager.keysfound >= 3)
        {
            if (EnhancedTouchSupport.enabled)
            {
                var touches = Touch.activeTouches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].phase == TouchPhase.Began && Time.time - lastTapTime >= tapCooldown)
                    {
                        lastTapTime = Time.time;
                        Diag.Log("ExitTrigger: direct touch GameWin");
                        GameWin();
                        break;
                    }
                }
            }
            // Editor/desktop fallback only. On device Input.GetMouseButtonDown(0) is an alias
            // for the first touch, so this duplicated the EnhancedTouch path above.
            if (!Application.isMobilePlatform && Input.GetMouseButtonDown(0) && Time.time - lastTapTime >= tapCooldown)
            {
                lastTapTime = Time.time;
                Diag.Log("ExitTrigger: direct click GameWin");
                GameWin();
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || !playerColliders.Add(other)) return;
        playerInside = playerColliders.Count > 0;
        RefreshExitPrompt();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || !playerColliders.Remove(other)) return;
        playerInside = playerColliders.Count > 0;
        if (!playerInside && openDoor_UI != null && openDoor_UI.activeSelf)
            openDoor_UI.SetActive(false);
    }

    void RefreshExitPrompt()
    {
        if (openDoor_UI == null) return;
        bool shouldShow = playerInside && gameManager != null && gameManager.keysfound >= 3;
        if (openDoor_UI.activeSelf != shouldShow) openDoor_UI.SetActive(shouldShow);
    }

    /// <summary>Opens the main entrance after the player collects all required keys.</summary>
    public void GameWin()
    {
        if (isLoadingNextScene) return;
        if (gameManager == null || gameManager.keysfound < 3)
        {
            if (gameManager != null) gameManager.ShowInteractionMessage("The entrance still needs all 3 keys.", 2f);
            return;
        }

        isLoadingNextScene = true;
        // 2.0 SECRET: record whether the whole story was found (all six pages) for the ending
        // cutscene. Always written, so a stale flag can never leak into a later playthrough;
        // EndRun's PlayerPrefs.Save() below is what persists it.
        PlayerPrefs.SetInt("Save.Truth", RunSave.PagesCollected >= 6 ? 1 : 0);
        // 2.0 SAVE & CONTINUE: the run is complete the moment the door opens - delete the
        // checkpoint so the menu offers a clean PLAY instead of resuming an escaped run.
        GameManager.EndRun();
        if (door_clip != null)
        {
            if (door_source != null)
                door_source.PlayOneShot(door_clip);
            else
                AudioSource.PlayClipAtPoint(door_clip, transform.position);

            if (doorSoundDelay > 0f)
            {
                StartCoroutine(LoadNextSceneAfterDelay());
                return;
            }
        }

        SceneManager.LoadScene("Bedroom");
    }

    IEnumerator LoadNextSceneAfterDelay()
    {
        yield return new WaitForSeconds(doorSoundDelay);
        SceneManager.LoadScene("Bedroom");
    }
}
