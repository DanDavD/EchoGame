using FishNet.Managing;
using Steamworks;
using UnityEngine;

namespace EchoProtocol.Networking.Steam
{
    [RequireComponent(typeof(NetworkManager))]
    public class SteamLobbyManager : MonoBehaviour
    {
        private NetworkManager _networkManager;

        protected Callback<LobbyCreated_t> LobbyCreated;
        protected Callback<GameLobbyJoinRequested_t> JoinRequest;
        protected Callback<LobbyEnter_t> LobbyEntered;

        public CSteamID CurrentLobbyID { get; private set; }
        private const string HostAddressKey = "HostAddress";

        private void Start()
        {
            _networkManager = GetComponent<NetworkManager>();

            if (!SteamManager.Initialized)
            {
                Debug.LogError("SteamManager is not initialized! Lobby manager will not work.");
                return;
            }

            // Register Steam Callbacks
            LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            JoinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
            LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        public void HostLobby(ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly, int maxPlayers = 4)
        {
            if (!SteamManager.Initialized) return;
            
            Debug.Log("Creating Steam Lobby...");
            // This triggers LobbyCreated_t callback
            SteamMatchmaking.CreateLobby(lobbyType, maxPlayers);
        }

        public void LeaveLobby()
        {
            if (CurrentLobbyID.IsValid())
            {
                SteamMatchmaking.LeaveLobby(CurrentLobbyID);
                CurrentLobbyID = CSteamID.Nil;
                Debug.Log("Left Steam Lobby.");
            }
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError("Failed to create Steam Lobby. Result: " + callback.m_eResult);
                return;
            }

            CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            Debug.Log("Steam Lobby Created Successfully! ID: " + CurrentLobbyID);

            // Set lobby data so clients know who to connect to
            string hostSteamId = SteamUser.GetSteamID().ToString();
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, HostAddressKey, hostSteamId);
            SteamMatchmaking.SetLobbyData(CurrentLobbyID, "name", SteamFriends.GetPersonaName() + "'s Lobby");

            // Start FishNet Host (Server + Client)
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
        }

        private void OnJoinRequest(GameLobbyJoinRequested_t callback)
        {
            Debug.Log($"Join Requested by friend {callback.m_steamIDFriend}. Joining lobby {callback.m_steamIDLobby}...");
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            
            if (_networkManager.IsServerStarted)
            {
                // We are the host, no need to connect as a client to someone else
                return;
            }

            Debug.Log("Entered Steam Lobby: " + CurrentLobbyID);

            string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyID, HostAddressKey);
            if (string.IsNullOrEmpty(hostAddress))
            {
                Debug.LogError("Lobby does not have a HostAddress set! Cannot connect.");
                return;
            }

            Debug.Log("Connecting to Host SteamID: " + hostAddress);

            // Set FishySteamworks Client Address
            var transport = _networkManager.TransportManager.Transport;
            if (transport != null)
            {
                transport.SetClientAddress(hostAddress);
                _networkManager.ClientManager.StartConnection();
            }
            else
            {
                Debug.LogError("FishySteamworks transport not found! Cannot connect via Steam.");
            }
        }
        
        private void OnDestroy()
        {
            LeaveLobby();
        }
    }
}
