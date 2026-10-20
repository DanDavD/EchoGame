using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;
using EchoProtocol.Networking.Steam;

namespace EchoProtocol.Networking.Core
{
    /// <summary>Where the player is in the multiplayer flow, as the menus show it.</summary>
    public enum SessionState
    {
        /// <summary>Not connected: the main menu.</summary>
        Offline,
        CreatingLobby,
        JoiningLobby,
        Connecting,
        /// <summary>Connected (or hosting), waiting for the host to start the match.</summary>
        WaitingRoom,
        InMatch,
        /// <summary>Something went wrong; <see cref="NetworkSession.ErrorMessage"/> says what and what to do.</summary>
        Error
    }

    /// <summary>
    /// The multiplayer flow from the player's point of view: host or join (Steam or local), wait, play, leave. It drives
    /// <see cref="NetworkBootstrap"/> and <see cref="SteamLobbyManager"/>, watches the connection, and turns every failure
    /// (Steam missing, lobby full, host gone, timeout) into one clear message. The menus only read it and call it.
    /// </summary>
    [RequireComponent(typeof(NetworkBootstrap))]
    [RequireComponent(typeof(SteamLobbyManager))]
    public class NetworkSession : MonoBehaviour
    {
        [Tooltip("Seconds to wait for the host before giving up on a join.")]
        [SerializeField] private float _connectTimeoutSeconds = 20f;

        private enum Phase { Idle, CreatingLobby, JoiningLobby, Connecting }

        private NetworkBootstrap _bootstrap;
        private SteamLobbyManager _lobby;
        private NetworkManager _networkManager;

        private Phase _phase = Phase.Idle;
        private string _error;
        // True while a session this component began is running. Only those report errors: the tests and the launch
        // flags drive NetworkBootstrap directly, and their disconnects are not the player's problem.
        private bool _active;
        private bool _clientConnected;
        private bool _serverStarted;
        private bool _hostLeft;
        private float _connectDeadline;
        private ulong _pendingLaunchLobby;
        private string _notice;
        private float _noticeUntil;

        public NetworkBootstrap Bootstrap => _bootstrap;
        public SteamLobbyManager Lobby => _lobby;

        /// <summary>Seconds a join may take before it fails. Tests shorten it.</summary>
        public float ConnectTimeoutSeconds
        {
            get => _connectTimeoutSeconds;
            set => _connectTimeoutSeconds = value;
        }

        public SessionState State
        {
            get
            {
                if (_error != null) return SessionState.Error;
                if (_phase == Phase.CreatingLobby) return SessionState.CreatingLobby;
                if (_phase == Phase.JoiningLobby) return SessionState.JoiningLobby;
                if (_phase == Phase.Connecting) return SessionState.Connecting;
                if (_bootstrap.HasLocalPlayer) return SessionState.InMatch;
                if (_bootstrap.IsHosting || _clientConnected) return SessionState.WaitingRoom;
                return SessionState.Offline;
            }
        }

        /// <summary>What went wrong, written for the player, while <see cref="State"/> is <see cref="SessionState.Error"/>.</summary>
        public string ErrorMessage => _error;

        /// <summary>A short-lived message that does not stop the session (e.g. an invite that arrived mid-game).</summary>
        public string Notice => Time.unscaledTime < _noticeUntil ? _notice : null;

        public bool IsHost => _bootstrap.IsHosting;

        /// <summary>True when this session runs through a Steam lobby (invites and the member list work).</summary>
        public bool IsSteamSession => _lobby.InLobby;

        /// <summary>True when Steam is running and the rig can host or join through it.</summary>
        public bool SteamAvailable => _bootstrap.SteamAvailable && _lobby.Available;

        /// <summary>Seconds left before a join gives up, while connecting.</summary>
        public float ConnectSecondsLeft => _phase == Phase.Connecting ? Mathf.Max(0f, _connectDeadline - Time.unscaledTime) : 0f;

        private bool CanBegin => State == SessionState.Offline || State == SessionState.Error;

