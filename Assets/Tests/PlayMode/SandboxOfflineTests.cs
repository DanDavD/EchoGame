using System.Collections;
using System.Linq;
using EchoProtocol.Relics;
using EchoProtocol.TeamNoise;
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

        [UnityTest]
        public IEnumerator Sandbox_BuildsItsOwnTeamNoiseDirector()
        {
            yield return NetTestUtil.WaitUntil(() => TeamNoiseDirector.Instance != null, "the offline team noise director");

            TeamNoiseDirector director = TeamNoiseDirector.Instance;
            Assert.IsFalse(director.IsSpawned, "offline, the director is a local object");
            Assert.IsFalse(director.IsAlarmed);

            director.AddNoise(2f);
            yield return null;
            Assert.IsTrue(director.IsAlarmed, "the meter and the alarm work offline too");
            Assert.IsTrue(Object.FindAnyObjectByType<EnemyBrain>().IsAlarmed, "and the alarm sends the monster");
        }

        [UnityTest]
        public IEnumerator AfterACatch_TheMonsterWalksAway_AndLeavesItsVictimAlone()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            var brain = Object.FindAnyObjectByType<EnemyBrain>();
            Assert.IsNotNull(player);
            Assert.IsNotNull(brain);

            // A silent player: the real microphone of the test PC must not call the monster.
            var microphone = player.GetComponent<MicrophoneLoudness>();
            if (microphone != null) microphone.enabled = false;

            int catches = 0;
            brain.OnPlayerCaught += _ => catches++;

            // Put the monster right next to the player.
            var agent = brain.GetComponent<NavMeshAgent>();
            Vector3 victim = player.transform.position;
            agent.Warp(victim + player.transform.forward * 1.2f);
            yield return NetTestUtil.WaitUntil(() => catches > 0, "the monster to catch the player", 5f);

            // Gloating (3 s), then the retreat: sated, deaf, walking away.
            yield return NetTestUtil.WaitUntil(() => brain.IsSated, "the monster to become sated after its gloat", 6f);
            Assert.AreEqual(EnemyState.Idle, brain.State, "a sated monster is not hunting");

            float end = Time.time + 4f;
            while (Time.time < end) yield return null;

            Vector3 offset = brain.transform.position - victim;
            offset.y = 0f;
            Assert.Greater(offset.magnitude, 6f, "it walked away from where it caught the player");
            Assert.AreEqual(1, catches, "the victim is not caught again while it gets up");
        }

        [UnityTest]
        public IEnumerator DestroyingAPlayer_DisablesAndDestroysItsInputCopy()
        {
            // A player's enabled input copy that outlives it makes the Input System throw on every key press
            // (worst with domain reload off, where it survives into the next Play).
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var field = typeof(PlayerController).GetField("_inputActions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // An InputActionAsset (read as its base type: this test assembly does not reference the Input System).
            var copy = field.GetValue(player) as ScriptableObject;
            Assert.IsNotNull(copy);
            Assert.IsTrue((bool)copy.GetType().GetProperty("enabled").GetValue(copy), "the local player's controls are on during play");

            Object.Destroy(player.gameObject);
            yield return null;

            Assert.IsTrue(copy == null, "the player's input copy is destroyed with it");
        }
    }
}
