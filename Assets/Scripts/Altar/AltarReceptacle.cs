using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using EchoProtocol.Extraction;
using EchoProtocol.Interaction;
using EchoProtocol.Player;
using EchoProtocol.Relics;

namespace EchoProtocol.Altar
{
    /// <summary>
    /// Central Altar Receptacle managing the primary objective of Echo Protocol.
    /// The server decides every docking: a player's request is checked on the server, the relic is locked there and the
    /// filled sockets replicate to all peers. When all sockets are filled the server starts the Symphony Choir, enrages
    /// the monster and opens the escape portal through <see cref="GameExtractionManager"/>.
    /// Without a network (the sandbox) the same rules run locally.
    /// </summary>
    public class AltarReceptacle : NetworkBehaviour, IInteractable
    {
        [Header("Socket Configuration")]
        [SerializeField] private List<AltarSocket> _sockets = new List<AltarSocket>();
        [Tooltip("Number of sockets required to trigger the climax (defaults to sockets list count).")]
        [SerializeField] private int _requiredSockets = 3;

        [Header("Symphony Climax Settings")]
        [Tooltip("Monster hunt speed when enraged by the symphony choir.")]
        [SerializeField] private float _enragedMonsterSpeed = 7.0f;
        [Tooltip("Duration of the extraction countdown in seconds.")]
        [SerializeField] private float _extractionDurationSeconds = 45f;

        [Header("Visual & Light Effects")]
        [SerializeField] private Light _centralAltarLight;
        [SerializeField] private Color _dormantColor = new Color(0.2f, 0.4f, 0.8f);
        [SerializeField] private Color _activeColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private ParticleSystem _symphonyParticles;

        // The state as the server holds it; the SyncVars below mirror it for the clients.
        private int _localFilledMask;
        private bool _localCompleted;

        private readonly SyncVar<int> _filledMask = new SyncVar<int>();
        private readonly SyncVar<bool> _completed = new SyncVar<bool>();

        // Relics docked by the server, by socket index, so the choir can start on them. Server or offline only.
        private readonly Dictionary<int, RelicCarryable> _dockedRelics = new Dictionary<int, RelicCarryable>();

        private int Mask => IsSpawned && !IsServerStarted ? _filledMask.Value : _localFilledMask;
        private bool Completed => IsSpawned && !IsServerStarted ? _completed.Value : _localCompleted;

        /// <summary>Number of sockets currently holding a living relic, as seen by this peer.</summary>
        public int FilledSocketsCount => AltarRules.Count(Mask);

        /// <summary>Total sockets required for extraction activation.</summary>
        public int RequiredSocketsCount => _requiredSockets > 0 ? _requiredSockets : Mathf.Max(1, _sockets.Count);

        /// <summary>True if the symphony has been activated.</summary>
        public bool IsCompleted => Completed;

        /// <summary>True when the socket at <paramref name="socketIndex"/> holds a relic, as seen by this peer.</summary>
        public bool IsSocketFilled(int socketIndex) => AltarRules.IsFilled(Mask, socketIndex);

        /// <inheritdoc/>
        public string PromptText => FilledSocketsCount >= RequiredSocketsCount
            ? "Altar Fully Powered"
            : $"Place Living Relic [{FilledSocketsCount}/{RequiredSocketsCount}] [E]";

        /// <summary>Raised on the server (or offline) whenever a relic is placed into any socket (currentFilled, requiredTotal).</summary>
        public event System.Action<int, int> OnSocketPlaced;

        /// <summary>Raised on the server (or offline) when all required relics are placed and the climax begins.</summary>
        public event System.Action OnSymphonyActivated;

        private void Awake()
        {
            // Auto-gather children sockets if list is empty
            if (_sockets.Count == 0)
            {
                GetComponentsInChildren(true, _sockets);
            }

            for (int i = 0; i < _sockets.Count; i++)
            {
                if (_sockets[i] != null)
                {
                    _sockets[i].Bind(this, i);
                }
            }

            if (_requiredSockets <= 0 && _sockets.Count > 0)
            {
                _requiredSockets = _sockets.Count;
            }

            _filledMask.OnChange += OnFilledMaskChanged;
            _completed.OnChange += OnCompletedChanged;
        }

