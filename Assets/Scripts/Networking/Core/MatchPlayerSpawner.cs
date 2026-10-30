using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace EchoProtocol.Networking.Core
{
    /// <summary>
    /// Server side: spawns one player per connection, but only once the host starts the match. Until then connected
    /// players wait in the waiting room with no avatar. A player who connects after the start (a LAN join) spawns at once.
    /// Replaces FishNet's PlayerSpawner, which spawns as soon as a client has loaded the scene.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class MatchPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;

        /// <summary>Where players appear, in turn. Set by <see cref="NetworkBootstrap"/> from the level's spawn points.</summary>
        public Transform[] Spawns = new Transform[0];

        /// <summary>Raised on the server for every player spawned.</summary>
        public event Action<NetworkObject> OnSpawned;

        private NetworkManager _networkManager;
        private readonly List<NetworkConnection> _waiting = new List<NetworkConnection>();
        private int _nextSpawn;

        public NetworkObject PlayerPrefab => _playerPrefab;

        /// <summary>True on the server once the host has started the match.</summary>
        public bool MatchStarted { get; private set; }

        /// <summary>Connections that have loaded the level and wait for the match to start.</summary>
        public int WaitingCount => _waiting.Count;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _networkManager.SceneManager.OnClientLoadedStartScenes += HandleClientLoaded;
            _networkManager.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;
        }

        private void OnDestroy()
        {
            if (_networkManager == null) return;
            if (_networkManager.SceneManager != null) _networkManager.SceneManager.OnClientLoadedStartScenes -= HandleClientLoaded;
            if (_networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
                _networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
            }
        }

        /// <summary>Server only: leaves the waiting room and spawns every connected player.</summary>
        public void BeginMatch()
        {
            if (MatchStarted) return;
            MatchStarted = true;

            foreach (NetworkConnection connection in _waiting.ToArray())
            {
                if (connection != null && connection.IsActive) Spawn(connection);
            }
            _waiting.Clear();
        }

        private void HandleClientLoaded(NetworkConnection connection, bool asServer)
        {
            if (!asServer) return;

            if (MatchStarted) Spawn(connection);
            else if (!_waiting.Contains(connection)) _waiting.Add(connection);
        }

        private void HandleRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped) _waiting.Remove(connection);
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;

            // A new session starts in the waiting room again.
            MatchStarted = false;
            _waiting.Clear();
            _nextSpawn = 0;
        }

        private void Spawn(NetworkConnection connection)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[MatchPlayerSpawner] No player prefab; cannot spawn a player.");
                return;
            }

            NextSpawn(out Vector3 position, out Quaternion rotation);
            NetworkObject player = _networkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
            _networkManager.ServerManager.Spawn(player, connection);
            _networkManager.SceneManager.AddOwnerToDefaultScene(player);
            OnSpawned?.Invoke(player);
        }

        private void NextSpawn(out Vector3 position, out Quaternion rotation)
        {
            Transform spawn = Spawns.Length > 0 ? Spawns[_nextSpawn % Spawns.Length] : null;
            _nextSpawn++;

            position = spawn != null ? spawn.position : _playerPrefab.transform.position;
            rotation = spawn != null ? spawn.rotation : _playerPrefab.transform.rotation;
        }
    }
}
