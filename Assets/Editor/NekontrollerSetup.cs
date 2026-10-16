using System.Collections.Generic;
using System.IO;
using System.Text;
using NeKoRoSYS.InputHandling.Mobile;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-shot setup / verification tool for the NeKONTROLLER touch controls in MainLevel.
/// MENU: Tools/NeKONTROLLER/Setup Touch Controls   - builds UI, removes UJoystick, wires references
/// MENU: Tools/NeKONTROLLER/Verify Touch Controls  - read-only assertions
/// </summary>
public static class NekontrollerSetup
{
    const string GeneratedFolder = "Assets/Main/UI/Generated";
    const string StickName = "ControlStick_Movement";
    const string PadName = "ControlPad_Look";

    // ---------------------------------------------------------------- setup

    [MenuItem("Tools/NeKONTROLLER/Setup Touch Controls")]
    public static void Setup()
    {
        var log = new StringBuilder();
        try
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "MainLevel")
            {
                Debug.LogError("[NeKONTROLLER] Active scene is '" + scene.name + "', expected MainLevel. Aborting.");
                return;
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null) { Debug.LogError("[NeKONTROLLER] No Canvas in scene. Aborting."); return; }
            log.AppendLine("canvas=" + canvas.name + " (scaler ref 800x600, matchWidth)");

            // ---- sprites -------------------------------------------------
            // Prefer the sprites shipped with NeKONTROLLER (extracted from the upstream repo);
            // fall back to generated ones if they are missing.
            var ring = LoadNekoSprite("All Axis Backgrounds/AllAxis_Outline.png");
            var disc = LoadNekoSprite("Handles/Handle_Plain.png");
            bool usedUpstream = ring != null && disc != null;
            if (ring == null) ring = CreateSprite("StickBase.png", BuildRing(256, 124f, 92f, 0.55f));
            if (disc == null) disc = CreateSprite("StickHandle.png", BuildDisc(128, 58f, 0.85f));
            log.AppendLine("sprites: ring=" + (ring != null ? ring.name : "NULL") + " disc=" + (disc != null ? disc.name : "NULL")
                + " source=" + (usedUpstream ? "NeKONTROLLER upstream art" : "generated fallback"));

            // ---- 1. remove legacy UJoystick ------------------------------
            foreach (var js in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (js == null || js.GetType().Name != "bl_Joystick") continue;
                log.AppendLine("removed legacy joystick: " + js.gameObject.name);
                Object.DestroyImmediate(js.gameObject);
            }

            // ---- 2. build the movement stick -----------------------------
            var oldStick = canvas.transform.Find(StickName);
            if (oldStick != null) Object.DestroyImmediate(oldStick.gameObject);
            var oldPad = canvas.transform.Find(PadName);
            if (oldPad != null) Object.DestroyImmediate(oldPad.gameObject);

            var stickGO = new GameObject(StickName, typeof(RectTransform), typeof(Image));
            stickGO.layer = 5;
            var stickRT = (RectTransform)stickGO.transform;
            stickRT.SetParent(canvas.transform, false);
            stickRT.anchorMin = Vector2.zero;
            stickRT.anchorMax = Vector2.zero;
            stickRT.pivot = new Vector2(0.5f, 0.5f);
            stickRT.anchoredPosition = new Vector2(98f, 98f);
            stickRT.sizeDelta = new Vector2(130f, 130f);
            var stickImg = stickGO.GetComponent<Image>();
            stickImg.color = new Color(1f, 1f, 1f, 0f);
            stickImg.raycastTarget = false;   // Fixed mode: the visible base is the grab target
            var stick = stickGO.AddComponent<ControlStick>();

            var boundsGO = new GameObject("StickBounds", typeof(RectTransform), typeof(Image));
            boundsGO.layer = 5;
            var boundsRT = (RectTransform)boundsGO.transform;
            boundsRT.SetParent(stickRT, false);
            boundsRT.anchorMin = boundsRT.anchorMax = new Vector2(0.5f, 0.5f);
            boundsRT.pivot = new Vector2(0.5f, 0.5f);
            boundsRT.anchoredPosition = Vector2.zero;
            boundsRT.sizeDelta = new Vector2(130f, 130f);
            var boundsImg = boundsGO.GetComponent<Image>();
            boundsImg.sprite = ring;
            boundsImg.color = Color.white;
            boundsImg.raycastTarget = true;   // this is what the player grabs

