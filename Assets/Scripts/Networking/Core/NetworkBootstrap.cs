using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Object;
using FishNet.Transporting;
using FishNet.Component.Spawning;
using Steamworks;
using UnityEngine;
using EchoProtocol.Level;
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

        [Header("Startup")]
        [Tooltip("What to do as soon as the scene starts. The -echo-host / -echo-join command-line flags override it. " +
                 "Leave on None to choose from the on-screen panel.")]
        [SerializeField] private LaunchMode _autoStart = LaunchMode.None;

        private NetworkManager _networkManager;
        private SteamLobbyManager _lobbyManager;
        private Transport _steamTransport;
        private Transport _localTransport;
        private FishNet.Object.NetworkObject _spawnedEnemyInstance;
        private readonly System.Collections.Generic.List<FishNet.Object.NetworkObject> _spawnedRelics = new System.Collections.Generic.List<FishNet.Object.NetworkObject>();

        // True once the server has spawned the monster and the relics for the current match.
        private bool _worldSpawned;

        // The match manager tells the rig when a networked match restarts.
        private EchoProtocol.Extraction.GameExtractionManager _matchFlow;

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
                _networkManager.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
            }
            if (_networkManager != null && _networkManager.ClientManager != null)
            {
                _networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
            }
            var playerSpawner = GetComponent<PlayerSpawner>();
            if (playerSpawner != null) playerSpawner.OnSpawned += HandlePlayerSpawned;

            _matchFlow = FindAnyObjectByType<EchoProtocol.Extraction.GameExtractionManager>();
            if (_matchFlow != null) _matchFlow.ServerRestarting += ServerRespawnWorld;

            DontDestroyOnLoad(gameObject);
        }

        // Plain log lines for connection milestones: they make a multiplayer session debuggable from player logs.
        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            Debug.Log($"[Bootstrap] Client connection: {args.ConnectionState}");
            if (args.ConnectionState == LocalConnectionState.Started) StartCoroutine(LogWorldSnapshot(5f));
        }

        // What this peer actually sees a few seconds after connecting: proves replication reached it, from the logs alone.
        private System.Collections.IEnumerator LogWorldSnapshot(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            int players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude).Length;
            int monsters = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude).Length;
            int relics = FindObjectsByType<EchoProtocol.Relics.RelicBrain>(FindObjectsInactive.Exclude).Length;
            Debug.Log($"[Bootstrap] World as seen by this peer: players={players} monsters={monsters} relics={relics}");
        }

        private void HandleRemoteConnectionState(FishNet.Connection.NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            Debug.Log($"[Bootstrap] Remote client {args.ConnectionId}: {args.ConnectionState}");
        }

        private void HandlePlayerSpawned(FishNet.Object.NetworkObject player)
        {
            Debug.Log($"[Bootstrap] Player spawned for client {player.OwnerId} at {player.transform.position}");
        }

        private void Start()
        {
            LaunchMode mode = LaunchOptions.Parse(System.Environment.GetCommandLineArgs(), out string address);
            if (mode == LaunchMode.None)
            {
                mode = _autoStart;
                address = null;
            }

            if (mode == LaunchMode.Host) StartLocalHost();
            else if (mode == LaunchMode.Join) JoinLocalClient(address);
        }

        private void OnDestroy()
        {
            if (_matchFlow != null) _matchFlow.ServerRestarting -= ServerRespawnWorld;

            if (_networkManager != null && _networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
                _networkManager.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            }
            if (_networkManager != null && _networkManager.ClientManager != null)
            {
                _networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
            }
            var playerSpawner = GetComponent<PlayerSpawner>();
            if (playerSpawner != null) playerSpawner.OnSpawned -= HandlePlayerSpawned;
        }

        /// <summary>True while this instance is running the server (host).</summary>
        public bool IsHosting => _networkManager != null && _networkManager.IsServerStarted;

        /// <summary>True while this instance is connected, or connecting, as a client.</summary>
        public bool IsClientRunning => _networkManager != null && _networkManager.IsClientStarted;

        /// <summary>True when a server or a client is running.</summary>
        public bool IsNetworkActive => IsHosting || IsClientRunning;

        /// <summary>Starts a local host (server and client in this process) over the Tugboat transport, no Steam needed.</summary>
        public bool StartLocalHost()
        {
            if (_networkManager == null || IsNetworkActive) return false;
            if (_localTransport == null)
            {
                Debug.LogError("[Bootstrap] Tugboat transport not found; cannot host locally.");
                return false;
            }

            _networkManager.TransportManager.Transport = _localTransport;
            bool serverStarted = _networkManager.ServerManager.StartConnection();
            bool clientStarted = _networkManager.ClientManager.StartConnection();
            Debug.Log($"[Bootstrap] Local host started. Server: {serverStarted}, Client: {clientStarted}");
            return serverStarted && clientStarted;
        }

        /// <summary>Connects to a local host over Tugboat. <paramref name="address"/> defaults to localhost.</summary>
        public bool JoinLocalClient(string address = null)
        {
            if (_networkManager == null || IsNetworkActive) return false;
            if (_localTransport == null)
            {
                Debug.LogError("[Bootstrap] Tugboat transport not found; cannot join locally.");
                return false;
            }

            _networkManager.TransportManager.Transport = _localTransport;
            if (!string.IsNullOrWhiteSpace(address)) _localTransport.SetClientAddress(address);

            bool clientStarted = _networkManager.ClientManager.StartConnection();
            Debug.Log($"[Bootstrap] Local join started. Client: {clientStarted}");
            return clientStarted;
        }

        /// <summary>Creates a friends-only Steam lobby; the host starts once Steam confirms the lobby.</summary>
        public void HostSteamLobby()
        {
            if (_networkManager == null || IsNetworkActive) return;
            if (_steamTransport != null) _networkManager.TransportManager.Transport = _steamTransport;
            _lobbyManager.HostLobby();
        }

        /// <summary>Stops the server and the client and leaves the Steam lobby, if any.</summary>
        public void Disconnect()
        {
            if (_networkManager == null) return;

            ForgetWorld();
            if (_networkManager.IsServerStarted) _networkManager.ServerManager.StopConnection(true);
            if (_networkManager.IsClientStarted) _networkManager.ClientManager.StopConnection();
            if (_lobbyManager != null) _lobbyManager.LeaveLobby();
        }

        private void Update()
        {
            if (_networkManager != null && _networkManager.IsServerStarted && !_worldSpawned)
            {
                SpawnWorld();
            }
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                ApplyLevelPlayerSpawns();
                SpawnWorld();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                ForgetWorld();
            }
        }

        /// <summary>Forgets the spawned monster and relics, e.g. when networking stops. Their objects are removed by FishNet.</summary>
        private void ForgetWorld()
        {
            _spawnedEnemyInstance = null;
            _spawnedRelics.Clear();
            _worldSpawned = false;
        }

        /// <summary>The monster and one relic per shrine, spawned by the server at the level's markers.</summary>
        private void SpawnWorld()
        {
            SpawnNetworkedEnemy();
            SpawnNetworkedRelics();
            _worldSpawned = true;
        }

        /// <summary>
        /// Server only: puts the world back for a new match. The monster and the relics are despawned and spawned again
        /// at their markers on the next frame, and every player is sent back to a spawn point.
        /// </summary>
        public void ServerRespawnWorld()
        {
            if (!IsHosting) return;

            if (_spawnedEnemyInstance != null) _networkManager.ServerManager.Despawn(_spawnedEnemyInstance);
            foreach (FishNet.Object.NetworkObject relic in _spawnedRelics)
            {
                if (relic != null) _networkManager.ServerManager.Despawn(relic);
            }
            ForgetWorld();

            var spawns = SpawnRegistry.Get(SpawnKind.Player);
            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || !player.IsSpawned || spawns.Count == 0) continue;

                SpawnPoint point = spawns[i % spawns.Count];
                player.ServerTeleport(point.transform.position, point.transform.rotation);
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

            _spawnedEnemyInstance = Instantiate(_enemyPrefab, LevelPosition(SpawnKind.Enemy, new Vector3(-6f, 1f, 16f)), Quaternion.identity);
            _networkManager.ServerManager.Spawn(_spawnedEnemyInstance);
            Debug.Log("<color=green>[NetworkBootstrap]</color> Networked Enemy_Blind successfully spawned and synchronized!");
        }

        /// <summary>One living relic per shrine (the level's relic spawn points), each with its shrine's personality.</summary>
        private void SpawnNetworkedRelics()
        {
            if (_networkManager == null || !_networkManager.IsServerStarted) return;

#if UNITY_EDITOR
            EnsurePrefabReferences();
#endif
            if (_relicPrefab == null)
            {
                Debug.LogWarning("[NetworkBootstrap] Relic prefab is null! Cannot spawn relics.");
                return;
            }

            var shrines = SpawnRegistry.Get(SpawnKind.Relic);
            if (shrines.Count == 0)
            {
                SpawnRelicAt(new Vector3(0f, 0.5f, 2.5f), EchoProtocol.Relics.RelicPersonalityType.SarcasticCritic);
                return;
            }

            foreach (SpawnPoint shrine in shrines)
            {
                SpawnRelicAt(shrine.transform.position, shrine.RelicPersonality);
            }
            Debug.Log($"<color=green>[NetworkBootstrap]</color> {_spawnedRelics.Count} networked relics spawned and synchronized.");
        }

        private void SpawnRelicAt(Vector3 position, EchoProtocol.Relics.RelicPersonalityType personality)
        {
            FishNet.Object.NetworkObject relic = Instantiate(_relicPrefab, position, Quaternion.identity);
            _networkManager.ServerManager.Spawn(relic);
            _spawnedRelics.Add(relic);

            // After the spawn, so the personality replicates to the clients.
            var brain = relic.GetComponent<EchoProtocol.Relics.RelicBrain>();
            if (brain != null) brain.SetPersonality(personality);
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

        /// <summary>Position of the first level spawn point of <paramref name="kind"/>, or <paramref name="fallback"/> when the level has none.</summary>
        private static Vector3 LevelPosition(SpawnKind kind, Vector3 fallback)
        {
            SpawnPoint point = SpawnRegistry.First(kind);
            return point != null ? point.transform.position : fallback;
        }

        /// <summary>Points the player spawner at the level's player spawn points (resolved once the scene is loaded).</summary>
        private void ApplyLevelPlayerSpawns()
        {
            var spawner = GetComponent<PlayerSpawner>();
            var points = SpawnRegistry.Get(SpawnKind.Player);
            if (spawner == null || points.Count == 0) return;

            var spawns = new Transform[points.Count];
            for (int i = 0; i < points.Count; i++) spawns[i] = points[i].transform;
            spawner.Spawns = spawns;
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

            // Before connecting this panel is the way in; once connected it is just a debug readout.
            if (IsNetworkActive && !DebugOverlay.Visible) return;

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
                if (GUILayout.Button("Host Steam Lobby (Friends Only)")) HostSteamLobby();

                GUILayout.Space(10);
                GUILayout.Label("-- Offline / LAN Testing --");

                if (GUILayout.Button("Host Local (No Steam)")) StartLocalHost();
                if (GUILayout.Button("Join Local Client")) JoinLocalClient();
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

                if (GUILayout.Button("Disconnect")) Disconnect();
            }
            
            GUILayout.EndArea();
        }
    }
}
