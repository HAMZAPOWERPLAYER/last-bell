using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 7 menus: pause panel, settings panel and credits panel, plus the HUD pause button.
///
/// The UI is built in code at runtime rather than baked into the scene - it cannot drift out of
/// sync with the scene file, and it needs no editor builder pass. Swap any of this for a designed
/// prefab later; the hook-ups (PauseManager, GameSettings) stay the same.
///
/// Pause button placement: TOP-LEFT, deliberately outside the look pad's right-half drag zone
/// (PART 7.5). The left half only has the movement stick at bottom-left, so the top-left is free.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    Canvas canvas;
    GameObject pausePanel, settingsPanel, creditsPanel;
    Text sensitivityLabel, masterLabel, ambientLabel, sfxLabel, tierLabel;

    static readonly string[] TierNames = { "Low", "Medium", "High" };

    void Start()
    {
        BuildUI();
        // Apply stored values as the menu opens so the labels reflect reality.
        GameSettings.ApplyGraphicsTier();
        RefreshLabels();

        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.PauseChanged += OnPauseChanged;
            OnPauseChanged(PauseManager.Instance.IsPaused);
        }
        else
        {
            ShowPause(false);
        }
    }

    void OnDestroy()
    {
        if (PauseManager.Instance != null) PauseManager.Instance.PauseChanged -= OnPauseChanged;
    }

    void OnPauseChanged(bool paused)
    {
        pausePanel.SetActive(paused);
        if (!paused)
        {
            settingsPanel.SetActive(false);
            creditsPanel.SetActive(false);
        }
        if (paused) RefreshLabels();
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
        var go = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0f;   // same convention as the game's HUD canvas

        var font = BuiltinFont();

        // ---- HUD pause button: top-left, outside the right-half look-pad drag zone ----
        MakeButton(canvas.transform, "PauseButton", "II",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -48f), new Vector2(72f, 72f),
            font, 32, OnPausePressed, new Color(0.10f, 0.12f, 0.11f, 0.85f));

        // ---- pause panel ----
        pausePanel = MakePanel(canvas.transform, "PausePanel", new Color(0.02f, 0.03f, 0.03f, 0.90f));

        // NOTE ON LAYOUT: the canvas matches WIDTH (reference 800x600), so on a landscape phone the
        // usable canvas is only ~360 units tall - anything beyond +/-180 is off-screen. Every menu
        // below is laid out to fit inside that, which is why the rows are compact.
        MakeText(pausePanel.transform, "Title", "PAUSED", font, 34,
            new Color(0.80f, 0.86f, 0.80f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 130f), new Vector2(520f, 44f));

        float y = 72f;
        MakeButton(pausePanel.transform, "Resume", "RESUME", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, y), new Vector2(300f, 40f), font, 22, OnResume);
        y -= 46f;
        MakeButton(pausePanel.transform, "Settings", "SETTINGS", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, y), new Vector2(300f, 40f), font, 22, () => { settingsPanel.SetActive(true); RefreshLabels(); });
        y -= 46f;
        MakeButton(pausePanel.transform, "Credits", "CREDITS", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, y), new Vector2(300f, 40f), font, 22, () => creditsPanel.SetActive(true));
        y -= 46f;
        MakeButton(pausePanel.transform, "MainMenu", "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, y), new Vector2(300f, 40f), font, 22, OnMainMenu);
        y -= 46f;
        MakeButton(pausePanel.transform, "Quit", "QUIT", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, y), new Vector2(300f, 40f), font, 22, OnQuit);

        BuildSettingsPanel(font);
        BuildCreditsPanel(font);

        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
    }

    void BuildSettingsPanel(Font font)
    {
        settingsPanel = MakePanel(canvas.transform, "SettingsPanel", new Color(0.02f, 0.03f, 0.03f, 0.96f));
        MakeText(settingsPanel.transform, "Title", "SETTINGS", font, 30,
            new Color(0.80f, 0.86f, 0.80f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 138f), new Vector2(520f, 40f));

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
            new Color(0.62f, 0.68f, 0.64f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-150f, y), new Vector2(200f, 28f));
        tierLabel = MakeText(settingsPanel.transform, "TierValue", "Medium", font, 18,
            new Color(0.80f, 0.90f, 0.82f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(20f, y), new Vector2(110f, 28f));
        MakeButton(settingsPanel.transform, "TierPrev", "<", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-46f, y), new Vector2(38f, 30f), font, 20, () => CycleTier(-1));
        MakeButton(settingsPanel.transform, "TierNext", ">", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(86f, y), new Vector2(38f, 30f), font, 20, () => CycleTier(1));

        MakeButton(settingsPanel.transform, "SettingsBack", "BACK", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -132f), new Vector2(200f, 38f), font, 20, () => settingsPanel.SetActive(false));
    }

    void BuildCreditsPanel(Font font)
    {
        creditsPanel = MakePanel(canvas.transform, "CreditsPanel", new Color(0.02f, 0.03f, 0.03f, 0.96f));
        MakeText(creditsPanel.transform, "Title", "CREDITS", font, 30,
            new Color(0.80f, 0.86f, 0.80f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 138f), new Vector2(520f, 40f));

        string body =
            "Built on High School Horror Android\nby yurifarion\n" +
            "github.com/yurifarion/High-School-Horror-Android\n" +
            "(CC-BY-4.0 - original base project)\n\n" +
            "Touch controls: NeKONTROLLER\nby NeKoRoSYS (MIT licence)\n" +
            "github.com/NeKoRoSYS/NeKONTROLLER\n\n" +
            "Mobile port and additional work:\nHAMZA POWERPLAYER";

        MakeText(creditsPanel.transform, "Body", body, font, 17,
            new Color(0.72f, 0.78f, 0.74f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 12f), new Vector2(660f, 220f));

        MakeButton(creditsPanel.transform, "CreditsBack", "BACK", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -132f), new Vector2(200f, 38f), font, 20, () => creditsPanel.SetActive(false));
    }

    Text MakeSettingRow(Transform parent, Font font, string caption, float y,
        UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
    {
        MakeText(parent, caption + "_cap", caption, font, 17, new Color(0.62f, 0.68f, 0.64f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, y), new Vector2(200f, 28f));
        var value = MakeText(parent, caption + "_val", "-", font, 18, new Color(0.80f, 0.90f, 0.82f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, y), new Vector2(110f, 28f));
        MakeButton(parent, caption + "_minus", "-", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-46f, y), new Vector2(38f, 30f), font, 20, minus);
        MakeButton(parent, caption + "_plus", "+", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(86f, y), new Vector2(38f, 30f), font, 20, plus);
        return value;
    }

    GameObject MakePanel(Transform parent, string name, Color colour)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = colour;
        img.raycastTarget = true;      // blocks the HUD underneath while paused
        return go;
    }

    Text MakeText(Transform parent, string name, string text, Font font, int size, Color colour,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = colour;
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    void MakeButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta, Font font, int fontSize, UnityEngine.Events.UnityAction action,
        Color? overrideColour = null)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var img = go.GetComponent<Image>();
        img.color = overrideColour.HasValue ? overrideColour.Value : new Color(0.13f, 0.17f, 0.15f, 0.92f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colours = btn.colors;
        colours.highlightedColor = new Color(0.22f, 0.30f, 0.26f);
        colours.pressedColor = new Color(0.30f, 0.42f, 0.34f);
        btn.colors = colours;
        if (action != null) btn.onClick.AddListener(action);

        MakeText(go.transform, "Label", label, font, fontSize, new Color(0.86f, 0.92f, 0.88f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    // ------------------------------------------------------------------ behaviour

    void ApplyAndLabel()
    {
        GameSettings.ApplyGraphicsTier();
        GameSettings.ApplyVolumes();
        GameSettings.ApplyLook(Object.FindObjectOfType<Controller>());
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

    void OnPausePressed()
    {
        if (PauseManager.Instance != null) PauseManager.Instance.Toggle();
        else ShowPause(!pausePanel.activeSelf);
    }

    void ShowPause(bool show) { pausePanel.SetActive(show); }

    void OnResume()
    {
        if (PauseManager.Instance != null) PauseManager.Instance.Resume();
        else ShowPause(false);
    }

    void OnMainMenu()
    {
        if (PauseManager.Instance != null) PauseManager.Instance.ReturnToMainMenu();
        else { Time.timeScale = 1f; AudioListener.pause = false; SceneManager.LoadScene("Menu"); }
    }

    void OnQuit()
    {
        if (PauseManager.Instance != null) PauseManager.Instance.QuitApp();
        else Application.Quit();
    }
}
