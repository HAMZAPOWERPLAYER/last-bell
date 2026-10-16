using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays a distinct, skippable horror ending before revealing the scene's existing buttons.</summary>
public class EndingCutscene : MonoBehaviour
{
    [Header("Scene References")]
    public Camera endingCamera;
    public Canvas endingMenuCanvas;
    public GameObject monsterPrefab;
    public AudioClip ambienceLoop;
    public AudioClip atmosphereLoop;
    public AudioClip trailerRiser;
    public AudioClip impactHit;
    public AudioClip monsterScream;

    [Header("Ending")]
    public bool gameOverEnding;
    [Min(0.1f)] public float fadeDuration = 0.8f;
    [Min(0.1f)] public float finalCardDuration = 2.5f;
    [Range(0f, 1f)] public float ambienceVolume = 0.20f;
    [Range(0f, 1f)] public float atmosphereVolume = 0.12f;
    [Range(0f, 1f)] public float riserVolume = 0.30f;
    [Range(0f, 1f)] public float impactVolume = 0.46f;
    [Range(0f, 1f)] public float screamVolume = 0.52f;

    const string TruthKey = "Save.Truth";
    bool secretTruth;
    CanvasGroup menuCanvasGroup;
    GameObject overlayRoot;
    GameObject stagedMonster;
    AudioSource ambienceSource;
    AudioSource atmosphereSource;
    AudioSource effectSource;
    Image fadeImage;
    Image flashImage;
    Text titleText;
    Text subtitleText;
    Button skipButton;
    Vector3 originalCameraPosition;
    Quaternion originalCameraRotation;
    float originalFieldOfView;
    CameraClearFlags originalCameraClearFlags;
    Color originalCameraBackgroundColor;
    bool originalMenuCanvasEnabled;
    Coroutine sequence;
    bool isPlaying;
    bool hasCapturedState;

