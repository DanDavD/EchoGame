using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace EchoProtocol.Networking.Steam
{
    /// <summary>A player in the current Steam lobby.</summary>
    public sealed class LobbyMember
    {
        public CSteamID Id;
        public string Name;
        public bool IsHost;
    }

    /// <summary>A lobby a Steam friend is in. Its data arrives a moment after the list is refreshed.</summary>
    public sealed class FriendLobby
    {
        public CSteamID Lobby;
        public string FriendName;
        public bool DataReady;
        public int Players;
        public int MaxPlayers;

        /// <summary>Why the lobby cannot be joined, or null when it can.</summary>
        public string Problem;

        /// <summary>False for lobbies of other Spacewar (AppID 480) games, which are hidden.</summary>
        public bool IsEcho = true;
    }

    /// <summary>A Steam friend who is online, for the in-game invite list.</summary>
    public sealed class SteamFriend
    {
        public CSteamID Id;
        public string Name;
        public bool PlayingThisGame;
        public bool InThisLobby;
    }

    /// <summary>The outcome of joining a lobby.</summary>
    public sealed class LobbyJoinResult
    {
        public bool Success;
        public string Error;
        public string HostAddress;

        public static LobbyJoinResult Failed(string error) => new LobbyJoinResult { Error = error };
        public static LobbyJoinResult Joined(string hostAddress) => new LobbyJoinResult { Success = true, HostAddress = hostAddress };
    }

    /// <summary>
    /// Steam lobbies for Echo Protocol: create a friends-only lobby, join one (by invite, launch argument or the friends
    /// list), invite friends, and describe friends' lobbies. Steam only: <see cref="Core.NetworkSession"/> decides what
    /// the network does with each result. Lobbies carry the game id, the build version, the match state and the host's
    /// SteamID, which the joining player connects to through FishySteamworks.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        /// <summary>Players per lobby, host included.</summary>
        public const int MaxMembers = 4;

        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyChatUpdate_t> _chatUpdated;
        private Callback<LobbyDataUpdate_t> _dataUpdated;

        private readonly List<FriendLobby> _friendLobbies = new List<FriendLobby>();
        private bool _creating;
        private bool _joining;
        private string _hostAddress;

        /// <summary>Raised when lobby creation ends: success, or false with a message.</summary>
        public event Action<bool, string> LobbyCreated;

        /// <summary>Raised when joining someone else's lobby ends.</summary>
        public event Action<LobbyJoinResult> LobbyJoined;

        /// <summary>Raised when the player accepts a Steam invite (from the overlay or chat) while the game runs.</summary>
        public event Action<CSteamID> InviteAccepted;

        /// <summary>Raised when the host of the lobby this player joined leaves it.</summary>
        public event Action HostLeft;

        /// <summary>Raised when someone enters or leaves the current lobby.</summary>
        public event Action MembersChanged;

        /// <summary>Raised when friends' lobby data arrives.</summary>
        public event Action FriendLobbiesChanged;

        /// <summary>True when Steam is running and the lobby callbacks are registered.</summary>
        public bool Available => _lobbyCreated != null && SteamManager.Initialized;

        public CSteamID CurrentLobbyID { get; private set; }

        public bool InLobby => CurrentLobbyID.IsValid();

        public bool IsLobbyHost => InLobby && SteamMatchmaking.GetLobbyOwner(CurrentLobbyID) == SteamUser.GetSteamID();

        /// <summary>The local player's Steam name, or null without Steam.</summary>
        public string LocalName => Available ? SteamFriends.GetPersonaName() : null;

        public IReadOnlyList<FriendLobby> FriendLobbies => _friendLobbies;

        private void Start()
        {
            if (!SteamManager.Initialized)
            {
                // Steam is optional: local hosting and joining work without it, so this is not an error.
                Debug.LogWarning("Steam is not initialized. Steam lobbies are unavailable; local hosting still works.");
                return;
            }

            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _chatUpdated = Callback<LobbyChatUpdate_t>.Create(OnChatUpdated);
            _dataUpdated = Callback<LobbyDataUpdate_t>.Create(OnDataUpdated);
        }

        // OnDisable, not OnDestroy: SteamManager.Initialized re-creates a SteamManager if it is already gone,
        // which throws "Tried to Initialize the SteamAPI twice" while the game is quitting.
        private void OnDisable()
        {
            LeaveLobby();

            _lobbyCreated?.Dispose();
            _lobbyEntered?.Dispose();
            _joinRequested?.Dispose();
            _chatUpdated?.Dispose();
            _dataUpdated?.Dispose();
            _lobbyCreated = null;
            _lobbyEntered = null;
            _joinRequested = null;
            _chatUpdated = null;
            _dataUpdated = null;
        }

        /// <summary>Creates a friends-only lobby. <see cref="LobbyCreated"/> reports the result.</summary>
        public bool CreateLobby()
        {
            if (!Available || InLobby || _creating) return false;

            _creating = true;
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxMembers);
            return true;
        }

        /// <summary>Joins someone else's lobby. <see cref="LobbyJoined"/> reports the result.</summary>
        public bool JoinLobby(CSteamID lobby)
        {
            if (!Available || InLobby || _joining || !lobby.IsValid()) return false;

            _joining = true;
            SteamMatchmaking.JoinLobby(lobby);
            return true;
        }

        public void LeaveLobby()
        {
            _creating = false;
            _joining = false;
            _hostAddress = null;
            if (!CurrentLobbyID.IsValid()) return;

            if (SteamManager.Initialized) SteamMatchmaking.LeaveLobby(CurrentLobbyID);
            CurrentLobbyID = CSteamID.Nil;
            Debug.Log("[SteamLobby] Left the lobby.");
        }

        /// <summary>Host only: the match has started, so friends see it as in progress and cannot join any more.</summary>
        public void MarkMatchStarted()
        {
            if (!IsLobbyHost) return;
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, LobbyRules.StateKey, LobbyRules.StateInMatch);
            SteamMatchmaking.SetLobbyJoinable(CurrentLobbyID, false);
        }

        /// <summary>Opens the Steam overlay's invite dialog for the current lobby. The overlay does not run in the Editor.</summary>
        public bool OpenInviteOverlay()
        {
            if (!Available || !InLobby) return false;
            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyID);
            return true;
        }

        /// <summary>Sends <paramref name="friend"/> a Steam invite to the current lobby.</summary>
        public bool InviteFriend(CSteamID friend)
        {
            return Available && InLobby && SteamMatchmaking.InviteUserToLobby(CurrentLobbyID, friend);
        }

        /// <summary>Everyone in the current lobby, host first.</summary>
        public List<LobbyMember> GetMembers()
        {
            var members = new List<LobbyMember>();
            if (!Available || !InLobby) return members;

            CSteamID host = SteamMatchmaking.GetLobbyOwner(CurrentLobbyID);
            int count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyID);
            for (int i = 0; i < count; i++)
            {
                CSteamID id = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyID, i);
                members.Add(new LobbyMember { Id = id, Name = SteamFriends.GetFriendPersonaName(id), IsHost = id == host });
            }
            members.Sort((a, b) => b.IsHost.CompareTo(a.IsHost));
            return members;
        }

        /// <summary>Online friends, those already in this game first. Used by the in-game invite list.</summary>
        public List<SteamFriend> GetOnlineFriends()
        {
            var friends = new List<SteamFriend>();
            if (!Available) return friends;

            var inLobby = new HashSet<CSteamID>();
            foreach (LobbyMember member in GetMembers()) inLobby.Add(member.Id);

            AppId_t app = SteamUtils.GetAppID();
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                CSteamID id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (SteamFriends.GetFriendPersonaState(id) == EPersonaState.k_EPersonaStateOffline) continue;

                bool playing = SteamFriends.GetFriendGamePlayed(id, out FriendGameInfo_t game) && game.m_gameID.AppID() == app;
                friends.Add(new SteamFriend
                {
                    Id = id,
                    Name = SteamFriends.GetFriendPersonaName(id),
                    PlayingThisGame = playing,
                    InThisLobby = inLobby.Contains(id)
                });
            }
            friends.Sort((a, b) => a.PlayingThisGame != b.PlayingThisGame
                ? b.PlayingThisGame.CompareTo(a.PlayingThisGame)
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return friends;
        }

        /// <summary>
        /// Rebuilds the list of lobbies friends are in and asks Steam for their data; <see cref="FriendLobbiesChanged"/>
        /// fires as it arrives. Friends-only lobbies are only visible this way, not through a lobby search.
        /// </summary>
        public void RefreshFriendLobbies()
        {
            _friendLobbies.Clear();
            if (!Available) return;

            AppId_t app = SteamUtils.GetAppID();
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                CSteamID friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (!SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t game)) continue;
                if (game.m_gameID.AppID() != app || !game.m_steamIDLobby.IsValid()) continue;
                if (_friendLobbies.Exists(l => l.Lobby == game.m_steamIDLobby)) continue;

                var entry = new FriendLobby { Lobby = game.m_steamIDLobby, FriendName = SteamFriends.GetFriendPersonaName(friend) };
                _friendLobbies.Add(entry);
                if (!SteamMatchmaking.RequestLobbyData(entry.Lobby)) entry.Problem = "Steam could not read this game.";
            }
            FriendLobbiesChanged?.Invoke();
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (!_creating)
            {
                // Cancelled while Steam was creating it: Steam has still put us in the new lobby, so leave it.
                if (callback.m_eResult == EResult.k_EResultOK) SteamMatchmaking.LeaveLobby(new CSteamID(callback.m_ulSteamIDLobby));
                return;
            }
            _creating = false;

            if (callback.m_eResult != EResult.k_EResultOK)
            {
                LobbyCreated?.Invoke(false, $"Steam could not create the lobby ({callback.m_eResult}). Check your connection and try again.");
                return;
            }

            CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, LobbyRules.GameKey, LobbyRules.GameId);
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, LobbyRules.VersionKey, Application.version);
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, LobbyRules.StateKey, LobbyRules.StateWaiting);
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, LobbyRules.HostKey, SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, LobbyRules.NameKey, SteamFriends.GetPersonaName() + "'s game");
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, LobbyRules.PlayersKey, "1");
            Debug.Log($"[SteamLobby] Created lobby {CurrentLobbyID}.");
            LobbyCreated?.Invoke(true, null);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            var lobby = new CSteamID(callback.m_ulSteamIDLobby);

            // The host also receives this for the lobby it just created; that is handled in OnLobbyCreated.
            if (lobby == CurrentLobbyID && IsLobbyHost) return;

            var response = (EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse;
            if (!_joining)
            {
                // A join that was cancelled while Steam processed it: do not stay in that lobby.
                if (response == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) SteamMatchmaking.LeaveLobby(lobby);
                return;
            }
            _joining = false;

            string failure = LobbyRules.EnterFailure(response);
            if (failure != null)
            {
                LobbyJoined?.Invoke(LobbyJoinResult.Failed(failure));
                return;
            }

            CurrentLobbyID = lobby;
            string host = SteamMatchmaking.GetLobbyData(lobby, LobbyRules.HostKey);
            string problem = LobbyRules.CheckLobby(
                SteamMatchmaking.GetLobbyData(lobby, LobbyRules.GameKey),
                SteamMatchmaking.GetLobbyData(lobby, LobbyRules.VersionKey),
                SteamMatchmaking.GetLobbyData(lobby, LobbyRules.StateKey),
                host,
                Application.version);
            if (problem != null)
            {
                LeaveLobby();
                LobbyJoined?.Invoke(LobbyJoinResult.Failed(problem));
                return;
            }

            _hostAddress = host;
            Debug.Log($"[SteamLobby] Joined lobby {lobby}; host {host}.");
            LobbyJoined?.Invoke(LobbyJoinResult.Joined(host));
            MembersChanged?.Invoke();
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t callback)
        {
            Debug.Log($"[SteamLobby] Invite accepted: lobby {callback.m_steamIDLobby} from {callback.m_steamIDFriend}.");
            InviteAccepted?.Invoke(callback.m_steamIDLobby);
        }

        private void OnChatUpdated(LobbyChatUpdate_t callback)
        {
            if (callback.m_ulSteamIDLobby != CurrentLobbyID.m_SteamID) return;

            const uint gone = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft
                                     | EChatMemberStateChange.k_EChatMemberStateChangeDisconnected
                                     | EChatMemberStateChange.k_EChatMemberStateChangeKicked
                                     | EChatMemberStateChange.k_EChatMemberStateChangeBanned);
            bool left = (callback.m_rgfChatMemberStateChange & gone) != 0;

            // Steam hands the lobby to another member when the host leaves, so compare with the host we connected to.
            if (left && _hostAddress != null && callback.m_ulSteamIDUserChanged.ToString() == _hostAddress)
            {
                HostLeft?.Invoke();
            }

            if (IsLobbyHost)
            {
                SteamMatchmaking.SetLobbyData(CurrentLobbyID, LobbyRules.PlayersKey,
                    SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyID).ToString());
            }
            MembersChanged?.Invoke();
        }

        private void OnDataUpdated(LobbyDataUpdate_t callback)
        {
            // Member data updates carry the member's id; only the lobby's own data matters here.
            if (callback.m_ulSteamIDMember != callback.m_ulSteamIDLobby) return;

            FriendLobby entry = _friendLobbies.Find(l => l.Lobby.m_SteamID == callback.m_ulSteamIDLobby);
            if (entry == null) return;

            entry.DataReady = true;
            if (callback.m_bSuccess == 0)
            {
                entry.Problem = "That game no longer exists.";
                FriendLobbiesChanged?.Invoke();
                return;
            }

            string game = SteamMatchmaking.GetLobbyData(entry.Lobby, LobbyRules.GameKey);
            entry.IsEcho = LobbyRules.IsEchoLobby(game);
            int.TryParse(SteamMatchmaking.GetLobbyData(entry.Lobby, LobbyRules.PlayersKey), out entry.Players);
            entry.MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(entry.Lobby);
            entry.Problem = entry.MaxPlayers > 0 && entry.Players >= entry.MaxPlayers
                ? "That game is full."
                : LobbyRules.CheckLobby(game,
                    SteamMatchmaking.GetLobbyData(entry.Lobby, LobbyRules.VersionKey),
                    SteamMatchmaking.GetLobbyData(entry.Lobby, LobbyRules.StateKey),
                    SteamMatchmaking.GetLobbyData(entry.Lobby, LobbyRules.HostKey),
                    Application.version);
            FriendLobbiesChanged?.Invoke();
        }
    }
}
