using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PART 6 Floor 2 texture work: configures the CC0 Poly Haven maps with the same ASTC presets
/// validated on Floor 1, then assigns them to Floor 2 surfaces.
///
/// Floor 1 is never touched: materials shared with Floor 1 are duplicated per Floor 2 renderer
/// instead of being edited in place.
///
/// MENU: Tools/Floor2/Configure CC0 Textures
/// MENU: Tools/Floor2/Assign CC0 Textures
/// </summary>
public static class Floor2Textures
{
    const string Cc0Root = "Assets/ThirdAssets/CC0";
    static readonly string[] ScopeRoots = { "SecondFloorCorridor", "Classroom_2_1" };

    // Same caps as the Floor 1 pass.
    const int ColourCap = 1024;
    const int DataCap = 512;
    static readonly TextureImporterFormat ColourFormat = TextureImporterFormat.ASTC_6x6;
    static readonly TextureImporterFormat NormalFormat = TextureImporterFormat.ASTC_4x4;

    static string FindMap(string pack, string suffix)
    {
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { Cc0Root }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains(pack) && p.Contains(suffix)) return p;
        }
        return null;
    }

    static string Classify(string path, out bool isNormal, out bool isData)
    {
        string n = System.IO.Path.GetFileName(path);
        isNormal = n.Contains("Normal");
        isData = n.Contains("Roughness") || n.Contains("AmbientOcclusion") || n.Contains("Metalness") || n.Contains("Displacement");
        if (n.Contains("Color")) return "albedo (sRGB)";
        if (isNormal) return n.Contains("NormalGL") ? "normal, OpenGL (Unity-correct)" : "normal, DirectX (unused)";
        return "data (linear)";
    }

    // ---------------------------------------------------------------- 1. import settings

    [MenuItem("Tools/Floor2/Configure CC0 Textures")]
    public static void ConfigureCc0()
    {
        var log = new StringBuilder();
        long beforeRuntime = 0, afterRuntime = 0;

        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { Cc0Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (imp == null || tex == null) continue;

            bool isNormal, isData;
            string kind = Classify(path, out isNormal, out isData);

            int cap = isNormal ? ColourCap : (isData ? DataCap : ColourCap);
            var fmt = isNormal ? NormalFormat : ColourFormat;

            int w = tex.width, h = tex.height;
            beforeRuntime += (long)w * h * 4;                       // uncompressed RGBA32
            int cw = Mathf.Min(w, cap), ch = Mathf.Min(h, cap);
            int block = isNormal ? 4 : 6;                            // ASTC block size
            afterRuntime += (long)Mathf.CeilToInt(cw / (float)block) * Mathf.CeilToInt(ch / (float)block) * 16;

            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !isData && !isNormal;                  // data maps must be linear
            imp.maxTextureSize = cap;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;                   // tiling surfaces
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Compressed;

            var ps = imp.GetPlatformTextureSettings("Android");
            ps.overridden = true;
            ps.maxTextureSize = cap;
            ps.format = fmt;
            ps.textureCompression = TextureImporterCompression.Compressed;
            imp.SetPlatformTextureSettings(ps);
            imp.SaveAndReimport();

            log.AppendLine("  " + System.IO.Path.GetFileName(path) + "  " + kind
                + "  " + w + "x" + h + " -> max=" + cap + " fmt=" + fmt
                + " sRGB=" + imp.sRGBTexture + " wrap=Repeat");
        }

        AssetDatabase.SaveAssets();
        log.AppendLine();
        log.AppendLine("  uncompressed RGBA32 total : " + (beforeRuntime / 1048576) + " MB");
        log.AppendLine("  ASTC (Android) total      : " + (afterRuntime / 1048576.0).ToString("F2") + " MB");
        Debug.Log("[FLOOR2 CC0 IMPORT]\n" + log);
    }

    // ---------------------------------------------------------------- 2. assignment

    [MenuItem("Tools/Floor2/Assign CC0 Textures")]
    public static void Assign()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();

        // ---------- walls: Wall.mat is used by 4 Floor 2 renderers and 0 Floor 1 renderers ----------
        var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Material/Wall.mat");
        if (wall == null) { Debug.LogError("[FLOOR2 CC0] Wall.mat not found."); return; }

        string brickColour = FindMap("Bricks059", "Color");
        string brickNormal = FindMap("Bricks059", "NormalGL");
        string brickAo = FindMap("Bricks059", "AmbientOcclusion");

        AssignToMaterial(wall, brickColour, brickNormal, brickAo, 0.18f, log, "Wall.mat (brick)");

        // ---------- Floor 2-only duplicates for anything shared with Floor 1 ----------
        log.AppendLine();
        log.AppendLine("## FLOOR-2-ONLY DUPLICATES");
        var dupMap = new Dictionary<Material, Material>();
        int repointed = 0;
        int duplicated = 0;

        // Deferred: forking EVERY FBX-embedded material would create ~30 .mat assets that then
        // receive no texture (pure churn). The wood-furniture subset is confirmed from the report
        // this method prints, then forked deliberately.
        foreach (var root in new string[0])
        {
            var go = GameObject.Find(root);
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    string src = AssetDatabase.GetAssetPath(m);
                    // Only FBX-embedded materials need forking; authored assets are handled directly.
                    if (string.IsNullOrEmpty(src) || !src.EndsWith(".fbx")) continue;
                    if (!dupMap.TryGetValue(m, out var dup))
                    {
                        dup = Object.Instantiate(m);
                        dup.name = m.name + "_FL2";
                        string dir = "Assets/Main/Material/Floor2";
                        if (!AssetDatabase.IsValidFolder(dir))
                        {
                            if (!AssetDatabase.IsValidFolder("Assets/Main/Material")) AssetDatabase.CreateFolder("Assets/Main", "Material");
                            AssetDatabase.CreateFolder("Assets/Main/Material", "Floor2");
                        }
                        string newPath = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + m.name + "_FL2.mat");
                        AssetDatabase.CreateAsset(dup, newPath);
                        dupMap[m] = dup;
                        duplicated++;
                        log.AppendLine("  + " + newPath + "   (copy of " + m.name + " from " + src + ")");
                    }
                    mats[i] = dup;
                    changed = true;
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    repointed++;
                }
            }
        }
        log.AppendLine("  duplicated " + duplicated + " material(s); repointed " + repointed + " Floor 2 renderer(s)");

        // ---------- floor candidates (report only) ----------
        log.AppendLine();
        log.AppendLine("## FLOOR-LIKE RENDERERS ON FLOOR 2 (candidates, nothing assigned yet)");
        foreach (var root in ScopeRoots)
        {
            var go = GameObject.Find(root);
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                if (b.size.y > 0.6f) continue;                       // must be a slab
                if (b.size.x < 4f && b.size.z < 4f) continue;         // must be large
                log.AppendLine("  " + root + "/" + r.gameObject.name
                    + "  size=" + b.size.ToString("F2") + "  centreY=" + b.center.y.ToString("F2")
                    + "  mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "null")
                    + "  path=" + AssetDatabase.GetAssetPath(r.sharedMaterial));
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR2 CC0 ASSIGN]\n" + log);
    }

    // ---------------------------------------------------------------- 3. wood pass

    static readonly string[] Floor1Roots = { "FirstFloorCorridor", "Classroom_1_1", "Classroom_1_2", "Restroom" };

    // FBX sources that are used ONLY by Floor 2 - texturing these cannot affect Floor 1.
    // (Materials with the same *name* exist across different FBX files, so matching is done on
    //  the material's asset path, never on its name.)
    static readonly string[] Floor2OnlyWoodSources =
    {
        "table1.fbx", "table2.fbx", "showcase.fbx", "board2.fbx", "board2_1.fbx", "tray.fbx"
    };

    [MenuItem("Tools/Floor2/Assign Wood Textures")]
    public static void AssignWood()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();

        // ---- safety: everything Floor 1 renderers currently use ----
        var floor1Mats = new HashSet<Material>();
        foreach (var root in Floor1Roots)
        {
            var go = GameObject.Find(root);
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials) if (m != null) floor1Mats.Add(m);
        }
        log.AppendLine("  Floor 1 uses " + floor1Mats.Count + " distinct material(s) - these are hard-blocked");
        log.AppendLine();

        // ---- shared floor-2-only wood material ----
        string woodColour = FindMap("Wood066", "Color");
        string woodNormal = FindMap("Wood066", "NormalGL");

        var woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Material/Floor2/Floor2_FurnitureWood.mat");
        if (woodMat == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Main/Material/Floor2"))
                AssetDatabase.CreateFolder("Assets/Main/Material", "Floor2");
            woodMat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(woodMat, "Assets/Main/Material/Floor2/Floor2_FurnitureWood.mat");
        }
        AssignToMaterial(woodMat, woodColour, woodNormal, null, 0.30f, log, "Floor2_FurnitureWood.mat");
        log.AppendLine();

        // ---- repoint only Floor-2-only wood renderers ----
        log.AppendLine("## WOOD FURNITURE (Floor-2-only sources)");
        int repointed = 0, skippedShared = 0;
        foreach (var root in ScopeRoots)
        {
            var go = GameObject.Find(root);
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    string src = AssetDatabase.GetAssetPath(m);
                    bool safeSource = false;
                    foreach (var s in Floor2OnlyWoodSources) if (src.EndsWith(s)) { safeSource = true; break; }
                    if (!safeSource) continue;
                    if (floor1Mats.Contains(m)) { skippedShared++; log.AppendLine("  ! SKIPPED (shared with Floor 1): " + m.name + " from " + src); continue; }
                    mats[i] = woodMat;
                    changed = true;
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    repointed++;
                }
            }
        }
        log.AppendLine("  repointed renderers: " + repointed + "   skipped(shared): " + skippedShared);

        // ---- floor: brand-new material on the SECOND-FLOOR slab renderers only ----
        log.AppendLine();
        log.AppendLine("## FLOOR SLABS (shell geometry, reported by elevation)");
        var room = GameObject.Find("Classroom_2_1");
        float floor2Bottom = room != null ? ObjectBounds(room).min.y : 4.5f;
        log.AppendLine("  Classroom_2_1 bottom (second-floor walking level) = " + floor2Bottom.ToString("F2"));

        var slabs = new List<Renderer>();
        var map = GameObject.Find("Map");
        if (map != null)
        {
            foreach (var r in map.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                if (b.size.y > 0.8f) continue;
                if (b.size.x < 4f && b.size.z < 4f) continue;
                slabs.Add(r);
                bool second = Mathf.Abs(b.center.y - floor2Bottom) <= 1.0f;
                log.AppendLine("  " + PathOf(r.transform) + "  centreY=" + b.center.y.ToString("F2")
                    + " size=" + b.size.ToString("F2")
                    + (second ? "   <-- SECOND FLOOR" : "")
                    + "  mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "null"));
            }
        }
        log.AppendLine("  slab-like renderers found: " + slabs.Count);

        string floorColour = FindMap("WoodFloor043", "Color");
        string floorNormal = FindMap("WoodFloor043", "NormalGL");
        string floorAo = FindMap("WoodFloor043", "AmbientOcclusion");

        var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Asset/Floor2_Floor.mat");
        if (floorMat == null) floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Material/Floor2/Floor2_Floor.mat");
        if (floorMat == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Main/Material/Floor2"))
                AssetDatabase.CreateFolder("Assets/Main/Material", "Floor2");
            floorMat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(floorMat, "Assets/Main/Material/Floor2/Floor2_Floor.mat");
        }
        AssignToMaterial(floorMat, floorColour, floorNormal, floorAo, 0.40f, log, "Floor2_Floor.mat");
        log.AppendLine();

        int floorsChanged = 0;
        foreach (var r in slabs)
        {
            Bounds b = r.bounds;
            if (Mathf.Abs(b.center.y - floor2Bottom) > 1.0f) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = floorMat;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
            floorsChanged++;
            log.AppendLine("  + floor -> " + PathOf(r.transform));
        }
        log.AppendLine("  second-floor slabs retextured: " + floorsChanged);
        if (floorsChanged > 0)
            log.AppendLine("  NOTE: if this slab doubles as Floor 1's ceiling you will see the wood from below.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR2 WOOD]\n" + log);
    }

    /// <summary>
    /// CORRECTIVE PASS. The first wood run targeted slabs by elevation using Classroom_2_1's
    /// bottom, which reported -0.58 (FIRST-floor level), so it textured
    /// "Main Structure/First Floor/Floor" at y=-0.62 - Floor 1's actual floor. This reverts that
    /// and re-targets by object NAME, which is unambiguous: First Floor vs Second Floor.
    /// </summary>
    [MenuItem("Tools/Floor2/Fix Floor Assignment")]
    public static void FixFloorAssignment()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();

        var floor2Mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Material/Floor2/Floor2_Floor.mat");
        var originalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Material/floor.mat");
        if (floor2Mat == null) { Debug.LogError("[FLOOR2 FIX] Floor2_Floor.mat not found."); return; }
        if (originalMat == null) { Debug.LogError("[FLOOR2 FIX] original floor.mat not found."); return; }

        // ---- 1. revert every renderer I wrongly pointed at Floor2_Floor.mat ----
        log.AppendLine("## REVERT");
        int reverted = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == floor2Mat) { mats[i] = originalMat; changed = true; }
            if (changed)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                reverted++;
                log.AppendLine("  " + PathOf(r.transform) + "  -> restored " + originalMat.name);
            }
        }
        log.AppendLine("  reverted renderers: " + reverted);

        // ---- 2. assign to the real second-floor slabs, by NAME ----
        log.AppendLine();
        log.AppendLine("## ASSIGN (Second Floor only)");
        int assigned = 0;
        var ms = GameObject.Find("Main Structure");
        if (ms == null) log.AppendLine("  !! Main Structure not found");
        else
        {
            foreach (var t in ms.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || !t.name.StartsWith("Floor")) continue;
                string path = PathOf(t);
                bool isSecond = path.Contains("Second Floor");
                var r = t.GetComponent<Renderer>();
                string was = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "null";

                if (!isSecond)
                {
                    log.AppendLine("  skip (First Floor): " + path + "  [kept " + was + "]");
                    continue;
                }
                if (r == null) continue;

                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = floor2Mat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                assigned++;
                log.AppendLine("  + " + path + "  was=" + was + " -> Floor2_Floor.mat");
            }
        }
        log.AppendLine("  second-floor slabs retextured: " + assigned);

        // ---- 3. verification ----
        log.AppendLine();
        log.AppendLine("## VERIFY");
        int onFloor1 = 0, onFloor2 = 0;
        foreach (var msr in Object.FindObjectsOfType<Renderer>(true))
        {
            if (msr.sharedMaterial != floor2Mat) continue;
            string p = PathOf(msr.transform);
            if (p.Contains("First Floor")) onFloor1++;
            if (p.Contains("Second Floor")) onFloor2++;
        }
        log.AppendLine("  Floor2_Floor.mat now on: Second Floor renderers=" + onFloor2 + ", First Floor renderers=" + onFloor1
            + (onFloor1 == 0 ? "  OK" : "  <-- STILL WRONG"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FLOOR2 FLOOR FIX]\n" + log);
    }

    /// <summary>
    /// Reverts the wood floor. Originals are recovered by OBJECT, not by name - "Mat.6" exists as
    /// several distinct materials inside different FBX files, so a name lookup could restore the
    /// wrong one. Each retextured slab is matched to its untouched counterpart at the same height
    /// (the ceiling/floor pair).
    /// </summary>
    [MenuItem("Tools/Floor2/Revert Floor Texture")]
    public static void RevertFloorTexture()
    {
        var log = new StringBuilder();
        var scene = SceneManager.GetActiveScene();

        var applied = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Material/Floor2/Floor2_Floor.mat");
        if (applied == null) { Debug.LogError("[FLOOR2 REVERT] Floor2_Floor.mat not found."); return; }

        var allMats = new List<Material>();
        foreach (var g in AssetDatabase.FindAssets("t:Material"))
            allMats.Add(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)));
        foreach (var g in AssetDatabase.FindAssets("t:Model"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Material mm) allMats.Add(mm);
        }
        Material byName(string n)
        {
            foreach (var m in allMats) if (m != null && m.name == n) return m;
            return null;
        }

        var counterparts = new List<Renderer>();
        var targets = new List<Renderer>();
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (!r.name.StartsWith("Floor")) continue;
            string p = PathOf(r.transform);
            if (!p.Contains("Main Structure")) continue;
            if (p.Contains("Second Floor") && r.sharedMaterial == applied) targets.Add(r);
            else if (p.Contains("First Floor")) counterparts.Add(r);
        }

        log.AppendLine("  slabs using Floor2_Floor.mat : " + targets.Count);
        log.AppendLine("  untouched counterparts       : " + counterparts.Count);

        int restored = 0;
        foreach (var t in targets)
        {
            float y = t.bounds.center.y;
            Renderer best = null;
            float bestDelta = float.MaxValue;
            foreach (var c in counterparts)
            {
                float d = Mathf.Abs(c.bounds.center.y - y) + Mathf.Abs(c.bounds.size.x - t.bounds.size.x);
                if (d < bestDelta) { bestDelta = d; best = c; }
            }
            Material original = best != null && best.sharedMaterial != applied ? best.sharedMaterial : null;
            string how = original != null ? ("copied from " + PathOf(best.transform) + " y=" + best.bounds.center.y.ToString("F2")) : null;

            if (original == null)
            {
                string wanted = y > 4.6f ? "floor 1" : "Mat.6";
                original = byName(wanted);
                how = "looked up by name '" + wanted + "'";
            }
            if (original == null) { log.AppendLine("  !! no original found for " + PathOf(t.transform)); continue; }

            var mats = t.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = original;
            t.sharedMaterials = mats;
            EditorUtility.SetDirty(t);
            restored++;
            log.AppendLine("  " + PathOf(t.transform) + " (y=" + y.ToString("F2") + ") -> "
                + original.name + "   [" + how + "]");
        }
        log.AppendLine("  restored: " + restored);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        int still = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true)) if (r.sharedMaterial == applied) still++;
        log.AppendLine("  renderers STILL using Floor2_Floor.mat: " + still + (still == 0 ? "  OK" : "  <-- CHECK"));
        Debug.Log("[FLOOR2 FLOOR REVERT]\n" + log);
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

    static void AssignToMaterial(Material m, string colour, string normal, string ao, float glossiness, StringBuilder log, string label)
    {
        if (m == null) return;
        if (colour != null && m.HasProperty("_MainTex"))
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(colour));
        if (normal != null && m.HasProperty("_BumpMap"))
        {
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture>(normal));
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_BumpScale", 1f);
        }
        if (ao != null && m.HasProperty("_OcclusionMap"))
        {
            m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture>(ao));
            m.SetFloat("_OcclusionStrength", 1f);
            m.EnableKeyword("_OCCLUSIONMAP");
        }
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", glossiness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        log.AppendLine("  " + label + ": albedo=" + (colour != null ? System.IO.Path.GetFileName(colour) : "none")
            + " normal=" + (normal != null ? System.IO.Path.GetFileName(normal) : "none")
            + " ao=" + (ao != null ? System.IO.Path.GetFileName(ao) : "none")
            + " glossiness=" + glossiness + " metallic=0");
    }
}
