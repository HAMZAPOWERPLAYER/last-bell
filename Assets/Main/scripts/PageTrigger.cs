using System.Collections.Generic;
using UnityEngine;

/// <summary>Connects an optional story-page pickup to the manager and its clue text.</summary>
public class PageTrigger : MonoBehaviour
{
    public int clueIndex;
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
        if (!other.CompareTag("Player") || gameManager == null || !playerColliders.Add(other)) return;
        gameManager.isOnPageTrigger_ = true;
        gameManager.currentPage = gameObject;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || !playerColliders.Remove(other) || playerColliders.Count > 0 || gameManager == null) return;
        if (gameManager.currentPage == gameObject) gameManager.currentPage = null;
        gameManager.isOnPageTrigger_ = gameManager.currentPage != null;
    }

    /// <summary>Assigns the lore fragment before this page enters play.</summary>
    public void Configure(int index, string clue)
    {
        clueIndex = Mathf.Max(0, index);
        storyClue = clue;
    }
}
