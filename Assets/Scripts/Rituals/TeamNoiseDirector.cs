using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
using EchoProtocol.Core;
using EchoProtocol.Extraction;
using EchoProtocol.Relics;

namespace EchoProtocol.Rituals
{
    /// <summary>What the human echo ritual is doing right now.</summary>
    public enum RitualPhase
    {
        None,
        /// <summary>The crying relic plays the rhythm everyone has to echo.</summary>
        Calling,
        /// <summary>One player at a time repeats the rhythm with their voice.</summary>
        Turn,
        /// <summary>The round is over; the result is on screen for a moment.</summary>
        Result
    }

    /// <summary>
    /// The team's shared noise and the relic rituals, decided by the server and shown on every peer.
    /// <list type="bullet">
    /// <item><b>Team noise meter.</b> Every voice fills it (an echo with the V key more than plain chat) and so does a
    /// crying relic; silence drains it. When it is full the beast charges at whoever made the racket, the music turns
    /// dramatic, and the alarm only ends once the whole team stays silent for a few seconds.</item>
    /// <item><b>Human echo ritual.</b> A crying relic plays a short rhythm. The players near it repeat it one after the
    /// other with their voice (any syllable, any language: only the rhythm counts). Everyone right: the relic calms down
    /// and the meter drops. Someone wrong: the meter rises and the relic tries an easier rhythm. Three failed rounds and it
    /// cries itself out, and the beast hears it.</item>
    /// </list>
    /// Without a network (the sandbox) the same rules run locally. One director exists per match.
    /// </summary>
    public class TeamNoiseDirector : NetworkBehaviour
    {
        [Header("Team noise meter")]
        [Tooltip("Meter level at which the alarm starts.")]
        [SerializeField] private float _capacity = 1f;
        [Tooltip("How fast one unit of team noise (a player at full voice) fills the meter, per second.")]
        [SerializeField] private float _fillPerSecond = 0.1f;
        [Tooltip("How fast the meter empties while the team is quiet, per second.")]
        [SerializeField] private float _drainPerSecond = 0.04f;
        [Tooltip("Seconds of total silence that end the alarm.")]
        [SerializeField] private float _calmSeconds = 4f;
        [Tooltip("Summed team noise below this counts as silence.")]
        [SerializeField] private float _quietThreshold = 0.06f;
        [Tooltip("How much a crying relic adds to the meter compared with a player at the same noise.")]
        [SerializeField, Range(0f, 1f)] private float _cryMeterWeight = 0.5f;

        [Header("Human echo ritual")]
        [Tooltip("Players within this distance (m) of a crying relic take part in its ritual.")]
        [SerializeField] private float _ritualRadius = 16f;
        [SerializeField, Range(1, 4)] private int _maxParticipants = 4;
        [Tooltip("Beats of the first rhythm; each failed round makes the next one a beat easier.")]
        [SerializeField, Range(EchoRhythm.MinBeats, EchoRhythm.MaxBeats)] private int _startBeats = 3;
        [Tooltip("Silence between the relic's rhythm and the first player's turn.")]
        [SerializeField] private float _leadInSeconds = 0.7f;
        [Tooltip("Seconds each player has on top of the rhythm's own length to echo it.")]
        [SerializeField] private float _turnExtraSeconds = 1.7f;
        [Tooltip("Extra seconds the server waits for an answer that is travelling over the network.")]
        [SerializeField] private float _submitSlackSeconds = 1.5f;
        [Tooltip("Seconds the result stays on screen before the next round.")]
        [SerializeField] private float _resultSeconds = 2.2f;
        [SerializeField, Range(1, 6)] private int _maxFailedRounds = 3;
        [Tooltip("Meter added when a round fails.")]
        [SerializeField] private float _failPenalty = 0.12f;
        [Tooltip("Meter taken off when the team soothes a relic.")]
        [SerializeField] private float _successReward = 0.3f;

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

