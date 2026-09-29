using UnityEngine;

/// <summary>
/// Stairs transition cue. While the player is inside the trigger volume the ambient bed is ducked,
/// and an optional one-shot sting fires once on first entry.
///
/// Deliberately the simplest option that sounds right: a filter sweep would need an AudioMixer
/// group, and per-step creaks would need footstep animation events. A volume duck plus one sting
/// reads as "the air changed" without either.
/// </summary>
[RequireComponent(typeof(Collider))]
public class StairsAmbience : MonoBehaviour
{
    [Tooltip("Ambient source to duck while the player is on the stairs. Leave empty to only play the sting.")]
    public AudioSource ambientSource;

    [Tooltip("Volume multiplier applied while on the stairs.")]
    [Range(0f, 1f)] public float duckedVolume = 0.25f;

    [Tooltip("Seconds to ease in/out of the duck.")]
    public float fadeTime = 0.6f;

    [Tooltip("One-shot played when the player steps onto the stairs.")]
    public AudioClip entrySting;

    [Tooltip("Optional dedicated source for the sting. Falls back to a 2D one-shot.")]
    public AudioSource stingSource;

    [Tooltip("Seconds before the sting may fire again.")]
    public float retriggerCooldown = 20f;

    float baseVolume = -1f;
    float targetMultiplier = 1f;
    float currentMultiplier = 1f;
    float lastStingTime = -999f;
    int occupants;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        if (ambientSource != null) baseVolume = ambientSource.volume;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        occupants++;
        targetMultiplier = duckedVolume;

        if (entrySting != null && Time.time - lastStingTime >= retriggerCooldown)
        {
            lastStingTime = Time.time;
            if (stingSource != null) stingSource.PlayOneShot(entrySting);
            else AudioSource.PlayClipAtPoint(entrySting, transform.position);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        occupants = Mathf.Max(0, occupants - 1);
        if (occupants == 0) targetMultiplier = 1f;
    }

    void Update()
    {
        if (ambientSource == null) return;
        if (baseVolume < 0f) baseVolume = Mathf.Max(0.01f, ambientSource.volume);

        float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
        currentMultiplier = Mathf.MoveTowards(currentMultiplier, targetMultiplier, step);
        ambientSource.volume = baseVolume * currentMultiplier;
    }
}
