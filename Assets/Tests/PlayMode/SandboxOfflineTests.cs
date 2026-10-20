using System.Collections;
using System.Linq;
using EchoProtocol.Relics;
using EchoProtocol.Rituals;
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
        public IEnumerator Sandbox_EchoingTheRhythm_SoothesACryingRelic()
        {
            yield return NetTestUtil.WaitUntil(() => TeamNoiseDirector.Instance != null, "the offline team noise director");
            yield return NetTestUtil.WaitUntil(() => Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Exclude).Length == 3, "three local relics");

            TeamNoiseDirector director = TeamNoiseDirector.Instance;
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            RelicBrain relic = Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Exclude)[0];

            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = relic.transform.position + new Vector3(1.5f, 0.5f, 0f);
            controller.enabled = true;
            yield return null;

            director.AddNoise(0.5f);
            float fillBefore = director.Fill01;

            relic.StartCrying();
            yield return NetTestUtil.WaitUntil(() => director.Phase == RitualPhase.Turn && director.IsLocalTurn, "the player's turn to echo", 10f);

            // Say "ba" on every beat, 0.2 s after the turn starts, each syllable 0.12 s long.
            var pattern = director.RitualPattern.ToArray();
            director.VoiceLevelOverride = () =>
            {
                float t = director.PhaseElapsed - 0.2f;
                foreach (float beat in pattern)
                {
                    if (t >= beat && t < beat + 0.12f) return 0.7f;
                }
                return 0f;
            };

            yield return NetTestUtil.WaitUntil(() => director.Phase == RitualPhase.Result, "the echo to be judged", 10f);
            director.VoiceLevelOverride = null;

            Assert.IsTrue(director.LastRoundSucceeded, "the same rhythm, in any voice, is a right echo");
            Assert.IsFalse(relic.IsCrying, "a right echo soothes the relic");
            Assert.Less(director.Fill01, fillBefore, "soothing a relic takes team noise off");
        }
    }
}
