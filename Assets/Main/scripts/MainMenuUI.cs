using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 7.1 main menu, restyled as the LAST BELL text-list menu.
///
/// UI is still built in code (same approach as PauseMenuUI) so it cannot drift from the scene file.
/// The presentation (procedural sprites, unscaled-time animation, highlight bar, grain, vignette,
/// art fade) lives in ONE reusable component - LastBellMenuStyle - shared with the pause menu.
/// Settings state, persistence and the graphics-tier mapping still live in GameSettings.
///
/// Layout note: the canvas matches WIDTH (reference 800x600), so on a landscape phone the usable
/// canvas is only ~360 units tall. Every row stays inside +/-180 or it would be off-screen.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Tooltip("Ambient bed to loop quietly behind the menu. Assigned in the editor.")]
    public AudioClip menuAmbient;

    [Range(0f, 1f)] public float menuAmbientVolume = 0.35f;

    [Header("LAST BELL menu style")]
    [Tooltip("Right-hand art slot. Empty = no art. Drop your own image (Sprite) in here later; " +
             "it fades into darkness toward the left of the screen automatically.")]
    public Sprite backdropArt;

    [Tooltip("Optional display font. Empty = the project's built-in font. " +
             "A third-party font belongs on the Licences screen.")]
    public Font displayFont;

    Canvas canvas;
    LastBellMenuStyle style;
    GameObject settingsPanel, creditsPanel, licencesPanel;
    Text sensitivityLabel, masterLabel, ambientLabel, sfxLabel, tierLabel;
    AudioSource ambientSource;

    static readonly string[] TierNames = { "Low", "Medium", "High" };

    void Start()
    {
        Time.timeScale = 1f;              // never inherit a frozen clock from a paused session
        AudioListener.pause = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        GameSettings.ApplyGraphicsTier();
        BuildUI();
        HideOtherCanvases();
        RefreshLabels();
        StartAmbient();
    }

    /// <summary>Android back button / Escape steps out of the open panel instead of doing nothing.</summary>
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) CloseTopPanel();
    }

    void CloseTopPanel()
    {
        if (licencesPanel != null && licencesPanel.activeSelf) { licencesPanel.SetActive(false); return; }
        if (creditsPanel != null && creditsPanel.activeSelf) { creditsPanel.SetActive(false); return; }
        if (settingsPanel != null && settingsPanel.activeSelf) { settingsPanel.SetActive(false); return; }
    }

    /// <summary>
    /// The Menu scene still ships the base project's placeholder menu canvas (the MenuAction one).
    /// Leaving it enabled draws two menus on top of each other. Canvas.enabled = false stops the
    /// rendering while leaving MenuAction's own Update running - it still resets PlayerPrefs "Keys"
    /// and drives the music toggle - so no existing functionality is lost.
    /// </summary>
    void HideOtherCanvases()
    {
        var all = Object.FindObjectsOfType<Canvas>(true);
        int hidden = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var other = all[i];
            if (other == null || other == canvas) continue;
            if (other.transform.IsChildOf(canvas.transform)) continue;
            other.enabled = false;
            hidden++;
        }
        if (hidden > 0) Debug.Log("[MainMenuUI] hid " + hidden + " pre-existing canvas(es) from the base project");
    }

    void StartAmbient()
    {
        if (menuAmbient == null) return;
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.clip = menuAmbient;
        ambientSource.loop = true;
        ambientSource.playOnAwake = false;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = Mathf.Clamp01(menuAmbientVolume * GameSettings.AmbientVolume);
        ambientSource.Play();
    }

    // ------------------------------------------------------------------ construction

    static Font BuiltinFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    void BuildUI()
    {
        var go = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0f;

        var font = BuiltinFont();

        // ---- LAST BELL text-list menu ----
        var styleGo = new GameObject("MenuStyle", typeof(RectTransform));
        styleGo.transform.SetParent(canvas.transform, false);
        style = styleGo.AddComponent<LastBellMenuStyle>();
        style.displayFont = displayFont;
        style.EnsureBuilt();
        style.BuildArt(backdropArt);
        style.BuildTitle("LAST BELL");
        style.BuildSubtitle("two floors. three keys. one way out.");
        style.AddItem(0, "PLAY", OnPlay);
        style.AddItem(1, "SETTINGS", OpenSettings);
        style.AddItem(2, "CREDITS", OpenCredits);
        style.AddItem(3, "LICENCES", OpenLicences);
        style.AddItem(4, "QUIT", OnQuit);
        style.SettleClosed();

        BuildSettingsPanel(font);
        BuildCreditsPanel(font);
        BuildLicencesPanel(font);

        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        licencesPanel.SetActive(false);

        StartCoroutine(style.OpenRoutine());
    }

    void OpenSettings()
    {
        settingsPanel.SetActive(true);
        RefreshLabels();
    }

    void OpenCredits() { creditsPanel.SetActive(true); }

    void OpenLicences() { licencesPanel.SetActive(true); }

    void BuildSettingsPanel(Font font)
    {
        settingsPanel = MakePanel(canvas.transform, "SettingsPanel", new Color(0.02f, 0.03f, 0.03f, 0.97f));
        MakeText(settingsPanel.transform, "Title", "SETTINGS", font, 30,
            new Color(0.80f, 0.86f, 0.80f), new Vector2(0f, 138f), new Vector2(520f, 40f));

        float y = 70f;
        sensitivityLabel = MakeSettingRow(settingsPanel.transform, font, "LOOK SENSITIVITY", y,
            () => { GameSettings.LookMultiplier -= 0.1f; GameSettings.Save(); ApplyAndLabel(); },
            () => { GameSettings.LookMultiplier += 0.1f; GameSettings.Save(); ApplyAndLabel(); });
        y -= 38f;
        masterLabel = MakeSettingRow(settingsPanel.transform, font, "MASTER VOLUME", y,
            () => { GameSettings.MasterVolume -= 0.1f; GameSettings.Save(); ApplyAndLabel(); },
            () => { GameSettings.MasterVolume += 0.1f; GameSettings.Save(); ApplyAndLabel(); });
        y -= 38f;
        ambientLabel = MakeSettingRow(settingsPanel.transform, font, "AMBIENT VOLUME", y,
            () => { GameSettings.AmbientVolume -= 0.1f; GameSettings.Save(); ApplyAndLabel(); },
            () => { GameSettings.AmbientVolume += 0.1f; GameSettings.Save(); ApplyAndLabel(); });
        y -= 38f;
        sfxLabel = MakeSettingRow(settingsPanel.transform, font, "SFX VOLUME", y,
            () => { GameSettings.SfxVolume -= 0.1f; GameSettings.Save(); ApplyAndLabel(); },
            () => { GameSettings.SfxVolume += 0.1f; GameSettings.Save(); ApplyAndLabel(); });
        y -= 38f;

        MakeText(settingsPanel.transform, "TierCaption", "GRAPHICS", font, 17,
            new Color(0.62f, 0.68f, 0.64f), new Vector2(-150f, y), new Vector2(200f, 28f));
        tierLabel = MakeText(settingsPanel.transform, "TierValue", "Medium", font, 18,
            new Color(0.80f, 0.90f, 0.82f), new Vector2(20f, y), new Vector2(110f, 28f));
        MakeButton(settingsPanel.transform, "TierPrev", "<", new Vector2(-46f, y), new Vector2(38f, 30f), font, 20, () => CycleTier(-1));
        MakeButton(settingsPanel.transform, "TierNext", ">", new Vector2(86f, y), new Vector2(38f, 30f), font, 20, () => CycleTier(1));

        MakeButton(settingsPanel.transform, "SettingsBack", "BACK", new Vector2(0f, -132f), new Vector2(200f, 38f), font, 20,
            () => settingsPanel.SetActive(false));
    }

    void BuildCreditsPanel(Font font)
    {
        creditsPanel = MakePanel(canvas.transform, "CreditsPanel", new Color(0.02f, 0.03f, 0.03f, 0.97f));
        MakeText(creditsPanel.transform, "Title", "CREDITS", font, 30,
            new Color(0.80f, 0.86f, 0.80f), new Vector2(0f, 138f), new Vector2(520f, 40f));

        string body =
            "Built on High School Horror Android\nby yurifarion\n" +
            "github.com/yurifarion/High-School-Horror-Android\n" +
            "(CC-BY-4.0 - original base project)\n\n" +
            "Touch controls: NeKONTROLLER\nby NeKoRoSYS (MIT licence)\n" +
            "github.com/NeKoRoSYS/NeKONTROLLER\n\n" +
            "Mobile port and additional work:\nHAMZA POWERPLAYER";

        MakeText(creditsPanel.transform, "Body", body, font, 17,
            new Color(0.72f, 0.78f, 0.74f), new Vector2(0f, 12f), new Vector2(660f, 220f));
        MakeButton(creditsPanel.transform, "CreditsBack", "BACK", new Vector2(0f, -132f), new Vector2(200f, 38f), font, 20,
            () => creditsPanel.SetActive(false));
    }

    /// <summary>
    /// Licence screen. Third-party code/art credits plus the reserved entry for a future free
    /// SIL OFL display font - if one is ever added it MUST be listed here.
    /// </summary>
    void BuildLicencesPanel(Font font)
    {
        licencesPanel = MakePanel(canvas.transform, "LicencesPanel", new Color(0.02f, 0.03f, 0.03f, 0.97f));
        MakeText(licencesPanel.transform, "Title", "LICENCES", font, 30,
            new Color(0.80f, 0.86f, 0.80f), new Vector2(0f, 150f), new Vector2(520f, 40f));

        string body =
            "BASE PROJECT\n" +
            "High School Horror Android - yurifarion\n" +
            "CC-BY-4.0, attribution required (License.txt)\n\n" +
            "TOUCH CONTROLS\n" +
            "NeKONTROLLER - NeKoRoSYS, MIT licence\n\n" +
            "MOBILE PORT, ADDITIONS AND SYSTEMS\n" +
            "HAMZA POWERPLAYER\n\n" +
            "TYPE\n" +
            "No third-party display font is bundled. Menu text\n" +
            "uses Unity's built-in font. A free SIL OFL font\n" +
            "added later must be credited here.\n\n" +
            "ART\n" +
            "The menu art placeholder is a screenshot of this\n" +
            "game's own level. No third-party art is used.";

        MakeText(licencesPanel.transform, "Body", body, font, 13,
            new Color(0.72f, 0.78f, 0.74f), new Vector2(0f, 0f), new Vector2(660f, 250f));
        MakeButton(licencesPanel.transform, "LicencesBack", "BACK", new Vector2(0f, -155f), new Vector2(200f, 38f), font, 20,
            () => licencesPanel.SetActive(false));
    }

    Text MakeSettingRow(Transform parent, Font font, string caption, float y,
        UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
    {
        MakeText(parent, caption + "_cap", caption, font, 17, new Color(0.62f, 0.68f, 0.64f),
            new Vector2(-150f, y), new Vector2(200f, 28f));
        var value = MakeText(parent, caption + "_val", "-", font, 18, new Color(0.80f, 0.90f, 0.82f),
            new Vector2(20f, y), new Vector2(110f, 28f));
        MakeButton(parent, caption + "_minus", "-", new Vector2(-46f, y), new Vector2(38f, 30f), font, 20, minus);
        MakeButton(parent, caption + "_plus", "+", new Vector2(86f, y), new Vector2(38f, 30f), font, 20, plus);
        return value;
    }

    GameObject MakePanel(Transform parent, string name, Color colour)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = colour;
        img.raycastTarget = true;
        return go;
    }

    Text MakeText(Transform parent, string name, string text, Font font, int size, Color colour,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.color = colour; t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    void MakeButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 sizeDelta,
        Font font, int fontSize, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.13f, 0.17f, 0.15f, 0.92f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colours = btn.colors;
        colours.highlightedColor = new Color(0.22f, 0.30f, 0.26f);
        colours.pressedColor = new Color(0.30f, 0.42f, 0.34f);
        btn.colors = colours;
        if (action != null) btn.onClick.AddListener(action);

        var textGo = new GameObject("Label", typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.offsetMin = new Vector2(6f, 0f);
        trt.offsetMax = new Vector2(-6f, 0f);
        var t = textGo.GetComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.color = new Color(0.86f, 0.92f, 0.88f);
        t.text = label;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
    }

    // ------------------------------------------------------------------ behaviour

    void ApplyAndLabel()
    {
        GameSettings.ApplyGraphicsTier();
        GameSettings.ApplyVolumes();
        if (ambientSource != null)
            ambientSource.volume = Mathf.Clamp01(menuAmbientVolume * GameSettings.AmbientVolume);
        RefreshLabels();
    }

    void CycleTier(int delta)
    {
        int t = (int)GameSettings.GraphicsTier + delta;
        if (t < 0) t = 2;
        if (t > 2) t = 0;
        GameSettings.GraphicsTier = (GameSettings.Tier)t;
        GameSettings.Save();
        ApplyAndLabel();
    }

    void RefreshLabels()
    {
        if (sensitivityLabel != null) sensitivityLabel.text = "x" + GameSettings.LookMultiplier.ToString("F1");
        if (masterLabel != null) masterLabel.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
        if (ambientLabel != null) ambientLabel.text = Mathf.RoundToInt(GameSettings.AmbientVolume * 100f) + "%";
        if (sfxLabel != null) sfxLabel.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
        if (tierLabel != null) tierLabel.text = TierNames[(int)GameSettings.GraphicsTier];
    }

    void OnPlay()
    {
        // Starting a new game is the explicit point at which run progress must be cleared. Relying
        // on the Menu scene's own load to reset it meant any other route into MainLevel (editor
        // Play, a direct scene load) resumed the previous session's key count.
        GameManager.StartNewRun();
        GameSettings.Save();
        SceneManager.LoadScene("MainLevel");
    }

    void OnQuit()
    {
        GameSettings.Save();
        Application.Quit();
    }
}
