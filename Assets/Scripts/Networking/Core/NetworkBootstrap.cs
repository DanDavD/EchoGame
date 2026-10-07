using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;
using EchoProtocol.Networking.Steam;

namespace EchoProtocol.Networking.Core
{
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(ServerManager))]
    [RequireComponent(typeof(ClientManager))]
    [RequireComponent(typeof(SteamManager))]
    [RequireComponent(typeof(SteamLobbyManager))]
    public class NetworkBootstrap : MonoBehaviour
    {
        private NetworkManager _networkManager;
        private SteamLobbyManager _lobbyManager;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _lobbyManager = GetComponent<SteamLobbyManager>();
            
            // Auto-link Transport
            var transport = GetComponent<Transport>();
            if (transport == null)
            {
                // Add FishySteamworks dynamically to avoid compile-time assembly conflicts
                System.Type fsType = System.Type.GetType("FishySteamworks.FishySteamworks, FishySteamworks");
                if (fsType != null)
                {
                    transport = (Transport)gameObject.AddComponent(fsType);
                }
            }

            if (_networkManager.TransportManager.Transport == null && transport != null)
            {
                _networkManager.TransportManager.Transport = transport;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            if (_networkManager == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

            GUILayout.Label("--- Echo Protocol Networking ---", headerStyle);

            if (SteamManager.Initialized)
            {
                GUILayout.Label($"Steam Status: Online ({SteamFriends.GetPersonaName()})");
            }
            else
            {
                GUILayout.Label("Steam Status: Offline / Not Initialized");
            }

            GUILayout.Space(10);

            if (!_networkManager.IsServerStarted && !_networkManager.IsClientStarted)
            {
                if (GUILayout.Button("Host Steam Lobby (Friends Only)"))
                {
                    _lobbyManager.HostLobby();
                }
                
                GUILayout.Space(5);
                GUILayout.Label("Tip: To join, use the Steam Overlay (Shift+Tab) -> Join Game.");
            }
            else
            {
                if (_networkManager.IsServerStarted) GUILayout.Label("[HOST] Server is running");
                if (_networkManager.IsClientStarted) GUILayout.Label("[CLIENT] Connected");

                if (_lobbyManager.CurrentLobbyID.IsValid())
                {
                    GUILayout.Label($"Lobby ID: {_lobbyManager.CurrentLobbyID}");
                }

                GUILayout.Space(5);

                if (GUILayout.Button("Disconnect"))
                {
                    if (_networkManager.IsServerStarted) _networkManager.ServerManager.StopConnection(true);
                    if (_networkManager.IsClientStarted) _networkManager.ClientManager.StopConnection();
                    _lobbyManager.LeaveLobby();
                }
            }
            
            GUILayout.EndArea();
        }
    }
}
