using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PART 9 tooling for the LAST BELL text-list menus.
///
/// MENU: Tools/Part9/Capture Menu Backdrop (while playing)
/// MENU: Tools/Part9/Assign Menu Art In Active Scene
/// MENU: Tools/Part9/Runtime Menu Animation Test (while playing)
/// MENU: Tools/Part9/Game View Size/&lt;device resolutions&gt;
/// MENU: Tools/Part7/Runtime Pause Channel Test (while playing)
///
/// The two runtime tests are editor-driven state machines (EditorApplication.update) rather than
/// blocking loops, so they can wait real frames - and real UNSCALED time - while the pause menu
/// holds Time.timeScale at 0. They are verification only: nothing here changes pause state,
/// audio routing or gameplay, it only calls the public API and reads it back.
/// </summary>
public static class Part9MenuStyle
{
    const string ArtPath = "Assets/Main/UI/Art/MenuBackdrop_Corridor.png";

    // ================================================================== device resolutions

    [MenuItem("Tools/Part9/Game View Size/2340x1080 (20:9 phone)")]
    public static void Size2340x1080() { SetGameViewSize(2340, 1080); }

    [MenuItem("Tools/Part9/Game View Size/1920x1080 (16:9)")]
    public static void Size1920x1080() { SetGameViewSize(1920, 1080); }

    [MenuItem("Tools/Part9/Game View Size/1280x720 (16:9)")]
    public static void Size1280x720() { SetGameViewSize(1280, 720); }

    [MenuItem("Tools/Part9/Game View Size/720x1280 (portrait, off-design)")]
    public static void Size720x1280() { SetGameViewSize(720, 1280); }

