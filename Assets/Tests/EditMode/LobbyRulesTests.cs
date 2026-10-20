using EchoProtocol.Networking.Steam;
using NUnit.Framework;
using Steamworks;

namespace EchoProtocol.Tests
{
    /// <summary>Which Steam lobbies can be joined, and the message a player sees when one cannot.</summary>
    public class LobbyRulesTests
    {
        private const string Version = "0.6.0";

        private static string Check(string game = LobbyRules.GameId, string version = Version,
            string state = LobbyRules.StateWaiting, string host = "76561198000000001")
        {
            return LobbyRules.CheckLobby(game, version, state, host, Version);
        }

        [Test]
        public void AWaitingEchoLobby_WithTheSameVersion_CanBeJoined()
        {
            Assert.IsNull(Check());
        }

        [Test]
        public void ALobbyOfAnotherGame_IsRefused()
        {
            // With the Spacewar test AppID every developer's lobbies share one app.
            Assert.IsNotNull(Check(game: "SomeOtherSpacewarGame"));
            Assert.IsNotNull(Check(game: null));
            Assert.IsFalse(LobbyRules.IsEchoLobby("SomeOtherSpacewarGame"));
            Assert.IsTrue(LobbyRules.IsEchoLobby(LobbyRules.GameId));
        }

        [Test]
        public void ADifferentBuild_IsRefused_AndTheMessageNamesBothVersions()
        {
            string problem = Check(version: "0.5.9");
            StringAssert.Contains("0.5.9", problem);
            StringAssert.Contains(Version, problem);
        }

        [Test]
        public void AMatchInProgress_IsRefused()
        {
            Assert.IsNotNull(Check(state: LobbyRules.StateInMatch));
        }

        [Test]
        public void ALobbyWithoutAHost_IsRefused()
        {
            Assert.IsNotNull(Check(host: ""));
            Assert.IsNotNull(Check(host: null));
        }

        [Test]
        public void SteamEnterResponses_BecomeMessages_ExceptSuccess()
        {
            Assert.IsNull(LobbyRules.EnterFailure(EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess));
            StringAssert.Contains("full", LobbyRules.EnterFailure(EChatRoomEnterResponse.k_EChatRoomEnterResponseFull));
            StringAssert.Contains("no longer exists", LobbyRules.EnterFailure(EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist));
            Assert.IsNotNull(LobbyRules.EnterFailure(EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned));
            Assert.IsNotNull(LobbyRules.EnterFailure(EChatRoomEnterResponse.k_EChatRoomEnterResponseError));
            Assert.IsNotNull(LobbyRules.EnterFailure((EChatRoomEnterResponse)999), "an unknown response still explains itself");
        }
    }
}
