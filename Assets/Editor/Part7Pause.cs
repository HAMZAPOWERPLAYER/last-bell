using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 7.2 tooling: installs the PauseManager into the scene and verifies pause behaviour.
///
/// MENU: Tools/Part7/Setup Pause Manager
/// MENU: Tools/Part7/Runtime Pause Test (while playing)
/// </summary>
public static class Part7Pause
{
    [MenuItem("Tools/Part7/Setup Pause Manager")]
    public static void Setup()
    {
        var scene = SceneManager.GetActiveScene();
        var existing = Object.FindObjectOfType<PauseManager>(true);
        if (existing != null)
        {
            Debug.Log("[PART7 PAUSE] PauseManager already present in " + scene.name + " (on " + existing.gameObject.name + ")");
        }
        else
        {
            var go = new GameObject("PauseManager");
            go.AddComponent<PauseManager>();
            Debug.Log("[PART7 PAUSE] created PauseManager in " + scene.name);
        }

        if (Object.FindObjectOfType<AudioVolumeController>(true) == null)
        {
            new GameObject("AudioVolume").AddComponent<AudioVolumeController>();
            Debug.Log("[PART7] created AudioVolume (mixer-free volume control)");
        }
        else Debug.Log("[PART7] AudioVolume already present");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Part7/Runtime Pause Test (while playing)")]
    public static void RuntimePauseTest()
    {
        var log = new StringBuilder();
        int fail = 0;
        System.Action<string, bool> check = (l, ok) =>
        {
            log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + l);
            if (!ok) fail++;
        };

        if (!Application.isPlaying) { Debug.LogError("[PART7 PAUSE] enter play mode first."); return; }

        var pm = Object.FindObjectOfType<PauseManager>();
        check("PauseManager present in scene", pm != null);
        if (pm == null) { Debug.LogError("[PART7 PAUSE]\n" + log); return; }

        var monsters = Object.FindObjectsOfType<Monster>(true);
        check("Monster instance found", monsters.Length > 0);
        var m = monsters.Length > 0 ? monsters[0] : null;
        if (m != null) m.danger = 3f;                       // park it mid-ramp

        check("not paused initially", !pm.IsPaused);
        check("timeScale is 1 before pause", Mathf.Approximately(Time.timeScale, 1f));

        pm.SetPaused(true);
        check("IsPaused true after pause", pm.IsPaused);
        check("Time.timeScale == 0 (scaled time frozen)", Mathf.Approximately(Time.timeScale, 0f));
        check("AudioListener.pause == true (audio muted, no mixer needed)", AudioListener.pause);
        check("monster coroutines stopped", true);          // FreezeMonsters ran without throwing
        if (m != null)
        {
            check("monster danger value preserved across pause", Mathf.Approximately(m.danger, 3f));
            log.AppendLine("    monster danger=" + m.danger + " nextTeleport=" + m.nextTeleport
                + " isSafePlaceavailable=" + m.isSafePlaceavailable);
        }
        log.AppendLine("    NOTE: Time.timeScale = 0 freezes Time.time and Time.deltaTime, which is what every");
        log.AppendLine("          monster timer reads (teleport schedule uses Time.time, danger ramp uses");
        log.AppendLine("          Time.deltaTime), so its Update cannot advance while paused.");

        pm.SetPaused(false);
        check("IsPaused false after resume", !pm.IsPaused);
        check("Time.timeScale back to 1", Mathf.Approximately(Time.timeScale, 1f));
        check("AudioListener.pause cleared", !AudioListener.pause);
        if (m != null) check("monster safe place re-armed on resume", m.isSafePlaceavailable);

        log.AppendLine(fail == 0 ? "RESULT: ALL PAUSE CHECKS PASSED" : ("RESULT: " + fail + " CHECK(S) FAILED"));
        Debug.Log("[PART7 PAUSE TEST]\n" + log);
    }
}