        // ---------------------------------------------------------------- server ritual state
        private RelicBrain _serverRelic;
        private float[] _serverPattern;
        private readonly List<PlayerController> _serverParticipants = new List<PlayerController>();
        private RitualPhase _serverPhase;
        private float _serverTimer;
        private int _serverTurn;
        private int _roundId;
        private int _failedRounds;
        private float _searchCooldown;

        // ---------------------------------------------------------------- what every peer shows
        private RelicBrain _viewRelic;
        private float[] _viewPattern = new float[0];
        private readonly List<PlayerController> _viewParticipants = new List<PlayerController>();
        private readonly List<int> _viewResults = new List<int>(); // 0 waiting, 1 right, -1 wrong
        private RitualPhase _viewPhase;
        private float _viewPhaseStart;
        private float _viewWindow;
        private int _viewTurn;
        private int _viewRound;
        private bool _viewSuccess;
        private bool _viewCriedOut;

        // ---------------------------------------------------------------- the local player's own echo
        private readonly VoiceOnsetDetector _detector = new VoiceOnsetDetector();
        private bool _recording;
        private float _recordUntil;
        private int _recordRound;
        private int _recordTurn;
        private MicrophoneLoudness _recordMic;

        /// <summary>True on the peer that decides: the server, or the machine without a network.</summary>
        private bool IsAuthority => !IsSpawned || IsServerStarted;

        /// <summary>Team noise meter, 0 to 1, as seen by this peer.</summary>
        public float Fill01 => IsAuthority ? (_budget != null ? _budget.Fill01 : 0f) : _fillSync.Value;

        /// <summary>True while the alarm is on (the beast charges and everyone must stay silent).</summary>
        public bool IsAlarmed => IsAuthority ? _budget != null && _budget.IsAlarmed : _alarmSync.Value;

        /// <summary>How close the team is to ending the alarm by staying silent, 0 to 1.</summary>
        public float Calm01 => IsAuthority ? (_budget != null && _calmSeconds > 0f ? Mathf.Clamp01(_budget.QuietSeconds / _calmSeconds) : 0f) : _calmSync.Value;

        /// <summary>The ritual as seen by this peer, for the HUD.</summary>
        public RitualPhase Phase => _viewPhase;
        public RelicBrain RitualRelic => _viewRelic;
        public IReadOnlyList<float> RitualPattern => _viewPattern;
        public IReadOnlyList<PlayerController> RitualParticipants => _viewParticipants;
        public IReadOnlyList<int> RitualResults => _viewResults;
        public int RitualTurn => _viewTurn;
        public float PhaseElapsed => Time.time - _viewPhaseStart;
        public float TurnWindow => _viewWindow;
        public bool LastRoundSucceeded => _viewSuccess;
        public bool LastRoundCriedOut => _viewCriedOut;

        /// <summary>True while the local player is the one echoing.</summary>
        public bool IsLocalTurn => _recording;

        /// <summary>Syllables heard from the local player during their turn so far.</summary>
        public int LocalBeatCount => _recording ? _detector.Onsets.Count : 0;

        /// <summary>
        /// When set, the local player's echo is read from this instead of the microphone (0 to 1, unsmoothed). Lets a
        /// test, or a future calibration screen, speak a rhythm without a real voice.
        /// </summary>
        public System.Func<float> VoiceLevelOverride { get; set; }

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
            UpdateLocalRecording();

            if (_viewPhase == RitualPhase.Result && PhaseElapsed > _resultSeconds) _viewPhase = RitualPhase.None;

            if (!IsAuthority) return;

            if (IsMatchOver())
            {
                if (_serverPhase != RitualPhase.None) AbortRitual();
                return;
            }

            TickMeter(Time.deltaTime);
            TickRitual(Time.deltaTime);
        }

        private static bool IsMatchOver()
        {
            GameExtractionManager match = GameExtractionManager.Instance;
            return match != null && MatchRules.IsResolved(match.State);
        }

        // =====================================================================
        // TEAM NOISE METER (server)
        // =====================================================================

