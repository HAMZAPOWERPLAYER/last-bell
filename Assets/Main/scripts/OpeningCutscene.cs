using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays a short, skippable horror opening before handing control to the player.</summary>
public class OpeningCutscene : MonoBehaviour
{
    [Header("Scene References")]
    public Controller playerController;
    public Camera cutsceneCamera;
    public Monster monster;
    public PauseManager pauseManager;
    public AudioClip monsterStinger;

    [Header("Timing")]
    [Min(0.1f)] public float openingFadeDuration = 0.8f;
    [Min(0.1f)] public float revealDuration = 2.2f;
    [Min(0.1f)] public float endingFadeDuration = 0.7f;
    [Range(25f, 60f)] public float revealFieldOfView = 42f;
    [Range(0f, 1f)] public float stingerVolume = 0.28f;

    Controller controller;
    Camera cameraToAnimate;
    Monster monsterToFreeze;
    Animator monsterAnimator;
    PauseManager pauseController;
    PauseMenuUI pauseMenu;
    Canvas[] hiddenCanvases;
    bool[] canvasEnabledStates;
    GameObject overlayRoot;
    Image fadeImage;
    Text titleText;
    Text captionText;
    Button skipButton;
    Coroutine sequence;
    bool isRunning;
    bool hasCapturedState;

    bool controllerWasEnabled;
    bool monsterWasEnabled;
    bool monsterAnimatorWasEnabled;
    bool pauseWasEnabled;
    bool pauseMenuWasEnabled;
    bool monsterWasStaged;
    Vector3 originalMonsterPosition;
    Quaternion originalMonsterRotation;
    float originalTimeScale;
    bool originalListenerPause;
    Vector3 originalCameraLocalPosition;
    Quaternion originalCameraLocalRotation;
    float originalFieldOfView;
    bool originalCursorVisible;
    CursorLockMode originalCursorLockMode;

    void Start()
    {
        controller = playerController != null ? playerController : FindObjectOfType<Controller>();
        cameraToAnimate = cutsceneCamera != null ? cutsceneCamera : controller != null ? controller.cam : null;
        monsterToFreeze = monster != null ? monster : FindObjectOfType<Monster>();
        monsterAnimator = monsterToFreeze != null ? monsterToFreeze.GetComponent<Animator>() : null;
        pauseController = pauseManager != null ? pauseManager : PauseManager.Instance;

        if (controller == null || cameraToAnimate == null)
        {
            Debug.LogError("[OpeningCutscene] Assign a player Controller and cutscene Camera.", this);
            enabled = false;
            return;
        }

        sequence = StartCoroutine(PlayOpening());
    }

    IEnumerator PlayOpening()
    {
        controllerWasEnabled = controller.enabled;
        pauseWasEnabled = pauseController != null && pauseController.enabled;
        pauseMenu = FindObjectOfType<PauseMenuUI>(true);
        pauseMenuWasEnabled = pauseMenu != null && pauseMenu.enabled;
        monsterWasEnabled = monsterToFreeze != null && monsterToFreeze.enabled;
        monsterAnimatorWasEnabled = monsterAnimator != null && monsterAnimator.enabled;
        if (monsterToFreeze != null)
        {
            originalMonsterPosition = monsterToFreeze.transform.position;
            originalMonsterRotation = monsterToFreeze.transform.rotation;
        }
        originalTimeScale = Time.timeScale;
        originalListenerPause = AudioListener.pause;
        originalCameraLocalPosition = cameraToAnimate.transform.localPosition;
        originalCameraLocalRotation = cameraToAnimate.transform.localRotation;
        originalFieldOfView = cameraToAnimate.fieldOfView;
        originalCursorVisible = Cursor.visible;
        originalCursorLockMode = Cursor.lockState;
        hasCapturedState = true;

        if (pauseMenu != null) pauseMenu.enabled = false;
        BuildOverlay();
        HideGameplayCanvases();
        isRunning = true;

        controller.enabled = false;
        if (pauseController != null) pauseController.enabled = false;
        if (monsterToFreeze != null) monsterToFreeze.enabled = false;
        StageMonsterForReveal();
        Time.timeScale = 1f;
        AudioListener.pause = false;

        fadeImage.color = new Color(0f, 0f, 0f, 1f);
        SetCaption("LAST BELL", "The final bell rang. The school should be empty.");
        SetSkipVisible(true);
        yield return FadeScreen(1f, 0f, openingFadeDuration);
        if (!isRunning) yield break;
        yield return WaitForCutscene(2.4f);
        if (!isRunning) yield break;

        SetCaption("NO ONE CAME BACK", "The main entrance is locked.");
        Quaternion lookingLeft = originalCameraLocalRotation * Quaternion.Euler(2f, -18f, 0f);
        Vector3 subtleLeftPosition = originalCameraLocalPosition + new Vector3(-0.12f, 0.035f, 0.12f);
        yield return MoveCamera(subtleLeftPosition, lookingLeft, originalFieldOfView, 1.8f);
        if (!isRunning) yield break;

        SetCaption("SOMETHING IS STILL HERE", "A shape waits at the far end of the hall.");
        if (monsterToFreeze != null)
        {
            Vector3 targetPoint = monsterToFreeze.transform.position + Vector3.up * 1.5f;
            Vector3 lookDirection = targetPoint - cameraToAnimate.transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion worldLook = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                Transform cameraParent = cameraToAnimate.transform.parent;
                Quaternion localLook = cameraParent != null
                    ? Quaternion.Inverse(cameraParent.rotation) * worldLook
                    : worldLook;
                yield return MoveCamera(originalCameraLocalPosition, localLook, revealFieldOfView, revealDuration);
            }
            else
            {
                yield return MoveCamera(originalCameraLocalPosition, originalCameraLocalRotation, revealFieldOfView, revealDuration);
            }

            if (!isRunning) yield break;
            if (monsterStinger != null)
                AudioSource.PlayClipAtPoint(monsterStinger, targetPoint, stingerVolume * GameSettings.SfxVolume);
        }
        else
        {
            yield return MoveCamera(originalCameraLocalPosition, originalCameraLocalRotation, revealFieldOfView, revealDuration);
        }

