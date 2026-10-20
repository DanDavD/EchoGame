using System.Collections;
using System.Linq;
using EchoProtocol.Networking.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// The multiplayer flow the menus drive, over a local host (no Steam): waiting room, match and leaving, and a join
    /// that fails with a message instead of hanging. The Steam path needs two Steam accounts and is tested by hand.
    /// </summary>
    public class NetworkSessionTests
    {
        private const string SceneName = "Dungeon_Main";
        private ErrorCollector _errors;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _errors = new ErrorCollector();
            Debug.Log("[TEST] start " + TestContext.CurrentContext.Test.Name);
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

        private static NetworkSession FindSession()
        {
            var session = Object.FindAnyObjectByType<NetworkSession>(FindObjectsInactive.Include);
            Assert.IsNotNull(session, "the network rig needs a NetworkSession");
            return session;
        }

        [UnityTest]
        public IEnumerator HostLocal_GoesFromTheWaitingRoomIntoTheMatch_AndLeavesCleanly()
        {
            NetworkSession session = FindSession();
            Assert.AreEqual(SessionState.Offline, session.State);

            session.HostLocal();
            yield return NetTestUtil.WaitUntil(() => session.State == SessionState.WaitingRoom, "the waiting room");
            Assert.IsTrue(session.IsHost);
            Assert.IsNull(NetTestUtil.FindLocalPlayer(), "nobody plays before the host starts");

            session.StartMatch();
            yield return NetTestUtil.WaitUntil(() => session.State == SessionState.InMatch, "the match");

            session.Leave();
            yield return NetTestUtil.WaitUntil(() => session.State == SessionState.Offline && !session.Bootstrap.IsNetworkActive,
                "back to the main menu");
            Assert.IsNull(session.ErrorMessage, "leaving is not an error");
        }

        [UnityTest]
        public IEnumerator JoiningAnAddressWhereNobodyHosts_EndsInAClearError()
        {
            NetworkSession session = FindSession();
            session.ConnectTimeoutSeconds = 3f;

            session.JoinLocal("127.0.0.1");
            Assert.AreEqual(SessionState.Connecting, session.State);
            yield return NetTestUtil.WaitUntil(() => session.State == SessionState.Error, "the join to fail", 15f);

            Assert.IsFalse(string.IsNullOrEmpty(session.ErrorMessage), "the player is told what went wrong");
            Assert.IsFalse(session.Bootstrap.IsNetworkActive, "a failed join leaves nothing running");

            session.DismissError();
            Assert.AreEqual(SessionState.Offline, session.State);
        }

        [UnityTest]
        public IEnumerator CancellingAJoin_ReturnsToTheMenuWithoutAnError()
        {
            NetworkSession session = FindSession();

            session.JoinLocal("127.0.0.1");
            yield return null;
            session.Leave();

            yield return NetTestUtil.WaitUntil(() => !session.Bootstrap.IsNetworkActive, "the connection attempt to stop");
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(SessionState.Offline, session.State);
            Assert.IsNull(session.ErrorMessage);
        }
    }
}
