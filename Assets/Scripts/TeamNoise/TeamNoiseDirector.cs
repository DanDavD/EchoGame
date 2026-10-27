using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
using EchoProtocol.Core;
using EchoProtocol.Extraction;

namespace EchoProtocol.TeamNoise
{
    /// <summary>
    /// The team's shared noise, decided by the server and shown on every peer. Every voice fills one meter (an echo with
    /// the V key more than plain chat) and silence drains it. When it is full the beast charges at whoever made the racket,
    /// the music turns dramatic, and the alarm only ends once the whole team stays silent for a few seconds.
    /// Without a network (the sandbox) the same rules run locally. One director exists per match.
    /// </summary>
    public class TeamNoiseDirector : NetworkBehaviour
    {
        [Header("Team noise meter")]
        [Tooltip("Meter level at which the alarm starts.")]
        [SerializeField] private float _capacity = 1f;
        [Tooltip("How fast one unit of team noise (a player at full voice) fills the meter, per second.")]
        [SerializeField] private float _fillPerSecond = 0.07f;
        [Tooltip("How fast the meter empties while the team is quiet, per second.")]
        [SerializeField] private float _drainPerSecond = 0.04f;
        [Tooltip("Seconds of total silence that end the alarm.")]
        [SerializeField] private float _calmSeconds = 4f;
        [Tooltip("Summed team noise below this counts as silence.")]
        [SerializeField] private float _quietThreshold = 0.15f;
        [Tooltip("A player quieter than this adds nothing: the hum of a room or a fan picked up by an idle microphone must not fill the meter.")]
        [SerializeField, Range(0f, 0.5f)] private float _playerNoiseGate = 0.15f;

        /// <summary>The director of the current match, on this peer.</summary>
        public static TeamNoiseDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        // Replicated meter, written by the server.
        private readonly SyncVar<float> _fillSync = new SyncVar<float>();
        private readonly SyncVar<bool> _alarmSync = new SyncVar<bool>();
        private readonly SyncVar<float> _calmSync = new SyncVar<float>();

        private TeamNoiseBudget _budget;
        private readonly Dictionary<PlayerController, VoiceNoiseEmitter> _emitters = new Dictionary<PlayerController, VoiceNoiseEmitter>();
        private Vector3 _lastLoudestPosition;
        private bool _hasLoudestPosition;

        /// <summary>True on the peer that decides: the server, or the machine without a network.</summary>
        private bool IsAuthority => !IsSpawned || IsServerStarted;

        /// <summary>Team noise meter, 0 to 1, as seen by this peer.</summary>
        public float Fill01 => IsAuthority ? (_budget != null ? _budget.Fill01 : 0f) : _fillSync.Value;

        /// <summary>True while the alarm is on (the beast charges and everyone must stay silent).</summary>
        public bool IsAlarmed => IsAuthority ? _budget != null && _budget.IsAlarmed : _alarmSync.Value;

        /// <summary>How close the team is to ending the alarm by staying silent, 0 to 1.</summary>
        public float Calm01 => IsAuthority ? (_budget != null && _calmSeconds > 0f ? Mathf.Clamp01(_budget.QuietSeconds / _calmSeconds) : 0f) : _calmSync.Value;

        /// <summary>
        /// Builds a director for an offline match (the sandbox). A networked match spawns the TeamNoiseDirector prefab on
        /// the server instead.
        /// </summary>
        public static TeamNoiseDirector CreateOffline()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("TeamNoiseDirector");
            go.SetActive(false); // bind before Awake/OnEnable read any network property
            var director = go.AddComponent<TeamNoiseDirector>();
            go.AddComponent<TeamNoiseHud>();
            go.AddComponent<TeamNoiseAudio>();
            OfflineSandboxGuard.BindOffline(go);
            go.SetActive(true);
            return director;
        }