        // Start, not Awake: reading the network state needs FishNet to have initialised this object.
        private void Start()
        {
            RefreshSockets();
            UpdateAltarVisuals();
        }

        private void OnDestroy()
        {
            _filledMask.OnChange -= OnFilledMaskChanged;
            _completed.OnChange -= OnCompletedChanged;
        }

        private void OnFilledMaskChanged(int prev, int next, bool asServer)
        {
            RefreshSockets();
        }

        private void OnCompletedChanged(bool prev, bool next, bool asServer)
        {
            ApplyCompletedEffects(next);
        }

        /// <summary>Registers a new socket at runtime.</summary>
        public void RegisterSocket(AltarSocket socket)
        {
            if (socket == null || _sockets.Contains(socket)) return;
            _sockets.Add(socket);
            socket.Bind(this, _sockets.Count - 1);
        }

        /// <summary>The socket closest to <paramref name="position"/> that is still empty, or null.</summary>
        public AltarSocket GetClosestEmptySocket(Vector3 position)
        {
            AltarSocket closest = null;
            float minDistSqr = float.MaxValue;

            for (int i = 0; i < _sockets.Count; i++)
            {
                var socket = _sockets[i];
                if (socket != null && !IsSocketFilled(i))
                {
                    float distSqr = (socket.transform.position - position).sqrMagnitude;
                    if (distSqr < minDistSqr)
                    {
                        minDistSqr = distSqr;
                        closest = socket;
                    }
                }
            }

            return closest;
        }

        /// <inheritdoc/>
        public bool CanInteract(PlayerController player)
        {
            if (player == null) return false;

            AltarSocket socket = GetClosestEmptySocket(player.transform.position);
            return socket != null && CanDockAt(socket.Index, player);
        }

        /// <inheritdoc/>
        public void Interact(PlayerController player)
        {
            if (player == null) return;

            AltarSocket socket = GetClosestEmptySocket(player.transform.position);
            if (socket != null) RequestDock(socket.Index, player);
        }

        /// <summary>True if <paramref name="player"/> may dock the relic they carry into the socket at <paramref name="socketIndex"/>.</summary>
        public bool CanDockAt(int socketIndex, PlayerController player)
        {
            if (player == null || Completed || socketIndex < 0 || socketIndex >= _sockets.Count) return false;
            if (IsSocketFilled(socketIndex)) return false;

            RelicCarryable relic = CarriedRelicOf(player);
            return relic != null && !relic.IsLockedInAltar;
        }

        /// <summary>
        /// Asks for the relic <paramref name="player"/> carries to be docked in the socket at <paramref name="socketIndex"/>.
        /// On a network the server checks the request and decides; without one the local rules apply directly.
        /// </summary>
        public void RequestDock(int socketIndex, PlayerController player)
        {
            if (!CanDockAt(socketIndex, player)) return;

            RelicCarryable relic = CarriedRelicOf(player);
            if (!IsSpawned)
            {
                Dock(relic, socketIndex);
            }
            else if (player.IsSpawned)
            {
                ServerRequestDock(relic.NetworkObject, player.NetworkObject, socketIndex);
            }
        }

