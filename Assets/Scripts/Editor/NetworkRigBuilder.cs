using System.IO;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
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
                // NetworkBootstrap requires the NetworkManager, ServerManager, ClientManager, Steam and PlayerSpawner components.
                root.AddComponent<NetworkManager>();
                var transportManager = root.AddComponent<TransportManager>();
                var tugboat = root.AddComponent<Tugboat>();
                transportManager.Transport = tugboat;
                root.AddComponent<NetworkBootstrap>();

                Assign(root.GetComponent<NetworkManager>(), "_spawnablePrefabs", spawnables);
                Assign(root.GetComponent<PlayerSpawner>(), "_playerPrefab", player);
                Assign(root.GetComponent<NetworkBootstrap>(), "_enemyPrefab", enemy);
                Assign(root.GetComponent<NetworkBootstrap>(), "_relicPrefab", relic);
                Assign(root.GetComponent<NetworkBootstrap>(), "_teamNoisePrefab", TeamNoisePrefabBuilder.EnsurePrefab());

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
