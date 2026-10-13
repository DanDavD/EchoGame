using System.Collections;
using System.Collections.Generic;
using EchoProtocol.Altar;
using EchoProtocol.Extraction;
using EchoProtocol.Level;
using EchoProtocol.Relics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// Loads the generated catacombs scene and checks that a full match is physically possible:
    /// everything the match flow needs exists, and the NavMesh links the start to every objective.
    /// </summary>
    public class DungeonSceneTests
    {
        private const string SceneName = "Dungeon_Main";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            // Let Awake/Start run (relics are spawned in GameExtractionManager.Start) and physics settle.
            for (int i = 0; i < 5; i++) yield return null;
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Scene_HasEverythingTheMatchNeeds()
        {
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerController>(), "player");
            Assert.IsNotNull(Object.FindAnyObjectByType<AltarReceptacle>(), "altar");
            Assert.IsNotNull(GameExtractionManager.Instance, "match manager");
            Assert.AreEqual(1, EscapePortal.AllPortals.Count, "exit portal");
            Assert.AreEqual(3, Object.FindAnyObjectByType<AltarReceptacle>().RequiredSocketsCount, "altar sockets");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnPoints_AreRegisteredForEveryKind()
        {
            Assert.GreaterOrEqual(SpawnRegistry.Get(SpawnKind.Player).Count, 2, "player spawns (co-op needs two)");
            Assert.AreEqual(3, SpawnRegistry.Get(SpawnKind.Relic).Count, "relic spawns");
            Assert.GreaterOrEqual(SpawnRegistry.Get(SpawnKind.Enemy).Count, 1, "enemy spawn");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Relics_AppearAtTheirShrines()
        {
            var relics = Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Exclude);
            Assert.AreEqual(3, relics.Length, "relic count");

            foreach (SpawnPoint shrine in SpawnRegistry.Get(SpawnKind.Relic))
            {
                bool found = false;
                foreach (RelicBrain relic in relics)
                {
                    if (Vector3.Distance(relic.transform.position, shrine.transform.position) < 1.5f) found = true;
                }
                Assert.IsTrue(found, $"no relic near {shrine.name}");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Monster_StandsOnTheNavMesh()
        {
            var agent = Object.FindAnyObjectByType<EnemyBrain>().GetComponent<NavMeshAgent>();
            Assert.IsTrue(agent.isOnNavMesh, "the monster must be placed on the baked NavMesh");
            yield return null;
        }

        // The portal is built against a wall and faces into the room. Its trigger is not solid, so a player simply walks
        // in; the NavMesh itself stops one agent radius short of the frame, so test the spot just in front of it.
        private static Vector3 PortalApproachPoint(EscapePortal portal)
        {
            Vector3 front = portal.transform.position + portal.transform.forward * 1.5f;
            front.y = 0f;
            return front;
        }

        [UnityTest]
        public IEnumerator NavMesh_ConnectsStartToEveryObjective()
        {
            Vector3 start = SpawnRegistry.First(SpawnKind.Player).transform.position;
            var targets = new List<(string label, Vector3 position)>
            {
                ("altar", Object.FindAnyObjectByType<AltarReceptacle>().transform.position),
                ("exit portal approach", PortalApproachPoint(Object.FindAnyObjectByType<EscapePortal>()))
            };
            foreach (SpawnPoint shrine in SpawnRegistry.Get(SpawnKind.Relic)) targets.Add((shrine.name, shrine.transform.position));

            Assert.IsTrue(NavMesh.SamplePosition(start, out NavMeshHit from, 4f, NavMesh.AllAreas), "player start is off the NavMesh");

            var problems = new List<string>();
            foreach (var target in targets)
            {
                if (!NavMesh.SamplePosition(target.position, out NavMeshHit to, 6f, NavMesh.AllAreas))
                {
                    problems.Add($"{target.label} at {target.position} is off the NavMesh");
                    continue;
                }

                var path = new NavMeshPath();
                NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
                if (path.status != NavMeshPathStatus.PathComplete)
                {
                    Vector3 end = path.corners.Length > 0 ? path.corners[path.corners.Length - 1] : from.position;
                    problems.Add($"{target.label}: {path.status} from {from.position} to {to.position} (target at {target.position}), path ends at {end}");
                }
            }

            Assert.IsEmpty(problems, "unreachable objectives:\n" + string.Join("\n", problems));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Match_CanBeWon_ThroughTheExitPortal()
        {
            var altar = Object.FindAnyObjectByType<AltarReceptacle>();
            var portal = Object.FindAnyObjectByType<EscapePortal>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var controller = player.GetComponent<CharacterController>();

            Assert.IsFalse(portal.IsActive, "the exit must start locked");

            altar.TriggerSymphonyClimax();
            yield return null;

            Assert.IsTrue(portal.IsActive, "completing the altar must unlock the exit");
            Assert.AreEqual(ExtractionState.ExtractionActive, GameExtractionManager.Instance.State);

            // Teleport the player into the portal trigger.
            controller.enabled = false;
            player.transform.position = portal.transform.position + Vector3.up * 0.5f;
            controller.enabled = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.AreEqual(ExtractionState.Victory, GameExtractionManager.Instance.State);
        }
    }
}