            var handleGO = new GameObject("StickHandle", typeof(RectTransform), typeof(Image));
            handleGO.layer = 5;
            var handleRT = (RectTransform)handleGO.transform;
            handleRT.SetParent(boundsRT, false);
            handleRT.anchorMin = handleRT.anchorMax = new Vector2(0.5f, 0.5f);
            handleRT.pivot = new Vector2(0.5f, 0.5f);
            handleRT.anchoredPosition = Vector2.zero;
            handleRT.sizeDelta = new Vector2(52f, 52f);
            var handleImg = handleGO.GetComponent<Image>();
            handleImg.sprite = disc;
            handleImg.color = Color.white;
            handleImg.raycastTarget = false;

            var stickSO = new SerializedObject(stick);
            // FindProperty returns null for a renamed/removed field; dereferencing it blindly threw
            // a NullReferenceException and aborted the whole setup, so guard every lookup.
            var inputAreaProp = Field(stickSO, "inputArea", stick);
            if (inputAreaProp != null) inputAreaProp.objectReferenceValue = stickRT;
            var stickBoundsProp = Field(stickSO, "stickBounds", stick);
            if (stickBoundsProp != null) stickBoundsProp.objectReferenceValue = boundsRT;
            var stickHandleProp = Field(stickSO, "stickHandle", stick);
            if (stickHandleProp != null) stickHandleProp.objectReferenceValue = handleRT;
            var stickModeProp = Field(stickSO, "stickMode", stick);
            if (stickModeProp != null) stickModeProp.enumValueIndex = 0;            // Fixed
            var stickAxisProp = Field(stickSO, "stickAxis", stick);
            if (stickAxisProp != null) stickAxisProp.enumValueIndex = 2;            // Both
            var stickRangeProp = Field(stickSO, "stickRange", stick);
            if (stickRangeProp != null) stickRangeProp.floatValue = 0.55f;          // keeps the handle inside the ring
            var fullInputProp = Field(stickSO, "fullInputThreshold", stick);
            if (fullInputProp != null) fullInputProp.floatValue = 0.75f;
            var moveThresholdProp = Field(stickSO, "moveThreshold", stick);
            if (moveThresholdProp != null) moveThresholdProp.floatValue = 0.17f;
            stickSO.ApplyModifiedPropertiesWithoutUndo();

            // Adaptive sizing: keeps the stick the same physical size on any aspect ratio.
            var layout = stickGO.GetComponent<MobileControlsLayout>();
            if (layout == null) layout = stickGO.AddComponent<MobileControlsLayout>();
            layout.stickBounds = boundsRT;
            layout.stickHandle = handleRT;
            EditorUtility.SetDirty(layout);
            log.AppendLine("built " + StickName + " (Fixed, adaptive size via MobileControlsLayout)");

            // ---- 3. build the look pad ----------------------------------
            var padGO = new GameObject(PadName, typeof(RectTransform));
            padGO.layer = 5;
            var padRT = (RectTransform)padGO.transform;
            padRT.SetParent(canvas.transform, false);
            padRT.anchorMin = new Vector2(0.5f, 0f);
            padRT.anchorMax = new Vector2(1f, 1f);
            padRT.offsetMin = Vector2.zero;
            padRT.offsetMax = Vector2.zero;
            var pad = padGO.AddComponent<ControlPad>();   // no Graphic: must not block uGUI buttons
            log.AppendLine("built " + PadName + " (right half, EnhancedTouch, no Graphic)");

            // ---- 4. convert the two action buttons ----------------------
            var flashlight = Object.FindObjectOfType<Flashlight>();
            var gm = Object.FindObjectOfType<GameManager>();
            if (flashlight == null) { Debug.LogError("[NeKONTROLLER] Flashlight component not found."); return; }
            if (gm == null) { Debug.LogError("[NeKONTROLLER] GameManager component not found."); return; }

            var flashGO = canvas.transform.Find("Button_Flash");
            if (flashGO == null) { Debug.LogError("[NeKONTROLLER] Canvas/Button_Flash not found."); return; }
            var flashBtn = ConvertToControlButton(flashGO.gameObject);
            flashlight.flashlightButton = flashBtn;
            EditorUtility.SetDirty(flashlight);
            log.AppendLine("Button_Flash -> ControlButton -> Flashlight.flashlightButton");

