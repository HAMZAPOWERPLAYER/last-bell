using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 7.1 main menu: Play / Settings / Credits / Quit.
///
/// UI is built in code (same approach as PauseMenuUI) so it cannot drift from the scene file.
/// Settings state, persistence and the graphics-tier mapping live in ONE place - GameSettings -
/// so this panel and the pause menu's panel are two views of the same implementation.
///
/// Layout note: the canvas matches WIDTH (reference 800x600), so on a landscape phone the usable
/// canvas is only ~360 units tall. Every row here stays inside +/-180 or it would be off-screen.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Tooltip("Ambient bed to loop quietly behind the menu. Assigned in the editor.")]
    public AudioClip menuAmbient;

    [Range(0f, 1f)] public float menuAmbientVolume = 0.35f;

    Canvas canvas;
    GameObject settingsPanel, creditsPanel;
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
        ambientSource.volume = Mathf.Clamp01(menuAmbientVolume * GameSettings.MasterVolume * GameSettings.AmbientVolume);
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

        // Backdrop
        var bg = new GameObject("Backdrop", typeof(Image));
        bg.transform.SetParent(canvas.transform, false);
        var brt = bg.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        var bimg = bg.GetComponent<Image>();
        bimg.color = new Color(0.02f, 0.03f, 0.03f, 1f);
        bimg.raycastTarget = false;

        var font = BuiltinFont();

        MakeText(canvas.transform, "Title", "HIGH SCHOOL HORROR", font, 34,
            new Color(0.80f, 0.86f, 0.80f), new Vector2(0f, 138f), new Vector2(760f, 44f));
        MakeText(canvas.transform, "Subtitle", "five floors. one way out.", font, 15,
            new Color(0.48f, 0.55f, 0.50f), new Vector2(0f, 106f), new Vector2(700f, 26f));

        float y = 54f;
        MakeButton(canvas.transform, "Play", "PLAY", new Vector2(0f, y), new Vector2(300f, 40f), font, 22, OnPlay);
        y -= 46f;
        MakeButton(canvas.transform, "Settings", "SETTINGS", new Vector2(0f, y), new Vector2(300f, 40f), font, 22,
            () => { settingsPanel.SetActive(true); RefreshLabels(); });
        y -= 46f;
        MakeButton(canvas.transform, "Credits", "CREDITS", new Vector2(0f, y), new Vector2(300f, 40f), font, 22,
            () => creditsPanel.SetActive(true));
        y -= 46f;
        MakeButton(canvas.transform, "Quit", "QUIT", new Vector2(0f, y), new Vector2(300f, 40f), font, 22, OnQuit);

        BuildSettingsPanel(font);
        BuildCreditsPanel(font);

        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
    }

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

    /// <summary>
    /// A label that FILLS its parent, centred both ways.
    ///
    /// This is the fix for the stacked-letter bug: the previous label was created with centre
    /// anchors and sizeDelta = zero, i.e. a 0x0 rect. With HorizontalWrapMode.Wrap a zero-width
    /// rect wraps after every single character, which renders "PLAY" as P/L/A/Y stacked vertically.
    /// Stretching the rect to the parent and using Overflow means a short label can never wrap.
    /// </summary>
    Text MakeStretchedText(Transform parent, string name, string text, Font font, int size, Color colour)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(6f, 0f);      // small side padding so text never touches the edge
        rt.offsetMax = new Vector2(-6f, 0f);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = colour;
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
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

        MakeStretchedText(go.transform, "Label", label, font, fontSize, new Color(0.86f, 0.92f, 0.88f));
    }

    // ------------------------------------------------------------------ behaviour

    void ApplyAndLabel()
    {
        GameSettings.ApplyGraphicsTier();
        GameSettings.ApplyVolumes();
        if (ambientSource != null)
            ambientSource.volume = Mathf.Clamp01(menuAmbientVolume * GameSettings.MasterVolume * GameSettings.AmbientVolume);
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
        GameSettings.Save();
        SceneManager.LoadScene("MainLevel");
    }

    void OnQuit()
    {
        GameSettings.Save();
        Application.Quit();
    }
}
