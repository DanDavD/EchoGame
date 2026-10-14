using System.Collections;
using System.Linq;
using EchoProtocol.Relics;
using FishNet.Managing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// The sandbox arena stays a plain offline scene (no network rig). These tests guarantee that adding multiplayer to
    /// the level did not break the original single-player path.
    /// </summary>
    public class SandboxOfflineTests
    {
        private ErrorCollector _errors;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _errors = new ErrorCollector();
            Debug.Log("[TEST] start " + NUnit.Framework.TestContext.CurrentContext.Test.Name);
            // Start from a networked scene on purpose: the offline guard must also cope with a stale previous state.
            yield return NetTestUtil.LoadScene("Dungeon_Main");
            yield return NetTestUtil.DestroyRig();
            yield return NetTestUtil.LoadScene("Sandbox_Test");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var errors = _errors.Errors.ToList();
            _errors.Dispose();
            Assert.IsEmpty(errors, "errors were logged during the test:\n" + string.Join("\n", errors));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sandbox_RunsWithoutANetworkManager()
        {
            Assert.IsNull(Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include), "the sandbox must stay offline");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sandbox_HasAPlayableLocalPlayer()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "the scene player must stay active offline");

            Camera camera = player.GetComponentInChildren<Camera>(true);
            Assert.IsTrue(camera != null && camera.enabled, "first-person camera");
            Assert.IsNotNull(camera.GetComponent<AudioListener>(), "audio listener");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sandbox_BuildsItsRelicsLocally_AndTheMonsterStandsOnTheNavMesh()
        {
            yield return NetTestUtil.WaitUntil(() => Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Exclude).Length == 3, "three local relics");

            var monster = Object.FindAnyObjectByType<EnemyBrain>();
            Assert.IsNotNull(monster, "monster");
            Assert.IsTrue(monster.GetComponent<NavMeshAgent>().isOnNavMesh, "monster on NavMesh");
        }
    }
}