            // ---- 4b. flashlight click sound -----------------------------
            var click = CreateClickClip();
            flashlight.flash_clip = click;
            if (flashlight.flash_source != null)
            {
                flashlight.flash_source.clip = click;
                flashlight.flash_source.playOnAwake = false;
                flashlight.flash_source.spatialBlend = 0f;   // 2D: always audible
                EditorUtility.SetDirty(flashlight.flash_source);
            }
            flashlight.batteryDrainEnabled = false;
            EditorUtility.SetDirty(flashlight);
            log.AppendLine("flashlight click = " + (click != null ? click.name : "NULL")
                + " | batteryDrainEnabled=false (infinite battery, drain is opt-in)");

            if (gm.pickUp_UI == null) { Debug.LogError("[NeKONTROLLER] GameManager.pickUp_UI is not assigned."); return; }
            var interactBtn = ConvertToControlButton(gm.pickUp_UI);
            gm.interactButton = interactBtn;
            EditorUtility.SetDirty(gm);
            log.AppendLine("pickUp_UI (" + gm.pickUp_UI.name + ") -> ControlButton -> GameManager.interactButton");

            // ---- 5. wire Controller -------------------------------------
            var ctrl = Object.FindObjectOfType<Controller>();
            if (ctrl == null) { Debug.LogError("[NeKONTROLLER] Controller component not found."); return; }
            ctrl.movementStick = stick;
            ctrl.cameraPad = pad;
            ctrl.cameraSensitivity = 0.15f;    // degrees per pixel of drag (was 0.35, tuned for degrees-per-frame)
            ctrl.movementSensitivity = 1f;     // stick already yields analog 0..1 (was 2, which saturated to full speed)
            EditorUtility.SetDirty(ctrl);
            log.AppendLine("Controller: movementStick + cameraPad wired, sensitivity 0.15 deg/px, moveMult 1");

