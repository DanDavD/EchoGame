using UnityEditor;
using UnityEngine;
using EchoProtocol.Voice;

namespace EchoProtocol.EditorTools
{
    /// <summary>
    /// Adds <see cref="ProximityVoice"/> to <c>Assets/Prefabs/Player.prefab</c>. A NetworkBehaviour has to be on the prefab
    /// before it is spawned (every peer builds the player from the same prefab), so it cannot be added at runtime.
    /// Idempotent. Menu: Echo Protocol > Build > Add Proximity Voice To Player. Headless:
    /// <c>-executeMethod EchoProtocol.EditorTools.ProximityVoiceSetup.AddToPlayerPrefab</c>.
    /// </summary>
    public static class ProximityVoiceSetup
    {
        [MenuItem("Echo Protocol/Build/Add Proximity Voice To Player")]
        public static void AddToPlayerPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(NetworkRigBuilder.PlayerPrefabPath);
            try
            {
                if (contents.GetComponent<ProximityVoice>() != null)
                {
                    Debug.Log("[ProximityVoiceSetup] The player prefab already has proximity voice.");
                    return;
                }

                contents.AddComponent<ProximityVoice>();
                PrefabUtility.SaveAsPrefabAsset(contents, NetworkRigBuilder.PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[ProximityVoiceSetup] Added proximity voice to the player prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