        private static RelicCarryable CarriedRelicOf(PlayerController player)
        {
            var playerInteraction = player.GetComponent<PlayerInteraction>();
            return playerInteraction != null ? playerInteraction.CarriedRelic : null;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRequestDock(NetworkObject relicNetObj, NetworkObject playerNetObj, int socketIndex, FishNet.Connection.NetworkConnection caller = null)
        {
            if (relicNetObj == null || playerNetObj == null) return;
            // Only the player's own client may dock the relic that player carries.
            if (caller != null && playerNetObj.Owner != caller) return;
            if (socketIndex < 0 || socketIndex >= _sockets.Count || _sockets[socketIndex] == null) return;

            RelicCarryable relic = relicNetObj.GetComponent<RelicCarryable>();
            // The server only accepts a relic that the requesting player is actually holding.
            if (relic == null || relic.Carrier != playerNetObj) return;

            float distance = Vector3.Distance(playerNetObj.transform.position, _sockets[socketIndex].transform.position);
            if (!AltarRules.CanDock(Mask, _sockets.Count, socketIndex, Completed, relic.IsLockedInAltar, distance)) return;

            Dock(relic, socketIndex);
        }

        /// <summary>Locks the relic into the socket and updates the filled state. Runs on the server, or locally without a network.</summary>
        private void Dock(RelicCarryable relic, int socketIndex)
        {
            AltarSocket socket = _sockets[socketIndex];
            relic.LockToAltar(socket.MountPoint);
            _dockedRelics[socketIndex] = relic;
            SetFilledMask(AltarRules.With(Mask, socketIndex));

            var brain = relic.GetComponent<RelicBrain>();
            if (brain != null)
            {
                brain.TriggerDialogue(RelicDialogueCategory.AltarPlaced);
            }

            OnSocketPlaced?.Invoke(FilledSocketsCount, RequiredSocketsCount);
            Debug.Log($"<color=yellow>[AltarReceptacle]</color> Relic '{relic.RelicName}' docked in socket {socketIndex + 1}. Progress: {FilledSocketsCount}/{RequiredSocketsCount}");

            if (AltarRules.ShouldComplete(Mask, RequiredSocketsCount, Completed))
            {
                Complete();
            }
        }

        /// <summary>
        /// Starts the Symphony Choir: plays all relic choir loops in harmony, enrages the monster and opens the escape
        /// portal with the extraction countdown. Runs on the server, or locally without a network.
        /// </summary>
        private void Complete()
        {
            if (Completed) return;
            SetCompleted(true);

            Debug.Log("<color=gold><b>[AltarReceptacle] SYMPHONY CHOIR ACTIVATED! ESCAPE PORTAL UNLOCKED!</b></color>");

            foreach (RelicCarryable relic in _dockedRelics.Values)
            {
                if (relic == null) continue;
                var brain = relic.GetComponent<RelicBrain>();
                if (brain != null) brain.StartSymphonyChoir();
            }

            // Sonic Overload: the harmonic shockwave stuns the blind beast for 4.5s before it goes berserk
            EnemyBrain.StunAll(4.5f);
            EnemyBrain.EnrageAll(_enragedMonsterSpeed);

            if (GameExtractionManager.Instance != null)
            {
                GameExtractionManager.Instance.StartExtraction(_extractionDurationSeconds);
            }
            else
            {
                EscapePortal.ActivateAll();
            }

            OnSymphonyActivated?.Invoke();
        }

        /// <summary>
        /// Completes the altar without docking relics. Used by the debug tools and the tests. Only the server (or the
        /// offline sandbox) may complete it.
        /// </summary>
        [ContextMenu("Debug: Trigger Symphony Climax")]
        public void TriggerSymphonyClimax()
        {
            if (IsSpawned && !IsServerStarted) return;
            Complete();
        }

        /// <summary>Empties the altar for a new match. Runs on the server, or locally without a network.</summary>
        public void ResetAltar()
        {
            _dockedRelics.Clear();
            SetCompleted(false);
            SetFilledMask(0);
        }

        private void SetFilledMask(int mask)
        {
            _localFilledMask = mask;
            if (IsSpawned && IsServerStarted) _filledMask.Value = mask;
            RefreshSockets();
            UpdateAltarVisuals();
        }

        private void SetCompleted(bool completed)
        {
            _localCompleted = completed;
            if (IsSpawned && IsServerStarted) _completed.Value = completed;
            ApplyCompletedEffects(completed);
        }

        /// <summary>What the completed state looks like on every peer: the golden arena light and the altar glow.</summary>
        private void ApplyCompletedEffects(bool completed)
        {
            EchoShaderPublisher.SetFullIllumination(completed);
            UpdateAltarVisuals();
        }

        private void RefreshSockets()
        {
            for (int i = 0; i < _sockets.Count; i++)
            {
                if (_sockets[i] != null) _sockets[i].Refresh();
            }
        }

        private void UpdateAltarVisuals()
        {
            if (_centralAltarLight != null)
            {
                _centralAltarLight.color = Completed ? _activeColor : _dormantColor;
                _centralAltarLight.intensity = Completed ? 18f : 3.5f;
            }

            if (_symphonyParticles != null)
            {
                if (Completed && !_symphonyParticles.isPlaying) _symphonyParticles.Play();
                else if (!Completed && _symphonyParticles.isPlaying) _symphonyParticles.Stop();
            }
        }
    }
}