            // ---- 6. repair spawnPositions -------------------------------
            var spawns = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("SpawnJumpScare")) spawns.Add(root.transform);
            }
            spawns.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            gm.spawnPositions = spawns.ToArray();
            EditorUtility.SetDirty(gm);
            log.AppendLine("GameManager.spawnPositions = " + spawns.Count + " (" + string.Join(", ", spawns.ConvertAll(s => s.name).ToArray()) + ")");

            // ---- 7. Active Input Handling -> Both -----------------------
            log.AppendLine("inputHandling: " + SetActiveInputHandlingBoth());

            // ---- 8. save ------------------------------------------------
            // NOTE: deliberately no AssetDatabase.Refresh() here - it can trigger script
            // compilation + a domain reload mid-method, which aborts this setup.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            log.AppendLine("scene saved");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[NeKONTROLLER] Setup threw: " + e);
            return;
        }
        Debug.Log("[NeKONTROLLER SETUP REPORT]\n" + log);
        Verify();
    }

    // ----------------------------------------------------------- verify

    [MenuItem("Tools/NeKONTROLLER/Verify Touch Controls")]
    public static void Verify()
    {
        var log = new StringBuilder();
        int fail = 0;
        System.Action<string, bool> check = (label, ok) =>
        {
            log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + label);
            if (!ok) fail++;
        };

        var canvas = Object.FindObjectOfType<Canvas>();
        check("Canvas present", canvas != null);
        if (canvas == null) { Debug.LogError("[NeKONTROLLER VERIFY]\n" + log); return; }

        var stick = Object.FindObjectOfType<ControlStick>(true);
        var pad = Object.FindObjectOfType<ControlPad>(true);
        var ctrl = Object.FindObjectOfType<Controller>();
        var flashlight = Object.FindObjectOfType<Flashlight>();
        var gm = Object.FindObjectOfType<GameManager>();

        check("ControlStick_Movement exists", stick != null);
        check("ControlPad_Look exists", pad != null);
        check("Controller.movementStick wired", ctrl != null && ctrl.movementStick != null);
        check("Controller.cameraPad wired", ctrl != null && ctrl.cameraPad != null);
        check("Flashlight.flashlightButton wired", flashlight != null && flashlight.flashlightButton != null);
        check("GameManager.interactButton wired", gm != null && gm.interactButton != null);

        if (stick != null)
        {
            var so = new SerializedObject(stick);
            var inputAreaCheck = Field(so, "inputArea", stick);
            var stickBoundsCheck = Field(so, "stickBounds", stick);
            var stickHandleCheck = Field(so, "stickHandle", stick);
            check("ControlStick.inputArea set", inputAreaCheck != null && inputAreaCheck.objectReferenceValue != null);
            check("ControlStick.stickBounds set", stickBoundsCheck != null && stickBoundsCheck.objectReferenceValue != null);
            check("ControlStick.stickHandle set", stickHandleCheck != null && stickHandleCheck.objectReferenceValue != null);
            check("ControlStick under a Canvas", stick.GetComponentInParent<Canvas>() != null);
            var b = stickBoundsCheck != null ? stickBoundsCheck.objectReferenceValue as RectTransform : null;
            check("StickBounds has raycast image", b != null && b.GetComponent<Image>() != null && b.GetComponent<Image>().raycastTarget);
        }
        if (pad != null)
        {
            var prt = pad.transform as RectTransform;
            check("ControlPad has a RectTransform", prt != null);
            check("ControlPad covers right half", prt != null && Mathf.Approximately(prt.anchorMin.x, 0.5f) && Mathf.Approximately(prt.anchorMax.x, 1f));
            check("ControlPad has no Graphic (won't block buttons)", pad.GetComponent<Graphic>() == null);
        }

        int joysticks = 0;
        foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mb != null && mb.GetType().Name == "bl_Joystick") joysticks++;
        }
        check("no bl_Joystick left in scene", joysticks == 0);

        if (gm != null)
        {
            int n = 0;
            if (gm.spawnPositions != null)
                foreach (var t in gm.spawnPositions) if (t != null) n++;
            check("spawnPositions populated (>=1 valid)", n >= 1);
        }

        check("EnhancedTouchInitializer present", TypeExists("NeKoRoSYS.InputHandling.EnhancedTouchInitializer"));
        check("Active Input Handling allows new Input System", NewInputSystemEnabled());

        log.AppendLine(fail == 0 ? "RESULT: ALL CHECKS PASSED" : ("RESULT: " + fail + " CHECK(S) FAILED"));
        Debug.Log("[NeKONTROLLER VERIFY]\n" + log);
    }

    [MenuItem("Tools/NeKONTROLLER/Runtime Smoke Test (while playing)")]
    public static void RuntimeSmokeTest()
    {
        var log = new StringBuilder();
        int fail = 0;
        System.Action<string, bool> check = (l, ok) =>
        {
            log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + l);
            if (!ok) fail++;
        };

        if (!Application.isPlaying)
        {
            Debug.LogError("[NeKONTROLLER RT] Not in play mode - enter play mode, then run this again.");
            return;
        }

        var ctrl = Object.FindObjectOfType<Controller>();
        var stick = Object.FindObjectOfType<ControlStick>(true);
        var pad = Object.FindObjectOfType<ControlPad>(true);
        var flashlight = Object.FindObjectOfType<Flashlight>();
        var gm = Object.FindObjectOfType<GameManager>();

        check("Controller alive, movementStick resolved", ctrl != null && ctrl.movementStick != null);
        check("Controller.cameraPad resolved", ctrl != null && ctrl.cameraPad != null);
        check("Flashlight.flashlightButton resolved", flashlight != null && flashlight.flashlightButton != null);
        check("GameManager.interactButton resolved", gm != null && gm.interactButton != null);

        // ---- ControlStick analog output ------------------------------------
        if (stick != null)
        {
            var so = new SerializedObject(stick);
            var boundsProp = Field(so, "stickBounds", stick);
            var bounds = boundsProp != null ? boundsProp.objectReferenceValue as RectTransform : null;
            check("stickBounds resolved at runtime", bounds != null);
            if (bounds == null)
            {
                // Without the serialized rect there is nothing meaningful to probe; skip the stick
                // probe (never dereference a null property) and keep the rest of the test running.
                Debug.LogError("[NeKONTROLLER RT] ControlStick.stickBounds is missing - analog probe skipped.");
            }
            else
            {
                var ped = new PointerEventData(EventSystem.current);
                ped.pointerId = 12345;
                Vector2 origin = RectTransformUtility.WorldToScreenPoint(null, bounds.position);
                var canvas = stick.GetComponentInParent<Canvas>();
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                // ControlStick divides the screen-space drag by (stickBounds.sizeDelta / 2 * canvas.scaleFactor),
                // so the probe offset must be expressed in that same screen-pixel space.
                float radiusUnits = bounds.sizeDelta.x * 0.5f;
                float radiusPixels = radiusUnits * scale;
                log.AppendLine("    probe: sizeDelta=" + bounds.sizeDelta + " canvasScale=" + scale.ToString("F3")
                    + " renderMode=" + (canvas != null ? canvas.renderMode.ToString() : "n/a")
                    + " canvasRect=" + (canvas != null ? ((RectTransform)canvas.transform).rect.size.ToString() : "n/a"));
                log.AppendLine("    probe: full-deflection radius = " + radiusUnits.ToString("F1") + " units = "
                    + radiusPixels.ToString("F1") + " screen px");
                float diaPx = bounds.sizeDelta.x * scale;
                log.AppendLine("    on-screen stick diameter = " + diaPx.ToString("F0") + " px = "
                    + (diaPx / Screen.width * 100f).ToString("F1") + "% of screen width, "
                    + (diaPx / Screen.height * 100f).ToString("F1") + "% of screen height"
                    + "   (screen " + Screen.width + "x" + Screen.height + ")");

                ped.position = origin;
                stick.OnPointerDown(ped);
                log.AppendLine("    input @ centre          = " + stick.input.magnitude.ToString("F3"));

                ped.position = origin + new Vector2(radiusPixels * 0.50f, 0f);
                stick.OnDrag(ped);
                float half = stick.input.magnitude;
                log.AppendLine("    input @ 0.50 x radius   = " + half.ToString("F3"));
                check("half deflection is analog (~0.5, not the old 0.45 step)", half > 0.35f && half < 0.65f);

                ped.position = origin + new Vector2(radiusPixels, 0f);
                stick.OnDrag(ped);
                float full = stick.input.magnitude;
                log.AppendLine("    input @ 1.00 x radius   = " + full.ToString("F3"));
                check("full deflection reaches ~1.0 (not the old 0.90 step)", full > 0.90f && full <= 1.0001f);

                FullInputProbe(stick, log);

                stick.OnPointerUp(ped);
                check("release zeroes the stick (no drift)", stick.input == Vector2.zero);
            }
        }

        // ---- look pad / touch backend --------------------------------------
        if (pad != null)
        {
            var prt = pad.transform as RectTransform;
            log.AppendLine("    ControlPad rect=" + (prt != null ? prt.rect.size.ToString() : "n/a")
                + "  EnhancedTouch.enabled=" + UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled
                + "  activeTouches=" + UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count);

            var devices = UnityEngine.InputSystem.InputSystem.devices;
            var names = new StringBuilder();
            for (int i = 0; i < devices.Count; i++)
            {
                if (i > 0) names.Append(", ");
                names.Append(devices[i].displayName);
            }
            log.AppendLine("    InputSystem.devices.Count=" + devices.Count + " [" + names + "]"
                + "  Touchscreen.current=" + (UnityEngine.InputSystem.Touchscreen.current != null));
            check("new Input System has live devices (restart applied)", devices.Count > 0);
        }

        // ---- flashlight toggle + click -------------------------------------
        if (flashlight != null)
        {
            check("flashlight has a click clip", flashlight.flash_clip != null);
            check("flashlight audio source resolved", flashlight.flash_source != null);

            float before = flashlight.battery;
            bool wasOn = flashlight.isFlashlightOn;
            flashlight.on_Off();
            check("on_Off() toggles state", flashlight.isFlashlightOn != wasOn);
            bool playing = flashlight.flash_source != null && flashlight.flash_source.clip != null;
            log.AppendLine("    after on_Off: isOn=" + flashlight.isFlashlightOn
                + "  clip=" + (playing ? flashlight.flash_source.clip.name : "null"));
            flashlight.on_Off();
            check("on_Off() restores state", flashlight.isFlashlightOn == wasOn);

            // ---- PART 4: drain + Addbattery maths (exercised with drain forced on) ----
            bool savedDrain = flashlight.batteryDrainEnabled;
            flashlight.batteryDrainEnabled = true;
            flashlight.starttime = Time.time;
            flashlight.timeOfBatteryLeft = 0f;
            flashlight.isFlashlightOn = false;                  // simulate a fully drained light
            flashlight.Addbattery(25f);
            float granted = flashlight.timeOfBatteryLeft;
            log.AppendLine("    Addbattery(25) on a dead battery -> timeOfBatteryLeft=" + granted.ToString("F2")
                + "s (battery=" + before.ToString("F0") + "s)");
            check("Addbattery(25) grants 25s, not 125s", Mathf.Abs(granted - 25f) < 0.75f);
            check("Addbattery re-ignites a drained flashlight", flashlight.isFlashlightOn);
            flashlight.batteryDrainEnabled = savedDrain;
            flashlight.starttime = Time.time;
            flashlight.timeOfBatteryLeft = before;
            flashlight.isFlashlightOn = true;
        }

        log.AppendLine(fail == 0 ? "RESULT: ALL RUNTIME CHECKS PASSED" : ("RESULT: " + fail + " CHECK(S) FAILED"));
        Debug.Log("[NeKONTROLLER RUNTIME]\n" + log);

        CaptureGameView();
    }

    [MenuItem("Tools/NeKONTROLLER/Capture Game View Screenshot")]
    public static void CaptureGameView()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[NeKONTROLLER] Screenshot needs play mode.");
            return;
        }
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../nekontroller_ui.png"));
        ScreenCapture.CaptureScreenshot(path, 1);
        Debug.Log("[NeKONTROLLER] screenshot requested -> " + path);
    }

    static void FullInputProbe(ControlStick stick, StringBuilder log)
    {
        var so = new SerializedObject(stick);
        var threshold = so.FindProperty("fullInputThreshold");
        log.AppendLine("    fullInputThreshold=" + (threshold != null ? threshold.floatValue.ToString("F2") : "n/a")
            + "  FullInput(sprint)=" + stick.FullInput);
    }

    /// <summary>
    /// SerializedObject.FindProperty with a guard: a renamed or removed field returns null, so name
    /// the missing field in the log and let the caller skip it instead of throwing a NullReference.
    /// </summary>
    static SerializedProperty Field(SerializedObject so, string name, UnityEngine.Object owner)
    {
        var p = so.FindProperty(name);
        if (p == null)
            Debug.LogError("[NeKONTROLLER] " + owner.GetType().Name + "." + name
                + " serialized field not found (renamed or removed?) - skipping it.");
        return p;
    }

    // ---------------------------------------------------------- helpers

    /// <summary>
    /// The project ships no switch/click SFX, so synthesise one: a broadband transient
    /// (the plastic "click") plus a short metallic tick and a low body thump.
    /// Replace with a recorded clip whenever a real one is available.
    /// </summary>
    [MenuItem("Tools/NeKONTROLLER/Regenerate Flashlight Click SFX")]
    public static void RegenerateClick()
    {
        var clip = CreateClickClip();
        Debug.Log("[NeKONTROLLER] click clip regenerated: " + (clip != null ? clip.name : "NULL"));
    }

    static AudioClip CreateClickClip()
    {
        const string audioFolder = "Assets/Main/Audio";
        if (!AssetDatabase.IsValidFolder(audioFolder)) AssetDatabase.CreateFolder("Assets/Main", "Audio");
        string path = audioFolder + "/flashlight_click.wav";
        WriteWav(path, BuildClickSamples(44100), 44100);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer != null)
        {
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;   // keep the transient crisp
            settings.quality = 1f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static float[] BuildClickSamples(int rate)
    {
        int n = Mathf.RoundToInt(rate * 0.09f);
        var s = new float[n];
        var rnd = new System.Random(20240521);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float click = noise * Mathf.Exp(-t / 0.0040f) * 0.90f;
            float tick = (Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.50f
                        + Mathf.Sin(2f * Mathf.PI * 4100f * t) * 0.35f) * Mathf.Exp(-t / 0.018f);
            float body = Mathf.Sin(2f * Mathf.PI * 240f * t) * Mathf.Exp(-t / 0.030f) * 0.50f;
            s[i] = click + tick + body;
        }
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(s[i]));
        if (peak > 0f)
            for (int i = 0; i < n; i++) s[i] = s[i] / peak * 0.75f;
        int fade = Mathf.Min(n, Mathf.RoundToInt(rate * 0.005f));
        for (int i = 0; i < fade; i++) s[n - 1 - i] *= i / (float)fade;
        return s;
    }

    static void WriteWav(string path, float[] samples, int rate)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int dataSize = samples.Length * 2;
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);                                  // PCM
            bw.Write((short)1);                                  // mono
            bw.Write(rate);
            bw.Write(rate * 2);                                  // byte rate
            bw.Write((short)2);                                  // block align
            bw.Write((short)16);                                 // bits per sample
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
            for (int i = 0; i < samples.Length; i++)
                bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767));
        }
    }

    static bool TypeExists(string fullName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName, false);
            if (t != null) return true;
        }
        return false;
    }

    [MenuItem("Tools/NeKONTROLLER/Diagnose Input Handling")]
    public static void Diagnose()
    {
        var sb = new StringBuilder();
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        sb.AppendLine("assets.Length=" + (assets == null ? -1 : assets.Length));
        if (assets != null && assets.Length > 0)
        {
            sb.AppendLine("assetType=" + assets[0].GetType().FullName);
            var so = new SerializedObject(assets[0]);
            var it = so.GetIterator();
            int n = 0;
            while (it.NextVisible(true))
            {
                n++;
                if (it.propertyPath.ToLowerInvariant().Contains("input"))
                    sb.AppendLine("PROP: " + it.propertyPath + " type=" + it.propertyType + " val=" + it.ToString());
            }
            sb.AppendLine("totalVisibleProps=" + n);
        }
        Debug.Log("[NeKONTROLLER DIAG]\n" + sb);
    }

    static ControlButton ConvertToControlButton(GameObject go)
    {
        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            // Remove the uGUI Button so its persistent onClick cannot double-fire with
            // ControlButton.OnButtonAction (both would toggle the flashlight twice).
            Object.DestroyImmediate(btn);
        }
        var cb = go.GetComponent<ControlButton>();
        if (cb == null) cb = go.AddComponent<ControlButton>();

        var so = new SerializedObject(cb);
        var iconProp = so.FindProperty("icon");
        if (iconProp != null && img != null) iconProp.objectReferenceValue = img;
        var released = so.FindProperty("releasedColor");
        var pressed = so.FindProperty("pressedColor");
        Color cur = img != null ? img.color : Color.white;
        if (released != null) released.colorValue = cur;
        if (pressed != null) pressed.colorValue = new Color(cur.r * 0.55f, cur.g * 0.55f, cur.b * 0.55f, cur.a);
        var animateColor = so.FindProperty("animateColor");
        if (animateColor != null) animateColor.boolValue = true;
        var animateSprite = so.FindProperty("animateSprite");
        if (animateSprite != null) animateSprite.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cb);
        return cb;
    }

    static Texture2D BuildRing(int n, float outer, float inner, float alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01((outer - d) / 1.5f) * Mathf.Clamp01((d - inner) / 1.5f) * alpha;
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Texture2D BuildDisc(int n, float radius, float alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01((radius - d) / 1.5f) * alpha;
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Sprite LoadNekoSprite(string relativePath)
    {
        string path = "Assets/ThirdAssets/NeKONTROLLER/Sprites/" + relativePath;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            // The upstream .meta may import as a plain texture depending on version; force Sprite.
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return sprite;
    }

    static Sprite CreateSprite(string fileName, Texture2D tex)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Main/UI"))
            AssetDatabase.CreateFolder("Assets/Main", "UI");
        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            AssetDatabase.CreateFolder("Assets/Main/UI", "Generated");

        string assetPath = GeneratedFolder + "/" + fileName;
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static int GetActiveInputHandler()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) return -1;
        var so = new SerializedObject(assets[0]);
        var p = so.FindProperty("activeInputHandler");
        return p == null ? -1 : p.intValue;
    }

    /// <summary>activeInputHandler: 0 = Input Manager (Old), 1 = Input System Package (New), 2 = Both.</summary>
    static bool NewInputSystemEnabled()
    {
        int v = GetActiveInputHandler();
        return v == 1 || v == 2;
    }

    static string SetActiveInputHandlingBoth()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) return "FAILED: ProjectSettings.asset not loadable";
        var so = new SerializedObject(assets[0]);
        var p = so.FindProperty("activeInputHandler");
        if (p == null) return "FAILED: activeInputHandler property not found";
        int before = p.intValue;
        // Both (2): enables the new Input System that NeKONTROLLER's ControlPad/EnhancedTouch needs,
        // while keeping the old one so the scene's existing StandaloneInputModule keeps driving uGUI.
        p.intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return "activeInputHandler " + before + " -> " + GetActiveInputHandler()
             + (GetActiveInputHandler() == 2 ? " (Both; requires editor restart)" : " UNEXPECTED");
    }
}
