using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnItem : MonoBehaviour
{
	public string kindofItem = "none";
	[Min(0)] public int progressionStage = 0;
	public GameObject pageprefab;
	public GameObject keyprefab;
	
	public void spawnItem(){
		TrySpawnItem(kindofItem);
	}

	/// <summary>Spawns the requested pickup and updates this spawn point only when successful.
	/// keyStageOverride keeps a Key on a specific progression stage even when this point's own
	/// stage differs (used by the saved-layout replay and the no-config fallback); -1 = use this
	/// point's stage.</summary>
	public bool TrySpawnItem(string itemKind, int narrativeIndex = -1, int keyStageOverride = -1){
		GameObject prefab = null;
		if(itemKind == "Key") prefab = keyprefab;
		else if(itemKind == "Page") prefab = pageprefab;
		if(prefab == null){
			Debug.LogWarning("[SpawnItem] Cannot spawn " + itemKind + " because its prefab is unassigned on " + gameObject.name + ".", this);
			return false;
		}
		GameObject spawnedItem = Instantiate(prefab, transform.position, transform.rotation);
		if(itemKind == "Key"){
			KeyTrigger key = spawnedItem.GetComponent<KeyTrigger>();
			if(key != null){
				int stage = keyStageOverride >= 0 ? Mathf.Clamp(keyStageOverride, 0, 2) : Mathf.Clamp(progressionStage, 0, 2);
				string[] titles = {"Classroom Key", "Stairwell Key", "Main Gate Key"};
				string[] clues = {
					"A classroom label is scratched into the key. The first gate is close.",
					"This is the Stairwell Key. The upper floors are no longer safe.",
					"The brass tag reads MAIN ENTRANCE. The monster heard the lock turn."
				};
				key.Configure(stage, titles[stage], clues[stage]);
			}
		}else if(itemKind == "Page"){
			PageTrigger page = spawnedItem.GetComponent<PageTrigger>();
			if(page != null && narrativeIndex >= 0){
				string[] clues = {
					"Night Watch: The bell rang at 9:13, but nobody pulled the cord.",
					"A student writes: Something follows the light, not the footsteps.",
					"The east classroom was locked after the headmaster vanished.",
					"Never stand beneath the bell when the lights flicker.",
					"The main gate key was moved upstairs after the third alarm.",
					"If the bell rings twice, do not turn toward the sound."
				};
				int clueIndex = Mathf.Clamp(narrativeIndex, 0, clues.Length - 1);
				page.Configure(clueIndex, clues[clueIndex]);
			}
		}
		kindofItem = itemKind;
		return true;
	}
}
