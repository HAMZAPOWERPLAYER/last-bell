using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 6 pass over Floor 2 only (SecondFloorCorridor, Classroom_2_1) plus the stairs transition.
/// Reuses the exact presets that were validated on Floor 1 in PART 5 - settings are copied, not
/// re-derived. Floor 1 objects are never touched.
///
/// MENU: Tools/Floor2/Audit
/// MENU: Tools/Floor2/Apply Texture Import Settings   (same presets as Floor 1)
/// MENU: Tools/Floor2/Build Lighting
/// MENU: Tools/Floor2/Build Stairs Cue
/// MENU: Tools/Floor2/Check Content Placement
/// MENU: Tools/Floor2/Run Whole Pass
/// </summary>
public static class Floor2Pass
{
    static readonly string[] ScopeRoots = { "SecondFloorCorridor", "Classroom_2_1" };

    // ---- Floor 1's validated presets, copied verbatim ----
    const int HeroCap = 1024;          // Floor 1 hero surfaces are 1024 natively
    const int MidCap = 512;
    const int SmallCap = 256;
    static readonly TextureImporterFormat NormalFormat = TextureImporterFormat.ASTC_4x4;
    static readonly TextureImporterFormat ColourFormat = TextureImporterFormat.ASTC_6x6;
    static readonly string[] TextureProps =
        { "_MainTex", "_BaseMap", "_BumpMap", "_NormalMap", "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailMask" };

    // ---------------------------------------------------------------- audit

    [MenuItem("Tools/Floor2/Audit")]
    public static void Audit()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        log.AppendLine("scene=" + scene.name);

        log.AppendLine();
        log.AppendLine("## FLOOR 2 SCOPE ROOTS");
        foreach (var n in ScopeRoots)
        {
            var go = GameObject.Find(n);
            if (go == null) { log.AppendLine("  MISSING: " + n); continue; }
            log.AppendLine("  " + n + ": renderers=" + go.GetComponentsInChildren<Renderer>(true).Length
                + " lights=" + go.GetComponentsInChildren<Light>(true).Length
                + " colliders=" + go.GetComponentsInChildren<Collider>(true).Length
                + " children=" + go.transform.childCount);
        }

