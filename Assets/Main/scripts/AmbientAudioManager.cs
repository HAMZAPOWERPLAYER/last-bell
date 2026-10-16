using System.Collections;
using UnityEngine;

/// <summary>
/// Plays random ambient horror audio at randomised intervals - the "occasional far-off sound cue"
/// layer that sits on top of the looping ambient bed.
///
/// Ported from the FiveFloors project's Assets/Scripts/Atmosphere/AmbientAudioManager.cs.
/// It supersedes the level's original randomSounds.cs, which rolled Random.Range(0, 10000) every
/// frame (frame-rate dependent, fired roughly once every 2.8 minutes) and then destroyed itself,
/// so it could only ever fire a single cue per playthrough.
///
/// Attach to a GameObject with an AudioSource. While ambientClips is empty it idles harmlessly.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientAudioManager : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("Creaks, distant footsteps, wind, pipe knocks. Leave empty until real assets exist.")]
    public AudioClip[] ambientClips;

    [Header("Interval (seconds between cues)")]
    public float minInterval = 12f;
    public float maxInterval = 35f;

    [Header("Volume")]
    public float minVolume = 0.3f;
    public float maxVolume = 0.8f;

    [Tooltip("Random delay before the first cue, so it never lands on level start.")]
    public float minStartDelay = 8f;
    public float maxStartDelay = 20f;

    AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.spatialBlend = 0f;   // 2D ambient bed
        _audio.loop = false;
        _audio.playOnAwake = false;
    }

    void Start()
    {
        StartCoroutine(PlayLoop());
    }

    IEnumerator PlayLoop()
    {
        yield return new WaitForSeconds(Random.Range(minStartDelay, maxStartDelay));

        while (true)
        {
            if (ambientClips != null && ambientClips.Length > 0)
            {
                AudioClip clip = ambientClips[Random.Range(0, ambientClips.Length)];
                if (clip == null) { yield return new WaitForSeconds(5f); continue; }
                float cueVolume = Random.Range(minVolume, maxVolume);
                _audio.PlayOneShot(clip, cueVolume);
                yield return new WaitForSeconds(clip.length + Random.Range(minInterval, maxInterval));
            }
            else
            {
                yield return new WaitForSeconds(5f);
            }
        }
    }
}
