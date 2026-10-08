using UnityEditor;
using UnityEngine;
using FishNet.Managing;
using FishNet.Object;

[InitializeOnLoad]
public static class AutoFixFishNetPrefabs
{
    static AutoFixFishNetPrefabs()
    {
        EditorApplication.delayCall += DoFix;
    }

    private static void DoFix()
    {
        if (SessionState.GetBool("FishNetAutoFixDone", false)) return;

        Debug.Log("<color=cyan>[Antigravity]</color> Auto-arreglando los Prefabs de FishNet...");

        string[] guids = AssetDatabase.FindAssets("t:Prefab Player");
        if (guids.Length == 0) return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        
        NetworkObject netObj = playerPrefab.GetComponent<NetworkObject>();
        if (netObj == null) {
            netObj = playerPrefab.AddComponent<NetworkObject>();
            EditorUtility.SetDirty(playerPrefab);
        }

        var nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm != null && nm.SpawnablePrefabs != null)
        {
            nm.SpawnablePrefabs.AddObject(netObj, true, true);
            EditorUtility.SetDirty(nm.SpawnablePrefabs);
            Debug.Log("<color=cyan>[Antigravity]</color> Player registrado exitosamente en el catálogo de FishNet.");
        }

        PrefabUtility.SavePrefabAsset(playerPrefab);
        AssetDatabase.SaveAssets();

        SessionState.SetBool("FishNetAutoFixDone", true);
        Debug.Log("<color=green>[Antigravity]</color> ¡Problema arreglado! Ya puedes darle a Play.");
    }
}