    void Start()
    {
        // 2.0 SECRET: "the whole story" beat. ExitTrigger records whether all six story pages
        // were found in the winning run; consume it here so a stale flag can never leak into a
        // later playthrough.
        secretTruth = PlayerPrefs.GetInt(TruthKey, 0) == 1;
        if (secretTruth)
        {
            PlayerPrefs.DeleteKey(TruthKey);
            PlayerPrefs.Save();
        }

        if (endingCamera == null) endingCamera = Camera.main;
        if (endingMenuCanvas == null)
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].GetComponent<GraphicRaycaster>() != null)
                {
                    endingMenuCanvas = canvases[i];
                    break;
                }
            }
        }

        if (endingCamera == null || endingMenuCanvas == null)
        {
            Debug.LogError("[EndingCutscene] Assign the ending camera and menu Canvas.", this);
            enabled = false;
            return;
        }

        originalCameraPosition = endingCamera.transform.position;
        originalCameraRotation = endingCamera.transform.rotation;
        originalFieldOfView = endingCamera.fieldOfView;
        originalCameraClearFlags = endingCamera.clearFlags;
        originalCameraBackgroundColor = endingCamera.backgroundColor;
        originalMenuCanvasEnabled = endingMenuCanvas.enabled;
        hasCapturedState = true;

        endingCamera.clearFlags = CameraClearFlags.SolidColor;
        endingCamera.backgroundColor = Color.black;
        BuildOverlay();
        BuildAudioSources();
        endingMenuCanvas.enabled = false;
        isPlaying = true;
        sequence = StartCoroutine(PlayEnding());
    }

    IEnumerator PlayEnding()
    {
        fadeImage.color = new Color(0f, 0f, 0f, 1f);
        flashImage.color = new Color(0.72f, 0.015f, 0.025f, 0f);
        titleText.text = gameOverEnding ? "THE BELL RINGS AGAIN" : "THE EXIT IS OPEN";
        subtitleText.text = gameOverEnding ? "Nobody answered your last call." : "Keep running. Don't look back.";

        StartAmbientBeds();
        yield return FadeTo(0.20f, fadeDuration);
        if (!isPlaying) yield break;
        yield return WaitUnscaled(0.75f);
        if (!isPlaying) yield break;

        if (gameOverEnding)
            yield return PlayGameOverEnding();
        else
            yield return PlayGameWinEnding();

        if (!isPlaying) yield break;
        yield return FinishCard();
    }

    IEnumerator PlayGameOverEnding()
    {
        SetCard("THE HALL IS QUIET", "You can hear your own breathing.");
        yield return WaitUnscaled(1.45f);
        if (!isPlaying) yield break;

        SetCard("DON'T TURN AROUND", "Something is standing behind you.");
        PlayEffect(trailerRiser, riserVolume);
        RevealMonster(5.5f);
        yield return MoveCamera(0.18f, 1.2f, 2f);
        if (!isPlaying) yield break;

        SetCard("IT FOUND YOU", "");
        PlayEffect(monsterScream, screamVolume);
        PlayEffect(impactHit, impactVolume);
        StartCoroutine(LungeMonster(2.2f, 0.28f));
        yield return FlashHit(0.82f, 0.60f);
        if (!isPlaying) yield break;
        yield return CameraShake(0.65f, 0.22f);
        if (!isPlaying) yield break;

        if (stagedMonster != null) stagedMonster.SetActive(false);
        yield return FadeTo(0.93f, 0.42f);
        SetCard("GAME OVER", "The school keeps its secrets.");
        yield return WaitUnscaled(1.1f);
    }

    IEnumerator PlayGameWinEnding()
    {
        SetCard("YOU MADE IT OUT", "The night air feels colder than it should.");
        yield return WaitUnscaled(1.35f);
        if (!isPlaying) yield break;

        PlayEffect(trailerRiser, riserVolume * 0.55f);
        RevealMonster(9f);
        SetCard("DON'T LOOK BACK", "The bell is ringing again.");
        yield return MoveCamera(0.10f, 0.9f, 1.2f);
        if (!isPlaying) yield break;

        PlayEffect(monsterScream, screamVolume * 0.24f);
        PlayEffect(impactHit, impactVolume * 0.24f);
        yield return FlashHit(0.22f, 0.12f);
        if (!isPlaying) yield break;

        if (stagedMonster != null) stagedMonster.SetActive(false);
        yield return FadeTo(0.97f, 0.5f);
        SetCard("YOU ESCAPED", "But the last bell was not for you.");
        yield return WaitUnscaled(1.2f);

        // 2.0 SECRET ending: only when every story page was found this run.
        if (secretTruth)
        {
            if (!isPlaying) yield break;
            PlayEffect(impactHit, impactVolume * 0.4f);
            SetCard("THE LOG IS COMPLETE", "Six pages. One name. Yours.");
            yield return WaitUnscaled(2.4f);
        }
    }

    /// <summary>Skips the cinematic and restores the existing ending buttons.</summary>
    public void SkipEnding()
    {
        if (!isPlaying) return;
        isPlaying = false;
        if (sequence != null) StopCoroutine(sequence);
        sequence = StartCoroutine(CompleteEnding());
    }

    IEnumerator FinishCard()
    {
        yield return FadeTo(1f, fadeDuration);
        if (!isPlaying) yield break;
        if (stagedMonster != null) stagedMonster.SetActive(false);
        SetCard(gameOverEnding ? "GAME OVER" : "YOU SURVIVED",
            gameOverEnding ? "Try again, if you dare." : "For now.");
        yield return FadeTo(0.86f, fadeDuration);
        if (!isPlaying) yield break;
        yield return WaitUnscaled(finalCardDuration);
        if (!isPlaying) yield break;
        yield return CompleteEnding();
    }

    IEnumerator CompleteEnding()
    {
        isPlaying = false;
        if (ambienceSource != null) ambienceSource.Stop();
        if (atmosphereSource != null) atmosphereSource.Stop();
        if (stagedMonster != null) Destroy(stagedMonster);
        if (endingCamera != null)
        {
            endingCamera.transform.position = originalCameraPosition;
            endingCamera.transform.rotation = originalCameraRotation;
            endingCamera.fieldOfView = originalFieldOfView;
            endingCamera.clearFlags = originalCameraClearFlags;
            endingCamera.backgroundColor = originalCameraBackgroundColor;
        }

        yield return FadeTo(1f, 0.45f);
        if (endingMenuCanvas != null) endingMenuCanvas.enabled = originalMenuCanvasEnabled;
        if (overlayRoot != null) Destroy(overlayRoot);
        sequence = null;
        hasCapturedState = false;
    }

    void StartAmbientBeds()
    {
        if (ambienceLoop != null)
        {
            ambienceSource.clip = ambienceLoop;
            ambienceSource.volume = ambienceVolume * GameSettings.AmbientVolume;
            ambienceSource.loop = true;
            ambienceSource.Play();
        }
        if (atmosphereLoop != null)
        {
            atmosphereSource.clip = atmosphereLoop;
            atmosphereSource.volume = atmosphereVolume * GameSettings.AmbientVolume;
            atmosphereSource.loop = true;
            atmosphereSource.Play();
        }
    }

    void BuildAudioSources()
    {
        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.spatialBlend = 0f;
        ambienceSource.priority = 32;

        atmosphereSource = gameObject.AddComponent<AudioSource>();
        atmosphereSource.playOnAwake = false;
        atmosphereSource.spatialBlend = 0f;
        atmosphereSource.priority = 48;

        effectSource = gameObject.AddComponent<AudioSource>();
        effectSource.playOnAwake = false;
        effectSource.spatialBlend = 0f;
        effectSource.priority = 16;
    }

    void PlayEffect(AudioClip clip, float volume)
    {
        if (clip != null && effectSource != null)
            effectSource.PlayOneShot(clip, Mathf.Clamp01(volume * GameSettings.SfxVolume));
    }

    void RevealMonster(float distance)
    {
        if (monsterPrefab == null || stagedMonster != null || endingCamera == null) return;
        stagedMonster = Instantiate(monsterPrefab);
        stagedMonster.name = "EndingMonsterReveal";
        stagedMonster.transform.position = endingCamera.transform.position + endingCamera.transform.forward * distance - Vector3.up * 1.05f;
        stagedMonster.transform.rotation = Quaternion.LookRotation(-endingCamera.transform.forward, Vector3.up);
        stagedMonster.transform.localScale = Vector3.one * 0.72f;

        Animator animator = stagedMonster.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
            int howl = Animator.StringToHash("Base Layer.JacksonRig|StandHowl2");
            if (!animator.HasState(0, howl)) howl = Animator.StringToHash("JacksonRig|IdleStand");
            if (animator.HasState(0, howl)) animator.Play(howl, 0, 0f);
        }
    }

    IEnumerator LungeMonster(float targetDistance, float duration)
    {
        if (stagedMonster == null) yield break;
        Vector3 startPosition = stagedMonster.transform.position;
        Vector3 targetPosition = endingCamera.transform.position + endingCamera.transform.forward * targetDistance - Vector3.up * 1.05f;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration && stagedMonster != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            t = t * t * (3f - 2f * t);
            stagedMonster.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }
        if (stagedMonster != null) stagedMonster.transform.position = targetPosition;
    }

    IEnumerator MoveCamera(float forwardDistance, float duration, float fovDelta)
    {
        Vector3 startPosition = endingCamera.transform.position;
        Quaternion startRotation = endingCamera.transform.rotation;
        float startFov = endingCamera.fieldOfView;
        Vector3 targetPosition = startPosition + endingCamera.transform.forward * forwardDistance;
        float targetFov = Mathf.Clamp(startFov - fovDelta, 35f, 65f);
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                SkipEnding();
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            t = t * t * (3f - 2f * t);
            endingCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            endingCamera.transform.rotation = Quaternion.Slerp(startRotation, startRotation * Quaternion.Euler(0f, Mathf.Sin(t * Mathf.PI) * 2.2f, 0f), t);
            endingCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
            yield return null;
        }
    }

    IEnumerator CameraShake(float duration, float strength)
    {
        Vector3 basePosition = endingCamera.transform.position;
        Quaternion baseRotation = endingCamera.transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                SkipEnding();
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float envelope = 1f - Mathf.Clamp01(elapsed / duration);
            endingCamera.transform.position = basePosition + Random.insideUnitSphere * strength * envelope;
            endingCamera.transform.rotation = baseRotation * Quaternion.Euler(Random.Range(-3f, 3f) * envelope, Random.Range(-4f, 4f) * envelope, Random.Range(-2f, 2f) * envelope);
            yield return null;
        }
        endingCamera.transform.position = basePosition;
        endingCamera.transform.rotation = baseRotation;
    }

    IEnumerator FlashHit(float peakAlpha = 0.72f, float duration = 0.42f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                SkipEnding();
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.Abs(Mathf.Sin(elapsed * 25f));
            Color color = flashImage.color;
            color.a = peakAlpha * pulse * (1f - Mathf.Clamp01(elapsed / duration));
            flashImage.color = color;
            yield return null;
        }
        Color finalColor = flashImage.color;
        finalColor.a = 0f;
        flashImage.color = finalColor;
    }

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float from = fadeImage.color.a;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                SkipEnding();
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            t = t * t * (3f - 2f * t);
            Color color = fadeImage.color;
            color.a = Mathf.Lerp(from, targetAlpha, t);
            fadeImage.color = color;
            yield return null;
        }
        Color finalColor = fadeImage.color;
        finalColor.a = targetAlpha;
        fadeImage.color = finalColor;
    }

    IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                SkipEnding();
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void SetCard(string title, string subtitle)
    {
        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;
    }

    void BuildOverlay()
    {
        overlayRoot = new GameObject("EndingCutsceneCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0.5f;

        fadeImage = CreateImage(overlayRoot.transform, "Blackout", Color.black);
        RectTransform fadeRect = fadeImage.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeImage.raycastTarget = true;

        BuildLetterbox(overlayRoot.transform, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -24f), new Vector2(0f, 48f));
        BuildLetterbox(overlayRoot.transform, "BottomBar", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 24f), new Vector2(0f, 48f));

        titleText = CreateText(overlayRoot.transform, "EndingTitle", "", 32, FontStyle.Bold,
            new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f), new Vector2(0f, 0f), new Vector2(720f, 62f));
        subtitleText = CreateText(overlayRoot.transform, "EndingSubtitle", "", 19, FontStyle.Normal,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(720f, 44f));
        flashImage = CreateImage(overlayRoot.transform, "ImpactFlash", new Color(0.72f, 0.015f, 0.025f, 0f));
        flashImage.rectTransform.anchorMin = Vector2.zero;
        flashImage.rectTransform.anchorMax = Vector2.one;
        flashImage.rectTransform.offsetMin = Vector2.zero;
        flashImage.rectTransform.offsetMax = Vector2.zero;
        flashImage.raycastTarget = false;

        GameObject skipObject = new GameObject("SkipEndingButton", typeof(RectTransform), typeof(Image), typeof(Button));
        skipObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform skipRect = skipObject.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 1f);
        skipRect.anchorMax = new Vector2(1f, 1f);
        skipRect.pivot = new Vector2(0.5f, 0.5f);
        skipRect.anchoredPosition = new Vector2(-76f, -70f);
        skipRect.sizeDelta = new Vector2(104f, 38f);
        Image skipImage = skipObject.GetComponent<Image>();
        skipImage.color = new Color(0.08f, 0.08f, 0.09f, 0.84f);
        skipButton = skipObject.GetComponent<Button>();
        skipButton.targetGraphic = skipImage;
        skipButton.onClick.AddListener(SkipEnding);
        CreateText(skipObject.transform, "Label", "SKIP", 15, FontStyle.Bold,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    Image CreateImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static void BuildLetterbox(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject bar = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = bar.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.94f);
        image.raycastTarget = false;
    }

    static Text CreateText(Transform parent, string objectName, string value, int fontSize, FontStyle fontStyle,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text text = textObject.GetComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    void OnDestroy()
    {
        if (!hasCapturedState) return;
        if (ambienceSource != null) ambienceSource.Stop();
        if (atmosphereSource != null) atmosphereSource.Stop();
        if (stagedMonster != null) Destroy(stagedMonster);
        if (endingCamera != null)
        {
            endingCamera.transform.position = originalCameraPosition;
            endingCamera.transform.rotation = originalCameraRotation;
            endingCamera.fieldOfView = originalFieldOfView;
            endingCamera.clearFlags = originalCameraClearFlags;
            endingCamera.backgroundColor = originalCameraBackgroundColor;
        }
        if (endingMenuCanvas != null) endingMenuCanvas.enabled = originalMenuCanvasEnabled;
        hasCapturedState = false;
    }
}
