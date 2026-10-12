using System.Collections.Generic;
using UnityEngine;
using EchoProtocol.Extraction;
using EchoProtocol.Interaction;
using EchoProtocol.Player;
using EchoProtocol.Relics;

namespace EchoProtocol.Altar
{
    /// <summary>
    /// Central Altar Receptacle managing the primary objective of Echo Protocol.
    /// Tracks installed living relics across sockets. When all sockets are filled,
    /// triggers the chaotic Symphony Choir, activates permanent golden arena illumination,
    /// enrages the blind monster to 7.0 m/s hunt mode, and begins the 45s extraction countdown.
    /// </summary>
    public class AltarReceptacle : MonoBehaviour, IInteractable
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

        private bool _isCompleted;

        /// <summary>Number of sockets currently holding a living relic.</summary>
        public int FilledSocketsCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _sockets.Count; i++)
                {
                    if (_sockets[i] != null && _sockets[i].IsOccupied) count++;
                }
                return count;
            }
        }

        /// <summary>Total sockets required for extraction activation.</summary>
        public int RequiredSocketsCount => _requiredSockets > 0 ? _requiredSockets : Mathf.Max(1, _sockets.Count);

        /// <summary>True if the symphony has been activated.</summary>
        public bool IsCompleted => _isCompleted;

        /// <inheritdoc/>
        public string PromptText => FilledSocketsCount >= RequiredSocketsCount
            ? "Altar Fully Powered"
            : $"Place Living Relic [{FilledSocketsCount}/{RequiredSocketsCount}] [E]";

        /// <summary>Raised whenever a relic is placed into any socket (currentFilled, requiredTotal).</summary>
        public event System.Action<int, int> OnSocketPlaced;

        /// <summary>Raised when all required relics are placed and the climax begins.</summary>
        public event System.Action OnSymphonyActivated;

        /// <inheritdoc/>
        public bool CanInteract(PlayerController player)
        {
            if (_isCompleted || player == null) return false;

            var playerInteraction = player.GetComponent<PlayerInteraction>();
            if (playerInteraction == null || playerInteraction.CarriedRelic == null) return false;
            if (playerInteraction.CarriedRelic.IsLockedInAltar) return false;

            return GetClosestEmptySocket(player.transform.position) != null;
        }

        /// <inheritdoc/>
        public void Interact(PlayerController player)
        {
            if (_isCompleted || player == null) return;

            var playerInteraction = player.GetComponent<PlayerInteraction>();
            if (playerInteraction == null || playerInteraction.CarriedRelic == null) return;

            var targetSocket = GetClosestEmptySocket(player.transform.position);
            if (targetSocket != null)
            {
                targetSocket.Interact(player);
            }
        }

        /// <summary>
        /// Finds the unoccupied socket physically closest to the given world point.
        /// </summary>
        public AltarSocket GetClosestEmptySocket(Vector3 position)
        {
            AltarSocket closest = null;
            float minDistSqr = float.MaxValue;

            for (int i = 0; i < _sockets.Count; i++)
            {
                var socket = _sockets[i];
                if (socket != null && !socket.IsOccupied)
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
                    _sockets[i].SetParentReceptacle(this);
                }
            }

            if (_requiredSockets <= 0 && _sockets.Count > 0)
            {
                _requiredSockets = _sockets.Count;
            }

            UpdateAltarVisuals();
        }

        /// <summary>
        /// Registers a new socket at runtime.
        /// </summary>
        public void RegisterSocket(AltarSocket socket)
        {
            if (socket == null || _sockets.Contains(socket)) return;
            _sockets.Add(socket);
            socket.SetParentReceptacle(this);
        }

        /// <summary>
        /// Called by an AltarSocket when a relic is placed inside it.
        /// </summary>
        public void NotifySocketFilled(AltarSocket socket)
        {
            int filled = FilledSocketsCount;
            int total = RequiredSocketsCount;

            Debug.Log($"<color=yellow>[AltarReceptacle]</color> Socket filled! Progress: {filled}/{total}");
            OnSocketPlaced?.Invoke(filled, total);

            if (filled >= total && !_isCompleted)
            {
                TriggerSymphonyClimax();
            }
        }

        /// <summary>
        /// Executes the Symphony Climax: plays all relic choir loops in harmony,
        /// reveals the entire arena in permanent golden light, awakens and enrages
        /// the monster, and unlocks the escape portal with countdown.
        /// </summary>
        [ContextMenu("Debug: Trigger Symphony Climax")]
        public void TriggerSymphonyClimax()
        {
            if (_isCompleted) return;
            _isCompleted = true;

            Debug.Log("<color=gold><b>[AltarReceptacle] SYMPHONY CHOIR ACTIVATED! ESCAPE PORTAL UNLOCKED!</b></color>");

            // 1. Start Symphony Choir on all docked relics
            for (int i = 0; i < _sockets.Count; i++)
            {
                var socket = _sockets[i];
                if (socket != null && socket.DockedRelic != null)
                {
                    var brain = socket.DockedRelic.GetComponent<RelicBrain>();
                    if (brain != null)
                    {
                        brain.StartSymphonyChoir();
                    }
                }
            }

            // 2. Full Permanent Golden Illumination
            EchoShaderPublisher.SetFullIllumination(true);

            // 3. Monster Enrage to 7.0 m/s
            EnemyBrain.EnrageAll(_enragedMonsterSpeed);

            // 4. Unlock Escape Portal & Start Extraction Countdown
            if (GameExtractionManager.Instance != null)
            {
                GameExtractionManager.Instance.StartExtraction(_extractionDurationSeconds);
            }
            else
            {
                EscapePortal.ActivateAll();
            }

            // 5. Visual & Particle feedback
            UpdateAltarVisuals();

            OnSymphonyActivated?.Invoke();
        }

        private void UpdateAltarVisuals()
        {
            if (_centralAltarLight != null)
            {
                _centralAltarLight.color = _isCompleted ? _activeColor : _dormantColor;
                _centralAltarLight.intensity = _isCompleted ? 18f : 3.5f;
            }

            if (_symphonyParticles != null)
            {
                if (_isCompleted && !_symphonyParticles.isPlaying) _symphonyParticles.Play();
                else if (!_isCompleted && _symphonyParticles.isPlaying) _symphonyParticles.Stop();
            }
        }
    }
}
