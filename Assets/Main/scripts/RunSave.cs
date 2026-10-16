using UnityEngine;

/// <summary>
/// 2.0 SAVE &amp; CONTINUE: the persistent RUN SAVE behind the main menu's CONTINUE button,
/// the GameOver REVIVE button and mid-run reloads.
///
/// Storage is PlayerPrefs only (same fully-offline approach as GameSettings). A save is written
/// at CHECKPOINTS - run start, key pickup, page pickup, gate unlock - so killing the app or
/// running out of time never loses more than the current interaction. The save survives death and
/// app restarts so CONTINUE resumes the same world: same spawn layout, same collected keys and
/// pages, same unlocked gates. It is deleted when the player escapes (run complete) or presses
/// PLAY / NEW RUN (an explicit new run).
///
/// Layout encoding: two characters per spawn point ("--" empty, "K0".."K2" a key of that
/// progression stage, "P0".."P5" a page with that clue index), concatenated in the deterministic
/// spawn-point order produced by GameManager.initSpawns. When the scene's spawn-point count no
/// longer matches the saved layout, the layout is discarded on load (keys and pages still
/// resume; only the randomised item positions are re-rolled).
/// </summary>
public static class RunSave
{
    public const int SchemaVersion = 1;

    public const string KeyState = "Save.State";      // 1 = a resumable run exists
    public const string KeyVersion = "Save.Version";  // schema version, for future migrations
    public const string KeyKeys = "Save.Keys";        // collected keys 0..3 (mirrors GameManager.KeyProgress)
    public const string KeyPages = "Save.Pages";      // bitmask of collected page clue indices (6 bits)
    public const string KeyGates = "Save.Gates";      // bitmask: bit 0 classroom wing, bit 1 upper corridor
    public const string KeyLayout = "Save.Layout";    // deterministic spawn layout, 2 chars per spawn point

    /// <summary>True when a checkpointed run exists and can be resumed.</summary>
    public static bool HasSave()
    {
        return PlayerPrefs.GetInt(KeyState, 0) == 1
            && PlayerPrefs.GetInt(KeyVersion, 0) == SchemaVersion;
    }

    public static int Keys { get { return Mathf.Clamp(PlayerPrefs.GetInt(KeyKeys, 0), 0, 3); } }
    public static int PageMask { get { return PlayerPrefs.GetInt(KeyPages, 0) & 0x3F; } }
    public static int GateMask { get { return PlayerPrefs.GetInt(KeyGates, 0) & 0x03; } }
    public static string Layout { get { return PlayerPrefs.GetString(KeyLayout, string.Empty); } }

    /// <summary>How many distinct pages have been collected in this run.</summary>
    public static int PagesCollected { get { return CountBits(PageMask, 6); } }

    public static int CountBits(int mask, int bits)
    {
        int count = 0;
        for (int i = 0; i < bits; i++)
            if ((mask & (1 << i)) != 0) count++;
        return count;
    }

    /// <summary>Stores the run-start checkpoint: the rolled spawn layout and zero progress. When a
    /// save already exists (layout re-roll after a scene change) the progress counters are kept.</summary>
    public static void BeginRun(string layout)
    {
        if (!HasSave())
        {
            PlayerPrefs.SetInt(KeyVersion, SchemaVersion);
            PlayerPrefs.SetInt(KeyState, 1);
            PlayerPrefs.SetInt(KeyKeys, 0);
            PlayerPrefs.SetInt(KeyPages, 0);
            PlayerPrefs.SetInt(KeyGates, 0);
        }
        PlayerPrefs.SetString(KeyLayout, layout != null ? layout : string.Empty);
        PlayerPrefs.Save();
    }

    /// <summary>Checkpoint a key pickup. No-op when no run is being saved.</summary>
    public static void RecordKeyPickup(int keys)
    {
        if (!HasSave()) return;
        PlayerPrefs.SetInt(KeyKeys, Mathf.Clamp(keys, 0, 3));
        PlayerPrefs.Save();
    }

    /// <summary>Checkpoint a page pickup by its clue index. No-op when no run is being saved.</summary>
    public static void RecordPagePickup(int clueIndex)
    {
        if (!HasSave() || clueIndex < 0 || clueIndex > 5) return;
        PlayerPrefs.SetInt(KeyPages, PageMask | (1 << clueIndex));
        PlayerPrefs.Save();
    }

    /// <summary>Checkpoint a gate unlock so the gate re-opens instantly on resume.</summary>
    public static void RecordGateUnlocked(int requiredKeys)
    {
        if (!HasSave()) return;
        PlayerPrefs.SetInt(KeyGates, GateMask | (requiredKeys <= 1 ? 1 : 2));
        PlayerPrefs.Save();
    }

    /// <summary>Deletes the save (run started fresh, run completed, or progress abandoned).</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KeyState);
        PlayerPrefs.DeleteKey(KeyVersion);
        PlayerPrefs.DeleteKey(KeyKeys);
        PlayerPrefs.DeleteKey(KeyPages);
        PlayerPrefs.DeleteKey(KeyGates);
        PlayerPrefs.DeleteKey(KeyLayout);
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------- layout codec

    /// <summary>Allocates a layout buffer of "--" codes, one pair per spawn point.</summary>
    public static char[] NewLayoutBuffer(int spawnCount)
    {
        char[] codes = new char[Mathf.Max(0, spawnCount) * 2];
        for (int i = 0; i < codes.Length; i += 2) { codes[i] = '-'; codes[i + 1] = '-'; }
        return codes;
    }

    /// <summary>Stores "K0".."K2" or "P0".."P5" (kind 'K' or 'P') at one spot of a layout buffer.</summary>
    public static void WriteSpot(char[] codes, int spotIndex, char kind, int value)
    {
        int offset = spotIndex * 2;
        if (codes == null || offset < 0 || offset + 1 >= codes.Length) return;
        codes[offset] = kind;
        codes[offset + 1] = (char)('0' + Mathf.Clamp(value, 0, 9));
    }

    /// <summary>Reads one two-character spot code. Returns false when the layout does not cover it.</summary>
    public static bool TryReadSpot(string layout, int spotIndex, out char kind, out int value)
    {
        kind = '-';
        value = 0;
        int offset = spotIndex * 2;
        if (string.IsNullOrEmpty(layout) || offset < 0 || offset + 1 >= layout.Length) return false;
        kind = layout[offset];
        value = layout[offset + 1] - '0';
        return true;
    }

    /// <summary>True when the layout holds exactly one code pair per existing spawn point.</summary>
    public static bool CoversSpawnCount(string layout, int spawnCount)
    {
        return !string.IsNullOrEmpty(layout) && layout.Length == spawnCount * 2;
    }
}
