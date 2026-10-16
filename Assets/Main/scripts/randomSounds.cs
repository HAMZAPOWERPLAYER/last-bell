using UnityEngine;

/// <summary>Activates one distant sound cue after a randomized, frame-rate-independent delay.</summary>
public class randomSounds : MonoBehaviour
{
    const float MinimumDelaySeconds = 90f;
    const float MaximumDelaySeconds = 240f;

    public GameObject Sounds;

    void Start()
    {
        if (Sounds == null)
        {
            Debug.LogWarning("[randomSounds] Sound object is not assigned.", this);
            enabled = false;
            return;
        }

        Invoke(nameof(ActivateSound), Random.Range(MinimumDelaySeconds, MaximumDelaySeconds));
    }

    void ActivateSound()
    {
        if (Sounds != null) Sounds.SetActive(true);
        Destroy(gameObject);
    }
}
