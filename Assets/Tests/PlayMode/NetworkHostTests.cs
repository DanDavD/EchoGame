using System.Collections;
using System.Linq;
using EchoProtocol.Altar;
using EchoProtocol.Extraction;
using EchoProtocol.Level;
using EchoProtocol.Networking.Core;
using EchoProtocol.Relics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// Hosts a real local server (Tugboat over loopback, no Steam) in the catacombs and checks what the network spawns:
    /// the local player at a level spawn point, the server-authoritative monster and the relic, and a clean
    /// disconnect / re-host cycle. This is the guard against losing multiplayer again.
    /// </summary>
    public class NetworkHostTests
    {
        private const string SceneName = "Dungeon_Main";
        private ErrorCollector _errors;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _errors = new ErrorCollector();
            Debug.Log("[TEST] start " + NUnit.Framework.TestContext.CurrentContext.Test.Name);
            yield return NetTestUtil.LoadScene(SceneName);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return NetTestUtil.DestroyRig();
            var errors = _errors.Errors.ToList();
            _errors.Dispose();
            Assert.IsEmpty(errors, "errors were logged during the test:\n" + string.Join("\n", errors));
        }

        private static IEnumerator HostAndWaitForPlayer()
        {
            NetworkBootstrap bootstrap = NetTestUtil.FindBootstrap();
            Assert.IsNotNull(bootstrap, "network rig");
            Assert.IsTrue(bootstrap.StartLocalHost(), "StartLocalHost should start the server and the client");

            yield return NetTestUtil.WaitUntil(() => bootstrap.IsHosting && bootstrap.IsClientRunning, "server and client started");
            yield return NetTestUtil.WaitUntil(() => NetTestUtil.FindLocalPlayer() != null, "the local player to spawn");
        }

        [UnityTest]
        public IEnumerator Host_SpawnsExactlyOnePlayer_AtALevelSpawnPoint()
        {
            yield return HostAndWaitForPlayer();

            var players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
            Assert.AreEqual(1, players.Length, "one player per connection (a scene copy would make two)");

            PlayerController player = NetTestUtil.FindLocalPlayer();
            Assert.IsTrue(SpawnRegistry.Get(SpawnKind.Player).Any(p => Vector3.Distance(p.transform.position, player.transform.position) < 1f),
                $"the player should spawn at a level spawn point, but is at {player.transform.position}");

            Camera camera = player.GetComponentInChildren<Camera>(true);
            Assert.IsTrue(camera != null && camera.enabled, "the owner's first-person camera must be active");
        }

        [UnityTest]
        public IEnumerator Host_SpawnsTheMonster_OnTheNavMesh()
        {
            yield return HostAndWaitForPlayer();
            yield return NetTestUtil.WaitUntil(() => Object.FindAnyObjectByType<EnemyBrain>() != null, "the monster to spawn");

            var monster = Object.FindAnyObjectByType<EnemyBrain>();
            Assert.IsTrue(monster.IsSpawned, "the monster must be a spawned network object");
            Assert.IsTrue(monster.GetComponent<NavMeshAgent>().isOnNavMesh, "the monster must stand on the baked NavMesh");

            SpawnPoint spawn = SpawnRegistry.First(SpawnKind.Enemy);
            Assert.Less(Vector3.Distance(spawn.transform.position, monster.transform.position), 3f, "the monster should start at its spawn point");
        }

        [UnityTest]
        public IEnumerator Host_SpawnsTheServerRelic_AtTheFirstShrine()
        {
            yield return HostAndWaitForPlayer();
            yield return NetTestUtil.WaitUntil(() => Object.FindAnyObjectByType<RelicBrain>() != null, "the relic to spawn");

            var relics = Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Include);
            Assert.AreEqual(1, relics.Length, "the server spawns one relic (the rest of the set belongs to the networked altar loop)");
            Assert.IsTrue(relics[0].IsSpawned, "the relic must be a spawned network object, not a local copy");

            SpawnPoint shrine = SpawnRegistry.First(SpawnKind.Relic);
            Assert.Less(Vector3.Distance(shrine.transform.position, relics[0].transform.position), 3f, "the relic should start at the first shrine");
        }

        [UnityTest]
        public IEnumerator Host_CanWinTheMatch_ThroughTheExitPortal()
        {
            yield return HostAndWaitForPlayer();

            var altar = Object.FindAnyObjectByType<AltarReceptacle>();
            var portal = Object.FindAnyObjectByType<EscapePortal>();
            PlayerController player = NetTestUtil.FindLocalPlayer();
            var controller = player.GetComponent<CharacterController>();

            altar.TriggerSymphonyClimax();
            yield return null;
            Assert.IsTrue(portal.IsActive, "completing the altar must unlock the exit");

            controller.enabled = false;
            player.transform.position = portal.transform.position + Vector3.up * 0.5f;
            controller.enabled = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.AreEqual(ExtractionState.Victory, GameExtractionManager.Instance.State);
        }

        [UnityTest]
        public IEnumerator Disconnect_RemovesThePlayer_AndHostingAgainWorks()
        {
            yield return HostAndWaitForPlayer();
            NetworkBootstrap bootstrap = NetTestUtil.FindBootstrap();

            bootstrap.Disconnect();
            yield return NetTestUtil.WaitUntil(() => !bootstrap.IsNetworkActive, "networking to stop");
            yield return NetTestUtil.WaitUntil(() => NetTestUtil.FindLocalPlayer() == null, "the player to despawn");

            Assert.IsTrue(bootstrap.StartLocalHost(), "hosting again after a disconnect");
            yield return NetTestUtil.WaitUntil(() => NetTestUtil.FindLocalPlayer() != null, "the player to spawn again");
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include).Length);
        }
    }
}
