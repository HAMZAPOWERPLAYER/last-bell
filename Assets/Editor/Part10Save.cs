using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 10 tooling for the 2.0 SAVE &amp; CONTINUE feature (RunSave + GameManager checkpoints).
///
/// MENU: Tools/Part10/Verify Save Round-Trip (edit mode)
/// MENU: Tools/Part10/Runtime Save Continue Test (while playing)
///
/// The edit-mode check exercises RunSave storage and the layout codec without touching a real
/// run: it preserves the current Save.* values, runs its assertions, then restores them.
///
/// The runtime test drives a real play session in MainLevel end to end:
///   1. starts a fresh run and reloads                 (the PLAY path)
///   2. simulates a key pickup, a page pickup and a gate unlock, recording what is expected
///   3. reloads the scene                               (the revive / mid-run reload path)
///   4. checks the checkpoint restored exactly
///   5. clears RunActive + sets ContinueRequest, reloads (the app-restart / CONTINUE path)
///   6. checks again
///   7. reloads once more with no resume flags          (the stale-save guard)
///   8. checks the run started fresh and the save was re-rolled
/// It ends with a PASS/FAIL summary in the console and leaves a clean, saveless state.
/// </summary>
public static class Part10Save
{
    [MenuItem("Tools/Part10/Verify Save Round-Trip (edit mode)")]
    public static void VerifyEditMode()
    {
        var log = new StringBuilder();
        int fail = 0;
        System.Action<string, bool> check = (label, ok) =>
        {
            log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + label);
            if (!ok) fail++;
        };

        // Preserve whatever is in Save.* right now (a developer may have a real run in progress).
        int prevState = PlayerPrefs.GetInt(RunSave.KeyState, -1);
        int prevVersion = PlayerPrefs.GetInt(RunSave.KeyVersion, -1);
        int prevKeys = PlayerPrefs.GetInt(RunSave.KeyKeys, -1);
        int prevPages = PlayerPrefs.GetInt(RunSave.KeyPages, -1);
        int prevGates = PlayerPrefs.GetInt(RunSave.KeyGates, -1);
        string prevLayout = PlayerPrefs.GetString(RunSave.KeyLayout, null);

        RunSave.Clear();
        check("Clear() removes the save", !RunSave.HasSave());

        RunSave.BeginRun("--K0P5K1---P3--");
        check("BeginRun() creates a savable run", RunSave.HasSave());
        check("layout round-trips", RunSave.Layout == "--K0P5K1---P3--");
        check("a fresh run starts at 0 keys", RunSave.Keys == 0);
        check("a fresh run starts with 0 pages", RunSave.PageMask == 0 && RunSave.PagesCollected == 0);
        check("a fresh run starts with 0 unlocked gates", RunSave.GateMask == 0);

        RunSave.RecordKeyPickup(2);
        check("RecordKeyPickup(2) stores 2", RunSave.Keys == 2);

        RunSave.RecordPagePickup(3);
        RunSave.RecordPagePickup(5);
        check("page bitmask records both picks", RunSave.PageMask == ((1 << 3) | (1 << 5)));
        check("PagesCollected counts the set bits", RunSave.PagesCollected == 2);
        RunSave.RecordPagePickup(3);
        check("re-picking the same page is idempotent", RunSave.PagesCollected == 2);

        RunSave.RecordGateUnlocked(1);
        check("gate bit 0 set for requiredKeys 1", RunSave.GateMask == 1);
        RunSave.RecordGateUnlocked(2);
        check("gate bit 1 set for requiredKeys 2", RunSave.GateMask == 3);

        char[] buffer = RunSave.NewLayoutBuffer(3);
        RunSave.WriteSpot(buffer, 1, 'K', 2);
        string encoded = new string(buffer);
        check("NewLayoutBuffer fills '--' and WriteSpot encodes K2", encoded == "--K2--");
        char kind;
        int value;
        bool read = RunSave.TryReadSpot(encoded, 1, out kind, out value);
        check("TryReadSpot decodes kind and value", read && kind == 'K' && value == 2);
        check("TryReadSpot refuses out-of-range spots", !RunSave.TryReadSpot(encoded, 3, out kind, out value));
        check("CoversSpawnCount matches exact sizes",
            RunSave.CoversSpawnCount(encoded, 3) && !RunSave.CoversSpawnCount(encoded, 2));

