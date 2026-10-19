using System.Collections;
using System.Linq;
using EchoProtocol.Altar;
using EchoProtocol.Extraction;
using EchoProtocol.Interaction;
using EchoProtocol.Level;
using EchoProtocol.Networking.Core;
using EchoProtocol.Relics;
using EchoProtocol.Rituals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// Hosts a real local server (Tugboat over loopback, no Steam) in the catacombs and checks what the network spawns
    /// and decides: the local player at a level spawn point, the server-authoritative monster and one relic per shrine,
    /// the altar docking, the match ending through the exit portal, and the restart. This is the guard against losing
    /// multiplayer again.
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

        /// <summary>Puts the local player at <paramref name="position"/> so triggers and distances can be tested.</summary>
        private static void MoveNear(PlayerController player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position + Vector3.up * 0.5f;
            controller.enabled = true;
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
        public IEnumerator Host_SpawnsOneRelicAtEachShrine_WithItsPersonality()
        {
            yield return HostAndWaitForPlayer();
            int shrines = SpawnRegistry.Get(SpawnKind.Relic).Count;
            yield return NetTestUtil.WaitUntil(() => Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Include).Length == shrines,
                "a relic at every shrine");

            RelicBrain[] relics = Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Include);
            foreach (RelicBrain relic in relics)
            {
                Assert.IsTrue(relic.IsSpawned, "relics must be spawned network objects, not local copies");
            }

            foreach (SpawnPoint shrine in SpawnRegistry.Get(SpawnKind.Relic))
            {
                RelicBrain atShrine = relics.FirstOrDefault(r => Vector3.Distance(r.transform.position, shrine.transform.position) < 3f);
                Assert.IsNotNull(atShrine, $"no relic at {shrine.name}");
                Assert.AreEqual(shrine.RelicPersonality, atShrine.Personality, $"the relic at {shrine.name} should have its shrine's personality");
            }
        }

        [UnityTest]
        public IEnumerator Host_DocksACarriedRelic_AndTheServerLocksIt()
        {
            yield return HostAndWaitForPlayer();
            yield return NetTestUtil.WaitUntil(() => Object.FindAnyObjectByType<RelicCarryable>() != null, "a relic to spawn");

            var altar = Object.FindAnyObjectByType<AltarReceptacle>();
            PlayerController player = NetTestUtil.FindLocalPlayer();
            RelicCarryable relic = Object.FindObjectsByType<RelicCarryable>(FindObjectsInactive.Include)[0];

            MoveNear(player, relic.transform.position);
            yield return null;
            relic.Interact(player);
            yield return NetTestUtil.WaitUntil(() => relic.IsCarried, "the relic to be picked up");

            MoveNear(player, altar.transform.position + new Vector3(0f, 0f, -1.5f));
            yield return null;
            Assert.IsTrue(altar.CanInteract(player), "a carried relic should be dockable at the altar");

            altar.Interact(player);
            yield return NetTestUtil.WaitUntil(() => altar.FilledSocketsCount == 1, "the relic to dock");

            Assert.IsTrue(relic.IsLockedInAltar, "the server locks a docked relic");
            Assert.IsNull(relic.Carrier, "a docked relic is no longer carried");
            Assert.IsFalse(altar.IsCompleted, "one relic does not complete the altar");
        }

        [UnityTest]
        public IEnumerator Host_CanWinTheMatch_ThroughTheExitPortal()
        {
            yield return HostAndWaitForPlayer();

            var altar = Object.FindAnyObjectByType<AltarReceptacle>();
            var portal = Object.FindAnyObjectByType<EscapePortal>();
            PlayerController player = NetTestUtil.FindLocalPlayer();

            altar.TriggerSymphonyClimax();
            yield return null;
            Assert.IsTrue(portal.IsActive, "completing the altar must unlock the exit");

            MoveNear(player, portal.transform.position);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.AreEqual(ExtractionState.Victory, GameExtractionManager.Instance.State);
        }

        [UnityTest]
        public IEnumerator Host_RestartAfterVictory_ResetsTheMatchAndRespawnsEveryone()
        {
            yield return HostAndWaitForPlayer();

            var altar = Object.FindAnyObjectByType<AltarReceptacle>();
            var portal = Object.FindAnyObjectByType<EscapePortal>();
            var match = GameExtractionManager.Instance;
            PlayerController player = NetTestUtil.FindLocalPlayer();

            altar.TriggerSymphonyClimax();
            yield return null;
            MoveNear(player, portal.transform.position);
            yield return NetTestUtil.WaitUntil(() => match.State == ExtractionState.Victory, "the match to be won");

            match.RestartMatch();
            yield return NetTestUtil.WaitUntil(() => match.State == ExtractionState.ObjectiveInProgress, "the match to restart");

            Assert.IsFalse(portal.IsActive, "the exit closes for the new match");
            Assert.IsFalse(altar.IsCompleted, "the altar starts empty again");
            Assert.AreEqual(0, altar.FilledSocketsCount, "no relic is docked in the new match");

            int shrines = SpawnRegistry.Get(SpawnKind.Relic).Count;
            yield return NetTestUtil.WaitUntil(() => Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Include).Length == shrines,
                "the relics to respawn at the shrines");
            yield return NetTestUtil.WaitUntil(() => SpawnRegistry.Get(SpawnKind.Player)
                .Any(p => Vector3.Distance(p.transform.position, player.transform.position) < 1f),
                "the player to be sent back to a spawn point");
        }

        [UnityTest]
        public IEnumerator Host_SpawnsTheTeamNoiseDirector_AndTooMuchNoiseSendsTheMonster()
        {
            yield return HostAndWaitForPlayer();
            yield return NetTestUtil.WaitUntil(() => TeamNoiseDirector.Instance != null && TeamNoiseDirector.Instance.IsSpawned, "the team noise director to spawn");
            yield return NetTestUtil.WaitUntil(() => Object.FindAnyObjectByType<EnemyBrain>() != null, "the monster to spawn");

            TeamNoiseDirector director = TeamNoiseDirector.Instance;
            var monster = Object.FindAnyObjectByType<EnemyBrain>();
            Assert.IsFalse(director.IsAlarmed, "a match starts quiet");

            director.AddNoise(2f);
            yield return null;

            Assert.IsTrue(director.IsAlarmed, "a full meter raises the alarm");
            Assert.IsTrue(monster.IsAlarmed, "the alarm sends the monster");
            Assert.AreEqual(EnemyState.Hunt, monster.State, "the monster charges");
            Assert.AreEqual(1, Object.FindObjectsByType<TeamNoiseDirector>(FindObjectsInactive.Include).Length, "one director per match");
        }

        [UnityTest]
        public IEnumerator Host_ACryingRelic_StartsTheHumanEchoRitual_AndSilenceFailsIt()
        {
            yield return HostAndWaitForPlayer();
            yield return NetTestUtil.WaitUntil(() => TeamNoiseDirector.Instance != null, "the team noise director to spawn");
            yield return NetTestUtil.WaitUntil(() => Object.FindAnyObjectByType<RelicBrain>() != null, "a relic to spawn");

            TeamNoiseDirector director = TeamNoiseDirector.Instance;
            PlayerController player = NetTestUtil.FindLocalPlayer();
            RelicBrain relic = Object.FindObjectsByType<RelicBrain>(FindObjectsInactive.Include)[0];
            MoveNear(player, relic.transform.position + new Vector3(1.5f, 0f, 0f));
            yield return null;

            relic.StartCrying();
            Assert.IsTrue(relic.IsCrying, "the server makes the relic cry");

            yield return NetTestUtil.WaitUntil(() => director.Phase == RitualPhase.Calling, "the relic to call its rhythm");
            Assert.AreSame(relic, director.RitualRelic);
            CollectionAssert.Contains(director.RitualParticipants.ToList(), player, "a player next to the relic takes part");
            Assert.GreaterOrEqual(director.RitualPattern.Count, 2, "a rhythm has at least two beats");

            yield return NetTestUtil.WaitUntil(() => director.Phase == RitualPhase.Turn, "the player's turn");
            Assert.IsTrue(director.IsLocalTurn, "the local player records their own echo");

            // Nobody speaks in a test: the turn runs out, the round fails and the meter rises.
            float before = director.Fill01;
            yield return NetTestUtil.WaitUntil(() => director.Phase == RitualPhase.Result, "the silent turn to fail", 15f);
            Assert.IsFalse(director.LastRoundSucceeded);
            Assert.Greater(director.Fill01, before, "a failed round adds team noise");
            Assert.IsTrue(relic.IsCrying, "a failed ritual leaves the relic crying");
        }

        [UnityTest]
        public IEnumerator Host_ADockedRelic_StopsCrying()
        {
            yield return HostAndWaitForPlayer();
            yield return NetTestUtil.WaitUntil(() => Object.FindAnyObjectByType<RelicCarryable>() != null, "a relic to spawn");

            var altar = Object.FindAnyObjectByType<AltarReceptacle>();
            PlayerController player = NetTestUtil.FindLocalPlayer();
            RelicCarryable relic = Object.FindObjectsByType<RelicCarryable>(FindObjectsInactive.Include)[0];
            var brain = relic.GetComponent<RelicBrain>();

            MoveNear(player, relic.transform.position);
            yield return null;
            relic.Interact(player);
            yield return NetTestUtil.WaitUntil(() => relic.IsCarried, "the relic to be picked up");

            brain.StartCrying();
            Assert.IsTrue(brain.IsCrying);

            MoveNear(player, altar.transform.position + new Vector3(0f, 0f, -1.5f));
            yield return null;
            altar.Interact(player);
            yield return NetTestUtil.WaitUntil(() => relic.IsLockedInAltar, "the relic to dock");
            yield return NetTestUtil.WaitUntil(() => !brain.IsCrying, "a docked relic falls asleep");
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
