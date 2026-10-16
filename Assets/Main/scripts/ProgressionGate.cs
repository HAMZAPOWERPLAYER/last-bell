using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Locks a corridor door until the player has collected its required number of keys.</summary>
public class ProgressionGate : MonoBehaviour
{
    [Header("Gate")]
    public string gateName = "Classroom";
    [Min(1)] public int requiredKeys = 1;
    public Transform doorVisual;
    public Collider doorBlocker;
    [Range(45f, 135f)] public float openAngle = 95f;
    [Min(0.1f)] public float openDuration = 0.8f;

    GameManager gameManager;
    readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
    Quaternion closedRotation;
    bool closedRotationCaptured;
    bool isOpen;

    /// <summary>True once this gate has been unlocked and opened (normal play or a restored save).</summary>
    public bool IsOpen { get { return isOpen; } }

    void Start()
    {
        gameManager = GameObject.FindGameObjectWithTag("GameManager")?.GetComponent<GameManager>();
        EnsureInitialized();
        if (doorBlocker != null) doorBlocker.enabled = !isOpen;
    }

    /// <summary>Resolves the serialized visual/blocker fallbacks and captures the closed rotation
    /// exactly once, so OpenInstant() is safe even before Start() has run (save restore).</summary>
    void EnsureInitialized()
    {
        if (doorVisual == null) doorVisual = transform.parent;
        if (doorBlocker == null && doorVisual != null) doorBlocker = doorVisual.GetComponent<Collider>();
        if (doorVisual != null && !closedRotationCaptured)
        {
            closedRotation = doorVisual.localRotation;
            closedRotationCaptured = true;
        }
    }

    /// <summary>Opens the gate immediately without the swing animation or the "unlocked" toast;
    /// used when a saved run is restored and its keys were already spent on this gate.</summary>
    public void OpenInstant()
    {
        if (isOpen) return;
        EnsureInitialized();
        isOpen = true;
        if (doorBlocker != null) doorBlocker.enabled = false;
        if (doorVisual != null) doorVisual.localRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        if (gameManager != null && gameManager.currentGate == this) gameManager.SetCurrentGate(null);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isOpen || !other.CompareTag("Player") || gameManager == null || !playerColliders.Add(other)) return;
        gameManager.SetCurrentGate(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || !playerColliders.Remove(other) || playerColliders.Count > 0) return;
        if (gameManager != null && gameManager.currentGate == this)
            gameManager.SetCurrentGate(null);
    }

    /// <summary>Attempts to unlock and open the door using the keys collected so far.</summary>
    public bool TryOpen(int keysFound)
    {
        if (isOpen) return true;
        if (keysFound < requiredKeys)
        {
            int missingKeys = requiredKeys - keysFound;
            string keyWord = missingKeys == 1 ? "key" : "keys";
            if (gameManager != null)
                gameManager.ShowInteractionMessage("Need " + missingKeys + " more " + keyWord + " for " + gateName + ".", 1.7f);
            return false;
        }

        isOpen = true;
        if (doorBlocker != null) doorBlocker.enabled = false;
        if (gameManager != null){
            if (gameManager.currentGate == this) gameManager.SetCurrentGate(null);
            gameManager.OnGateUnlocked(this);
        }
        if (doorVisual != null) StartCoroutine(OpenDoor());
        return true;
    }

    IEnumerator OpenDoor()
    {
        Quaternion startRotation = doorVisual.localRotation;
        Quaternion targetRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, openDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            doorVisual.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        doorVisual.localRotation = targetRotation;
    }
}
