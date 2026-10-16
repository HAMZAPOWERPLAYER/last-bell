using System.Collections.Generic;
using UnityEngine;

/// <summary>Connects a key pickup to the manager and carries its story/progression identity.</summary>
public class KeyTrigger : MonoBehaviour
{
    public int progressionStage;
    public string keyTitle = "School Key";
    [TextArea] public string storyClue;

    GameManager gameManager;
    readonly HashSet<Collider> playerColliders = new HashSet<Collider>();

    void Start()
    {
        GameObject managerObject = GameObject.FindGameObjectWithTag("GameManager");
        if (managerObject != null) gameManager = managerObject.GetComponent<GameManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        Diag.Log("KeyTrigger OnTriggerEnter: " + other.name + " tag=" + other.tag);
        if (!other.CompareTag("Player") || gameManager == null || !playerColliders.Add(other)) return;
        Diag.Log("KeyTrigger: Player entered, setting currentKey");
        gameManager.isOnKeyTrigger_ = true;
        gameManager.currentKey = gameObject;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || !playerColliders.Remove(other) || playerColliders.Count > 0 || gameManager == null) return;
        if (gameManager.currentKey == gameObject) gameManager.currentKey = null;
        gameManager.isOnKeyTrigger_ = gameManager.currentKey != null;
    }

    /// <summary>Assigns narrative identity before this spawned key enters play.</summary>
    public void Configure(int stage, string title, string clue)
    {
        progressionStage = Mathf.Clamp(stage, 0, 2);
        keyTitle = title;
        storyClue = clue;
    }
}