        private void Awake()
        {
            _bootstrap = GetComponent<NetworkBootstrap>();
            _lobby = GetComponent<SteamLobbyManager>();
            _networkManager = GetComponent<NetworkManager>();

            _networkManager.ClientManager.OnClientConnectionState += HandleClientState;
            _networkManager.ServerManager.OnServerConnectionState += HandleServerState;
            _bootstrap.MatchStarted += HandleMatchStarted;
            _lobby.LobbyCreated += HandleLobbyCreated;
            _lobby.LobbyJoined += HandleLobbyJoined;
            _lobby.InviteAccepted += HandleInviteAccepted;
            _lobby.HostLeft += HandleHostLeft;

            _pendingLaunchLobby = LaunchOptions.ConnectLobby(System.Environment.GetCommandLineArgs());
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                if (_networkManager.ClientManager != null) _networkManager.ClientManager.OnClientConnectionState -= HandleClientState;
                if (_networkManager.ServerManager != null) _networkManager.ServerManager.OnServerConnectionState -= HandleServerState;
            }
            if (_bootstrap != null) _bootstrap.MatchStarted -= HandleMatchStarted;
            if (_lobby != null)
            {
                _lobby.LobbyCreated -= HandleLobbyCreated;
                _lobby.LobbyJoined -= HandleLobbyJoined;
                _lobby.InviteAccepted -= HandleInviteAccepted;
                _lobby.HostLeft -= HandleHostLeft;
            }
        }

        private void Update()
        {
            // Steam starts the game with +connect_lobby when an invite is accepted while it is closed. The lobby
            // callbacks register in Start, so the join waits for the first frame where Steam is ready.
            if (_pendingLaunchLobby != 0 && _lobby.Available)
            {
                ulong lobby = _pendingLaunchLobby;
                _pendingLaunchLobby = 0;
                JoinSteamLobby(new CSteamID(lobby));
            }

            if (_phase == Phase.Connecting && Time.unscaledTime > _connectDeadline)
            {
                Fail("The host did not answer. Check that they are still hosting, then try again.");
            }
        }

        /// <summary>Hosts a match through Steam: creates a friends-only lobby, then the Steam host.</summary>
        public void HostSteam()
        {
            if (!CanBegin) return;
            if (!SteamAvailable)
            {
                Fail("Steam is not running. Start Steam to play with friends, or host a local game.");
                return;
            }

            Begin();
            _phase = Phase.CreatingLobby;
            if (!_lobby.CreateLobby()) Fail("Steam could not create a lobby. Try again.");
        }

        /// <summary>Joins a friend's Steam lobby (from the friends list, an invite or the launch argument).</summary>
        public void JoinSteamLobby(CSteamID lobby)
        {
            if (!CanBegin)
            {
                ShowNotice("Leave your current game first to join another one.");
                return;
            }
            if (!SteamAvailable)
            {
                Fail("Steam is not running. Start Steam to join your friend.");
                return;
            }

            Begin();
            _phase = Phase.JoiningLobby;
            if (!_lobby.JoinLobby(lobby)) Fail("Steam could not join that game. Try again.");
        }

        /// <summary>Hosts a local or LAN match over UDP (no Steam).</summary>
        public void HostLocal()
        {
            if (!CanBegin) return;

            Begin();
            if (!_bootstrap.StartLocalHost()) Fail("Could not open a local game. Another copy of the game may already be hosting on this PC.");
        }

        /// <summary>Joins a local or LAN host at <paramref name="address"/> (empty means this PC).</summary>
        public void JoinLocal(string address)
        {
            if (!CanBegin) return;

            Begin();
            StartConnecting();
            if (!_bootstrap.JoinLocalClient(string.IsNullOrWhiteSpace(address) ? null : address.Trim()))
            {
                Fail("Could not start the connection. Check the address.");
            }
        }

        /// <summary>Host only: leaves the waiting room and starts the match for everyone.</summary>
        public void StartMatch()
        {
            if (State == SessionState.WaitingRoom && IsHost) _bootstrap.StartMatch();
        }

