using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 5.1 + 5.2 pass over the Floor 1 rooms only (FirstFloorCorridor, Classroom_1_1,
/// Classroom_1_2, Restroom). SecondFloorCorridor and Classroom_2_1 are never touched.
///
/// MENU: Tools/Floor1/Apply Texture Import Settings
/// MENU: Tools/Floor1/Build Lighting
/// MENU: Tools/Floor1/Run Whole Pass
/// </summary>
public static class Floor1Pass
{
    static readonly string[] ScopeRoots = { "FirstFloorCorridor", "Classroom_1_1", "Classroom_1_2", "Restroom" };
    static readonly string[] RoomRoots = { "Classroom_1_1", "Classroom_1_2", "Restroom" };

    // ---------------------------------------------------------------- textures

    [MenuItem("Tools/Floor1/Apply Texture Import Settings")]
    public static void ApplyTextureSettings()
    {
        var log = new StringBuilder();
        var seen = new HashSet<Texture>();

        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r == null || !InScope(r.transform)) continue;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                foreach (var prop in new[] { "_MainTex", "_BaseMap", "_BumpMap", "_NormalMap", "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailMask" })
                {
                    if (!m.HasProperty(prop)) continue;
                    var t = m.GetTexture(prop);
                    if (t != null) seen.Add(t);
                }
            }
        }

        int changed = 0;
        foreach (var t in seen)
        {
            string path = AssetDatabase.GetAssetPath(t);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;

            bool isNormal = imp.textureType == TextureImporterType.NormalMap;
            var tex2d = t as Texture2D;
            int native = tex2d != null ? Mathf.Max(tex2d.width, tex2d.height) : 1024;

            // Hero floor surfaces are 1024 natively; minor props get capped lower.
            int capped = native >= 1024 ? 1024 : (native >= 512 ? 512 : 256);
            // ASTC 4x4 for normals (block artefacts are far more visible on them), 6x6 elsewhere.
            var fmt = isNormal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;

            var ps = imp.GetPlatformTextureSettings("Android");
            string before = "override=" + ps.overridden + " max=" + ps.maxTextureSize + " fmt=" + ps.format;

            if (capped > 2048) capped = 2048;   // never exceed the 2048 hero cap

            ps.overridden = true;
            ps.maxTextureSize = capped;
            ps.format = fmt;
            ps.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(ps);
            imp.SaveAndReimport();
            changed++;

            log.AppendLine("  " + path + " native=" + native + " : " + before
                + "  ->  max=" + capped + " fmt=" + fmt + (isNormal ? " (normal)" : ""));
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR1 5.1 TEXTURES] updated=" + changed + " of " + seen.Count + " floor-1 textures\n" + log);
    }

    // ---------------------------------------------------------------- lighting

    [MenuItem("Tools/Floor1/Build Lighting")]
    public static void BuildLighting()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();

        // Remove any previously generated fixtures so the pass is idempotent.
        // (The player's flashlight lives under the "Player" root and is never touched.)
        int removed = 0;
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t != null && t.name.StartsWith("FL1_"))
            {
                Object.DestroyImmediate(t.gameObject);
                removed++;
            }
        }
        log.AppendLine("  removed " + removed + " previously generated fixture(s)");

        // ---- corridor: dim, flickering, cool/green fluorescent ----
        var corridor = GameObject.Find("FirstFloorCorridor");
        if (corridor == null) { Debug.LogError("[FLOOR1 5.2] FirstFloorCorridor not found."); return; }
        Bounds cb = ObjectBounds(corridor);
        log.AppendLine("  corridor bounds center=" + cb.center + " size=" + cb.size);

        bool alongX = cb.size.x >= cb.size.z;
        for (int i = 0; i < 3; i++)
        {
            float frac = (i + 1) / 4f;
            Vector3 p = alongX
                ? new Vector3(Mathf.Lerp(cb.min.x, cb.max.x, frac), cb.max.y - 0.7f, cb.center.z)
                : new Vector3(cb.center.x, cb.max.y - 0.7f, Mathf.Lerp(cb.min.z, cb.max.z, frac));

            var go = new GameObject("FL1_CorridorLight_" + (i + 1));
            go.transform.SetParent(corridor.transform, true);
            go.transform.position = p;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.78f, 0.92f, 0.80f);   // sickly fluorescent white
            light.intensity = 1.15f;
            light.range = 9f;
            light.shadows = i == 1 ? LightShadows.Soft : LightShadows.None;  // one shadow caster only, for mobile
            light.lightmapBakeType = LightmapBakeType.Realtime;             // flicker cannot be baked
            var flicker = go.AddComponent<FlickerLight>();
            flicker.baseIntensity = 1.15f;
            flicker.flickerAmount = 0.28f;
            flicker.flickerSpeed = 6f;
            flicker.dropoutChance = 0.45f;
            log.AppendLine("  + FL1_CorridorLight_" + (i + 1) + " at " + p + " (flicker, shadows=" + light.shadows + ")");
        }

        // ---- rooms: a distinct lighting signature each, so they do not read as reskins ----
        // New textures are not available for this release, so room identity is carried by light:
        // colour temperature, fixture count, intensity and whether the fixture flickers.
        foreach (var roomName in RoomRoots)
        {
            var room = GameObject.Find(roomName);
            if (room == null) { log.AppendLine("  MISSING room: " + roomName); continue; }
            Bounds rb = ObjectBounds(room);

            Color colour; float intensity; float rangeScale; int fixtures; bool flicker; string mood;
            if (roomName == "Classroom_1_1")
            {
                colour = new Color(1.00f, 0.93f, 0.80f); intensity = 1.55f; rangeScale = 0.80f;
                fixtures = 1; flicker = false;
                mood = "warm + steady single fixture (the calm, 'safe' classroom)";
            }
            else if (roomName == "Classroom_1_2")
            {
                colour = new Color(0.86f, 0.92f, 1.00f); intensity = 1.30f; rangeScale = 0.62f;
                fixtures = 2; flicker = true;
                mood = "cooler + twin fixtures, one failing tube (the 'wrong' classroom)";
            }
            else // Restroom
            {
                colour = new Color(0.70f, 0.82f, 0.95f); intensity = 1.10f; rangeScale = 0.70f;
                fixtures = 1; flicker = false;
                mood = "cold + dim (sterile, damp)";
            }

            bool roomAlongX = rb.size.x >= rb.size.z;
            for (int i = 0; i < fixtures; i++)
            {
                float frac = fixtures == 1 ? 0.5f : (i + 1f) / (fixtures + 1f);
                Vector3 p = roomAlongX
                    ? new Vector3(Mathf.Lerp(rb.min.x, rb.max.x, frac), rb.max.y - 0.7f, rb.center.z)
                    : new Vector3(rb.center.x, rb.max.y - 0.7f, Mathf.Lerp(rb.min.z, rb.max.z, frac));

                var go = new GameObject("FL1_RoomLight_" + roomName + (fixtures > 1 ? "_" + (i + 1) : ""));
                go.transform.SetParent(room.transform, true);
                go.transform.position = p;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = colour;
                light.intensity = intensity;
                light.range = Mathf.Max(9f, Mathf.Max(rb.size.x, rb.size.z) * rangeScale);
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;

                bool thisFlickers = flicker && i == fixtures - 1;   // only the last tube is failing
                if (thisFlickers)
                {
                    var fl = go.AddComponent<FlickerLight>();
                    fl.baseIntensity = intensity;
                    fl.flickerAmount = 0.35f;
                    fl.flickerSpeed = 9f;
                    fl.dropoutChance = 0.30f;
                    fl.dropoutDuration = 0.09f;
                }
                log.AppendLine("  + " + go.name + " colour=" + ColorUtility.ToHtmlStringRGB(colour)
                    + " intensity=" + intensity + " range=" + light.range.ToString("F1")
                    + (thisFlickers ? " FLICKER" : " steady"));
            }
            log.AppendLine("      " + roomName + " -> " + mood);
        }

        // ---- ambient cue host (clips supplied later; idles harmlessly while empty) ----
        var ambientGo = GameObject.Find("FL1_AmbientAudio");
        if (ambientGo == null)
        {
            ambientGo = new GameObject("FL1_AmbientAudio");
            ambientGo.AddComponent<AudioSource>();
            ambientGo.AddComponent<AmbientAudioManager>();
            log.AppendLine("  + FL1_AmbientAudio (AmbientAudioManager, ambientClips EMPTY for you to assign)");
        }
        else
        {
            log.AppendLine("  = FL1_AmbientAudio already present");
        }

        // ---- quality settings tuned for mobile ----
        // Applied to EVERY quality level: the Android per-platform default level differs from the
        // editor's (Android = 2, Standalone = 5 here), so changing only the active level would
        // leave the actual device running whatever it already had.
        int levelCount = QualitySettings.names.Length;
        int originalLevel = QualitySettings.GetQualityLevel();
        var before = new StringBuilder();
        for (int i = 0; i < levelCount; i++)
        {
            QualitySettings.SetQualityLevel(i, true);
            before.AppendLine("    level " + i + " (" + QualitySettings.names[i] + "): shadows=" + QualitySettings.shadows
                + " res=" + QualitySettings.shadowResolution + " dist=" + QualitySettings.shadowDistance
                + " cascades=" + QualitySettings.shadowCascades + " pixelLights=" + QualitySettings.pixelLightCount);

            QualitySettings.shadows = ShadowQuality.All;          // enables soft shadows
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = 30f;                 // was 150 on the top level: far too expensive on mobile
            QualitySettings.shadowCascades = 0;                   // single shadow map: cheapest
            QualitySettings.pixelLightCount = 4;                  // cap per-pixel lights
        }
        QualitySettings.SetQualityLevel(originalLevel, true);
        log.AppendLine("  quality levels BEFORE:\n" + before);
        log.AppendLine("  quality AFTER (applied to all " + levelCount + " levels; active=" + originalLevel + "):"
            + " shadows=" + QualitySettings.shadows
            + " res=" + QualitySettings.shadowResolution
            + " dist=" + QualitySettings.shadowDistance
            + " cascades=" + QualitySettings.shadowCascades
            + " pixelLights=" + QualitySettings.pixelLightCount);

        log.AppendLine("  ambient (global, Built-in): mode=" + RenderSettings.ambientMode
            + " color=" + RenderSettings.ambientLight + " intensity=" + RenderSettings.ambientIntensity);
        log.AppendLine("  NOTE: Built-in RP has no per-room ambient volume without baked lightmaps or a");
        log.AppendLine("        post-processing package, so corridor/room contrast is done with local lights.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR1 5.2 LIGHTING]\n" + log);
    }

    [MenuItem("Tools/Floor1/Runtime Monster Test (while playing)")]
    public static void RuntimeMonsterTest()
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
            Debug.LogError("[FLOOR1 RT] Not in play mode - enter play mode first.");
            return;
        }

        var gm = Object.FindObjectOfType<GameManager>();
        var mon = Object.FindObjectOfType<Monster>();
        check("GameManager present", gm != null);
        check("Monster present", mon != null);
        check("DetectMonster tag lookup resolves (was inert before)",
            GameObject.FindGameObjectWithTag("Monster") != null);
        if (gm == null || mon == null) { Debug.LogError("[FLOOR1 RT]\n" + log); return; }

        Transform view = mon.cube_transform != null ? mon.cube_transform
            : (Camera.main != null ? Camera.main.transform : null);
        check("view transform resolved (cube_transform / camera)", view != null);
        if (view == null) { Debug.LogError("[FLOOR1 RT]\n" + log); return; }

        // Monster.Update is gated behind keysfound > 0, so nothing can be observed until a key exists.
        if (gm.keysfound < 1) gm.keysfound = 1;
        mon.nextTeleport = float.MaxValue;              // silence the normal teleport timer
        float savedThreshold = mon.dangerDeathThreshold;
        mon.dangerDeathThreshold = 1e9f;                // never load GameOver mid-test

        const int iterations = 200;
        var mi = typeof(Monster).GetMethod("TeleportOutOfView",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        check("TeleportOutOfView reachable", mi != null);
        if (mi != null)
        {
            int outOfCone = 0, tooClose = 0, stillVisible = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                mon.transform.position = view.position + view.forward * 3f;   // start in full view
                mon.spawnOrigin = view.position;                               // what Update() sets each frame
                mi.Invoke(mon, null);
                Vector3 p = mon.transform.position;
                Vector3 eye = view.position;
                float angle = Vector3.Angle(view.forward, p - eye);
                if (angle >= mon.viewConeAngle) outOfCone++;
                if (Vector3.Distance(p, eye) < mon.teleportMinDistance) tooClose++;
                if (angle < mon.viewConeAngle)
                {
                    RaycastHit h;
                    if (!Physics.Linecast(eye, p, out h, ~0, QueryTriggerInteraction.Ignore)) stillVisible++;
                }
            }
            sw.Stop();
            log.AppendLine("    " + iterations + " teleports in " + sw.ElapsedMilliseconds + " ms ("
                + (sw.ElapsedMilliseconds / (float)iterations).ToString("F2") + " ms each)");
            log.AppendLine("    outside the " + mon.viewConeAngle + " deg view cone : " + outOfCone + "/" + iterations);
            log.AppendLine("    within " + mon.teleportMinDistance + "m of the player   : " + tooClose);
            log.AppendLine("    still fully visible to the player : " + stillVisible);
            check("all " + iterations + " searches terminated (no infinite loop)", true);
            check("no destination left inside the player's view", stillVisible == 0);
            check("no destination within teleportMinDistance", tooClose == 0);
            check("most destinations are outside the view cone", outOfCone > iterations / 2);
        }

        // Danger-meter timing, derived from the live serialized values.
        float rate = mon.dangerCeiling / Mathf.Max(0.01f, mon.lifeDuration);
        float window = mon.dangerDeathThreshold / rate;
        log.AppendLine("    dangerCeiling=" + mon.dangerCeiling + "  lifeDuration=" + mon.lifeDuration
            + "  deathThreshold=" + mon.dangerDeathThreshold);
        log.AppendLine("    ramp=" + rate.ToString("F2") + "/s  ->  reaction window " + window.ToString("F2") + "s");
        check("death is reachable (threshold < ceiling)", mon.dangerDeathThreshold < mon.dangerCeiling);
        check("reaction window is fair (2s..10s)", window >= 2f && window <= 10f);

        mon.dangerDeathThreshold = savedThreshold;
        log.AppendLine(fail == 0 ? "RESULT: ALL RUNTIME MONSTER CHECKS PASSED" : ("RESULT: " + fail + " CHECK(S) FAILED"));
        Debug.Log("[FLOOR1 RT MONSTER]\n" + log);
    }

    [MenuItem("Tools/Floor1/Wire Audio Hooks")]
    public static void WireAudioHooks()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();

        // Reuse the existing, unused "AudioEffect" AudioSource on the camera for pickup sounds.
        var effectGo = GameObject.Find("Player/Main Camera/AudioEffect");
        if (effectGo == null)
        {
            foreach (var a in Object.FindObjectsOfType<AudioSource>(true))
                if (a.name == "AudioEffect") { effectGo = a.gameObject; break; }
        }
        AudioSource pickupSource = effectGo != null ? effectGo.GetComponent<AudioSource>() : null;
        if (pickupSource != null)
        {
            pickupSource.playOnAwake = false;
            pickupSource.loop = false;
            pickupSource.spatialBlend = 0f;
            EditorUtility.SetDirty(pickupSource);
        }

        var gm = Object.FindObjectOfType<GameManager>();
        if (gm != null)
        {
            if (pickupSource != null) gm.pickup_source = pickupSource;
            EditorUtility.SetDirty(gm);
            log.AppendLine("  GameManager.pickup_source = "
                + (pickupSource != null ? PathOf(pickupSource.transform) : "NOT FOUND")
                + "  (keyPickup_clip / pagePickup_clip left EMPTY for you to assign)");
        }

        var exit = Object.FindObjectOfType<ExitTrigger>();
        if (exit != null)
        {
            var doorSource = exit.GetComponent<AudioSource>();
            if (doorSource == null) doorSource = exit.gameObject.AddComponent<AudioSource>();
            doorSource.playOnAwake = false;
            doorSource.loop = false;
            doorSource.spatialBlend = 0f;
            exit.door_source = doorSource;
            EditorUtility.SetDirty(exit);
            log.AppendLine("  ExitTrigger.door_source = AudioSource added on " + PathOf(exit.transform)
                + "  (door_clip left EMPTY for you to assign)");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR1 5.3 AUDIO HOOKS]\n" + log);
    }

    [MenuItem("Tools/Floor1/Run Whole Pass")]
    public static void RunWholePass()
    {
        ApplyTextureSettings();
        BuildLighting();
        WireAudioHooks();
    }

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        Transform p = t.parent;
        while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- helpers

    static bool InScope(Transform t)
    {
        Transform p = t;
        while (p != null)
        {
            foreach (var s in ScopeRoots) if (p.name == s) return true;
            p = p.parent;
        }
        return false;
    }

    static Bounds ObjectBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 4f);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}