        if (!isRunning) yield break;
        yield return WaitForCutscene(1.1f);
        if (!isRunning) yield break;

        SetCaption("FIND 3 KEYS", "Reach the main entrance and get out.");
        yield return MoveCamera(originalCameraLocalPosition, originalCameraLocalRotation, originalFieldOfView, 1.2f);
        if (!isRunning) yield break;
        yield return WaitForCutscene(2.2f);
        if (!isRunning) yield break;

        yield return CompleteOpening();
    }

    /// <summary>Lets the player skip the opening and enter gameplay immediately.</summary>
    public void SkipCutscene()
    {
        if (!isRunning) return;
        isRunning = false;
        if (sequence != null) StopCoroutine(sequence);
        sequence = StartCoroutine(CompleteOpening());
    }

    IEnumerator CompleteOpening()
    {
        if (fadeImage != null)
            yield return FadeScreen(fadeImage.color.a, 1f, 0.35f);

        cameraToAnimate.transform.localPosition = originalCameraLocalPosition;
        cameraToAnimate.transform.localRotation = originalCameraLocalRotation;
        cameraToAnimate.fieldOfView = originalFieldOfView;

        Time.timeScale = originalTimeScale > 0f ? originalTimeScale : 1f;
        AudioListener.pause = originalListenerPause;
        Cursor.visible = originalCursorVisible;
        Cursor.lockState = originalCursorLockMode;

        RestoreMonster();
        if (fadeImage != null)
            yield return FadeScreen(1f, 0f, endingFadeDuration);

        if (pauseController != null) pauseController.enabled = pauseWasEnabled;
        if (pauseMenu != null) pauseMenu.enabled = pauseMenuWasEnabled;
        if (controller != null) controller.enabled = controllerWasEnabled;
        RestoreGameplayCanvases();

        isRunning = false;
        hasCapturedState = false;
        sequence = null;
        if (overlayRoot != null) Destroy(overlayRoot);
    }

    IEnumerator WaitForCutscene(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                SkipCutscene();
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator MoveCamera(Vector3 targetPosition, Quaternion targetRotation, float targetFov, float duration)
    {
        Vector3 startPosition = cameraToAnimate.transform.localPosition;
        Quaternion startRotation = cameraToAnimate.transform.localRotation;
        float startFov = cameraToAnimate.fieldOfView;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                SkipCutscene();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float linear = Mathf.Clamp01(elapsed / safeDuration);
            float eased = linear * linear * (3f - 2f * linear);
            cameraToAnimate.transform.localPosition = Vector3.Lerp(startPosition, targetPosition, eased);
            cameraToAnimate.transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            cameraToAnimate.fieldOfView = Mathf.Lerp(startFov, targetFov, eased);
            yield return null;
        }

        cameraToAnimate.transform.localPosition = targetPosition;
        cameraToAnimate.transform.localRotation = targetRotation;
        cameraToAnimate.fieldOfView = targetFov;
    }

    IEnumerator FadeScreen(float from, float to, float duration)
    {
        if (fadeImage == null) yield break;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / safeDuration));
            Color color = fadeImage.color;
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        Color finalColor = fadeImage.color;
        finalColor.a = to;
        fadeImage.color = finalColor;
    }

    void SetCaption(string title, string caption)
    {
        if (titleText != null) titleText.text = title;
        if (captionText != null) captionText.text = caption;
    }

    void SetSkipVisible(bool visible)
    {
        if (skipButton != null) skipButton.gameObject.SetActive(visible);
    }

    void StageMonsterForReveal()
    {
        if (monsterToFreeze == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(cameraToAnimate.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) return;
        forward.Normalize();

        Vector3 stagedPosition = cameraToAnimate.transform.position + forward * 11.5f;
        stagedPosition.y = originalMonsterPosition.y;
        monsterToFreeze.transform.position = stagedPosition;
        monsterToFreeze.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        monsterWasStaged = true;

        if (monsterAnimator != null)
        {
            monsterAnimator.enabled = true;
            int howlState = Animator.StringToHash("Base Layer.JacksonRig|StandHowl2");
            if (!monsterAnimator.HasState(0, howlState))
                howlState = Animator.StringToHash("JacksonRig|StandHowl2");
            if (monsterAnimator.HasState(0, howlState)) monsterAnimator.Play(howlState, 0, 0f);
        }
    }

    void RestoreMonster()
    {
        if (monsterToFreeze != null && monsterWasStaged)
        {
            monsterToFreeze.transform.position = originalMonsterPosition;
            monsterToFreeze.transform.rotation = originalMonsterRotation;
        }

        if (monsterAnimator != null)
        {
            if (monsterAnimator.enabled && monsterAnimator.runtimeAnimatorController != null)
            {
                int idleState = Animator.StringToHash("Base Layer.JacksonRig|IdleStand");
                if (monsterAnimator.HasState(0, idleState))
                {
                    monsterAnimator.Play(idleState, 0, 0f);
                    monsterAnimator.Update(0f);
                }
            }
            monsterAnimator.enabled = monsterAnimatorWasEnabled;
        }

        if (monsterToFreeze != null) monsterToFreeze.enabled = monsterWasEnabled;
        monsterWasStaged = false;
    }

    void BuildOverlay()
    {
        overlayRoot = new GameObject("OpeningCutsceneCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject shadeObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        shadeObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform shadeRect = shadeObject.GetComponent<RectTransform>();
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = Vector2.zero;
        shadeRect.offsetMax = Vector2.zero;
        fadeImage = shadeObject.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = true;

        BuildBar(overlayRoot.transform, "TopLetterbox", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(0f, 52f));
        BuildBar(overlayRoot.transform, "BottomLetterbox", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 36f), new Vector2(0f, 72f));

        titleText = BuildText(overlayRoot.transform, "Title", "LAST BELL", 30, FontStyle.Bold,
            new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), new Vector2(0f, 0f), new Vector2(720f, 58f));
        captionText = BuildText(overlayRoot.transform, "Caption", "", 20, FontStyle.Normal,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(720f, 42f));

        GameObject buttonObject = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-82f, -80f);
        buttonRect.sizeDelta = new Vector2(112f, 40f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.08f, 0.10f, 0.12f, 0.88f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(SkipCutscene);
        skipButton = button;
        BuildText(buttonObject.transform, "Label", "SKIP  >>", 16, FontStyle.Bold,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    static void BuildBar(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(parent, false);
        RectTransform rect = barObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = barObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.9f);
        image.raycastTarget = false;
    }

    static Text BuildText(Transform parent, string objectName, string value, int fontSize, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    void HideGameplayCanvases()
    {
        Canvas[] sceneCanvases = FindObjectsOfType<Canvas>(true);
        List<Canvas> hidden = new List<Canvas>();
        List<bool> wasEnabled = new List<bool>();

        for (int i = 0; i < sceneCanvases.Length; i++)
        {
            Canvas sceneCanvas = sceneCanvases[i];
            if (sceneCanvas == null || sceneCanvas.gameObject == overlayRoot) continue;
            hidden.Add(sceneCanvas);
            wasEnabled.Add(sceneCanvas.enabled);
            sceneCanvas.enabled = false;
        }

        hiddenCanvases = hidden.ToArray();
        canvasEnabledStates = wasEnabled.ToArray();
    }

    void RestoreGameplayCanvases()
    {
        if (hiddenCanvases == null || canvasEnabledStates == null) return;
        int count = Mathf.Min(hiddenCanvases.Length, canvasEnabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            if (hiddenCanvases[i] != null) hiddenCanvases[i].enabled = canvasEnabledStates[i];
        }
        hiddenCanvases = null;
        canvasEnabledStates = null;
    }

    void OnDestroy()
    {
        if (!hasCapturedState) return;
        if (cameraToAnimate != null)
        {
            cameraToAnimate.transform.localPosition = originalCameraLocalPosition;
            cameraToAnimate.transform.localRotation = originalCameraLocalRotation;
            cameraToAnimate.fieldOfView = originalFieldOfView;
        }
        RestoreMonster();
        if (pauseController != null) pauseController.enabled = pauseWasEnabled;
        if (pauseMenu != null) pauseMenu.enabled = pauseMenuWasEnabled;
        if (controller != null) controller.enabled = controllerWasEnabled;
        RestoreGameplayCanvases();
        Time.timeScale = originalTimeScale > 0f ? originalTimeScale : 1f;
        AudioListener.pause = originalListenerPause;
        Cursor.visible = originalCursorVisible;
        Cursor.lockState = originalCursorLockMode;
        hasCapturedState = false;
        if (overlayRoot != null) Destroy(overlayRoot);
    }
}
