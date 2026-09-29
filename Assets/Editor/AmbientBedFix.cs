using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pre-PART-7 audio hygiene: rename the placeholder-named ambient bed and compress it properly
/// (Vorbis, mono, ~0.45 quality) so it stops eating a third of the audio budget.
///
/// MENU: Tools/Audio/Compress Ambient Bed
/// </summary>
public static class AmbientBedFix
{
    const string OldPath = "Assets/Main/Audio/Z test2.mp3";
    const string NewPath = "Assets/Main/Audio/Ambient_SchoolNight_Bed.mp3";

    [MenuItem("Tools/Audio/Compress Ambient Bed")]
    public static void CompressAndRename()
    {
        var log = new StringBuilder();

        // ---- 1. rename (AssetDatabase.MoveAsset keeps the GUID, so the AudioSource keeps working) ----
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(OldPath);
        if (clip != null)
        {
            string guidBefore = AssetDatabase.AssetPathToGUID(OldPath);
            string err = AssetDatabase.MoveAsset(OldPath, NewPath);
            if (!string.IsNullOrEmpty(err))
            {
                log.AppendLine("  rename FAILED: " + err);
            }
            else
            {
                string guidAfter = AssetDatabase.AssetPathToGUID(NewPath);
                log.AppendLine("  renamed  '" + System.IO.Path.GetFileName(OldPath) + "' -> '" + System.IO.Path.GetFileName(NewPath) + "'");
                log.AppendLine("  GUID     " + guidBefore + " -> " + guidAfter
                    + (guidBefore == guidAfter ? "   (unchanged, references intact)" : "   <-- CHANGED, references may break"));
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(NewPath);
            }
        }
        else
        {
            log.AppendLine("  '" + OldPath + "' not found - assuming already renamed.");
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(NewPath);
        }

        if (clip == null) { Debug.LogError("[AMBIENT BED] could not resolve the clip.\n" + log); return; }

        var fi = new System.IO.FileInfo(NewPath);
        long sourceBytes = fi.Exists ? fi.Length : 0;
        float length = clip.length;
        int freq = clip.frequency;
        int channels = clip.channels;

        log.AppendLine();
        log.AppendLine("  clip      : " + clip.name);
        log.AppendLine("  duration  : " + length.ToString("F1") + " s   frequency=" + freq + " Hz   channels=" + channels);
        log.AppendLine("  source    : " + (sourceBytes / 1024) + " KB on disk (mp3 source is NOT recompressed by Unity)");

        // ---- 2. importer settings: Vorbis / mono / ~0.45 ----
        var importer = AssetImporter.GetAtPath(NewPath) as AudioImporter;
        if (importer == null) { Debug.LogError("[AMBIENT BED] no AudioImporter at " + NewPath); return; }

        var settings = importer.defaultSampleSettings;
        string before = settings.compressionFormat + " q=" + settings.quality + " load=" + settings.loadType + " mono=" + importer.forceToMono;

        importer.forceToMono = true;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.45f;
        // CompressedInMemory (not Streaming) so the loop is guaranteed gapless.
        settings.loadType = AudioClipLoadType.CompressedInMemory;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();

        log.AppendLine();
        log.AppendLine("  BEFORE: " + before);
        log.AppendLine("  AFTER : Vorbis q=0.45 load=CompressedInMemory mono=True");

        // ---- 3. estimated runtime footprint ----
        // Mono Vorbis at q~0.45 lands near 48 kbps; report it as an estimate, not a measurement.
        double estBytes = length * (48000.0 / 8.0);
        double uncompressed = (double)length * freq * 1 * 2;   // mono, 16-bit
        log.AppendLine();
        log.AppendLine("  estimated runtime: ~" + (estBytes / 1048576.0).ToString("F2") + " MB mono Vorbis"
            + "   (vs ~" + (uncompressed / 1048576.0).ToString("F2") + " MB uncompressed mono PCM)");
        log.AppendLine("  note: the source mp3 stays " + (sourceBytes / 1048576.0).ToString("F2")
            + " MB on disk; only the built/loaded size changes.");

        AssetDatabase.SaveAssets();
        Debug.Log("[AMBIENT BED]\n" + log);
    }
}
