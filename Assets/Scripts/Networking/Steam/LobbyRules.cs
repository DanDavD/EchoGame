using Steamworks;

namespace EchoProtocol.Networking.Steam
{
    /// <summary>
    /// Pure rules for Echo Protocol's Steam lobbies: the lobby data keys, and whether a lobby can be joined. Every
    /// refusal comes with a sentence the player can act on. No Steam calls here, so the rules are unit tested.
    /// </summary>
    public static class LobbyRules
    {
        public const string GameKey = "game";
        public const string GameId = "EchoProtocol";
        public const string VersionKey = "version";
        public const string StateKey = "state";
        public const string HostKey = "HostAddress";
        public const string NameKey = "name";
        public const string PlayersKey = "players";

        public const string StateWaiting = "waiting";
        public const string StateInMatch = "inmatch";

        /// <summary>
        /// True when the lobby belongs to this game. With the Spacewar test AppID (480) every developer's lobbies share
        /// one app, so lobbies are recognised by their data, not by the app.
        /// </summary>
        public static bool IsEchoLobby(string game) => game == GameId;

        /// <summary>Why Steam refused to put the player in the lobby, or null when it accepted.</summary>
        public static string EnterFailure(EChatRoomEnterResponse response)
        {
            switch (response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess: return null;
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist: return "That game no longer exists. The host may have left.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull: return "That game is full (4 players).";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed: return "You are not allowed to join that game. Ask the host for an invite.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned: return "You were removed from that game.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited: return "Your Steam account is limited and cannot join games.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseCommunityBan: return "A Steam community ban stops you from joining games.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseMemberBlockedYou: return "A player in that game has blocked you.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseYouBlockedMember: return "You have blocked a player in that game.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseRatelimitExceeded: return "Too many join attempts. Wait a moment and try again.";
                default: return "Steam could not join that game. Try again.";
            }
        }

        /// <summary>
        /// Why a lobby the player has entered cannot be played in, or null when it can. Checks that it is an Echo
        /// Protocol lobby, that both games run the same version, that the match has not started and that it has a host.
        /// </summary>
        public static string CheckLobby(string game, string version, string state, string hostAddress, string localVersion)
        {
            if (!IsEchoLobby(game)) return "That lobby is not an Echo Protocol game.";
            if (version != localVersion) return $"The host runs version {Describe(version)} and you run {Describe(localVersion)}. Both players need the same build.";
            if (state == StateInMatch) return "That match has already started. Ask the host to invite you to the next one.";
            if (string.IsNullOrWhiteSpace(hostAddress)) return "That lobby has no host. Ask your friend to host again.";
            return null;
        }

        private static string Describe(string version) => string.IsNullOrEmpty(version) ? "(unknown)" : version;
    }
}