    /// <summary>
    /// Switches the Game View to (or creates) a fixed device resolution, so the same canvas maths the
    /// Android Device Simulator uses can be reproduced for screenshots. Reports the size actually
    /// resolved so the caller can confirm it took effect before capturing.
    /// </summary>
    public static void SetGameViewSize(int w, int h)
    {
        try
        {
            var asm = typeof(Editor).Assembly;
            var gameViewType = asm.GetType("UnityEditor.GameView");
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var groupEnumType = asm.GetType("UnityEditor.GameViewSizeGroupType");
            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var kindEnumType = asm.GetType("UnityEditor.GameViewSizeType");
            if (gameViewType == null || sizesType == null || sizeType == null)
            {
                Debug.LogError("[PART9 MENU] GameView reflection types missing in this Unity version.");
                return;
            }

            var gameView = EditorWindow.GetWindow(gameViewType);
            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singletonType.GetProperty("instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null, null);
            var android = Enum.Parse(groupEnumType, "Android");
            var group = sizesType.GetMethod("GetGroup", new Type[] { groupEnumType })
                .Invoke(instance, new object[] { android });

            string label = "LASTBELL " + w + "x" + h;
            var displayNames = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
            int idx = Array.IndexOf(displayNames, label);
            if (idx < 0)
            {
                var ctor = sizeType.GetConstructor(new Type[] { kindEnumType, typeof(int), typeof(int), typeof(string) });
                var kind = Enum.Parse(kindEnumType, "FixedResolution");
                var newSize = ctor.Invoke(new object[] { kind, w, h, label });
                group.GetType().GetMethod("AddCustomSize", new Type[] { sizeType }).Invoke(group, new object[] { newSize });
                displayNames = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
                idx = Array.IndexOf(displayNames, label);
            }
            if (idx < 0) { Debug.LogError("[PART9 MENU] could not register " + label); return; }

            var cb = gameViewType.GetMethod("SizeSelectionCallback",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (cb != null) cb.Invoke(gameView, new object[] { idx, null });
            else
            {
                var prop = gameViewType.GetProperty("selectedSizeIndex",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (prop == null) { Debug.LogError("[PART9 MENU] no Game View size selection API."); return; }
                prop.SetValue(gameView, idx, null);
            }
            gameView.Repaint();
            Debug.Log("[PART9 MENU] Game View -> " + label + " (index " + idx + "); Screen reports "
                + Screen.width + "x" + Screen.height + " one frame later.");
        }
        catch (Exception e)
        {
            Debug.LogError("[PART9 MENU] Game View resize failed: " + e.GetType().Name + ": " + e.Message);
        }
    }

    // ================================================================== backdrop capture

    /// <summary>
    /// Renders the active game camera into the placeholder art slot. Screen Space Overlay UI is not
    /// drawn by a camera render, so the shot is clean of the HUD without disabling anything.
    /// </summary>
    [MenuItem("Tools/Part9/Capture Menu Backdrop (while playing)")]
    public static void CaptureBackdrop()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[PART9 MENU] enter play mode in MainLevel, frame the corridor you want, then run this again.");
            return;
        }

        var cam = FindGameCamera();
        if (cam == null) { Debug.LogError("[PART9 MENU] no enabled camera found."); return; }

        const int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
        tex.Apply();

        cam.targetTexture = prevTarget;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        if (!AssetDatabase.IsValidFolder("Assets/Main/UI/Art"))
            AssetDatabase.CreateFolder("Assets/Main/UI", "Art");

        var full = System.IO.Path.GetFullPath(ArtPath);
        System.IO.File.WriteAllBytes(full, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceUpdate);
        ConfigureSpriteImporter();

        long bytes = new System.IO.FileInfo(full).Length;
        Debug.Log("[PART9 MENU] backdrop captured from '" + cam.name + "' -> " + ArtPath
            + " (" + w + "x" + h + ", " + (bytes / 1024) + " KB)");
    }

    /// <summary>
    /// This project's player camera is tagged 'Player', not 'MainCamera', so Camera.main is null.
    /// Look it up by tag, then by name, then fall back to any enabled camera.
    /// </summary>
    static Camera FindGameCamera()
    {
        if (Camera.main != null) return Camera.main;
        var all = Camera.allCameras;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].CompareTag("MainCamera")) return all[i];
        }
        var named = GameObject.Find("Main Camera");
        if (named != null)
        {
            var c = named.GetComponent<Camera>();
            if (c != null && c.enabled) return c;
        }
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled) return all[i];
        }
        var any = UnityEngine.Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < any.Length; i++)
        {
            if (any[i] != null && any[i].enabled) return any[i];
        }
        return null;
    }

    static void ConfigureSpriteImporter()
    {
        var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;

        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 1024;
        android.format = TextureImporterFormat.ASTC_6x6;   // same convention as the project's albedo maps
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
    }

    /// <summary>Drops the placeholder sprite into the serialized art slot of every menu in the open scene.</summary>
    [MenuItem("Tools/Part9/Assign Menu Art In Active Scene")]
    public static void AssignMenuArt()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        if (sprite == null)
        {
            Debug.LogError("[PART9 MENU] no sprite at " + ArtPath + " - run the capture first.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        int n = 0;
        var menus = UnityEngine.Object.FindObjectsOfType<MainMenuUI>(true);
        for (int i = 0; i < menus.Length; i++)
        {
            menus[i].backdropArt = sprite;
            EditorUtility.SetDirty(menus[i]);
            n++;
        }
        var pauses = UnityEngine.Object.FindObjectsOfType<PauseMenuUI>(true);
        for (int i = 0; i < pauses.Length; i++)
        {
            pauses[i].backdropArt = sprite;
            EditorUtility.SetDirty(pauses[i]);
            n++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PART9 MENU] assigned " + ArtPath + " to " + n + " menu component(s) in " + scene.name);
    }

    // ================================================================== shared step machine

    delegate void StepFn();

    static StepFn _step;
    static float _waitUntil;
    static int _frameWait;
    static readonly List<string> _log = new List<string>();
    static int _fail;

    static void Begin(string title, StepFn first)
    {
        _log.Clear();
        _log.Add("[" + title + "]");
        _fail = 0;
        _step = first;
        _frameWait = 0;
        _waitUntil = 0f;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (_step == null) { EditorApplication.update -= Tick; return; }
        if (!Application.isPlaying)
        {
            _log.Add("  ABORT play mode ended before the test finished");
            _fail++;
            Finish();
            return;
        }
        if (_frameWait > 0) { _frameWait--; return; }
        if (_waitUntil > 0f)
        {
            if (Time.unscaledTime < _waitUntil) return;
            _waitUntil = 0f;
        }
        var s = _step;
        _step = null;
        try { s(); }
        catch (Exception e)
        {
            _fail++;
            _log.Add("  FAIL  exception: " + e.GetType().Name + ": " + e.Message);
            Finish();
        }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        _step = null;
        string body = string.Join("\n", _log.ToArray());
        if (_fail == 0) Debug.Log(body + "\nRESULT: ALL CHECKS PASSED");
        else Debug.LogError(body + "\nRESULT: " + _fail + " CHECK(S) FAILED");
    }

    static void Check(string label, bool ok)
    {
        _log.Add((ok ? "  PASS  " : "  FAIL  ") + label);
        if (!ok) _fail++;
    }

    static void Note(string text) { _log.Add("    " + text); }

    static void Wait(float unscaledSeconds) { _waitUntil = Time.unscaledTime + unscaledSeconds; }
    static void WaitFrames(int frames) { _frameWait = frames; }
    static void Next(StepFn fn) { _step = fn; }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    // ================================================================== audio channel snapshot

    class AudioSnapshot
    {
        public readonly List<string> lines = new List<string>();
        public bool listenerPause;
        public float listenerVolume;
    }

    /// <summary>
    /// Routing fingerprint: per-source volume / pitch / mute / enabled / loop / mixer group, plus
    /// AudioListener.volume. AudioListener.pause is deliberately NOT part of the diff - flipping it
    /// IS the designed pause mute, and it is asserted separately.
    /// </summary>
    static string DiffAudio(AudioSnapshot a, AudioSnapshot b)
    {
        if (!Mathf.Approximately(a.listenerVolume, b.listenerVolume)) return "AudioListener.volume changed";
        if (a.lines.Count != b.lines.Count) return "source count changed " + a.lines.Count + " -> " + b.lines.Count;
        for (int i = 0; i < a.lines.Count; i++)
            if (a.lines[i] != b.lines[i]) return "source changed: " + a.lines[i] + " -> " + b.lines[i];
        return null;
    }

    static AudioSnapshot SnapshotAudio()
    {
        var snap = new AudioSnapshot();
        snap.listenerPause = AudioListener.pause;
        snap.listenerVolume = AudioListener.volume;
        var sources = UnityEngine.Object.FindObjectsOfType<AudioSource>(true);
        Array.Sort(sources, (a, b) => string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < sources.Length; i++)
        {
            var s = sources[i];
            snap.lines.Add(s.name + "|" + s.volume.ToString("F4") + "|" + s.pitch.ToString("F4")
                + "|" + s.mute + "|" + s.enabled + "|" + s.loop
                + "|" + (s.outputAudioMixerGroup != null ? s.outputAudioMixerGroup.name : "none"));
        }
        return snap;
    }

    // ================================================================== pause channel test

    static PauseManager _pm;
    static PauseMenuUI _menuUI;
    static int _events;
    static AudioSnapshot _audioBefore;
    static float _titleMidAlpha = -1f;

    [MenuItem("Tools/Part7/Runtime Pause Channel Test (while playing)")]
    public static void PauseChannelTest()
    {
        if (!Application.isPlaying) { Debug.LogError("[PART7 CHANNEL] enter play mode first."); return; }
        Begin("PART7 PAUSE CHANNEL TEST", Channel_Setup);
    }

    static void Channel_Setup()
    {
        _pm = UnityEngine.Object.FindObjectOfType<PauseManager>();
        _menuUI = UnityEngine.Object.FindObjectOfType<PauseMenuUI>();
        Check("PauseManager present in scene", _pm != null);
        Check("PauseMenuUI present in scene", _menuUI != null);
        if (_pm == null || _menuUI == null) { Finish(); return; }

        _pm.SetPaused(false);
        _events = 0;
        _pm.PauseChanged += OnPauseChangedEvent;
        _audioBefore = SnapshotAudio();
        Note("audio sources fingerprinted: " + _audioBefore.lines.Count);
        Next(Channel_DoPause);
        WaitFrames(2);
    }

    static void OnPauseChangedEvent(bool paused) { _events++; }

    static void Channel_DoPause()
    {
        _events = 0;
        _pm.SetPaused(true);
        Check("pause raised exactly one PauseChanged event", _events == 1);
        Check("Time.timeScale == 0 while paused", Mathf.Approximately(Time.timeScale, 0f));
        Check("AudioListener.pause == true while paused", AudioListener.pause);

        var after = SnapshotAudio();
        string diff = DiffAudio(_audioBefore, after);
        Check("pausing did not rewire any audio channel (per-source volumes/mixer groups intact)", diff == null);
        if (diff != null) Note(diff);

        Wait(0.12f);
        Next(Channel_MidOpen);
    }

    static void Channel_MidOpen()
    {
        var title = _menuUI.Style != null ? FindDeep(_menuUI.Style.Content, "Title") : null;
        var group = title != null ? title.GetComponent<CanvasGroup>() : null;
        _titleMidAlpha = group != null ? group.alpha : -1f;
        Check("menu title group found", group != null);
        Check("title is mid fade-in while timeScale is 0 (animation channel is unscaled)",
            _titleMidAlpha > 0f && _titleMidAlpha < 1f);
        Note("title alpha at ~0.12s unscaled = " + _titleMidAlpha.ToString("F3") + "  (timeScale=" + Time.timeScale + ")");
        Wait(1.4f);
        Next(Channel_OpenSettled);
    }

    static void Channel_OpenSettled()
    {
        Check("menu finished opening even though timeScale stayed 0", _menuUI.Style != null && _menuUI.Style.IsOpen);
        Check("timeScale still 0 after the open animation", Mathf.Approximately(Time.timeScale, 0f));
        var title = FindDeep(_menuUI.Style.Content, "Title");
        var group = title != null ? title.GetComponent<CanvasGroup>() : null;
        Check("title alpha settled at 1 while paused", group != null && Mathf.Approximately(group.alpha, 1f));

        // the HUD pause button must not sit on top of (or inside) the open text-list menu
        var btn = _menuUI.PauseButtonGroup;
        Check("HUD pause button faded out while the menu is open", btn != null && btn.alpha < 0.02f);
        Check("HUD pause button stops receiving taps while the menu is open", btn != null && !btn.blocksRaycasts);

        _events = 0;
        _pm.SetPaused(false);
        Check("resume raised exactly one PauseChanged event", _events == 1);
        Check("Time.timeScale restored to 1", Mathf.Approximately(Time.timeScale, 1f));
        Check("AudioListener.pause cleared", !AudioListener.pause);
        Wait(0.6f);
        Next(Channel_Resumed);
    }

    static void Channel_Resumed()
    {
        var after = SnapshotAudio();
        string diff = DiffAudio(_audioBefore, after);
        Check("every audio channel is byte-identical after pause+resume", diff == null);
        if (diff != null) Note(diff);
        Check("menu is closed again", _menuUI.Style != null && !_menuUI.Style.IsOpen);
        var btn = _menuUI.PauseButtonGroup;
        Check("HUD pause button is back in the HUD after resume", btn != null && btn.alpha > 0.98f && btn.blocksRaycasts);
        WaitFrames(2);
        Next(Channel_Done);
    }

    static void Channel_Done()
    {
        _pm.PauseChanged -= OnPauseChangedEvent;
        Finish();
    }

    // ================================================================== menu animation test

    static int _rapidLeft;
    static int _rapidCycles;

    [MenuItem("Tools/Part9/Runtime Menu Animation Test (while playing)")]
    public static void RuntimeMenuAnimationTest()
    {
        if (!Application.isPlaying) { Debug.LogError("[PART9 MENU] enter play mode first."); return; }
        Begin("PART9 MENU ANIMATION TEST", Anim_Setup);
    }

    static void Anim_Setup()
    {
        _pm = UnityEngine.Object.FindObjectOfType<PauseManager>();
        _menuUI = UnityEngine.Object.FindObjectOfType<PauseMenuUI>();
        Check("PauseManager present in scene", _pm != null);
        Check("PauseMenuUI present in scene", _menuUI != null);
        Check("menu style built", _menuUI != null && _menuUI.Style != null);
        if (_pm == null || _menuUI == null || _menuUI.Style == null) { Finish(); return; }

        _pm.SetPaused(false);
        _rapidLeft = 20;      // 10 open/close pairs, two frames apart
        _rapidCycles = 0;
        Note("driving 10 fast open/close cycles (2 frames apart)");
        Next(Anim_Rapid);
        WaitFrames(2);
    }

    static void Anim_Rapid()
    {
        if (_rapidLeft > 0)
        {
            _pm.Toggle();
            _rapidLeft--;
            if (_rapidLeft % 2 == 0) _rapidCycles++;
            Next(Anim_Rapid);
            WaitFrames(2);
            return;
        }
        Note("toggled " + _rapidCycles + " cycles, now letting the last transition settle");
        Next(Anim_Settled);
        Wait(2f);
    }

    static void Anim_Settled()
    {
        _rapidLeft = 0;
        Check("pause state is defined after rapid toggling (not paused -> even toggle count)", !_pm.IsPaused);
        Check("timeScale back to 1 after rapid toggling", Mathf.Approximately(Time.timeScale, 1f));
        Check("AudioListener.pause false after rapid toggling", !AudioListener.pause);
        Check("menu reports closed", !_menuUI.Style.IsOpen);
        Check("menu content is inactive when closed", !_menuUI.Style.Content.gameObject.activeSelf);
        CheckNoStuckState("after 10 fast cycles");
        WaitFrames(2);
        Next(Anim_PauseForOpen);
    }

    /// <summary>Every animated value must be either fully closed or fully open - never in between.</summary>
    static void CheckNoStuckState(string label)
    {
        var content = _menuUI.Style.Content;
        float worstAlpha = 0f;
        Vector3 worstScale = Vector3.one;
        bool finite = true;
        var groups = content.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            float a = groups[i].alpha;
            if (float.IsNaN(a) || a < -0.001f || a > 1.001f) finite = false;
            worstAlpha = Mathf.Max(worstAlpha, a);
        }
        var rects = content.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            var p = rects[i].anchoredPosition;
            var s = rects[i].localScale;
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y)) finite = false;
            if (Mathf.Abs(s.x - 1f) > 0.001f || Mathf.Abs(s.y - 1f) > 0.001f || Mathf.Abs(s.z - 1f) > 0.001f)
                worstScale = s;
        }
        Check("no NaN/out-of-range alpha or position " + label, finite);
        Check("no stuck alpha (all groups at 0 when closed) " + label, worstAlpha <= 0.001f);
        Check("no stuck scale (every RectTransform scale is 1) " + label, worstScale == Vector3.one);

        // item rest positions must be exactly where the layout puts them (or at the closed offset)
        string[] names = { "Item_RESUME", "Item_SETTINGS", "Item_CREDITS", "Item_MAIN MENU", "Item_QUIT" };
        bool positionsOk = true;
        for (int i = 0; i < names.Length; i++)
        {
            var t = FindDeep(content, names[i]);
            if (t == null) { Check("item rect found: " + names[i], false); return; }
            var rt = t.GetComponent<RectTransform>();
            float expectX = LastBellMenuStyle.LeftMargin;
            float expectY = LastBellMenuStyle.FirstItemY - i * LastBellMenuStyle.ItemPitch;
            bool atRest = Mathf.Abs(rt.anchoredPosition.x - expectX) < 0.5f;
            bool atClosed = Mathf.Abs(rt.anchoredPosition.x - (expectX - 34f)) < 0.5f;   // itemSlideOffset default
            if (!(atRest || atClosed) || Mathf.Abs(rt.anchoredPosition.y - expectY) > 0.5f)
            {
                positionsOk = false;
                Note(names[i] + " at " + rt.anchoredPosition + ", expected x " + expectX + " (rest) or "
                    + (expectX - 34f) + " (closed), y " + expectY);
                break;
            }
        }
        Check("all five item rects sit at a defined position " + label, positionsOk);
    }

    static void Anim_PauseForOpen()
    {
        _pm.SetPaused(true);
        Check("pause accepted before the open check", _pm.IsPaused);
        Check("timeScale is 0 for the open check", Mathf.Approximately(Time.timeScale, 0f));
        Wait(0.15f);
        Next(Anim_MidOpen);
    }

    static void Anim_MidOpen()
    {
        var title = FindDeep(_menuUI.Style.Content, "Title");
        var group = title != null ? title.GetComponent<CanvasGroup>() : null;
        float a = group != null ? group.alpha : -1f;
        Check("menu is animating open while timeScale == 0", a > 0f && a < 1f);
        Note("title alpha mid-open = " + a.ToString("F3"));
        Wait(1.4f);
        Next(Anim_OpenSettled);
    }

    static void Anim_OpenSettled()
    {
        Check("menu fully open while still paused", _menuUI.Style.IsOpen);
        Check("content active while open", _menuUI.Style.Content.gameObject.activeSelf);

        var content = _menuUI.Style.Content;
        var groups = content.GetComponentsInChildren<CanvasGroup>(true);
        bool allOne = true;
        bool artOk = true;
        for (int i = 0; i < groups.Length; i++)
        {
            // the vignette sits inside its own alpha band, the highlight bars stay at 0 until pressed
            if (groups[i].name == "Highlight" || groups[i].name == "Vignette") continue;
            // the art is deliberately at artAlpha (the pause menu keeps it below 1)
            if (groups[i].name == "Art")
            {
                artOk = Mathf.Approximately(groups[i].alpha, _menuUI.Style.artAlpha);
                continue;
            }
            if (!Mathf.Approximately(groups[i].alpha, 1f)) allOne = false;
        }
        Check("title/subtitle/item groups all settled at alpha 1", allOne);
        Check("art group settled at its configured alpha (" + _menuUI.Style.artAlpha.ToString("F2") + ")", artOk);

        for (int i = 0; i < 5; i++)
        {
            string[] names = { "Item_RESUME", "Item_SETTINGS", "Item_CREDITS", "Item_MAIN MENU", "Item_QUIT" };
            var t = FindDeep(content, names[i]);
            if (t == null) { Check("item rect found: " + names[i], false); continue; }
            var rt = t.GetComponent<RectTransform>();
            float expectX = LastBellMenuStyle.LeftMargin;
            float expectY = LastBellMenuStyle.FirstItemY - i * LastBellMenuStyle.ItemPitch;
            bool ok = Mathf.Abs(rt.anchoredPosition.x - expectX) < 0.5f && Mathf.Abs(rt.anchoredPosition.y - expectY) < 0.5f;
            if (!ok)
            {
                Check("item " + names[i] + " settled at its layout position", false);
                Note("got " + rt.anchoredPosition + " expected (" + expectX + ", " + expectY + ")");
                break;
            }
            if (i == 4) Check("all five items settled exactly at their layout positions", true);
        }

        _pm.SetPaused(false);
        Wait(0.8f);
        Next(Anim_Done);
    }

    static void Anim_Done()
    {
        Check("not paused at the end of the test", !_pm.IsPaused);
        Check("timeScale restored to 1", Mathf.Approximately(Time.timeScale, 1f));
        Check("AudioListener.pause cleared at the end", !AudioListener.pause);
        Check("menu closed at the end", !_menuUI.Style.IsOpen);
        Check("menu content inactive at the end", !_menuUI.Style.Content.gameObject.activeSelf);
        Finish();
    }
}