        private void Awake()
        {
            _budget = new TeamNoiseBudget(_capacity, _fillPerSecond, _drainPerSecond, _calmSeconds, _quietThreshold);
            _budget.Alarmed += HandleAlarmed;
            _budget.Calmed += HandleCalmed;
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (IsAuthority && _budget != null && _budget.IsAlarmed) EnemyBrain.CalmAll();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _fillSync.UpdateSendRate(0.1f);
            _calmSync.UpdateSendRate(0.1f);
            _alarmSync.UpdateSendRate(0f);
        }

        private void Update()
        {
            HandleDebugKeys();

            if (!IsAuthority || IsMatchOver()) return;
            TickMeter(Time.deltaTime);
        }

        private static bool IsMatchOver()
        {
            GameExtractionManager match = GameExtractionManager.Instance;
            return match != null && MatchRules.IsResolved(match.State);
        }

        private void TickMeter(float dt)
        {
            float total = 0f;
            float loudest = 0f;
            Vector3 loudestPosition = Vector3.zero;

            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController player = players[i];
                if (player == null) continue;

                float noise = PlayerNoise(player);
                total += noise;
                if (noise > loudest)
                {
                    loudest = noise;
                    loudestPosition = player.transform.position;
                }
            }

            if (loudest > 0f)
            {
                _lastLoudestPosition = loudestPosition;
                _hasLoudestPosition = true;
            }
            _budget.Tick(dt, total);
            PublishMeter();
        }

        /// <summary>Where the alarm sends the beast: the last loud player, or the first player if nobody made a sound yet.</summary>
        private Vector3 AlarmTarget()
        {
            if (_hasLoudestPosition) return _lastLoudestPosition;

            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null) return players[i].transform.position;
            }
            return transform.position;
        }

        private float PlayerNoise(PlayerController player)
        {
            if (!_emitters.TryGetValue(player, out VoiceNoiseEmitter emitter) || emitter == null)
            {
                emitter = player.GetComponent<VoiceNoiseEmitter>();
                _emitters[player] = emitter;
            }
            return CountedNoise(emitter != null ? emitter.NoiseLevel01 : 0f, _playerNoiseGate);
        }

        /// <summary>What one player adds to the meter: nothing below the gate (room noise), the real level above it.</summary>
        public static float CountedNoise(float noise, float gate) => noise < gate ? 0f : noise;

        private void PublishMeter()
        {
            if (!IsSpawned || !IsServerStarted) return;

            float fill = _budget.Fill01;
            if (Mathf.Abs(_fillSync.Value - fill) > 0.01f || (fill == 0f && _fillSync.Value != 0f)) _fillSync.Value = fill;
            if (_alarmSync.Value != _budget.IsAlarmed) _alarmSync.Value = _budget.IsAlarmed;

            float calm = Calm01;
            if (Mathf.Abs(_calmSync.Value - calm) > 0.02f || (calm == 0f && _calmSync.Value != 0f)) _calmSync.Value = calm;
        }

        private void HandleAlarmed()
        {
            Vector3 target = AlarmTarget();
            EnemyBrain.AlarmAll(target);
            Debug.Log($"<color=red>[TeamNoise]</color> The team was too loud: the beast charges at {target}.");
        }

        private void HandleCalmed()
        {
            EnemyBrain.CalmAll();
            Debug.Log("<color=cyan>[TeamNoise]</color> Silence held. The beast calms down.");
        }

        /// <summary>Server (or offline) only: adds noise straight to the meter. Used by tests and the debug key.</summary>
        public void AddNoise(float amount)
        {
            if (!IsAuthority) return;
            _budget.Add(amount);
            PublishMeter();
        }

        private void HandleDebugKeys()
        {
            // [N] Add a chunk of team noise.
            if (!DebugKeys.WasPressed(Key.N)) return;
            if (IsAuthority) AddNoise(0.35f);
            else ServerDebugNoise();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerDebugNoise()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddNoise(0.35f);
#endif
        }
    }
}