        RunSave.Clear();
        check("Clear() again removes the save", !RunSave.HasSave());

        // Restore the developer's previous Save.* values.
        if (prevState >= 0) PlayerPrefs.SetInt(RunSave.KeyState, prevState);
        if (prevVersion >= 0) PlayerPrefs.SetInt(RunSave.KeyVersion, prevVersion);
        if (prevKeys >= 0) PlayerPrefs.SetInt(RunSave.KeyKeys, prevKeys);
        if (prevPages >= 0) PlayerPrefs.SetInt(RunSave.KeyPages, prevPages);
        if (prevGates >= 0) PlayerPrefs.SetInt(RunSave.KeyGates, prevGates);
        if (prevLayout != null) PlayerPrefs.SetString(RunSave.KeyLayout, prevLayout);
        PlayerPrefs.Save();

        log.AppendLine(fail == 0 ? "RESULT: ALL SAVE ROUND-TRIP CHECKS PASSED" : ("RESULT: " + fail + " CHECK(S) FAILED"));
        Debug.Log("[PART10 SAVE CHECK]\n" + log);
    }

    // ---------------------------------------------------------------- runtime test

    static int _stage;
    static float _stageDeadline;
    static int _reloadTarget;
    static int _scenesLoaded;
    static string _expectedLayout;
    static int _expectedPageClue = -1;
    static StringBuilder _rtLog;
    static int _rtFail;

    [MenuItem("Tools/Part10/Runtime Save Continue Test (while playing)")]
    public static void RuntimeSaveContinueTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[PART10 SAVE TEST] enter play mode in MainLevel first.");
            return;
        }
        if (SceneManager.GetActiveScene().name != "MainLevel")
        {
            Debug.LogError("[PART10 SAVE TEST] the active scene is " + SceneManager.GetActiveScene().name + ", expected MainLevel.");
            return;
        }

        _rtLog = new StringBuilder();
        _rtFail = 0;
        _stage = 0;
        _stageDeadline = Time.realtimeSinceStartup + 20f;
        EditorApplication.update -= RuntimeTick;
        EditorApplication.update += RuntimeTick;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _rtLog.AppendLine("runtime save/continue test started");
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _scenesLoaded++;
    }

    static void FinishRuntimeTest()
    {
        GameManager.EndRun();      // leave a clean, saveless state behind
        EditorApplication.update -= RuntimeTick;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _rtLog.AppendLine(_rtFail == 0 ? "RESULT: ALL RUNTIME SAVE CONTINUE CHECKS PASSED" : ("RESULT: " + _rtFail + " CHECK(S) FAILED"));
        Debug.Log("[PART10 SAVE TEST]\n" + _rtLog);
        _rtLog = null;
    }

    static GameManager FindManager()
    {
        GameObject managerObject = GameObject.FindGameObjectWithTag("GameManager");
        return managerObject != null ? managerObject.GetComponent<GameManager>() : null;
    }

    static void RuntimeCheck(string label, bool ok)
    {
        _rtLog.AppendLine((ok ? "  PASS  " : "  FAIL  ") + label);
        if (!ok) _rtFail++;
    }

    static void BeginReload(string why, int nextStage)
    {
        _rtLog.AppendLine("    " + why);
        SceneManager.LoadScene("MainLevel");
        _reloadTarget = _scenesLoaded + 1;
        _stage = nextStage;
        _stageDeadline = Time.realtimeSinceStartup + 20f;
    }

    static void RuntimeTick()
    {
        if (Time.realtimeSinceStartup > _stageDeadline)
        {
            GameManager gmAtTimeout = FindManager();
            _rtLog.AppendLine("  FAIL  stage " + _stage + " timed out (keysfound="
                + (gmAtTimeout != null ? gmAtTimeout.keysfound.ToString() : "no manager")
                + ", save=" + RunSave.HasSave() + ", layoutSpots=" + (RunSave.Layout.Length / 2)
                + ", scenesLoaded=" + _scenesLoaded + ")");
            _rtFail++;
            FinishRuntimeTest();
            return;
        }

        switch (_stage)
        {
            case 0:
            {
                GameManager.ContinueRequest = false;
                GameManager.StartNewRun();      // the PLAY path: fresh, save-less run
                BeginReload("stage 0: started a fresh run, reloading MainLevel", 1);
                return;
            }
            case 1:
            {
                if (_scenesLoaded < _reloadTarget) return;
                GameManager gm = FindManager();
                if (gm == null || gm.keysfound != 0) return;
                var spawnedKeys = Object.FindObjectsOfType<KeyTrigger>();
                if (spawnedKeys.Length != 3) return;      // wait until initSpawns has finished

                RuntimeCheck("fresh run: run-start checkpoint written", RunSave.HasSave() && RunSave.Keys == 0);
                RuntimeCheck("fresh run: layout covers every spawn point",
                    RunSave.CoversSpawnCount(RunSave.Layout, Object.FindObjectsOfType<SpawnItem>().Length));

                // --- simulate a key pickup (the engine's stage-0 key, the one real players reach first)
                KeyTrigger stageZeroKey = null;
                for (int i = 0; i < spawnedKeys.Length; i++)
                    if (spawnedKeys[i].progressionStage == 0) stageZeroKey = spawnedKeys[i];
                if (stageZeroKey == null) { RuntimeCheck("stage-0 key present", false); FinishRuntimeTest(); return; }
                gm.currentKey = stageZeroKey.gameObject;
                gm.pick();
                RuntimeCheck("key pickup updates the live count", gm.keysfound == 1);
                RuntimeCheck("key pickup writes the checkpoint", RunSave.Keys == 1);

                // --- simulate a page pickup
                var spawnedPages = Object.FindObjectsOfType<PageTrigger>();
                if (spawnedPages.Length == 0) { RuntimeCheck("pages present", false); FinishRuntimeTest(); return; }
                _expectedPageClue = spawnedPages[0].clueIndex;
                gm.currentPage = spawnedPages[0].gameObject;
                gm.pick();
                RuntimeCheck("page pickup opens the story panel", gm.isGamePaused);
                RuntimeCheck("page pickup writes the checkpoint",
                    RunSave.PagesCollected == 1 && (RunSave.PageMask & (1 << _expectedPageClue)) != 0);
                gm.closePage();

                // --- simulate a gate unlock (the classroom gate needs 1 key, which we now have)
                var gates = Object.FindObjectsOfType<ProgressionGate>();
                ProgressionGate classroomGate = null;
                for (int i = 0; i < gates.Length; i++)
                    if (gates[i].requiredKeys <= 1 && !gates[i].IsOpen) { classroomGate = gates[i]; break; }
                if (classroomGate == null) { RuntimeCheck("classroom gate present", false); FinishRuntimeTest(); return; }
                gm.currentGate = classroomGate;
                gm.pick();
                RuntimeCheck("gate unlock swings the gate open", classroomGate.IsOpen);
                RuntimeCheck("gate unlock writes the checkpoint", (RunSave.GateMask & 1) != 0);

                _expectedLayout = RunSave.Layout;
                BeginReload("stage 1: interactions simulated (key 1/3, page clue " + _expectedPageClue
                    + ", " + classroomGate.gateName + " gate open); reloading via the revive path", 2);
                return;
            }
            case 2:
            {
                if (_scenesLoaded < _reloadTarget) return;
                GameManager gm = FindManager();
                if (gm == null || gm.keysfound != 1) return;
                if (Object.FindObjectsOfType<KeyTrigger>().Length != 2) return;

                RunRestoreChecks("revive reload");
                // App-restart path: no RunActive, only the CONTINUE request flag.
                PlayerPrefs.SetInt(GameManager.KeyRunActive, 0);
                PlayerPrefs.Save();
                GameManager.ContinueRequest = true;
                BeginReload("stage 2: repeating via the app-restart path (RunActive cleared, ContinueRequest set)", 3);
                return;
            }
            case 3:
            {
                if (_scenesLoaded < _reloadTarget) return;
                GameManager gm = FindManager();
                if (gm == null || gm.keysfound != 1) return;
                if (Object.FindObjectsOfType<KeyTrigger>().Length != 2) return;

                RunRestoreChecks("app-restart reload");
                // Stale-save guard: no flags at all - the next MainLevel entry must start fresh.
                PlayerPrefs.SetInt(GameManager.KeyRunActive, 0);
                PlayerPrefs.Save();
                GameManager.ContinueRequest = false;
                BeginReload("stage 3: stale-save guard (no resume flags set)", 4);
                return;
            }
            case 4:
            {
                if (_scenesLoaded < _reloadTarget) return;
                GameManager gm = FindManager();
                if (gm == null || gm.keysfound != 0) return;
                if (Object.FindObjectsOfType<KeyTrigger>().Length != 3) return;

                RuntimeCheck("stale save dropped: fresh run starts at 0 keys", gm.keysfound == 0 && RunSave.Keys == 0);
                RuntimeCheck("stale save dropped: page and gate progress cleared", RunSave.PageMask == 0 && RunSave.GateMask == 0);
                RuntimeCheck("new run-start checkpoint written with a full layout",
                    RunSave.HasSave() && RunSave.CoversSpawnCount(RunSave.Layout, Object.FindObjectsOfType<SpawnItem>().Length));
                var gates = Object.FindObjectsOfType<ProgressionGate>();
                bool anyGateOpen = false;
                for (int i = 0; i < gates.Length; i++) if (gates[i].IsOpen) anyGateOpen = true;
                RuntimeCheck("stale save dropped: every gate is locked again", !anyGateOpen);
                FinishRuntimeTest();
                return;
            }
        }
    }

    static void RunRestoreChecks(string context)
    {
        GameManager gm = FindManager();
        if (gm == null) { RuntimeCheck(context + ": GameManager present", false); return; }

        RuntimeCheck(context + ": key count restored (1/3)", gm.keysfound == 1 && RunSave.Keys == 1);
        RuntimeCheck(context + ": saved spawn layout preserved", RunSave.Layout == _expectedLayout);

        KeyTrigger[] spawnedKeys = Object.FindObjectsOfType<KeyTrigger>();
        bool hasStage0 = false;
        bool hasStage1 = false;
        bool hasStage2 = false;
        for (int i = 0; i < spawnedKeys.Length; i++)
        {
            if (spawnedKeys[i].progressionStage == 0) hasStage0 = true;
            if (spawnedKeys[i].progressionStage == 1) hasStage1 = true;
            if (spawnedKeys[i].progressionStage == 2) hasStage2 = true;
        }
        RuntimeCheck(context + ": uncollected keys re-spawned (stages 1 and 2, not stage 0)",
            spawnedKeys.Length == 2 && hasStage1 && hasStage2 && !hasStage0);

        PageTrigger[] spawnedPages = Object.FindObjectsOfType<PageTrigger>();
        bool collectedPageAbsent = true;
        for (int i = 0; i < spawnedPages.Length; i++)
            if (spawnedPages[i].clueIndex == _expectedPageClue) collectedPageAbsent = false;
        RuntimeCheck(context + ": collected page stays gone, the other 5 re-spawned",
            spawnedPages.Length == 5 && collectedPageAbsent);

        RuntimeCheck(context + ": page counters restored",
            gm.currentPageNumber == 1 && gm.amountOfPagesIngame == 1);
        RuntimeCheck(context + ": classroom wing flagged unlocked", gm.classroomWingUnlocked);
        RuntimeCheck(context + ": escape not started", !gm.escapeStarted);

        ProgressionGate[] gates = Object.FindObjectsOfType<ProgressionGate>();
        bool classroomGateFound = false;
        bool classroomGateOpen = true;
        bool upperGateClosed = true;
        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i].requiredKeys <= 1)
            {
                classroomGateFound = true;
                if (!gates[i].IsOpen) classroomGateOpen = false;
            }
            else if (gates[i].IsOpen) upperGateClosed = false;
        }
        RuntimeCheck(context + ": classroom gate present", classroomGateFound);
        RuntimeCheck(context + ": classroom gate re-opened instantly", classroomGateOpen);
        RuntimeCheck(context + ": upper-corridor gate still locked", upperGateClosed);
    }
}
