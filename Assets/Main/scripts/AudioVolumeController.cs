using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PART 7.3 volume control WITHOUT AudioMixer groups (the approved fallback).
///
/// Classifies each AudioSource as Ambient or SFX, remembers its authored volume,
/// and writes authored * roleVolume. The AudioListener owns the master-volume multiplier.
///
/// Limitation: runtime-created sources (e.g. AudioSource.PlayClipAtPoint) are not tracked, since
/// their authored volume is unknown. Swap to real mixer groups later without touching the UI.
/// </summary>
public class AudioVolumeController : MonoBehaviour
{
    public static AudioVolumeController Instance { get; private set; }

    readonly Dictionary<AudioSource, float> authored = new Dictionary<AudioSource, float>();
    readonly Dictionary<AudioSource, Dictionary<int, float>> sourceMultipliers = new Dictionary<AudioSource, Dictionary<int, float>>();

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
        AudioListener.volume = GameSettings.MasterVolume;
        var sources = Object.FindObjectsOfType<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            if (source != null) ApplySource(source);
        }
    }

    /// <summary>Applies a temporary volume multiplier owned by a scene component.</summary>
    public void SetSourceMultiplier(AudioSource source, Object owner, float multiplier)
    {
        if (source == null || owner == null) return;
        if (!authored.ContainsKey(source)) authored[source] = source.volume;

        Dictionary<int, float> owners;
        if (!sourceMultipliers.TryGetValue(source, out owners))
        {
            owners = new Dictionary<int, float>();
            sourceMultipliers[source] = owners;
        }
        owners[owner.GetInstanceID()] = Mathf.Clamp01(multiplier);
        ApplySource(source);
    }

    /// <summary>Removes a component-owned volume multiplier and reapplies saved audio settings.</summary>
    public void ClearSourceMultiplier(AudioSource source, Object owner)
    {
        if (source == null || owner == null) return;
        Dictionary<int, float> owners;
        if (sourceMultipliers.TryGetValue(source, out owners))
        {
            owners.Remove(owner.GetInstanceID());
            if (owners.Count == 0) sourceMultipliers.Remove(source);
        }
        ApplySource(source);
    }

    void ApplySource(AudioSource source)
    {
        float baseVolume;
        if (!authored.TryGetValue(source, out baseVolume))
        {
            baseVolume = source.volume;
            authored[source] = baseVolume;
        }

        float role = IsAmbient(source) ? GameSettings.AmbientVolume : GameSettings.SfxVolume;
        float multiplier = 1f;
        Dictionary<int, float> owners;
        if (sourceMultipliers.TryGetValue(source, out owners))
        {
            foreach (KeyValuePair<int, float> ownerMultiplier in owners)
                multiplier = Mathf.Min(multiplier, ownerMultiplier.Value);
        }
        source.volume = Mathf.Clamp01(baseVolume * role * multiplier);
    }
}