        /// <summary>
        /// What the meter hears this tick. During the alarm only the players count: the alarm asks the team to go silent,
        /// and a relic crying somewhere far away must not make that impossible.
        /// </summary>
        public static float MeterInput(float playerNoise, float relicNoise, bool alarmed)
        {
            return alarmed ? playerNoise : playerNoise + relicNoise;
        }

        private void TickMeter(float dt)
        {
            float total = 0f;
            float relicTotal = 0f;
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

            var relics = RelicBrain.ActiveRelics;
            for (int i = 0; i < relics.Count; i++)
            {
                RelicBrain relic = relics[i];
                if (relic == null || !relic.IsCrying) continue;

                float noise = RelicBrain.CryNoiseLevel * _cryMeterWeight;
                relicTotal += noise;
                if (noise > loudest)
                {
                    loudest = noise;
                    loudestPosition = relic.transform.position;
                }
            }

            if (loudest > 0f)
            {
                _lastLoudestPosition = loudestPosition;
                _hasLoudestPosition = true;
            }
            _budget.Tick(dt, MeterInput(total, relicTotal, _budget.IsAlarmed));
            PublishMeter();
        }

        private Vector3 _lastLoudestPosition;
        private bool _hasLoudestPosition;

        /// <summary>Where the alarm sends the beast: the last loud source, or the first player if nobody made a sound yet.</summary>
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
            return emitter != null ? emitter.NoiseLevel01 : 0f;
        }

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

        // =====================================================================
        // HUMAN ECHO RITUAL (server)
        // =====================================================================

        private void TickRitual(float dt)
        {
            switch (_serverPhase)
            {
                case RitualPhase.None:
                    // Looking for a crying relic and the players around it does not need to happen every frame.
                    _searchCooldown -= dt;
                    if (_searchCooldown <= 0f)
                    {
                        _searchCooldown = 0.25f;
                        TryStartRound();
                    }
                    break;

                case RitualPhase.Calling:
                    if (!RitualRelicStillCrying()) { AbortRitual(); return; }
                    _serverTimer -= dt;
                    if (_serverTimer <= 0f) BeginTurn(0);
                    break;

                case RitualPhase.Turn:
                    if (!RitualRelicStillCrying()) { AbortRitual(); return; }
                    _serverTimer -= dt;
                    if (_serverTimer <= 0f) FailRound($"no echo from player {_serverTurn + 1}");
                    break;

                case RitualPhase.Result:
                    _serverTimer -= dt;
                    if (_serverTimer <= 0f) _serverPhase = RitualPhase.None;
                    break;
            }
        }

        private bool RitualRelicStillCrying()
        {
            return _serverRelic != null && _serverRelic.isActiveAndEnabled && _serverRelic.IsCrying;
        }

        private void TryStartRound()
        {
            RelicBrain crying = null;
            var relics = RelicBrain.ActiveRelics;
            for (int i = 0; i < relics.Count; i++)
            {
                if (relics[i] != null && relics[i].IsCrying) { crying = relics[i]; break; }
            }

            if (crying == null)
            {
                _failedRounds = 0;
                return;
            }

            if (crying != _serverRelic) _failedRounds = 0;

            GatherParticipants(crying);
            if (_serverParticipants.Count == 0) return; // nobody close enough yet: it just keeps crying

            _serverRelic = crying;
            _roundId++;

            int beats = EchoRhythm.BeatsForAttempt(_startBeats, _failedRounds);
            _serverPattern = EchoRhythm.CreatePattern(Random.Range(int.MinValue, int.MaxValue), beats, Tempo(crying.Personality));

            _serverPhase = RitualPhase.Calling;
            _serverTimer = EchoRhythm.Duration(_serverPattern) + _leadInSeconds;
            BroadcastCall();
        }

        /// <summary>The carrier first, then everyone else in range from nearest to farthest.</summary>
        private void GatherParticipants(RelicBrain relic)
        {
            _serverParticipants.Clear();

            Vector3 center = relic.transform.position;
            float maxSqr = _ritualRadius * _ritualRadius;
            var candidates = new List<(PlayerController player, float sqr)>();

            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController player = players[i];
                if (player == null) continue;
                float sqr = (player.transform.position - center).sqrMagnitude;
                if (sqr <= maxSqr) candidates.Add((player, sqr));
            }

            GameObject carrier = relic.Carryable != null && relic.Carryable.Carrier != null ? relic.Carryable.Carrier.gameObject : null;
            candidates.Sort((a, b) =>
            {
                bool aCarrier = a.player.gameObject == carrier;
                bool bCarrier = b.player.gameObject == carrier;
                if (aCarrier != bCarrier) return aCarrier ? -1 : 1;
                return a.sqr.CompareTo(b.sqr);
            });

            for (int i = 0; i < candidates.Count && _serverParticipants.Count < _maxParticipants; i++)
            {
                _serverParticipants.Add(candidates[i].player);
            }
        }

        private static float Tempo(RelicPersonalityType personality) => personality switch
        {
            RelicPersonalityType.AnxiousCoward => 0.8f,
            RelicPersonalityType.DumbGloop => 0.9f,
            RelicPersonalityType.GrumpyElder => 1.3f,
            _ => 1f
        };

        private float TurnWindowSeconds => EchoRhythm.Duration(_serverPattern) + _turnExtraSeconds;

        private void BeginTurn(int turn)
        {
            // A player who left the match is skipped rather than blamed.
            while (turn < _serverParticipants.Count && _serverParticipants[turn] == null) turn++;
            if (turn >= _serverParticipants.Count)
            {
                SucceedRound();
                return;
            }

            _serverTurn = turn;
            _serverPhase = RitualPhase.Turn;
            _serverTimer = TurnWindowSeconds + _submitSlackSeconds;
            BroadcastTurn(turn, _serverParticipants[turn], TurnWindowSeconds);
        }

        /// <summary>Server side of an echo: checks it belongs to the current turn, scores it and moves on.</summary>
        private void ReceiveEcho(int round, int turn, float[] onsets, PlayerController sender)
        {
            if (_serverPhase != RitualPhase.Turn || round != _roundId || turn != _serverTurn) return;
            if (turn >= _serverParticipants.Count || _serverParticipants[turn] != sender) return;
            if (onsets == null || onsets.Length > 16) onsets = new float[0];

            bool right = EchoRhythm.Matches(_serverPattern, onsets, EchoRhythm.DefaultTolerance, out float error);
            Debug.Log($"[TeamNoise] Echo from player {turn + 1}: {onsets.Length}/{_serverPattern.Length} beats, worst error {error:F2}s -> {(right ? "right" : "wrong")}");
            BroadcastTurnResult(turn, right);

            if (!right)
            {
                FailRound("wrong rhythm");
                return;
            }
            BeginTurn(turn + 1);
        }

        private void SucceedRound()
        {
            _failedRounds = 0;
            _budget.Remove(_successReward);
            if (_serverRelic != null) _serverRelic.Soothe();
            EndRound(true, false);
        }

        private void FailRound(string reason)
        {
            _failedRounds++;
            bool criedOut = _failedRounds >= _maxFailedRounds;
            Debug.Log($"[TeamNoise] Ritual round failed ({reason}). Failed rounds: {_failedRounds}/{_maxFailedRounds}");

            if (criedOut)
            {
                // It cries itself out and the whole cave hears it.
                _failedRounds = 0;
                if (_serverRelic != null) _serverRelic.CryItselfOut();
                _budget.ForceAlarm();
            }
            else
            {
                _budget.Add(_failPenalty);
            }
            EndRound(false, criedOut);
        }

        private void EndRound(bool success, bool criedOut)
        {
            _serverPhase = RitualPhase.Result;
            _serverTimer = _resultSeconds;
            PublishMeter();
            BroadcastEnd(success, criedOut);
        }

        /// <summary>The relic stopped crying on its own (docked in the altar) or vanished: the round just ends, no penalty.</summary>
        private void AbortRitual()
        {
            _serverPhase = RitualPhase.None;
            _serverRelic = null;
            _failedRounds = 0;
            BroadcastAbort();
        }

        // =====================================================================
        // BROADCASTS: an RPC on a network, a direct call offline
        // =====================================================================

        private void BroadcastCall()
        {
            var participants = _serverParticipants.ToArray();
            if (IsSpawned)
            {
                var objects = new NetworkObject[participants.Length];
                for (int i = 0; i < participants.Length; i++) objects[i] = participants[i] != null ? participants[i].NetworkObject : null;
                RpcCall(_roundId, _serverRelic.NetworkObject, _serverPattern, objects);
            }
            else
            {
                ApplyCall(_roundId, _serverRelic, _serverPattern, participants);
            }
        }

        [ObserversRpc]
        private void RpcCall(int round, NetworkObject relic, float[] pattern, NetworkObject[] participants)
        {
            var players = new PlayerController[participants != null ? participants.Length : 0];
            for (int i = 0; i < players.Length; i++) players[i] = participants[i] != null ? participants[i].GetComponent<PlayerController>() : null;
            ApplyCall(round, relic != null ? relic.GetComponent<RelicBrain>() : null, pattern, players);
        }

        private void ApplyCall(int round, RelicBrain relic, float[] pattern, PlayerController[] participants)
        {
            _viewRound = round;
            _viewRelic = relic;
            _viewPattern = pattern ?? new float[0];
            _viewParticipants.Clear();
            _viewResults.Clear();
            foreach (PlayerController p in participants)
            {
                _viewParticipants.Add(p);
                _viewResults.Add(0);
            }
            _viewTurn = -1;
            SetViewPhase(RitualPhase.Calling);
            StopRecording();

            if (relic != null) relic.PlayRitualPattern(_viewPattern);
        }

        private void BroadcastTurn(int turn, PlayerController player, float window)
        {
            if (IsSpawned) RpcTurn(_roundId, turn, player != null ? player.NetworkObject : null, window);
            else ApplyTurn(_roundId, turn, player, window);
        }

        [ObserversRpc]
        private void RpcTurn(int round, int turn, NetworkObject player, float window)
        {
            ApplyTurn(round, turn, player != null ? player.GetComponent<PlayerController>() : null, window);
        }

        private void ApplyTurn(int round, int turn, PlayerController player, float window)
        {
            if (round != _viewRound) return;

            _viewTurn = turn;
            _viewWindow = window;
            SetViewPhase(RitualPhase.Turn);
            if (_viewRelic != null) _viewRelic.PlayTurnTick();

            bool isLocal = player != null && (!player.IsSpawned || player.IsOwner);
            if (isLocal) StartRecording(round, turn, window, player);
            else StopRecording();
        }

        private void BroadcastTurnResult(int turn, bool right)
        {
            if (IsSpawned) RpcTurnResult(_roundId, turn, right);
            else ApplyTurnResult(_roundId, turn, right);
        }

        [ObserversRpc]
        private void RpcTurnResult(int round, int turn, bool right) => ApplyTurnResult(round, turn, right);

        private void ApplyTurnResult(int round, int turn, bool right)
        {
            if (round != _viewRound || turn < 0 || turn >= _viewResults.Count) return;
            _viewResults[turn] = right ? 1 : -1;
        }

        private void BroadcastEnd(bool success, bool criedOut)
        {
            if (IsSpawned) RpcEnd(_roundId, success, criedOut);
            else ApplyEnd(_roundId, success, criedOut);
        }

        [ObserversRpc]
        private void RpcEnd(int round, bool success, bool criedOut) => ApplyEnd(round, success, criedOut);

        private void ApplyEnd(int round, bool success, bool criedOut)
        {
            if (round != _viewRound) return;

            _viewSuccess = success;
            _viewCriedOut = criedOut;
            SetViewPhase(RitualPhase.Result);
            StopRecording();
            if (_viewRelic != null) _viewRelic.PlayRitualResult(success);
        }

        private void BroadcastAbort()
        {
            if (IsSpawned) RpcAbort();
            else ApplyAbort();
        }

        [ObserversRpc]
        private void RpcAbort() => ApplyAbort();

        private void ApplyAbort()
        {
            _viewPhase = RitualPhase.None;
            _viewRelic = null;
            StopRecording();
        }

        private void SetViewPhase(RitualPhase phase)
        {
            _viewPhase = phase;
            _viewPhaseStart = Time.time;
        }

        // =====================================================================
        // THE LOCAL PLAYER'S ECHO (any peer)
        // =====================================================================

        private void StartRecording(int round, int turn, float window, PlayerController player)
        {
            _recordMic = player.GetComponent<MicrophoneLoudness>();
            _detector.Reset();
            _recording = true;
            _recordUntil = Time.time + window;
            _recordRound = round;
            _recordTurn = turn;
        }

        private void StopRecording()
        {
            _recording = false;
            _recordMic = null;
        }

        private void UpdateLocalRecording()
        {
            if (!_recording) return;

            float level = VoiceLevelOverride != null ? VoiceLevelOverride() : (_recordMic != null ? _recordMic.RawLevel01 : 0f);
            _detector.Sample(Time.time, level);

            var onsets = _detector.Onsets;
            bool allBeatsIn = onsets.Count >= _viewPattern.Length && Time.time - onsets[onsets.Count - 1] > 0.35f;
            if (Time.time >= _recordUntil || allBeatsIn) SubmitLocalEcho();
        }

        private void SubmitLocalEcho()
        {
            var onsets = _detector.Onsets;
            var relative = new float[onsets.Count];
            for (int i = 0; i < onsets.Count; i++) relative[i] = onsets[i] - onsets[0];

            int round = _recordRound;
            int turn = _recordTurn;
            StopRecording();

            if (IsSpawned)
            {
                ServerSubmitEcho(round, turn, relative);
            }
            else
            {
                PlayerController local = turn >= 0 && turn < _viewParticipants.Count ? _viewParticipants[turn] : null;
                ReceiveEcho(round, turn, relative, local);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerSubmitEcho(int round, int turn, float[] onsets, NetworkConnection caller = null)
        {
            if (turn < 0 || turn >= _serverParticipants.Count) return;
            PlayerController expected = _serverParticipants[turn];
            if (expected == null) return;
            if (caller != null && expected.Owner != caller) return;

            ReceiveEcho(round, turn, onsets, expected);
        }

        // =====================================================================
        // DEBUG
        // =====================================================================

        private void HandleDebugKeys()
        {
            // [N] Add a chunk of team noise. [B] Make the nearest relic cry.
            if (DebugKeys.WasPressed(Key.N))
            {
                if (IsAuthority) AddNoise(0.35f);
                else ServerDebugNoise();
            }

            if (DebugKeys.WasPressed(Key.B))
            {
                if (IsAuthority) DebugMakeNearestRelicCry();
                else ServerDebugCry();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerDebugNoise()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddNoise(0.35f);
#endif
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerDebugCry()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugMakeNearestRelicCry();
#endif
        }

        private void DebugMakeNearestRelicCry()
        {
            var players = PlayerController.ActivePlayers;
            Vector3 from = players.Count > 0 && players[0] != null ? players[0].transform.position : Vector3.zero;

            RelicBrain nearest = null;
            float best = float.MaxValue;
            var relics = RelicBrain.ActiveRelics;
            for (int i = 0; i < relics.Count; i++)
            {
                RelicBrain relic = relics[i];
                if (relic == null || relic.IsCrying || (relic.Carryable != null && relic.Carryable.IsLockedInAltar)) continue;
                float sqr = (relic.transform.position - from).sqrMagnitude;
                if (sqr < best) { best = sqr; nearest = relic; }
            }
            if (nearest != null) nearest.StartCrying();
        }
    }
}
