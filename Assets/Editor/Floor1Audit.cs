using System.Collections.Generic;
using System.Text;
using NeKoRoSYS.InputHandling.Mobile;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Read-only audit of the Floor 1 content in MainLevel:
/// materials/shader/texture-import settings, lights, ambient+bake settings,
/// audio sources and spawn points. MENU: Tools/Floor1/Audit
/// </summary>
public static class Floor1Audit
{
    static readonly string[] ScopeRoots = { "FirstFloorCorridor", "Classroom_1_1", "Classroom_1_2", "Restroom" };
    static readonly string[] OutOfScope = { "SecondFloorCorridor", "Classroom_2_1" };

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        Transform p = t.parent;
        while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
        return sb.ToString();
    }

    static string RootOf(Transform t)
    {
        Transform p = t;
        while (p.parent != null) p = p.parent;
        return p.name;
    }

    static bool InScope(Transform t)
    {
        // The rooms are children of "Map", so walk the whole ancestor chain looking for a
        // scope root rather than just checking the top-level ancestor.
        Transform p = t;
        while (p != null)
        {
            foreach (var s in ScopeRoots) if (p.name == s) return true;
            p = p.parent;
        }
        return false;
    }

    [MenuItem("Tools/Floor1/Audit")]
    public static void Audit()
    {
        var sb = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        sb.AppendLine("scene=" + scene.name);

        // ---------- scope roots ----------
        sb.AppendLine();
        sb.AppendLine("## SCOPE ROOTS");
        foreach (var name in ScopeRoots)
        {
            var go = GameObject.Find(name);
            if (go == null) { sb.AppendLine("  MISSING: " + name); continue; }
            var rends = go.GetComponentsInChildren<Renderer>(true);
            int lights = go.GetComponentsInChildren<Light>(true).Length;
            int colliders = go.GetComponentsInChildren<Collider>(true).Length;
            sb.AppendLine("  " + name + ": renderers=" + rends.Length + " lights=" + lights + " colliders=" + colliders
                + " children=" + (go.transform.childCount));
        }
        sb.AppendLine("  out-of-scope (must not be touched):");
        foreach (var name in OutOfScope)
        {
            var go = GameObject.Find(name);
            sb.AppendLine("    " + name + ": " + (go == null ? "MISSING" : ("renderers=" + go.GetComponentsInChildren<Renderer>(true).Length)));
        }
        sb.AppendLine("  other floor-1-ish roots present: Library, Lab, Stair, Main Structure, a door*, fire, Cross");

        // ---------- Floor 1 materials ----------
        sb.AppendLine();
        sb.AppendLine("## FLOOR 1 MATERIALS (distinct, by shader)");
        var mats = new HashSet<Material>();
        var matUsers = new Dictionary<Material, int>();
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r == null || !InScope(r.transform)) continue;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                mats.Add(m);
                matUsers[m] = matUsers.TryGetValue(m, out var c) ? c + 1 : 1;
            }
        }
        foreach (var m in mats)
        {
            string path = AssetDatabase.GetAssetPath(m);
            sb.AppendLine("  " + m.name + "  shader=" + m.shader.name + "  users=" + matUsers[m]
                + "  asset=" + (string.IsNullOrEmpty(path) ? "<scene-embedded>" : path));
        }
        sb.AppendLine("  total distinct Floor 1 materials: " + mats.Count);

        // ---------- textures on those materials ----------
        sb.AppendLine();
        sb.AppendLine("## TEXTURES REFERENCED BY FLOOR 1 MATERIALS");
        var texProps = new[] { "_MainTex", "_BaseMap", "_BumpMap", "_NormalMap", "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailMask" };
        var textures = new HashSet<Texture>();
        var texRole = new Dictionary<Texture, string>();
        foreach (var m in mats)
        {
            foreach (var p in texProps)
            {
                if (!m.HasProperty(p)) continue;
                var t = m.GetTexture(p);
                if (t == null) continue;
                textures.Add(t);
                if (!texRole.ContainsKey(t)) texRole[t] = p;
            }
        }
        foreach (var t in textures)
        {
            string path = AssetDatabase.GetAssetPath(t);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            string info = "no importer";
            if (imp != null)
            {
                var ps = imp.GetPlatformTextureSettings("Android");
                info = "max=" + imp.maxTextureSize + " type=" + imp.textureType
                     + " compression=" + imp.textureCompression
                     + " androidOverride=" + ps.overridden
                     + (ps.overridden ? (" androidMax=" + ps.maxTextureSize + " androidFmt=" + ps.format + " androidComp=" + ps.textureCompression) : "");
            }
            var dims = t is Texture2D t2 ? (" " + t2.width + "x" + t2.height) : "";
            sb.AppendLine("  " + t.name + dims + " [" + texRole[t] + "] " + info + "  <- " + path);
        }
        sb.AppendLine("  total distinct Floor 1 textures: " + textures.Count);

        // ---------- third-party pack textures used by Floor 1 ----------
        sb.AppendLine();
        sb.AppendLine("## TEXTURES ON FLOOR 1 MATERIALS THAT LIVE IN THIRD-PARTY PACKS");
        var packDirs = new Dictionary<string, int>();
        foreach (var t in textures)
        {
            string path = AssetDatabase.GetAssetPath(t);
            string dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            // collapse to the pack root (first 4 segments)
            var parts = dir.Split('/');
            string key = string.Join("/", parts, 0, System.Math.Min(4, parts.Length));
            packDirs[key] = packDirs.TryGetValue(key, out var c) ? c + 1 : 1;
        }
        foreach (var kv in packDirs) sb.AppendLine("  " + kv.Key + "  textures=" + kv.Value);
        sb.AppendLine("  (all floor-1 textures, with importer state:)");
        foreach (var t in textures)
        {
            string path = AssetDatabase.GetAssetPath(t);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            var ps = imp != null ? imp.GetPlatformTextureSettings("Android") : null;
            var t2 = t as Texture2D;
            sb.AppendLine("  TXT " + path + " " + (t2 != null ? t2.width + "x" + t2.height : "?")
                + " max=" + (imp != null ? imp.maxTextureSize.ToString() : "?")
                + " comp=" + (imp != null ? imp.textureCompression.ToString() : "?")
                + " androidOverride=" + (ps != null ? ps.overridden.ToString() : "?")
                + (ps != null && ps.overridden ? (" androidFmt=" + ps.format + " androidMax=" + ps.maxTextureSize) : "")
                + " androidCompressed=" + (imp != null && imp.androidETC2FallbackOverride != AndroidETC2FallbackOverride.UseBuildSettings));
        }

        // ---------- all project textures import summary ----------
        sb.AppendLine();
        sb.AppendLine("## ALL TEXTURES UNDER Assets/Main (import summary)");
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Main" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            var ps = imp.GetPlatformTextureSettings("Android");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            sb.AppendLine("  " + path + " " + (tex != null ? tex.width + "x" + tex.height : "?")
                + " max=" + imp.maxTextureSize + " compression=" + imp.textureCompression
                + " androidOverride=" + ps.overridden + (ps.overridden ? (" androidMax=" + ps.maxTextureSize + " androidFmt=" + ps.format) : ""));
        }

        // ---------- lights ----------
        sb.AppendLine();
        sb.AppendLine("## LIGHTS (all)");
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            sb.AppendLine("  " + PathOf(l.transform) + " type=" + l.type + " intensity=" + l.intensity
                + " range=" + l.range + " color=" + ColorUtility.ToHtmlStringRGB(l.color)
                + " shadows=" + l.shadows + " bake=" + l.lightmapBakeType
                + " enabled=" + l.enabled + " inScope=" + InScope(l.transform));
        }

        // ---------- render / lightmap settings ----------
        sb.AppendLine();
        sb.AppendLine("## RENDER + LIGHTMAP SETTINGS");
        sb.AppendLine("  ambientMode=" + RenderSettings.ambientMode + " ambientLight=" + RenderSettings.ambientLight
            + " ambientIntensity=" + RenderSettings.ambientIntensity + " ambientSkyColor=" + RenderSettings.ambientSkyColor);
        sb.AppendLine("  fog=" + RenderSettings.fog + " fogMode=" + RenderSettings.fogMode + " fogDensity=" + RenderSettings.fogDensity + " fogColor=" + RenderSettings.fogColor);
        sb.AppendLine("  skybox=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "null"));
        var ls = Lightmapping.lightingSettings;
        sb.AppendLine("  lightingSettingsAsset=" + (ls != null ? AssetDatabase.GetAssetPath(ls) : "null (scene defaults)"));
        var eff = ls != null ? ls : Lightmapping.GetLightingSettingsForScene(scene);
        if (eff != null)
        {
            sb.AppendLine("  lightmapper=" + eff.lightmapper
                + " lightmapResolution=" + eff.lightmapResolution
                + " lightmapMaxSize=" + eff.lightmapMaxSize
                + " mixedBakeMode=" + eff.mixedBakeMode
                + " directSampleCount=" + eff.directSampleCount
                + " indirectSampleCount=" + eff.indirectSampleCount
                + " ao=" + eff.ao + " aoMaxDistance=" + eff.aoMaxDistance);
        }
        sb.AppendLine("  bakedLightmapsInScene=" + LightmapSettings.lightmaps.Length);

        // ---------- monster tag wiring ----------
        sb.AppendLine();
        sb.AppendLine("## MONSTER TAG / TRIGGER WIRING (DetectMonster depends on this)");
        var tagged = GameObject.FindGameObjectsWithTag("Monster");
        sb.AppendLine("  objects tagged 'Monster' in scene: " + tagged.Length);
        foreach (var g in tagged) sb.AppendLine("    " + PathOf(g.transform));
        var dm = Object.FindObjectOfType<DetectMonster>(true);
        sb.AppendLine("  DetectMonster instance: " + (dm != null ? PathOf(dm.transform) : "NONE"));
        var trig = GameObject.Find("FlashLightTrigger");
        sb.AppendLine("  FlashLightTrigger found: " + (trig != null ? PathOf(trig.transform) + " active=" + trig.activeInHierarchy : "NO"));
        var mon = Object.FindObjectOfType<Monster>(true);
        if (mon != null)
        {
            sb.AppendLine("  Monster instance: " + PathOf(mon.transform) + " tag=" + mon.gameObject.tag
                + " pos=" + mon.transform.position);
            sb.AppendLine("  Monster.cube_transform=" + (mon.cube_transform != null ? PathOf(mon.cube_transform) : "NULL")
                + " safePlace=" + (mon.safePlace != null ? PathOf(mon.safePlace) : "NULL")
                + " deathscreen=" + (mon.deathscreen != null ? PathOf(mon.deathscreen.transform) : "NULL"));
        }
        var enh = Object.FindObjectOfType<ControlPad>(true);
        sb.AppendLine("  ControlPad present: " + (enh != null));
        sb.AppendLine("  EnhancedTouchSupport.enabled (edit mode): " + UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled);

        // ---------- audio ----------
        sb.AppendLine();
        sb.AppendLine("## AUDIO SOURCES IN SCENE");
        foreach (var a in Object.FindObjectsOfType<AudioSource>(true))
        {
            sb.AppendLine("  " + PathOf(a.transform) + " clip=" + (a.clip != null ? a.clip.name : "null")
                + " loop=" + a.loop + " playOnAwake=" + a.playOnAwake + " vol=" + a.volume
                + " spatial=" + a.spatialBlend + " inScope=" + InScope(a.transform));
        }
        sb.AppendLine();
        sb.AppendLine("## AUDIO CLIPS UNDER Assets (with sizes)");
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var fi = new System.IO.FileInfo(path);
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            string fmt = "n/a";
            if (imp != null)
            {
                var s = imp.defaultSampleSettings;
                fmt = s.compressionFormat + "/" + s.loadType + " q=" + s.quality + " forceMono=" + imp.forceToMono;
            }
            sb.AppendLine("  " + path + "  " + (fi.Exists ? (fi.Length / 1024) + " KB" : "?") + "  " + fmt);
        }

        // ---------- spawn points ----------
        sb.AppendLine();
        sb.AppendLine("## SPAWN POINTS");
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name.StartsWith("SpawnJumpScare") || root.name == "SafePlaceSpawn")
                sb.AppendLine("  " + root.name + " pos=" + root.transform.position);
        }
        var monster = Object.FindObjectOfType<Monster>();
        if (monster != null)
            sb.AppendLine("  Monster instance at " + PathOf(monster.transform) + " pos=" + monster.transform.position);

        Debug.Log("[FLOOR1 AUDIT]\n" + sb);
    }
}
