using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Object;
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
        [Header("Prefabs")]
        [SerializeField] private FishNet.Object.NetworkObject _enemyPrefab;
        [SerializeField] private FishNet.Object.NetworkObject _relicPrefab;

        private NetworkManager _networkManager;
        private SteamLobbyManager _lobbyManager;
        private Transport _steamTransport;
        private Transport _localTransport;
        private FishNet.Object.NetworkObject _spawnedEnemyInstance;
        private FishNet.Object.NetworkObject _spawnedRelicInstance;

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsurePrefabReferences();
        }

        private void EnsurePrefabReferences()
        {
            if (_enemyPrefab == null)
            {
                _enemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<FishNet.Object.NetworkObject>("Assets/Prefabs/Enemy_Blind.prefab");
            }
            if (_relicPrefab == null)
            {
                _relicPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<FishNet.Object.NetworkObject>("Assets/Prefabs/Relic_Test.prefab");
            }
        }
#endif

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _lobbyManager = GetComponent<SteamLobbyManager>();
            
            _steamTransport = GetComponent("FishySteamworks") as Transport;
            _localTransport = GetComponent("Tugboat") as Transport;

#if UNITY_EDITOR
            EnsurePrefabReferences();
#endif
            SetupSpawnPoints();
            EnsureSpawnablePrefabs();

            if (_networkManager != null && _networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_networkManager != null && _networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
            }
        }

        private void Update()
        {
            if (_networkManager != null && _networkManager.IsServerStarted)
            {
                if (_spawnedEnemyInstance == null) SpawnNetworkedEnemy();
                if (_spawnedRelicInstance == null) SpawnNetworkedRelic();
            }
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                SpawnNetworkedEnemy();
                SpawnNetworkedRelic();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _spawnedEnemyInstance = null;
                _spawnedRelicInstance = null;
            }
        }

        private void SpawnNetworkedEnemy()
        {
            if (_networkManager == null || !_networkManager.IsServerStarted) return;
            if (_spawnedEnemyInstance != null) return;

#if UNITY_EDITOR
            EnsurePrefabReferences();
#endif
            if (_enemyPrefab == null)
            {
                Debug.LogError("[NetworkBootstrap] Enemy prefab is null! Cannot spawn enemy.");
                return;
            }

            _spawnedEnemyInstance = Instantiate(_enemyPrefab, new Vector3(-6f, 1f, 16f), Quaternion.identity);
            _networkManager.ServerManager.Spawn(_spawnedEnemyInstance);
            Debug.Log("<color=green>[NetworkBootstrap]</color> Networked Enemy_Blind successfully spawned and synchronized!");
        }

        private void SpawnNetworkedRelic()
        {
            if (_networkManager == null || !_networkManager.IsServerStarted) return;
            if (_spawnedRelicInstance != null) return;

#if UNITY_EDITOR
            EnsurePrefabReferences();
#endif
            if (_relicPrefab == null)
            {
                Debug.LogWarning("[NetworkBootstrap] Relic prefab is null! Cannot spawn relic.");
                return;
            }

            _spawnedRelicInstance = Instantiate(_relicPrefab, new Vector3(0f, 0.5f, 2.5f), Quaternion.identity);
            _networkManager.ServerManager.Spawn(_spawnedRelicInstance);
            Debug.Log("<color=green>[NetworkBootstrap]</color> Networked Relic successfully spawned and synchronized!");
        }

        private void EnsureSpawnablePrefabs()
        {
#if UNITY_EDITOR
            EnsurePrefabReferences();
#endif
            var spawner = GetComponent<PlayerSpawner>();
            FishNet.Object.NetworkObject playerPrefab = null;
            if (spawner != null)
            {
                var fi = typeof(PlayerSpawner).GetField("_playerPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                playerPrefab = fi?.GetValue(spawner) as FishNet.Object.NetworkObject;
            }

#if UNITY_EDITOR
            if (playerPrefab == null)
            {
                playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<FishNet.Object.NetworkObject>("Assets/Prefabs/Player.prefab");
            }
#endif

            if (playerPrefab != null)
            {
                bool needsFix = _networkManager.SpawnablePrefabs == null;

                if (!needsFix && _networkManager.SpawnablePrefabs is SinglePrefabObjects spoCheck)
                {
                    if (spoCheck.Prefabs == null || spoCheck.Prefabs.Count < 3 || spoCheck.Prefabs[0] == null)
                        needsFix = true;
                }
                else if (!needsFix && _networkManager.SpawnablePrefabs.GetObjectCount() < 3)
                {
                    needsFix = true;
                }

                if (needsFix)
                {
                    SinglePrefabObjects spo = ScriptableObject.CreateInstance<SinglePrefabObjects>();
                    spo.AddObject(playerPrefab, false, true);
                    if (_enemyPrefab != null)
                    {
                        spo.AddObject(_enemyPrefab, false, true);
                    }
                    if (_relicPrefab != null)
                    {
                        spo.AddObject(_relicPrefab, false, true);
                    }
                    _networkManager.SpawnablePrefabs = spo;
                    Debug.Log($"<color=green>[NetworkBootstrap]</color> Dynamic runtime fix: {spo.GetObjectCount()} prefabs bound to SpawnablePrefabs (Player + Enemy + Relic)!");
                }
            }
        }

        private void SetupSpawnPoints()
        {
            var spawner = GetComponent<PlayerSpawner>();
            if (spawner != null && (spawner.Spawns == null || spawner.Spawns.Length == 0))
            {
                var s1 = new GameObject("SpawnPoint_1").transform;
                s1.position = new Vector3(-3.5f, 1.1f, -4f);
                s1.rotation = Quaternion.identity;
                s1.SetParent(transform);

                var s2 = new GameObject("SpawnPoint_2").transform;
                s2.position = new Vector3(3.5f, 1.1f, -4f);
                s2.rotation = Quaternion.identity;
                s2.SetParent(transform);

                spawner.Spawns = new Transform[] { s1, s2 };
            }
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
                    _spawnedEnemyInstance = null;
                    _spawnedRelicInstance = null;
                    if (_networkManager.IsServerStarted) _networkManager.ServerManager.StopConnection(true);
                    if (_networkManager.IsClientStarted) _networkManager.ClientManager.StopConnection();
                    _lobbyManager.LeaveLobby();
                }
            }
            
            GUILayout.EndArea();
        }
    }
}
