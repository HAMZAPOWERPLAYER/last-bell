using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PART 7.3 volume control WITHOUT AudioMixer groups (the approved fallback).
///
/// Classifies every AudioSource in the scene as either Ambient or SFX, remembers the volume it
/// was authored with, and writes authored * roleVolume * masterVolume. Setting sliders call Apply().
///
/// Limitation: runtime-created sources (e.g. AudioSource.PlayClipAtPoint) are not tracked, since
/// their authored volume is unknown. Swap to real mixer groups later without touching the UI.
/// </summary>
public class AudioVolumeController : MonoBehaviour
{
    public static AudioVolumeController Instance { get; private set; }

    readonly Dictionary<AudioSource, float> authored = new Dictionary<AudioSource, float>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() { Apply(); }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Ambience vs one-shot SFX. The ambient bed and the cue manager are ambience.</summary>
    public static bool IsAmbient(AudioSource s)
    {
        if (s == null) return false;
        if (s.GetComponent<AmbientAudioManager>() != null) return true;
        if (s.GetComponent<StairsAmbience>() != null) return true;
        string n = s.gameObject.name.ToLowerInvariant();
        return n.Contains("ambient") || n.Contains("background") || n.Contains("randomsound") || n == "audioeffect";
    }

    /// <summary>Re-applies volumes to every known source. Cheap enough to call on slider change.</summary>
    public void Apply()
    {
        float master = GameSettings.MasterVolume;
        float ambient = GameSettings.AmbientVolume;
        float sfx = GameSettings.SfxVolume;

        var sources = Object.FindObjectsOfType<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            var s = sources[i];
            if (s == null) continue;

            float baseVolume;
            if (!authored.TryGetValue(s, out baseVolume))
            {
                baseVolume = s.volume;
                authored[s] = baseVolume;
            }

            float role = IsAmbient(s) ? ambient : sfx;
            s.volume = Mathf.Clamp01(baseVolume * role * master);
        }
    }
}
