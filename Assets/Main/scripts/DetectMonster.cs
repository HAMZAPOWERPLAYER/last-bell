using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Marks the monster as lit by the flashlight while it stands inside this trigger volume.</summary>
public class DetectMonster : MonoBehaviour
{
	// Cached Monster component. BUGFIX: the original cached the GameObject and called
	// GetComponent<Monster>() on every trigger, with no null check, so a missing/renamed tag or a
	// destroyed monster threw a NullReferenceException from inside the physics callback.
	Monster monster;

	void Start()
	{
		if(ResolveMonster() == null)
			Debug.LogWarning("[DetectMonster] No '" + MonsterTag + "' GameObject was found; flashlight detection is inactive.", this);
	}

	/// <summary>Re-resolves the monster lazily so a late-spawned or re-enabled monster is still found.</summary>
	Monster ResolveMonster()
	{
		if(monster != null) return monster;
		GameObject monsterObject = GameObject.FindGameObjectWithTag(MonsterTag);
		if(monsterObject != null) monster = monsterObject.GetComponent<Monster>();
		return monster;
	}

	public void OnTriggerEnter(Collider col)
	{
		if(col == null || !col.CompareTag(MonsterTag)) return;
		Monster target = ResolveMonster();
		if(target != null) target.isOnFlashLightTrigger = true;
	}

	public void OnTriggerExit(Collider col)
	{
		if(col == null || !col.CompareTag(MonsterTag)) return;
		Monster target = ResolveMonster();
		if(target != null) target.isOnFlashLightTrigger = false;
	}

	const string MonsterTag = "Monster";
}