        /// <summary>Leaves the match or the waiting room, or cancels a join, and returns to the main menu.</summary>
        public void Leave()
        {
            Shutdown();
            _error = null;
        }

        /// <summary>Closes the error screen.</summary>
        public void DismissError()
        {
            _error = null;
        }

        /// <summary>The players in the waiting room, for the menus. Steam names in a Steam lobby; a count otherwise.</summary>
        public List<string> GetPlayerNames()
        {
            var names = new List<string>();
            if (_lobby.InLobby)
            {
                foreach (LobbyMember member in _lobby.GetMembers()) names.Add(member.IsHost ? member.Name + "  (host)" : member.Name);
            }
            else if (IsHost)
            {
                int count = Mathf.Max(1, _bootstrap.PlayersInWaitingRoom);
                names.Add("You  (host)");
                for (int i = 1; i < count; i++) names.Add($"Player {i + 1}");
            }
            return names;
        }

        private void Begin()
        {
            _error = null;
            _active = true;
            _hostLeft = false;
            _phase = Phase.Idle;
        }

        private void StartConnecting()
        {
            _phase = Phase.Connecting;
            _connectDeadline = Time.unscaledTime + _connectTimeoutSeconds;
        }

        private void Fail(string message)
        {
            Debug.LogWarning("[Session] " + message);
            Shutdown();
            _error = message;
        }

        // Stops whatever runs (network and lobby). Stop events that follow are expected and are not reported as errors.
        private void Shutdown()
        {
            _active = false;
            _phase = Phase.Idle;
            _bootstrap.Disconnect();
            _lobby.LeaveLobby();
            _clientConnected = false;
        }

        private void ShowNotice(string message)
        {
            _notice = message;
            _noticeUntil = Time.unscaledTime + 5f;
        }

        private void HandleLobbyCreated(bool success, string message)
        {
            if (_phase != Phase.CreatingLobby)
            {
                // Cancelled while Steam was creating it.
                _lobby.LeaveLobby();
                return;
            }
            if (!success)
            {
                Fail(message);
                return;
            }

            _phase = Phase.Idle;
            if (!_bootstrap.StartSteamHost()) Fail("The Steam lobby is ready but the game could not start hosting. Try again.");
        }

        private void HandleLobbyJoined(LobbyJoinResult result)
        {
            if (_phase != Phase.JoiningLobby)
            {
                _lobby.LeaveLobby();
                return;
            }
            if (!result.Success)
            {
                Fail(result.Error);
                return;
            }

            StartConnecting();
            if (!_bootstrap.JoinSteamHost(result.HostAddress)) Fail("Could not start the Steam connection. Try again.");
        }

        private void HandleInviteAccepted(CSteamID lobby)
        {
            JoinSteamLobby(lobby);
        }

        private void HandleHostLeft()
        {
            _hostLeft = true;
            if (_phase == Phase.Connecting || _phase == Phase.JoiningLobby) Fail("The host left before you could join.");
        }

        private void HandleMatchStarted()
        {
            _lobby.MarkMatchStarted();
        }

        private void HandleClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                _clientConnected = true;
                if (_phase == Phase.Connecting) _phase = Phase.Idle;
                return;
            }
            if (args.ConnectionState != LocalConnectionState.Stopped) return;

            bool wasConnected = _clientConnected;
            _clientConnected = false;
            if (!_active || _serverStarted) return;

            if (_phase == Phase.Connecting)
            {
                Fail("Could not connect to the host. Check that they are still hosting, then try again.");
            }
            else if (wasConnected)
            {
                Fail(_hostLeft ? "The host left the match." : "Lost the connection to the host.");
            }
        }

        private void HandleServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                _serverStarted = true;
                return;
            }
            if (args.ConnectionState != LocalConnectionState.Stopped) return;

            bool wasStarted = _serverStarted;
            _serverStarted = false;
            if (!_active) return;

            Fail(wasStarted
                ? "The game server stopped unexpectedly."
                : "Could not start the game server. Another copy of the game may already be hosting on this PC.");
        }
    }
}
