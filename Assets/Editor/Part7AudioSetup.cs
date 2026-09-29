using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 7.3 foundation: create the Master / Ambient / SFX AudioMixer and route existing sources.
/// There is no public API for creating a mixer asset, so this goes through the internal
/// UnityEditor.Audio.AudioMixerController and reports the real API surface if that changes.
///
/// MENU: Tools/Part7/Setup Audio Mixer
/// MENU: Tools/Part7/Report
/// </summary>
public static class Part7AudioSetup
{
    const string MixerPath = "Assets/Main/Audio/GameMixer.mixer";

    static System.Type ControllerType()
    {
        return System.Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor")
            ?? System.Type.GetType("UnityEditor.Audio.AudioMixerController");
    }

    [MenuItem("Tools/Part7/Setup Audio Mixer")]
    public static void Setup()
    {
        var log = new StringBuilder();
        var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (existing != null)
        {
            log.AppendLine("  mixer already exists: " + MixerPath);
            RouteSources(existing, log);
            Debug.Log("[PART7 MIXER]\n" + log);
            return;
        }

        var t = ControllerType();
        if (t == null)
        {
            log.AppendLine("  !! UnityEditor.Audio.AudioMixerController not found - cannot create a mixer from script.");
            log.AppendLine("  ACTION: create it by hand once - right-click in Assets/Main/Audio > Create > Audio Mixer,");
            log.AppendLine("          name it 'GameMixer', add two child groups 'Ambient' and 'SFX' under Master,");
            log.AppendLine("          then expose MasterVolume / AmbientVolume / SFXVolume. I wire the rest.");
            Debug.LogWarning("[PART7 MIXER]\n" + log);
            return;
        }

        // Report the real surface so a version change is diagnosable rather than mysterious.
        log.AppendLine("## " + t.FullName + " API surface");
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
        {
            if (m.Name.Contains("Create") || m.Name.Contains("Group") || m.Name.Contains("Exposed") || m.Name.Contains("Parameter"))
            {
                var ps = new List<string>();
                foreach (var p in m.GetParameters()) ps.Add(p.ParameterType.Name + " " + p.Name);
                log.AppendLine("  " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", ps.ToArray()) + ")");
            }
        }

        // Attempt creation.
        try
        {
            var create = t.GetMethod("CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (create == null)
            {
                log.AppendLine();
                log.AppendLine("  !! CreateMixerControllerAtPath not present - create GameMixer by hand (see above).");
            }
            else
            {
                create.Invoke(null, new object[] { MixerPath });
                AssetDatabase.SaveAssets();
                var made = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                log.AppendLine();
                log.AppendLine("  created: " + MixerPath + "  -> " + (made != null ? "OK" : "asset did not appear"));
                if (made != null) RouteSources(made, log);
            }
        }
        catch (System.Exception e)
        {
            log.AppendLine();
            log.AppendLine("  !! creation threw: " + e.GetType().Name + ": " + e.Message);
            log.AppendLine("  ACTION: create GameMixer by hand (see above).");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PART7 MIXER]\n" + log);
    }

    /// <summary>Routes the existing sources into the mixer groups by role.</summary>
    static void RouteSources(AudioMixer mixer, StringBuilder log)
    {
        var groups = mixer.FindMatchingGroups("");
        log.AppendLine("  groups found: " + groups.Length);
        foreach (var g in groups) log.AppendLine("    " + g.name);

        AudioMixerGroup ambient = null, sfx = null;
        foreach (var g in groups)
        {
            if (g.name == "Ambient") ambient = g;
            if (g.name == "SFX") sfx = g;
        }
        if (ambient == null || sfx == null)
        {
            log.AppendLine("  !! 'Ambient' and/or 'SFX' group missing - add them under Master in the mixer window.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        int n = 0;
        foreach (var src in Object.FindObjectsOfType<AudioSource>(true))
        {
            string path = src.gameObject.name;
            AudioMixerGroup target = null;
            // The ambient bed and the random-cue manager are ambience; everything else is SFX.
            if (path == "Ambient_SchoolNight_Bed" || path == "Background" || src.name == "AudioEffect"
                || src.GetComponent<AmbientAudioManager>() != null || src.name == "RandomSound")
                target = ambient;
            else
                target = sfx;

            if (src.outputAudioMixerGroup != target)
            {
                src.outputAudioMixerGroup = target;
                EditorUtility.SetDirty(src);
                n++;
                log.AppendLine("  " + src.name + " -> " + target.name);
            }
        }
        log.AppendLine("  routed " + n + " source(s)");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Part7/Add Ambient + SFX Groups")]
    public static void AddGroups()
    {
        var log = new StringBuilder();
        var t = ControllerType();
        if (t == null) { Debug.LogError("[PART7 GROUPS] AudioMixerController type not found."); return; }

        object controller = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(MixerPath))
            if (a != null && a.GetType().Name == "AudioMixerController") controller = a;
        if (controller == null) { Debug.LogError("[PART7 GROUPS] controller not found in " + MixerPath); return; }

        var create = t.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var listAll = t.GetMethod("GetAllAudioGroupsSlow", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (create == null) { Debug.LogError("[PART7 GROUPS] CreateNewGroup not found."); return; }

        System.Func<List<string>> snapshot = () =>
        {
            var names = new List<string>();
            if (listAll == null) return names;
            var en = listAll.Invoke(controller, null) as System.Collections.IEnumerable;
            if (en == null) return names;
            foreach (var g in en)
            {
                var o = g as Object;
                if (o != null) names.Add(o.name);
            }
            return names;
        };

        log.AppendLine("  groups before: " + string.Join(", ", snapshot().ToArray()));

        foreach (var name in new[] { "Ambient", "SFX" })
        {
            if (snapshot().Contains(name)) { log.AppendLine("  = " + name + " already present"); continue; }
            try
            {
                var g = create.Invoke(controller, new object[] { name, false });
                var o = g as Object;
                log.AppendLine("  + " + name + " -> " + (o != null ? o.name : "NULL"));
            }
            catch (System.Exception e)
            {
                log.AppendLine("  !! " + name + " threw: " + (e.InnerException != null ? e.InnerException.Message : e.Message));
            }
        }

        AssetDatabase.SaveAssets();
        log.AppendLine("  groups after : " + string.Join(", ", snapshot().ToArray()));

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer != null)
        {
            log.AppendLine("  FindMatchingGroups(\"\") from the public API:");
            foreach (var g in mixer.FindMatchingGroups("")) log.AppendLine("    " + g.name);
        }
        Debug.Log("[PART7 GROUPS]\n" + log);
    }

    [MenuItem("Tools/Part7/Report")]
    public static void Report()
    {
        var log = new StringBuilder();
        log.AppendLine("  mixer asset      : " + (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null ? MixerPath : "MISSING"));
        log.AppendLine("  stored settings  : sensitivity x" + GameSettings.LookMultiplier.ToString("F2")
            + " (" + GameSettings.LookSensitivityDegPerPixel.ToString("F3") + " deg/px)"
            + "  master=" + GameSettings.MasterVolume.ToString("F2")
            + " ambient=" + GameSettings.AmbientVolume.ToString("F2")
            + " sfx=" + GameSettings.SfxVolume.ToString("F2")
            + "  tier=" + GameSettings.GraphicsTier);
        log.AppendLine("  applied quality  : " + GameSettings.DescribeApplied());
        int routed = 0, unrouted = 0;
        foreach (var s in Object.FindObjectsOfType<AudioSource>(true))
        {
            if (s.outputAudioMixerGroup != null) routed++; else unrouted++;
        }
        log.AppendLine("  scene sources    : routed=" + routed + " unrouted=" + unrouted);
        Debug.Log("[PART7 REPORT]\n" + log);
    }
}
