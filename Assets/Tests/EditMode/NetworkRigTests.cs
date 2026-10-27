using System.Collections.Generic;
using System.Linq;
using EchoProtocol.EditorTools;
using EchoProtocol.Networking.Core;

using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// Guards the multiplayer wiring. The rig was once lost silently when a scene was regenerated; these tests make that
    /// impossible to repeat: the prefab must be complete and every scene in the build must contain it.
    /// </summary>
    public class NetworkRigTests
    {
        // Scenes that are deliberately offline (no NetworkManager). Any other scene in the build must carry the rig.
        private static readonly HashSet<string> OfflineScenes = new HashSet<string>
        {
            "Assets/Scenes/Sandbox_Test.unity"
        };

        private static GameObject LoadRig()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkRigBuilder.PrefabPath);
            Assert.IsNotNull(rig, $"{NetworkRigBuilder.PrefabPath} is missing. Run Echo Protocol > Build > Network Rig Prefab.");
            return rig;
        }

        [Test]
        public void RigPrefab_HasEveryNetworkComponent()
        {
            GameObject rig = LoadRig();

            Assert.IsNotNull(rig.GetComponent<NetworkManager>(), nameof(NetworkManager));
            Assert.IsNotNull(rig.GetComponent<FishNet.Managing.Server.ServerManager>(), "ServerManager");
            Assert.IsNotNull(rig.GetComponent<FishNet.Managing.Client.ClientManager>(), "ClientManager");
            Assert.IsNotNull(rig.GetComponent<MatchPlayerSpawner>(), nameof(MatchPlayerSpawner));
            Assert.IsNotNull(rig.GetComponent<NetworkBootstrap>(), nameof(NetworkBootstrap));
            Assert.IsNotNull(rig.GetComponent<EchoProtocol.Networking.Steam.SteamLobbyManager>(), "SteamLobbyManager");
            Assert.IsNotNull(rig.GetComponent<NetworkSession>(), nameof(NetworkSession));
            Assert.IsNotNull(rig.GetComponent<EchoProtocol.Networking.UI.LobbyMenuUI>(), "LobbyMenuUI");
        }

        [Test]
        public void RigPrefab_CarriesTugboatAndSteam_ThroughMultipass()
        {
            GameObject rig = LoadRig();

            // FishNet initialises only the transport it starts with; Multipass is what makes both usable at runtime.
            var multipass = rig.GetComponent<Multipass>();
            Assert.IsNotNull(multipass, "Multipass must be the rig's transport");
            Assert.AreSame(multipass, rig.GetComponent<TransportManager>().Transport);
            Assert.IsFalse(multipass.GlobalServerActions, "the server must start on one transport at a time");

            var tugboat = rig.GetComponent<Tugboat>();
            var steam = rig.GetComponent<FishySteamworks.FishySteamworks>();
            Assert.IsNotNull(tugboat, "Tugboat must be on the rig so local hosting works without Steam");
            Assert.IsNotNull(steam, "FishySteamworks must be on the rig so Steam lobbies can connect");
            CollectionAssert.AreEquivalent(new FishNet.Transporting.Transport[] { tugboat, steam }, multipass.Transports.ToArray());

            Assert.IsTrue(new SerializedObject(steam).FindProperty("_peerToPeer").boolValue,
                "a match hosted by a player's Steam client is peer to peer, not a dedicated game server");
        }

        [Test]
        public void RigPrefab_ReferencesThePlayerEnemyAndRelicPrefabs()
        {
            GameObject rig = LoadRig();

            var player = AssetDatabase.LoadAssetAtPath<NetworkObject>(NetworkRigBuilder.PlayerPrefabPath);
            var enemy = AssetDatabase.LoadAssetAtPath<NetworkObject>(NetworkRigBuilder.EnemyPrefabPath);
            var relic = AssetDatabase.LoadAssetAtPath<NetworkObject>(NetworkRigBuilder.RelicPrefabPath);

            var spawner = new SerializedObject(rig.GetComponent<MatchPlayerSpawner>());
            Assert.AreSame(player, spawner.FindProperty("_playerPrefab").objectReferenceValue, "player prefab");

            var bootstrap = new SerializedObject(rig.GetComponent<NetworkBootstrap>());
            Assert.AreSame(enemy, bootstrap.FindProperty("_enemyPrefab").objectReferenceValue, "enemy prefab");
            Assert.AreSame(relic, bootstrap.FindProperty("_relicPrefab").objectReferenceValue, "relic prefab");

            var teamNoise = AssetDatabase.LoadAssetAtPath<NetworkObject>(TeamNoisePrefabBuilder.PrefabPath);
            Assert.IsNotNull(teamNoise, "the TeamNoiseDirector prefab must exist (Echo Protocol > Build > Team Noise Director Prefab)");
            Assert.AreSame(teamNoise, bootstrap.FindProperty("_teamNoisePrefab").objectReferenceValue, "team noise director prefab");
        }

        [Test]
        public void TeamNoisePrefab_CarriesTheDirectorItsHudAndItsMusic()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TeamNoisePrefabBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "the TeamNoiseDirector prefab must exist");
            Assert.IsNotNull(prefab.GetComponent<NetworkObject>(), "it is spawned over the network");
            Assert.IsNotNull(prefab.GetComponent<EchoProtocol.TeamNoise.TeamNoiseDirector>(), "director");
            Assert.IsNotNull(prefab.GetComponent<EchoProtocol.TeamNoise.TeamNoiseHud>(), "HUD");
            Assert.IsNotNull(prefab.GetComponent<EchoProtocol.TeamNoise.TeamNoiseAudio>(), "music");
        }

        [Test]
        public void RigPrefab_RegistersPlayerEnemyAndRelicAsSpawnable()
        {
            GameObject rig = LoadRig();
            PrefabObjects spawnables = new SerializedObject(rig.GetComponent<NetworkManager>())
                .FindProperty("_spawnablePrefabs").objectReferenceValue as PrefabObjects;
            Assert.IsNotNull(spawnables, "the NetworkManager needs a spawnable prefab list");

            var registered = new List<NetworkObject>();
            for (int i = 0; i < spawnables.GetObjectCount(); i++) registered.Add(spawnables.GetObject(true, i));

            foreach (string path in new[] { NetworkRigBuilder.PlayerPrefabPath, NetworkRigBuilder.EnemyPrefabPath, NetworkRigBuilder.RelicPrefabPath, TeamNoisePrefabBuilder.PrefabPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
                Assert.IsTrue(registered.Contains(prefab), $"{path} is not in the spawnable prefab list: FishNet could not spawn it");
            }
        }

        [Test]
        public void EverySceneInTheBuild_IsEitherOfflineOrCarriesTheRig()
        {
            var problems = new List<string>();
            var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToList();
            Assert.IsNotEmpty(enabledScenes, "no scenes in Build Settings");

            foreach (EditorBuildSettingsScene entry in enabledScenes)
            {
                if (OfflineScenes.Contains(entry.path)) continue;

                Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                try
                {
                    int rigs = scene.GetRootGameObjects().Count(go => go.GetComponentInChildren<NetworkBootstrap>(true) != null);
                    if (rigs != 1) problems.Add($"{entry.path} has {rigs} network rig(s), expected exactly 1");
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void NetworkedLevel_HoldsNoPlayerOrMonsterOfItsOwn()
        {
            Scene scene = EditorSceneManager.OpenScene(DungeonBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.IsFalse(roots.Any(go => go.GetComponentInChildren<PlayerController>(true) != null),
                    "a scene Player would duplicate the one MatchPlayerSpawner creates");
                Assert.IsFalse(roots.Any(go => go.GetComponentInChildren<EnemyBrain>(true) != null),
                    "a scene monster would duplicate the one the server spawns");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Installer_AddsTheRig_ToASceneThatLostIt_AndIsIdempotent()
        {
            // Single mode: Unity refuses to add a scene next to an unsaved untitled one, and a lone scene cannot be closed.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsFalse(NetworkRigInstaller.HasRig(scene), "a new scene starts without a rig");

            GameObject first = NetworkRigInstaller.Ensure(scene);
            GameObject second = NetworkRigInstaller.Ensure(scene);

            Assert.IsNotNull(first, "the installer must create the rig");
            Assert.AreSame(first, second, "calling the installer twice must not duplicate the rig");
            Assert.IsTrue(NetworkRigInstaller.HasRig(scene));
        }
    }
}
