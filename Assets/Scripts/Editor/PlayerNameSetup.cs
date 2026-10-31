using UnityEditor;
using UnityEngine;
using EchoProtocol.Player;

namespace EchoProtocol.EditorTools
{
    /// <summary>
    /// Adds <see cref="PlayerName"/> to <c>Assets/Prefabs/Player.prefab</c>. A NetworkBehaviour has to be on the prefab
    /// before it is spawned, so it cannot be added at runtime. Idempotent. Menu: Echo Protocol > Build > Add Player Name
    /// To Player. Headless: <c>-executeMethod EchoProtocol.EditorTools.PlayerNameSetup.AddToPlayerPrefab</c>.
    /// </summary>
    public static class PlayerNameSetup
    {
        [MenuItem("Echo Protocol/Build/Add Player Name To Player")]
        public static void AddToPlayerPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(NetworkRigBuilder.PlayerPrefabPath);
            try
            {
                if (contents.GetComponent<PlayerName>() != null)
                {
                    Debug.Log("[PlayerNameSetup] The player prefab already has a player name.");
                    return;
                }

                contents.AddComponent<PlayerName>();
                PrefabUtility.SaveAsPrefabAsset(contents, NetworkRigBuilder.PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[PlayerNameSetup] Added the player name to the player prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
