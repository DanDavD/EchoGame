using System.IO;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using EchoProtocol.Rituals;

namespace EchoProtocol.EditorTools
{
    /// <summary>
    /// Generates <c>Assets/Prefabs/TeamNoiseDirector.prefab</c> (the team noise meter, the relic rituals, their HUD and
    /// music), registers it as a spawnable network prefab and points the network rig at it, so a networked server can spawn
    /// one per match. Derived data: edit the components, then rebuild.
    /// Menu: Echo Protocol > Build > Team Noise Director Prefab. Headless:
    /// <c>-executeMethod EchoProtocol.EditorTools.TeamNoisePrefabBuilder.BuildAndWire</c>.
    /// </summary>
    public static class TeamNoisePrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/TeamNoiseDirector.prefab";

        /// <summary>Loads the director prefab, generating it first when it does not exist yet.</summary>
        public static NetworkObject EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabPath);
            return existing != null ? existing : BuildPrefab();
        }

        /// <summary>(Re)generates the director prefab and returns its NetworkObject.</summary>
        public static NetworkObject BuildPrefab()
        {
            var root = new GameObject("TeamNoiseDirector");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<TeamNoiseDirector>();
                root.AddComponent<TeamNoiseHud>();
                root.AddComponent<TeamNoiseAudio>();

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TeamNoisePrefabBuilder] Built {PrefabPath}.");
                return prefab.GetComponent<NetworkObject>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Builds the prefab, adds it to the spawnable prefabs and assigns it to the network rig prefab.</summary>
        [MenuItem("Echo Protocol/Build/Team Noise Director Prefab")]
        public static void BuildAndWire()
        {
            NetworkObject prefab = BuildPrefab();
            RegisterSpawnable(prefab);
            AssignToRig(prefab);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds <paramref name="prefab"/> to FishNet's default spawnable prefabs, once.</summary>
        public static void RegisterSpawnable(NetworkObject prefab)
        {
            var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(NetworkRigBuilder.DefaultPrefabObjectsPath);
            if (collection == null || prefab == null)
            {
                Debug.LogError("[TeamNoisePrefabBuilder] DefaultPrefabObjects asset or director prefab missing; cannot register it.");
                return;
            }

            for (int i = 0; i < collection.GetObjectCount(); i++)
            {
                if (collection.GetObject(true, i) == prefab) return;
            }

            collection.AddObject(prefab, true, false);
            EditorUtility.SetDirty(collection);
            Debug.Log("[TeamNoisePrefabBuilder] Registered the director as a spawnable prefab.");
        }

        /// <summary>Points the rig prefab's NetworkBootstrap at <paramref name="prefab"/>.</summary>
        public static void AssignToRig(NetworkObject prefab)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(NetworkRigBuilder.PrefabPath);
            try
            {
                var bootstrap = contents.GetComponent<EchoProtocol.Networking.Core.NetworkBootstrap>();
                if (bootstrap == null)
                {
                    Debug.LogError("[TeamNoisePrefabBuilder] The rig prefab has no NetworkBootstrap.");
                    return;
                }

                var so = new SerializedObject(bootstrap);
                so.FindProperty("_teamNoisePrefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, NetworkRigBuilder.PrefabPath);
                Debug.Log("[TeamNoisePrefabBuilder] The network rig now spawns the team noise director.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
