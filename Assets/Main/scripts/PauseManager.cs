using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 7.2 pause system. Owns the pause STATE - the pause UI just calls into it.
///
/// - Android back button / Escape toggles pause instead of quitting the app.
///   (Controller used to call Application.Quit() on Escape - that is removed.)
/// - Time.timeScale = 0 freezes scaled time, which is what every monster timer uses
///   (Time.time for the teleport schedule, Time.deltaTime for the danger ramp), so the
///   monster genuinely stops thinking rather than being left half-updated.
/// - AudioListener.pause mutes everything without needing AudioMixer groups.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    public bool IsPaused { get; private set; }

    /// <summary>Fired with the new paused state. Hook the pause menu UI to this.</summary>
    public event System.Action<bool> PauseChanged;

    [Tooltip("Scene loaded by the pause menu's Main Menu button.")]
    public string mainMenuScene = "Menu";

    [Tooltip("Master volume applied when resuming, so a ducked pause does not leave audio quiet.")]
    public bool manageListenerPause = true;

    // Frame on which the pause menu spent the Back/Escape press on one of its own panels.
    int backButtonConsumedFrame = -1;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        // Android maps the hardware back button to Escape.
        // The pause menu's Settings/Credits panels listen for the same key. When one of them
        // closes on Back it claims the press here, so Back steps out of the panel instead of
        // doing that AND unpausing in the same frame. Comparing frame counts rather than
        // ordering makes this independent of Unity's (undefined) script execution order.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (backButtonConsumedFrame == Time.frameCount) return;
            Toggle();
        }
    }

    /// <summary>
    /// Claims this frame's Back/Escape press so a pause-menu panel can close without the press
    /// also reaching <see cref="Toggle"/>.
    /// </summary>
    public void ConsumeBackButton()
    {
        backButtonConsumedFrame = Time.frameCount;
    }

    public void Toggle() { SetPaused(!IsPaused); }
    public void Resume() { SetPaused(false); }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;
        ResetPlayerInput();

        Time.timeScale = paused ? 0f : 1f;
        if (manageListenerPause) AudioListener.pause = paused;

        FreezeMonsters(paused);

        Cursor.visible = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;

        var handler = PauseChanged;
        if (handler != null) handler(paused);
    }

    static void ResetPlayerInput()
    {
        var controllers = Object.FindObjectsOfType<Controller>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null) controllers[i].ResetTouchInput();
        }
    }

    /// <summary>
    /// Belt and braces. timeScale = 0 already stops WaitForSeconds coroutines and freezes
    /// Time.time, but stopping them explicitly guarantees nothing is mid-flight, and re-arming
    /// the safe place on resume avoids leaving the monster unable to use it.
    /// </summary>
    static void FreezeMonsters(bool paused)
    {
        var monsters = Object.FindObjectsOfType<Monster>(true);
        for (int i = 0; i < monsters.Length; i++)
        {
            var m = monsters[i];
            if (m == null) continue;
            if (paused)
            {
                m.StopAllCoroutines();
            }
            else
            {
                m.isSafePlaceavailable = true;
            }
        }
    }

    // ---- pause menu button targets ----

    public void ReturnToMainMenu()
    {
        SetPaused(false);
        SceneManager.LoadScene(mainMenuScene);
    }

    public void QuitApp()
    {
        SetPaused(false);
        Application.Quit();
    }

    void OnDestroy()
    {
        // Never leave the next scene muted or frozen.
        if (Instance == this)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Instance = null;
        }
    }
}
