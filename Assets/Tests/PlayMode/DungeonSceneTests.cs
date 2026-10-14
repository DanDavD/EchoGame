using System.Collections;
using System.Collections.Generic;
using EchoProtocol.Altar;
using EchoProtocol.Extraction;
using EchoProtocol.Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// The static content of the generated catacombs scene, checked without starting the network: every system the match
    /// needs exists and the NavMesh links the start to every objective.
    /// </summary>
    public class DungeonSceneTests
    {
        private const string SceneName = "Dungeon_Main";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Debug.Log("[TEST] start " + TestContext.CurrentContext.Test.Name);
            yield return NetTestUtil.LoadScene(SceneName);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return NetTestUtil.DestroyRig();
        }

        [UnityTest]
        public IEnumerator Scene_HasEverythingTheMatchNeeds()
        {
            Assert.IsNotNull(Object.FindAnyObjectByType<AltarReceptacle>(), "altar");
            Assert.IsNotNull(GameExtractionManager.Instance, "match manager");
            Assert.AreEqual(1, EscapePortal.AllPortals.Count, "exit portal");
            Assert.AreEqual(3, Object.FindAnyObjectByType<AltarReceptacle>().RequiredSocketsCount, "altar sockets");
            Assert.IsNotNull(NetTestUtil.FindBootstrap(), "the network rig must be part of the scene");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_HoldsNoPlayerOrMonsterOfItsOwn()
        {
            // They are spawned by the network at the spawn points; a scene copy would duplicate them.
            Assert.IsNull(Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include), "scene player");
            Assert.IsNull(Object.FindAnyObjectByType<EnemyBrain>(FindObjectsInactive.Include), "scene monster");
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
                ("exit portal approach", PortalApproachPoint(Object.FindAnyObjectByType<EscapePortal>())),
                ("monster spawn", SpawnRegistry.First(SpawnKind.Enemy).transform.position)
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
    }
}
