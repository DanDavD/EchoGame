using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Object;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
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
    [RequireComponent(typeof(MatchPlayerSpawner))]
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
        private MatchPlayerSpawner _spawner;

        // Set by the launch flags: start the match as soon as the server is up, skipping the waiting room.
        private bool _startMatchWhenReady;

        // FishNet initialises only the transport it starts with, so the rig uses Multipass, which initialises both:
        // Tugboat (UDP, local and LAN play, tests) and FishySteamworks (Steam P2P through the Steam relay).
        private Multipass _multipass;
        private Tugboat _tugboat;
        private FishySteamworks.FishySteamworks _steamTransport;
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
            _spawner = GetComponent<MatchPlayerSpawner>();

            _multipass = GetComponent<Multipass>();
            _tugboat = GetComponent<Tugboat>();
            _steamTransport = GetComponent<FishySteamworks.FishySteamworks>();
            if (_multipass == null)
            {
                Debug.LogError("[Bootstrap] The rig has no Multipass transport. Rebuild it: Echo Protocol > Build > Network Rig Prefab.");
            }
            else if (_tugboat != null)
            {
                // FishNet asks Multipass for a client transport early; give it one so it does not pick one itself.
                _multipass.SetClientTransport(_tugboat);
            }

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
            if (_spawner != null) _spawner.OnSpawned += HandlePlayerSpawned;

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

            // The flags are for automation (tests, the photo mode, two-process runs): they skip the waiting room.
            if (mode == LaunchMode.Host)
            {
                if (StartLocalHost()) StartMatchWhenReady();
            }
            else if (mode == LaunchMode.Join)
            {
                JoinLocalClient(address);
            }
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
            if (_spawner != null) _spawner.OnSpawned -= HandlePlayerSpawned;
        }

        /// <summary>True while this instance is running the server (host).</summary>
        public bool IsHosting => _networkManager != null && _networkManager.IsServerStarted;

        /// <summary>True while this instance is connected, or connecting, as a client.</summary>
        public bool IsClientRunning => _networkManager != null && _networkManager.IsClientStarted;

        /// <summary>True when a server or a client is running.</summary>
        public bool IsNetworkActive => IsHosting || IsClientRunning;

        /// <summary>True on the server once the host has started the match (the waiting room is over).</summary>
        public bool IsMatchStarted => _spawner != null && _spawner.MatchStarted;

        /// <summary>Connected players waiting for the host to start the match, host included. Server only.</summary>
        public int PlayersInWaitingRoom => _spawner != null ? _spawner.WaitingCount : 0;

        /// <summary>True on any peer once this peer's own player has spawned, i.e. it is in the match.</summary>
        public bool HasLocalPlayer
        {
            get
            {
                var players = PlayerController.ActivePlayers;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] != null && players[i].IsSpawned && players[i].IsOwner) return true;
                }
                return false;
            }
        }

        /// <summary>Raised on the server when the host starts the match.</summary>
        public event System.Action MatchStarted;

        /// <summary>
        /// Server only: leaves the waiting room. Spawns the monster, the relics and one player per connected client.
        /// Players who connect later (a LAN join) spawn as soon as they arrive.
        /// </summary>
        public bool StartMatch()
        {
            if (!IsHosting || IsMatchStarted) return false;

            ApplyLevelPlayerSpawns();
            SpawnWorld();
            _spawner.BeginMatch();
            Debug.Log("[Bootstrap] Match started.");
            MatchStarted?.Invoke();
            return true;
        }

        /// <summary>Starts the match now if the server is up, or as soon as it is.</summary>
        public void StartMatchWhenReady()
        {
            if (IsHosting) StartMatch();
            else _startMatchWhenReady = true;
        }

        /// <summary>True when this rig can host or join through Steam (the transport is there and Steam is running).</summary>
        public bool SteamAvailable => _steamTransport != null && SteamManager.Initialized;

        /// <summary>Starts a local host (server and client in this process) over the Tugboat transport, no Steam needed.</summary>
        public bool StartLocalHost()
        {
            if (!CanStart(_tugboat, "Tugboat")) return false;

            bool serverStarted = StartServerOn(_tugboat);
            bool clientStarted = serverStarted && StartClientOn(_tugboat, null);
            Debug.Log($"[Bootstrap] Local host started. Server: {serverStarted}, Client: {clientStarted}");
            return serverStarted && clientStarted;
        }

        /// <summary>Connects to a local host over Tugboat. <paramref name="address"/> defaults to localhost.</summary>
        public bool JoinLocalClient(string address = null)
        {
            if (!CanStart(_tugboat, "Tugboat")) return false;

            bool clientStarted = StartClientOn(_tugboat, address);
            Debug.Log($"[Bootstrap] Local join started. Client: {clientStarted}");
            return clientStarted;
        }

        /// <summary>
        /// Starts a host over Steam (FishySteamworks, peer to peer through the Steam relay). Friends connect to this
        /// player's SteamID, which the Steam lobby publishes. Steam must be running.
        /// </summary>
        public bool StartSteamHost()
        {
            if (!CanStart(_steamTransport, "FishySteamworks")) return false;

            bool serverStarted = StartServerOn(_steamTransport);
            bool clientStarted = serverStarted && StartClientOn(_steamTransport, null);
            Debug.Log($"[Bootstrap] Steam host started. Server: {serverStarted}, Client: {clientStarted}");
            return serverStarted && clientStarted;
        }

        /// <summary>Connects through Steam to the host whose SteamID64 is <paramref name="hostSteamId"/>.</summary>
        public bool JoinSteamHost(string hostSteamId)
        {
            if (!CanStart(_steamTransport, "FishySteamworks")) return false;
            if (string.IsNullOrWhiteSpace(hostSteamId)) return false;

            bool clientStarted = StartClientOn(_steamTransport, hostSteamId);
            Debug.Log($"[Bootstrap] Steam join started (host {hostSteamId}). Client: {clientStarted}");
            return clientStarted;
        }

        /// <summary>Stops the server and the client. Leaving a Steam lobby is the caller's job.</summary>
        public void Disconnect()
        {
            if (_networkManager == null) return;

            ForgetWorld();
            _startMatchWhenReady = false;
            StopServer();
            if (_networkManager.IsClientStarted) _networkManager.ClientManager.StopConnection();
        }

        private bool CanStart(Transport transport, string transportName)
        {
            if (_networkManager == null || _multipass == null || IsNetworkActive) return false;
            if (transport == null)
            {
                Debug.LogError($"[Bootstrap] The rig has no {transportName} transport. Rebuild it: Echo Protocol > Build > Network Rig Prefab.");
                return false;
            }
            return true;
        }

        // Multipass runs with GlobalServerActions off, so only the chosen transport opens a server (a Steam host does not
        // also open a UDP port, and a local host does not need Steam). ServerManager.StartConnection would ask Multipass to
        // start every transport, so the server is started per transport here.
        private bool StartServerOn(Transport transport)
        {
            int index = TransportIndex(transport);
            return index >= 0 && _multipass.StartConnection(true, index);
        }

        private bool StartClientOn(Transport transport, string address)
        {
            _multipass.SetClientTransport(transport);
            if (!string.IsNullOrWhiteSpace(address)) transport.SetClientAddress(address);
            return _networkManager.ClientManager.StartConnection();
        }

        private void StopServer()
        {
            if (_multipass == null || !_networkManager.IsServerStarted) return;

            // What ServerManager.StopConnection(true) does, minus the call Multipass refuses without GlobalServerActions.
            var clients = new System.Collections.Generic.List<NetworkConnection>(_networkManager.ServerManager.Clients.Values);
            if (clients.Count > 0) _networkManager.ServerManager.SendDisconnectMessages(clients, true);

            for (int i = 0; i < _multipass.Transports.Count; i++)
            {
                if (_multipass.Transports[i].GetConnectionState(true) != LocalConnectionState.Stopped)
                {
                    _multipass.StopConnection(true, i);
                }
            }
        }

        private int TransportIndex(Transport transport)
        {
            for (int i = 0; i < _multipass.Transports.Count; i++)
            {
                if (_multipass.Transports[i] == transport) return i;
            }
            Debug.LogError($"[Bootstrap] {transport.GetType().Name} is not one of the Multipass transports. Rebuild the rig.");
            return -1;
        }

        private void Update()
        {
            // After a restart the world is despawned; spawn it again once the match is running.
            if (_networkManager != null && _networkManager.IsServerStarted && IsMatchStarted && !_worldSpawned)
            {
                SpawnWorld();
            }
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                // The server opens the waiting room: the world and the players spawn when the host starts the match.
                ApplyLevelPlayerSpawns();
                if (_startMatchWhenReady)
                {
                    _startMatchWhenReady = false;
                    StartMatch();
                }
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                ForgetWorld();
                _startMatchWhenReady = false;
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
            FishNet.Object.NetworkObject playerPrefab = _spawner != null ? _spawner.PlayerPrefab : null;

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
            var spawner = GetComponent<MatchPlayerSpawner>();
            var points = SpawnRegistry.Get(SpawnKind.Player);
            if (spawner == null || points.Count == 0) return;

            var spawns = new Transform[points.Count];
            for (int i = 0; i < points.Count; i++) spawns[i] = points[i].transform;
            spawner.Spawns = spawns;
        }

        private void SetupSpawnPoints()
        {
            var spawner = GetComponent<MatchPlayerSpawner>();
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

            // Before the match this panel is the way in; once this player is in the match it is just a debug readout.
            if (HasLocalPlayer && !DebugOverlay.Visible) return;

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
                GUI.enabled = SteamAvailable;
                if (GUILayout.Button("Host Steam Lobby (Friends Only)")) _lobbyManager.HostLobby();
                GUI.enabled = true;

                GUILayout.Space(10);
                GUILayout.Label("-- Offline / LAN Testing --");

                if (GUILayout.Button("Host Local (No Steam)")) StartLocalHost();
                if (GUILayout.Button("Join Local Client")) JoinLocalClient();
            }
            else
            {
                if (_networkManager.IsServerStarted) GUILayout.Label("[HOST] Server is running");
                if (IsHosting && !IsMatchStarted && GUILayout.Button("Start Match")) StartMatch();
                if (_networkManager.IsClientStarted) GUILayout.Label("[CLIENT] Connected");

                if (_lobbyManager.CurrentLobbyID.IsValid())
                {
                    GUILayout.Label($"Lobby ID: {_lobbyManager.CurrentLobbyID}");
                }

                GUILayout.Space(5);

                if (GUILayout.Button("Disconnect"))
                {
                    Disconnect();
                    _lobbyManager.LeaveLobby();
                }
            }
            
            GUILayout.EndArea();
        }
    }
}
