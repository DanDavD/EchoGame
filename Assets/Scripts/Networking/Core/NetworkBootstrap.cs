using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using FishNet.Component.Spawning;
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
    [RequireComponent(typeof(PlayerSpawner))]
    public class NetworkBootstrap : MonoBehaviour
    {
        private NetworkManager _networkManager;
        private SteamLobbyManager _lobbyManager;
        private Transport _steamTransport;
        private Transport _localTransport;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _lobbyManager = GetComponent<SteamLobbyManager>();
            
            _steamTransport = GetComponent("FishySteamworks") as Transport;
            _localTransport = GetComponent("Tugboat") as Transport;

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
                    if (_steamTransport != null) _networkManager.TransportManager.Transport = _steamTransport;
                    _lobbyManager.HostLobby();
                }

                GUILayout.Space(10);
                GUILayout.Label("-- Offline / LAN Testing --");

                if (GUILayout.Button("Host Local (No Steam)"))
                {
                    if (_localTransport == null) Debug.LogError("[Bootstrap] Tugboat no encontrado.");
                    else { _networkManager.TransportManager.Transport = _localTransport;  }
                    
                    bool hostStarted = _networkManager.ServerManager.StartConnection();
                    bool clientStarted = _networkManager.ClientManager.StartConnection();
                    Debug.Log($"[Bootstrap] Local Host intentado. Server: {hostStarted}, Client: {clientStarted}");
                }
                if (GUILayout.Button("Join Local Client"))
                {
                    if (_localTransport == null) Debug.LogError("[Bootstrap] Tugboat no encontrado.");
                    else { _networkManager.TransportManager.Transport = _localTransport;  }
                    
                    bool clientStarted = _networkManager.ClientManager.StartConnection();
                    Debug.Log($"[Bootstrap] Local Join intentado. Client: {clientStarted}");
                }
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
