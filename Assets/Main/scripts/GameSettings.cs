using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Offline settings store for PART 7. PlayerPrefs only - no network, no accounts, no analytics.
///
/// Owns: look sensitivity, Master/Ambient/SFX volume, and the graphics tier.
/// The graphics tiers deliberately map LOW to disabled real-time shadows and one per-pixel light,
/// with MEDIUM using hard shadows at a shorter distance for mobile devices.
/// </summary>
public static class GameSettings
{
    public const string KeySensitivity = "opt_lookSensitivity";
    public const string KeyMaster = "opt_volumeMaster";
    public const string KeyAmbient = "opt_volumeAmbient";
    public const string KeySfx = "opt_volumeSfx";
    public const string KeyTier = "opt_graphicsTier";
    public const string KeyReduceFlashing = "opt_reduceFlashing";

    /// <summary>PART 3's tuned baseline, in degrees per pixel of drag.</summary>
    public const float BaseSensitivity = 0.15f;

    /// <summary>Slider 0..1 maps to this range of multipliers around the baseline.</summary>
    public const float MinMultiplier = 0.4f;
    public const float MaxMultiplier = 2.0f;

    // ---------------------------------------------------------------- look sensitivity

    /// <summary>Multiplier around BaseSensitivity. 1 = the PART 3 tuning, untouched.</summary>
    public static float LookMultiplier
    {
        get { return Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 1f), MinMultiplier, MaxMultiplier); }
        set { PlayerPrefs.SetFloat(KeySensitivity, Mathf.Clamp(value, MinMultiplier, MaxMultiplier)); }
    }

    /// <summary>Degrees per pixel actually written to the Controller.</summary>
    public static float LookSensitivityDegPerPixel
    {
        get { return BaseSensitivity * LookMultiplier; }
    }

    // ---------------------------------------------------------------- volumes (0..1)

    public static float MasterVolume { get { return PlayerPrefs.GetFloat(KeyMaster, 1f); } set { PlayerPrefs.SetFloat(KeyMaster, Mathf.Clamp01(value)); } }
    public static float AmbientVolume { get { return PlayerPrefs.GetFloat(KeyAmbient, 0.7f); } set { PlayerPrefs.SetFloat(KeyAmbient, Mathf.Clamp01(value)); } }
    public static float SfxVolume { get { return PlayerPrefs.GetFloat(KeySfx, 1f); } set { PlayerPrefs.SetFloat(KeySfx, Mathf.Clamp01(value)); } }

    // ---------------------------------------------------------------- graphics tier

    public enum Tier { Low = 0, Medium = 1, High = 2 }

    public static Tier GraphicsTier
    {
        get { return (Tier)Mathf.Clamp(PlayerPrefs.GetInt(KeyTier, (int)Tier.Medium), 0, 2); }
        set { PlayerPrefs.SetInt(KeyTier, (int)value); }
    }

    // ---------------------------------------------------------------- accessibility

    /// <summary>
    /// 2.0 accessibility option: softens the level's fluorescent-tube flicker (no hard dropouts,
    /// shallower swing) and stops the menu title's hard dips. Off by default.
    /// </summary>
    public static bool ReduceFlashing
    {
        get { return PlayerPrefs.GetInt(KeyReduceFlashing, 0) == 1; }
        set { PlayerPrefs.SetInt(KeyReduceFlashing, value ? 1 : 0); }
    }

    /// <summary>
    /// Applies the tier to QualitySettings. Low disables shadows for low-end devices;
    /// Medium uses a short hard-shadow distance to limit mobile shadow-map cost.
    /// </summary>
    public static void ApplyGraphicsTier()
    {
        switch (GraphicsTier)
        {
            case Tier.Low:
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowDistance = 10f;
                QualitySettings.shadowCascades = 0;
                QualitySettings.pixelLightCount = 1;
                break;
            case Tier.High:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                QualitySettings.shadowDistance = 30f;
                QualitySettings.shadowCascades = 0;
                QualitySettings.pixelLightCount = 4;
                break;
            default: // Medium - avoid realtime shadow-map passes on the mobile default.
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowDistance = 18f;
                QualitySettings.shadowCascades = 0;
                QualitySettings.pixelLightCount = 2;
                break;
        }
    }

    /// <summary>Reads back what is actually applied, for the 7.6 gate check.</summary>
    public static string DescribeApplied()
    {
        return "tier=" + GraphicsTier
             + " shadows=" + QualitySettings.shadows
             + " shadowRes=" + QualitySettings.shadowResolution
             + " shadowDist=" + QualitySettings.shadowDistance
             + " cascades=" + QualitySettings.shadowCascades
             + " pixelLightCount=" + QualitySettings.pixelLightCount;
    }

    // ---------------------------------------------------------------- look / volume plumbing

    /// <summary>Writes the stored look sensitivity onto the player controller.</summary>
    public static void ApplyLook(Controller controller)
    {
        if (controller == null) return;
        controller.cameraSensitivity = LookSensitivityDegPerPixel;
    }

    /// <summary>
    /// Mixer-free volume application (the approved fallback): applies master volume through the
    /// AudioListener and role-specific gains through the scene's AudioVolumeController.
    /// </summary>
    public static void ApplyVolumes()
    {
        AudioListener.volume = MasterVolume;
        if (AudioVolumeController.Instance != null) AudioVolumeController.Instance.Apply();
    }

    /// <summary>Pushes the three volume values into an AudioMixer's exposed parameters.</summary>
    public static void ApplyVolumes(AudioMixer mixer)
    {
        if (mixer == null) return;
        SetMixerVolume(mixer, "MasterVolume", MasterVolume);
        SetMixerVolume(mixer, "AmbientVolume", AmbientVolume);
        SetMixerVolume(mixer, "SFXVolume", SfxVolume);
    }

    static void SetMixerVolume(AudioMixer mixer, string exposedParam, float linear01)
    {
        float db = linear01 <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear01)) * 20f;
        if (mixer.SetFloat(exposedParam, db))
        {
            // stored
        }
        else
        {
            Debug.LogWarning("[GameSettings] mixer has no exposed parameter '" + exposedParam + "'");
        }
    }

    public static void Save() { PlayerPrefs.Save(); }

    /// <summary>Restores every option to its shipped default (2.0 settings screens: RESET).</summary>
    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(KeySensitivity);
        PlayerPrefs.DeleteKey(KeyMaster);
        PlayerPrefs.DeleteKey(KeyAmbient);
        PlayerPrefs.DeleteKey(KeySfx);
        PlayerPrefs.DeleteKey(KeyTier);
        PlayerPrefs.DeleteKey(KeyReduceFlashing);
        PlayerPrefs.Save();
    }

    /// <summary>Applies everything. Call from a bootstrap or each menu's OnEnable.</summary>
    public static void ApplyAll(AudioMixer mixer, Controller controller)
    {
        ApplyGraphicsTier();
        if (mixer != null) ApplyVolumes(mixer); else ApplyVolumes();
        ApplyLook(controller);
    }
}