        log.AppendLine();
        log.AppendLine("## FLOOR 2 MATERIALS (distinct)");
        var mats = new HashSet<Material>();
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r == null || !InScope(r.transform)) continue;
            foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
        }
        foreach (var m in mats)
            log.AppendLine("  " + m.name + " shader=" + m.shader.name + " asset=" + AssetDatabase.GetAssetPath(m));
        log.AppendLine("  total: " + mats.Count);

        log.AppendLine();
        log.AppendLine("## TEXTURES ON FLOOR 2 MATERIALS");
        var texes = new HashSet<Texture>();
        foreach (var m in mats)
            foreach (var p in TextureProps)
            {
                if (!m.HasProperty(p)) continue;
                var t = m.GetTexture(p);
                if (t != null) texes.Add(t);
            }
        foreach (var t in texes)
        {
            string path = AssetDatabase.GetAssetPath(t);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            var t2 = t as Texture2D;
            string state = "no importer";
            if (imp != null)
            {
                var ps = imp.GetPlatformTextureSettings("Android");
                state = "max=" + imp.maxTextureSize + " type=" + imp.textureType
                      + " androidOverride=" + ps.overridden
                      + (ps.overridden ? (" androidMax=" + ps.maxTextureSize + " androidFmt=" + ps.format) : "");
            }
            log.AppendLine("  " + path + " " + (t2 != null ? t2.width + "x" + t2.height : "?") + " " + state);
        }
        log.AppendLine("  total: " + texes.Count);

        log.AppendLine();
        log.AppendLine("## LIGHTS (Floor 2 + stairs)");
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            if (l.transform.root.name == "Player") continue;
            log.AppendLine("  " + PathOf(l.transform) + " type=" + l.type + " intensity=" + l.intensity
                + " inScope=" + InScope(l.transform));
        }

        log.AppendLine();
        log.AppendLine("## STAIRS");
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t != null && t.name.ToLowerInvariant().Contains("stair"))
                log.AppendLine("  " + PathOf(t) + " renderers=" + t.GetComponentsInChildren<Renderer>(true).Length
                    + " colliders=" + t.GetComponentsInChildren<Collider>(true).Length
                    + " pos=" + t.position);
        }
        log.AppendLine("  NavMeshAgent on monster: "
            + (Object.FindObjectOfType<UnityEngine.AI.NavMeshAgent>() != null ? "PRESENT" : "NONE (monster teleports, never walks)"));

        Debug.Log("[FLOOR2 AUDIT]\n" + log);
    }

    static readonly string[] Floor1Roots = { "FirstFloorCorridor", "Classroom_1_1", "Classroom_1_2", "Restroom" };

    static bool InScopeOf(Transform t, string[] roots)
    {
        Transform p = t;
        while (p != null)
        {
            foreach (var s in roots) if (p.name == s) return true;
            p = p.parent;
        }
        return false;
    }

    /// <summary>
    /// Reports exactly which materials Floor 2 uses, which of them are SHARED with Floor 1
    /// (assigning textures there would break the "Floor 1 untouched" rule), and which authored
    /// .mat assets are available. Run this before assigning any new textures.
    /// </summary>
    [MenuItem("Tools/Floor2/Texture Assign Prep")]
    public static void TextureAssignPrep()
    {
        var log = new StringBuilder();
        var f2 = new Dictionary<Material, int>();
        var f1 = new Dictionary<Material, int>();

        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r == null) continue;
            bool in2 = InScopeOf(r.transform, ScopeRoots);
            bool in1 = InScopeOf(r.transform, Floor1Roots);
            if (!in2 && !in1) continue;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (in2) f2[m] = f2.TryGetValue(m, out var a) ? a + 1 : 1;
                if (in1) f1[m] = f1.TryGetValue(m, out var b) ? b + 1 : 1;
            }
        }

        log.AppendLine("## FLOOR 2 MATERIALS (" + f2.Count + " distinct)");
        var bySource = new Dictionary<string, List<string>>();
        foreach (var kv in f2)
        {
            string src = AssetDatabase.GetAssetPath(kv.Key);
            string key = string.IsNullOrEmpty(src) ? "<scene-embedded>" : src;
            if (!bySource.ContainsKey(key)) bySource[key] = new List<string>();
            bySource[key].Add(kv.Key.name + " (x" + kv.Value + (f1.ContainsKey(kv.Key) ? ", SHARED" : "") + ")");
        }
        foreach (var kv in bySource)
            log.AppendLine("  " + kv.Key + "\n      " + string.Join(", ", kv.Value.ToArray()));

        log.AppendLine();
        log.AppendLine("## SHARED WITH FLOOR 1  <-- DO NOT ASSIGN TEXTURES HERE");
        int shared = 0;
        foreach (var kv in f2)
            if (f1.ContainsKey(kv.Key))
            {
                shared++;
                log.AppendLine("  " + kv.Key.name + "  floor2=" + kv.Value + " floor1=" + f1[kv.Key]
                    + "  src=" + AssetDatabase.GetAssetPath(kv.Key));
            }
        if (shared == 0) log.AppendLine("  (none)");

        log.AppendLine();
        log.AppendLine("## AUTHORED .mat ASSETS UNDER Assets/Main/Material");
        foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Main/Material" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            int u2 = 0, u1 = 0;
            if (mat != null)
            {
                if (f2.ContainsKey(mat)) u2 = f2[mat];
                if (f1.ContainsKey(mat)) u1 = f1[mat];
            }
            string tex = "-";
            if (mat != null && mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
                tex = AssetDatabase.GetAssetPath(mat.GetTexture("_MainTex"));
            log.AppendLine("  " + path + "  shader=" + (mat != null ? mat.shader.name : "?")
                + "  usedByFloor2=" + u2 + "  usedByFloor1=" + u1 + "  albedo=" + tex);
        }

        log.AppendLine();
        log.AppendLine("## RENDERERS BY FLOOR 2 ROOM (for targeting wall/floor surfaces)");
        foreach (var root in ScopeRoots)
        {
            var go = GameObject.Find(root);
            if (go == null) continue;
            var names = new Dictionary<string, int>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                names[n] = names.TryGetValue(n, out var c) ? c + 1 : 1;
            }
            log.AppendLine("  " + root + ": " + go.GetComponentsInChildren<Renderer>(true).Length + " renderers");
            foreach (var kv in names) log.AppendLine("      " + kv.Key + " x" + kv.Value);
        }

        Debug.Log("[FLOOR2 TEXTURE PREP]\n" + log);
    }

    // ---------------------------------------------------------------- textures

    [MenuItem("Tools/Floor2/Apply Texture Import Settings")]
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
                foreach (var p in TextureProps)
                {
                    if (!m.HasProperty(p)) continue;
                    var t = m.GetTexture(p);
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
            int native = tex2d != null ? Mathf.Max(tex2d.width, tex2d.height) : HeroCap;
            int capped = native >= HeroCap ? HeroCap : (native >= MidCap ? MidCap : SmallCap);
            var fmt = isNormal ? NormalFormat : ColourFormat;

            var ps = imp.GetPlatformTextureSettings("Android");
            if (ps.overridden && ps.maxTextureSize == capped && ps.format == fmt)
            {
                log.AppendLine("  = " + path + " already " + capped + "/" + fmt);
                continue;
            }
            ps.overridden = true;
            ps.maxTextureSize = capped;
            ps.format = fmt;
            ps.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(ps);
            imp.SaveAndReimport();
            changed++;
            log.AppendLine("  " + path + " native=" + native + " -> max=" + capped + " fmt=" + fmt + (isNormal ? " (normal)" : ""));
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR2 6.1 TEXTURES] changed=" + changed + " of " + seen.Count + " (Floor 1 presets)\n" + log);
    }

    // ---------------------------------------------------------------- lighting

    [MenuItem("Tools/Floor2/Build Lighting")]
    public static void BuildLighting()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();

        // Only clean up our own light fixtures - matching the bare "FL2_" prefix would also delete
        // the stair cue volumes, which this method does not recreate.
        int removed = 0;
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t == null) continue;
            if (t.name.StartsWith("FL2_CorridorLight") || t.name.StartsWith("FL2_RoomLight"))
            {
                Object.DestroyImmediate(t.gameObject);
                removed++;
            }
        }
        log.AppendLine("  removed " + removed + " previously generated Floor 2 light(s)");

        // ---- second floor corridor: same tension language as Floor 1, deliberately darker ----
        var corridor = GameObject.Find("SecondFloorCorridor");
        if (corridor == null) { Debug.LogError("[FLOOR2 6.1] SecondFloorCorridor not found."); return; }
        Bounds cb = ObjectBounds(corridor);
        log.AppendLine("  corridor bounds center=" + cb.center + " size=" + cb.size);

        bool alongX = cb.size.x >= cb.size.z;
        for (int i = 0; i < 3; i++)
        {
            float frac = (i + 1) / 4f;
            Vector3 p = alongX
                ? new Vector3(Mathf.Lerp(cb.min.x, cb.max.x, frac), cb.max.y - 0.7f, cb.center.z)
                : new Vector3(cb.center.x, cb.max.y - 0.7f, Mathf.Lerp(cb.min.z, cb.max.z, frac));

            var go = new GameObject("FL2_CorridorLight_" + (i + 1));
            go.transform.SetParent(corridor.transform, true);
            go.transform.position = p;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            // Slightly greener and dimmer than Floor 1's corridor: upstairs should feel worse.
            light.color = new Color(0.70f, 0.88f, 0.74f);
            light.intensity = 0.95f;
            light.range = 8.5f;
            light.shadows = i == 1 ? LightShadows.Soft : LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;

            var fl = go.AddComponent<FlickerLight>();
            fl.baseIntensity = 0.95f;
            fl.flickerAmount = 0.32f;
            fl.flickerSpeed = 6.5f;
            fl.dropoutChance = 0.55f;      // fails more often than downstairs
            log.AppendLine("  + " + go.name + " at " + p + " (flicker, heavier dropouts)");
        }

        // ---- Classroom_2_1: sterile "lab" identity, unlike both Floor 1 classrooms ----
        var room = GameObject.Find("Classroom_2_1");
        if (room != null)
        {
            Bounds rb = ObjectBounds(room);
            bool rAlongX = rb.size.x >= rb.size.z;
            for (int i = 0; i < 3; i++)          // 3 fixtures: brightest, flattest room in the game
            {
                float frac = (i + 1) / 4f;
                Vector3 p = rAlongX
                    ? new Vector3(Mathf.Lerp(rb.min.x, rb.max.x, frac), rb.max.y - 0.7f, rb.center.z)
                    : new Vector3(rb.center.x, rb.max.y - 0.7f, Mathf.Lerp(rb.min.z, rb.max.z, frac));

                var go = new GameObject("FL2_RoomLight_Classroom_2_1_" + (i + 1));
                go.transform.SetParent(room.transform, true);
                go.transform.position = p;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.88f, 1.00f, 0.94f);   // clinical green-white
                light.intensity = 1.45f;
                // Capped at 8-10m: the uncapped bounds-derived value came out at 26m, which is both a
                // per-pixel light cost and a light-bleed risk across the room's walls.
                light.range = Mathf.Clamp(Mathf.Max(rb.size.x, rb.size.z) * 0.55f, 8f, 10f);
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                log.AppendLine("  + " + go.name + " colour=" + ColorUtility.ToHtmlStringRGB(light.color)
                    + " intensity=" + light.intensity + " range=" + light.range.ToString("F1") + " steady");
            }
            log.AppendLine("      Classroom_2_1 -> clinical lab white (vs 1_1 warm single, 1_2 cool flickering)");
        }
        else log.AppendLine("  MISSING room: Classroom_2_1");

        // Lighting stays realtime, matching the PART 5 decision; shadows already tuned on all levels.

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR2 6.1 LIGHTING]\n" + log);
    }

    // ---------------------------------------------------------------- stairs (6.2)

    [MenuItem("Tools/Floor2/Build Stairs Cue")]
    public static void BuildStairsCue()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();

        int removedCues = 0;
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
            if (t != null && t.name.StartsWith("FL2_StairsCue")) { Object.DestroyImmediate(t.gameObject); removedCues++; }
        log.AppendLine("  removed " + removedCues + " previously generated cue volume(s)");

        // There are four stair meshes in the level (Map/Stair plus three under Main Structure),
        // so one volume would only cover a single flight. Place one per stair object.
        var stairObjects = new List<Transform>();
        var map = GameObject.Find("Map");
        if (map != null)
        {
            foreach (var t in map.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (!t.name.ToLowerInvariant().Contains("stair")) continue;
                if (t.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                bool nested = false;
                foreach (var s in stairObjects) if (t.IsChildOf(s)) { nested = true; break; }
                if (!nested) stairObjects.Add(t);
            }
        }

        if (stairObjects.Count == 0)
        {
            log.AppendLine("  !! no stair geometry found - no cue volume created.");
            log.AppendLine("     Tell me where the stairs are (or rename their object) and I will place it.");
            Debug.LogWarning("[FLOOR2 6.2 STAIRS]\n" + log);
            return;
        }

        var ambientHost = GameObject.Find("FL1_AmbientAudio");
        var ambientSource = ambientHost != null ? ambientHost.GetComponent<AudioSource>() : null;

        int placed = 0;
        foreach (var stair in stairObjects)
        {
            Bounds sb = ObjectBounds(stair.gameObject);

            var cue = new GameObject("FL2_StairsCue_" + (++placed));
            cue.transform.SetParent(stair, true);
            cue.transform.position = sb.center;
            var box = cue.AddComponent<BoxCollider>();
            box.isTrigger = true;
            // Generous vertically and slightly padded laterally so it catches the player mid-flight
            // rather than only on the top step - but capped, because the Main Structure stair bounds
            // are large enough to produce a 13m-tall volume that would duck ambient across the floor.
            box.size = new Vector3(
                Mathf.Clamp(sb.size.x * 0.9f, 3f, 12f),
                Mathf.Clamp(sb.size.y * 2.2f, 4f, 8f),
                Mathf.Clamp(sb.size.z * 0.9f, 3f, 12f));

            var amb = cue.AddComponent<StairsAmbience>();
            amb.ambientSource = ambientSource;
            amb.duckedVolume = 0.25f;
            amb.entrySting = null;              // left for the user's clip

            var hits = Physics.OverlapBox(cue.transform.position, box.size * 0.5f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            log.AppendLine("  + " + cue.name + " on " + PathOf(stair) + " at " + sb.center
                + " box=" + box.size + " solidsInside=" + hits.Length);
        }

        log.AppendLine("      cue volumes: " + placed + "   ambientSource="
            + (ambientSource != null ? PathOf(ambientSource.transform) : "NONE") + "   entrySting=EMPTY (assign your clip)");
        log.AppendLine("  NavMeshAgent present: " + (Object.FindObjectOfType<UnityEngine.AI.NavMeshAgent>() != null
            ? "YES" : "NO - the monster teleports and never walks, so nothing needs to path across floors."));
        log.AppendLine("  Cross-floor behaviour: the monster's teleport Y is derived from the PLAYER's Y"
            + " (player_transform.position.y - floorOffset), so it already follows the player between"); 
        log.AppendLine("  floors by construction. No cross-floor behaviour was invented.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR2 6.2 STAIRS]\n" + log);
    }

    // ---------------------------------------------------------------- content placement (6.4)

    [MenuItem("Tools/Floor2/Check Content Placement")]
    public static void CheckContentPlacement()
    {
        var log = new StringBuilder();
        var spots = GameObject.FindGameObjectsWithTag("SpawnItem");
        log.AppendLine("  SpawnItem spots: " + spots.Length);
        int floating = 0, embedded = 0, unsupported = 0, flagged = 0;

        foreach (var s in spots)
        {
            Vector3 p = s.transform.position;
            // Is the spot intersecting solid geometry?
            var overlaps = Physics.OverlapSphere(p, 0.25f, ~0, QueryTriggerInteraction.Ignore);
            bool inSolid = false;
            foreach (var c in overlaps)
                if (!c.transform.IsChildOf(s.transform) && c.transform != s.transform) inSolid = true;

            // Is there floor beneath it?
            RaycastHit hit;
            bool hasFloor = Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out hit, 5f, ~0, QueryTriggerInteraction.Ignore);
            float gap = hasFloor ? hit.distance - 0.5f : -1f;

            string flags = "";
            if (inSolid) { embedded++; flags += " EMBEDDED"; }
            if (!hasFloor) { unsupported++; flags += " NO-FLOOR-BELOW"; }
            else if (gap > 1.0f) { floating++; flags += " FLOATING(gap=" + gap.ToString("F2") + "m)"; }
            if (flags != "")
            {
                flagged++;
                log.AppendLine("  ! " + PathOf(s.transform) + " at " + p + flags);
            }
        }

        log.AppendLine("  summary: flagged=" + flagged + " of " + spots.Length
            + " (embedded=" + embedded + ", floating=" + floating + ", no-floor-below=" + unsupported + ")");
        log.AppendLine("  NOTE: the monster does NOT use these spots - it teleports relative to the player.");
        Debug.Log("[FLOOR2 6.4 PLACEMENT]\n" + log);
    }

    [MenuItem("Tools/Floor2/Run Whole Pass")]
    public static void RunWholePass()
    {
        ApplyTextureSettings();
        BuildLighting();
        BuildStairsCue();
        CheckContentPlacement();
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

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        Transform p = t.parent;
        while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
        return sb.ToString();
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
