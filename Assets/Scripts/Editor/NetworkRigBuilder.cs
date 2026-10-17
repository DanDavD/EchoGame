using System.IO;

using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEngine;
using EchoProtocol.Networking.Core;

namespace EchoProtocol.EditorTools
{
    /// <summary>
    /// Generates <c>Assets/Prefabs/Networking/NetworkRig.prefab</c>: the single object that wires multiplayer
    /// (FishNet NetworkManager, Tugboat transport, player spawner, Steam lobby support and <see cref="NetworkBootstrap"/>).
    ///
    /// Level builders instantiate this prefab instead of hand-assembling networking, and a test fails if a networked
    /// scene ever loses it. The prefab is derived data: edit this class, rebuild, and every scene that uses the prefab
    /// picks the change up. Menu: Echo Protocol > Build > Network Rig Prefab.
    /// </summary>
    public static class NetworkRigBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Networking/NetworkRig.prefab";
        public const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        public const string EnemyPrefabPath = "Assets/Prefabs/Enemy_Blind.prefab";
        public const string RelicPrefabPath = "Assets/Prefabs/Relic_Test.prefab";
        public const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        /// <summary>Players per match, host included. The Steam lobby uses the same limit.</summary>
        public const int MaxPlayers = 4;

        /// <summary>Loads the rig prefab, generating it first when it does not exist yet.</summary>
        public static GameObject EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return existing != null ? existing : BuildPrefab();
        }

        /// <summary>(Re)generates the rig prefab and returns it, or null when a required prefab is missing.</summary>
        [MenuItem("Echo Protocol/Build/Network Rig Prefab")]
        public static GameObject BuildPrefab()
        {
            var player = AssetDatabase.LoadAssetAtPath<NetworkObject>(PlayerPrefabPath);
            var enemy = AssetDatabase.LoadAssetAtPath<NetworkObject>(EnemyPrefabPath);
            var relic = AssetDatabase.LoadAssetAtPath<NetworkObject>(RelicPrefabPath);
            var spawnables = AssetDatabase.LoadAssetAtPath<PrefabObjects>(DefaultPrefabObjectsPath);
            if (player == null || enemy == null || relic == null)
            {
                Debug.LogError("[NetworkRigBuilder] Missing Player / Enemy_Blind / Relic_Test prefab (each needs a NetworkObject).");
                return null;
            }

            var root = new GameObject("NetworkRig");
            try
            {
                // NetworkBootstrap requires the NetworkManager, ServerManager, ClientManager, Steam and MatchPlayerSpawner components.
                root.AddComponent<NetworkManager>();
                var transportManager = root.AddComponent<TransportManager>();

                // FishNet initialises only the transport it starts with, so Multipass carries both: Tugboat for local and
                // LAN play, FishySteamworks for Steam. The bootstrap starts the server on one of them at a time.
                var multipass = root.AddComponent<Multipass>();
                var tugboat = root.AddComponent<Tugboat>();
                var steam = root.AddComponent<FishySteamworks.FishySteamworks>();
                multipass.GlobalServerActions = false;
                SetTransports(multipass, tugboat, steam);

                // A listen server run by a player's Steam client is peer to peer (the default is a dedicated game server).
                var steamSettings = new SerializedObject(steam);
                steamSettings.FindProperty("_peerToPeer").boolValue = true;
                steamSettings.FindProperty("_maximumClients").intValue = MaxPlayers;
                steamSettings.ApplyModifiedPropertiesWithoutUndo();

                transportManager.Transport = multipass;
                root.AddComponent<NetworkBootstrap>();
                root.AddComponent<NetworkSession>();
                root.AddComponent<EchoProtocol.Networking.UI.LobbyMenuUI>();

                Assign(root.GetComponent<NetworkManager>(), "_spawnablePrefabs", spawnables);
                Assign(root.GetComponent<MatchPlayerSpawner>(), "_playerPrefab", player);
                Assign(root.GetComponent<NetworkBootstrap>(), "_enemyPrefab", enemy);
                Assign(root.GetComponent<NetworkBootstrap>(), "_relicPrefab", relic);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[NetworkRigBuilder] Built {PrefabPath}.");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetTransports(Multipass multipass, params FishNet.Transporting.Transport[] transports)
        {
            var so = new SerializedObject(multipass);
            SerializedProperty list = so.FindProperty("_transports");
            list.ClearArray();
            for (int i = 0; i < transports.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = transports[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[NetworkRigBuilder] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
