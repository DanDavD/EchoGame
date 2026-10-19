using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using EchoProtocol.Interaction;
using EchoProtocol.Player;

namespace EchoProtocol.Relics
{
    /// <summary>
    /// Living relic brain: monitors carrier actions, sprint fatigue, mic loudness,
    /// monster proximity, and high-velocity flight to trigger High on Life style comedy dialogues,
    /// procedural speech audio, Doppler flying screams, and noise generation for enemy hearing.
    /// Relics are sensitive, like babies: the carrier has to whisper, a hard landing hurts, and with teammates around it
    /// wants to be passed from hand to hand. Otherwise it cries (loud, constant noise) until the team soothes it with the
    /// human echo ritual (<see cref="EchoProtocol.Rituals.TeamNoiseDirector"/>). The server decides when it cries.
    /// </summary>
    [RequireComponent(typeof(RelicCarryable), typeof(RelicVoiceSynthesizer), typeof(RelicDialogueUI))]
    public class RelicBrain : NetworkBehaviour, INoiseSource
    {
        [Header("Personality")]
        [SerializeField] private RelicPersonalityType _personality = RelicPersonalityType.SarcasticCritic;

        [Header("Timers & Thresholds")]
        [SerializeField] private float _carryChatterInterval = 16f;
        [SerializeField] private float _idleSleepInterval = 28f;
        [SerializeField] private float _sprintRoastDuration = 2.2f;
        [SerializeField] private float _micRoastThreshold = 0.38f;
        [SerializeField] private float _micRoastDuration = 0.7f;
        [SerializeField] private float _monsterAlertDistance = 14f;
        [SerializeField] private float _flyingVelocityThreshold = 11.5f;

        [Header("Sensitivity (crying)")]
        [Tooltip("Carrier voice level (0..1) that is too loud for the relic. Whispering stays below it.")]
        [SerializeField, Range(0.1f, 1f)] private float _loudVoiceLevel = 0.3f;
        [Tooltip("Seconds the carrier may stay too loud before it cries.")]
        [SerializeField] private float _loudVoiceSeconds = 0.8f;
        [Tooltip("Seconds one player may keep it, with teammates around, before it cries. 0 = never asks to be passed.")]
        [SerializeField] private float _patienceSeconds = 35f;
        [Tooltip("Impact speed (m/s) of a landing that makes it cry.")]
        [SerializeField] private float _hurtImpactSpeed = 7f;
        [Tooltip("Seconds it cannot cry again after being soothed.")]
        [SerializeField] private float _sootheGraceSeconds = 8f;

        /// <summary>
        /// Noise a crying relic makes, constantly, until it is soothed. Loud enough to bring the monster over to investigate
        /// from about 25 m, not so loud that it starts a hunt before the team can try the ritual.
        /// </summary>
        public const float CryNoiseLevel = 0.6f;

        private RelicCarryable _carryable;
        private RelicVoiceSynthesizer _synthesizer;
        private RelicDialogueUI _dialogueUI;
        private Rigidbody _rb;

        // INoiseSource implementation
        private float _currentNoiseLevel;
        Transform INoiseSource.Owner => transform;
        public float NoiseLevel01 => IsCrying ? Mathf.Max(_currentNoiseLevel, CryNoiseLevel) : _currentNoiseLevel;

        // Runtime state trackers
        private float _chatterTimer;
        private float _idleSleepTimer;
        private float _sprintTimer;
        private float _micTimer;
        private float _monsterReactionCooldown;
        private bool _isAirborneFlying;
        private bool _isChoirActive;
        private float _noiseDecayTimer;

        /// <summary>True while every relic is silenced (debug toggle): no subtitles, no speech blips, no dialogue noise.</summary>
        public static bool Muted { get; private set; }

        private static readonly List<RelicBrain> _activeRelics = new List<RelicBrain>();

        /// <summary>Every relic currently enabled in the scene.</summary>
        public static IReadOnlyList<RelicBrain> ActiveRelics => _activeRelics;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Muted = false;
            _activeRelics.Clear();
        }

        /// <summary>Silences or restores all relics; silencing also hides the line being shown right now.</summary>
        public static void ToggleMuted()
        {
            Muted = !Muted;
            Debug.Log($"<color=yellow>[RelicBrain]</color> Relics muted: {Muted}");
            if (!Muted) return;

            foreach (RelicDialogueUI ui in FindObjectsByType<RelicDialogueUI>(FindObjectsInactive.Exclude)) ui.ClearDialogue();
        }

        // Cached carrier components
        private PlayerController _carrierController;
        private IVoiceLevelSource _carrierVoiceSource;

        /// <summary>The personality chosen by the server, so every peer speaks and sings with the same voice.</summary>
        private readonly SyncVar<int> _syncedPersonality = new SyncVar<int>();

        /// <summary>Whether the relic is crying, decided by the server and mirrored by every peer.</summary>
        private readonly SyncVar<bool> _cryingSync = new SyncVar<bool>();
        private bool _cryingLocal;

        private RelicTemper _temper;
        private GameObject _lastCarrier;

        public RelicPersonalityType Personality => _personality;

        /// <summary>The carry component of this relic.</summary>
        public RelicCarryable Carryable => _carryable;

        /// <summary>True while the relic cries, as seen by this peer.</summary>
        public bool IsCrying => IsSpawned ? _cryingSync.Value : _cryingLocal;

        /// <summary>True on the peer that decides the relic's mood: the server, or the machine without a network.</summary>
        private bool IsAuthority => !IsSpawned || IsServerStarted;

        private void Awake()
        {
            _carryable = GetComponent<RelicCarryable>();
            _synthesizer = GetComponent<RelicVoiceSynthesizer>();
            if (_synthesizer == null) _synthesizer = gameObject.AddComponent<RelicVoiceSynthesizer>();
            _dialogueUI = GetComponent<RelicDialogueUI>();
            if (_dialogueUI == null) _dialogueUI = gameObject.AddComponent<RelicDialogueUI>();
            _rb = GetComponent<Rigidbody>();

            _synthesizer.SetPersonality(_personality);
            _temper = new RelicTemper(_loudVoiceLevel, _loudVoiceSeconds, _patienceSeconds);
            _chatterTimer = Random.Range(6f, _carryChatterInterval);
            _idleSleepTimer = Random.Range(10f, _idleSleepInterval);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // The value that arrived with the spawn; later changes come through OnPersonalityChanged.
            if (!IsServerStarted)
            {
                ApplyPersonality((RelicPersonalityType)_syncedPersonality.Value);
                ApplyCryingAudio(_cryingSync.Value);
            }
        }

        private void OnEnable()
        {
            NoiseRegistry.Register(this);
            if (!_activeRelics.Contains(this)) _activeRelics.Add(this);
            _syncedPersonality.OnChange += OnPersonalityChanged;
            _cryingSync.OnChange += OnCryingChanged;

            if (_carryable != null)
            {
                _carryable.OnPickedUp += HandlePickedUp;
                _carryable.OnDropped += HandleDropped;
            }
        }

        private void OnDisable()
        {
            NoiseRegistry.Unregister(this);
            _activeRelics.Remove(this);
            _syncedPersonality.OnChange -= OnPersonalityChanged;
            _cryingSync.OnChange -= OnCryingChanged;

            if (_carryable != null)
            {
                _carryable.OnPickedUp -= HandlePickedUp;
                _carryable.OnDropped -= HandleDropped;
            }

            if (_synthesizer != null)
            {
                _synthesizer.StopFlyingScream();
                _synthesizer.StopCrying();
            }
        }

        /// <summary>
        /// Switches to another personality archetype. On a spawned relic only the server can change it; the new
        /// value replicates to the clients.
        /// </summary>
        public void SetPersonality(RelicPersonalityType newPersonality)
        {
            if (IsSpawned && IsServerStarted) _syncedPersonality.Value = (int)newPersonality;
            ApplyPersonality(newPersonality);
        }

        private void OnPersonalityChanged(int prev, int next, bool asServer)
        {
            if (!IsServerStarted) ApplyPersonality((RelicPersonalityType)next);
        }

        private void ApplyPersonality(RelicPersonalityType newPersonality)
        {
            _personality = newPersonality;
            if (_synthesizer != null)
            {
                _synthesizer.SetPersonality(newPersonality);
            }
            if (_carryable != null)
            {
                var profile = RelicPersonality.GetProfile(_personality);
                Debug.Log($"<color=cyan>[RelicBrain]</color> Switched personality to: {profile.Name} ({profile.Title})");
            }
        }

        private void HandlePickedUp(NetworkObject carrierNetObj)
        {
            _isAirborneFlying = false;
            _synthesizer.StopFlyingScream();
            _carrierController = carrierNetObj != null ? carrierNetObj.GetComponent<PlayerController>() : null;
            _carrierVoiceSource = carrierNetObj != null ? ResolveVoiceSource(carrierNetObj) : null;

            _chatterTimer = Random.Range(5f, _carryChatterInterval);
            _sprintTimer = 0f;
            _micTimer = 0f;

            // A different pair of hands is a pass: the relic is happy to be shared and its patience starts again.
            GameObject carrier = carrierNetObj != null ? carrierNetObj.gameObject : null;
            bool handedOver = _lastCarrier != null && carrier != null && carrier != _lastCarrier;
            if (carrier != _lastCarrier) _temper.ResetPatience();
            _lastCarrier = carrier;

            if (IsCrying) return;
            TriggerDialogue(handedOver ? RelicDialogueCategory.PassedAround : RelicDialogueCategory.Pickup);
        }

        /// <summary>
        /// Prefers the networked EchoVisionController: on the server, a remote carrier's MicrophoneLoudness is
        /// disabled and always reads 0, while EchoVisionController exposes the loudness replicated from the client.
        /// </summary>
        private static IVoiceLevelSource ResolveVoiceSource(NetworkObject carrier)
        {
            var networked = carrier.GetComponent<EchoVisionController>();
            if (networked != null) return networked;
            return carrier.GetComponent<IVoiceLevelSource>();
        }

        private void HandleDropped(NetworkObject previousCarrier)
        {
            _carrierController = null;
            _carrierVoiceSource = null;
            _idleSleepTimer = Random.Range(12f, _idleSleepInterval);

            // Docking in the altar is not a drop: the altar's own line plays instead. A crying relic just keeps crying.
            if (_carryable.IsLockedInAltar || IsCrying) return;

            // If not thrown with violent impulse, trigger gentle drop line
            if (!_isAirborneFlying && (_rb == null || _rb.linearVelocity.magnitude < _flyingVelocityThreshold))
            {
                TriggerDialogue(RelicDialogueCategory.NormalDrop);
            }
        }

        private void Update()
        {
            // Debug hotkeys for testing personality and dialogue
            HandleDebugKeys();

            // Maintain high continuous noise level during choir, or decay noise smoothly
            if (_isChoirActive)
            {
                _currentNoiseLevel = 0.6f;
            }
            else if (_noiseDecayTimer > 0f)
            {
                _noiseDecayTimer -= Time.deltaTime;
                if (_noiseDecayTimer <= 0f && !_isAirborneFlying)
                {
                    _currentNoiseLevel = 0f;
                }
            }

            if (_monsterReactionCooldown > 0f)
            {
                _monsterReactionCooldown -= Time.deltaTime;
            }

            if (IsAuthority) UpdateTemper();

            // A crying relic only cries: no chatter, roasts or murmurs on top of the wailing.
            if (IsCrying) return;

            if (_carryable.IsCarried)
            {
                UpdateCarriedState();
            }
            else if (!_carryable.IsLockedInAltar)
            {
                UpdateUnattendedState();
            }
        }

        /// <summary>Server (or offline) only: runs the mood rules and starts the crying.</summary>
        private void UpdateTemper()
        {
            // Docked in the altar the relic is home and asleep: no more tears.
            if (_carryable.IsLockedInAltar)
            {
                if (IsCrying) SetCrying(false);
                return;
            }
            if (IsCrying) return;

            float carrierLevel = _carryable.IsCarried && _carrierVoiceSource != null ? _carrierVoiceSource.Level01 : 0f;
            switch (_temper.Tick(Time.deltaTime, _carryable.IsCarried, carrierLevel, PlayerController.ActivePlayers.Count))
            {
                case TemperEvent.Fussy:
                    TriggerDialogue(RelicDialogueCategory.Fussy);
                    break;
                case TemperEvent.Cry:
                    StartCrying();
                    break;
            }
        }

        /// <summary>Server (or offline) only: the relic bursts into tears. Ignored while docked or recently soothed.</summary>
        public void StartCrying()
        {
            if (!IsAuthority || IsCrying || _carryable.IsLockedInAltar || _temper.IsInGrace) return;

            SetCrying(true);
            TriggerDialogue(RelicDialogueCategory.Crying, isScream: true);
            Debug.Log($"<color=orange>[RelicBrain]</color> {RelicPersonality.GetProfile(_personality).Name} started crying.");
        }

        /// <summary>Server (or offline) only: the team echoed the rhythm, so the relic calms down for a while.</summary>
        public void Soothe()
        {
            if (!IsAuthority || !IsCrying) return;

            _temper.Soothe(_sootheGraceSeconds);
            SetCrying(false);
            TriggerDialogue(RelicDialogueCategory.Soothed);
        }

        /// <summary>
        /// Server (or offline) only: the relic cried itself out after the team failed every ritual. It goes quiet, but only
        /// for a short grace.
        /// </summary>
        public void CryItselfOut()
        {
            if (!IsAuthority || !IsCrying) return;

            _temper.Soothe(_sootheGraceSeconds * 0.5f);
            SetCrying(false);
        }

        private void SetCrying(bool crying)
        {
            if (IsSpawned)
            {
                if (IsServerStarted) _cryingSync.Value = crying;
            }
            else
            {
                _cryingLocal = crying;
            }
            ApplyCryingAudio(crying);
        }

        private void OnCryingChanged(bool prev, bool next, bool asServer)
        {
            ApplyCryingAudio(next);
        }

        private void ApplyCryingAudio(bool crying)
        {
            if (_synthesizer == null) return;
            if (crying) _synthesizer.StartCrying(_personality);
            else _synthesizer.StopCrying();
        }

        /// <summary>Plays a ritual rhythm (beat times in seconds, the first at 0) on this relic. Runs on every peer.</summary>
        public void PlayRitualPattern(float[] pattern)
        {
            if (pattern == null || pattern.Length == 0 || !isActiveAndEnabled) return;
            StartCoroutine(PlayPatternRoutine(pattern));
        }

        private System.Collections.IEnumerator PlayPatternRoutine(float[] pattern)
        {
            // The wailing would drown the beats: hush it while the relic sings its rhythm.
            if (_synthesizer != null) _synthesizer.StopCrying();

            float start = Time.time;
            for (int i = 0; i < pattern.Length; i++)
            {
                while (Time.time - start < pattern[i]) yield return null;
                if (_synthesizer != null) _synthesizer.PlayRitualBeat(_personality, i == 0);
            }

            yield return new WaitForSeconds(0.5f);
            if (IsCrying) ApplyCryingAudio(true);
        }

        /// <summary>The "your turn" tick of the ritual. Runs on every peer.</summary>
        public void PlayTurnTick()
        {
            if (_synthesizer != null) _synthesizer.PlayTurnTick();
        }

        /// <summary>The ritual's success jingle or failure trombone. Runs on every peer.</summary>
        public void PlayRitualResult(bool success)
        {
            if (_synthesizer != null) _synthesizer.PlayRitualResult(success);
        }

        private void FixedUpdate()
        {
            // Airborne flying launch detection
            if (!_carryable.IsCarried && _rb != null)
            {
                float speed = _rb.linearVelocity.magnitude;

                if (speed >= _flyingVelocityThreshold && !_isAirborneFlying)
                {
                    _isAirborneFlying = true;
                    _currentNoiseLevel = 0.95f; // Screaming draws monster hearing
                    _noiseDecayTimer = 2f;

                    TriggerDialogue(RelicDialogueCategory.FlyingScream, isScream: true);
                    _synthesizer.StartFlyingScream(_personality);
                }
                else if (speed < 1.5f && _isAirborneFlying)
                {
                    // Slowed down without hard collision
                    _isAirborneFlying = false;
                    _synthesizer.StopFlyingScream();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isAirborneFlying)
            {
                _isAirborneFlying = false;
                _synthesizer.StopFlyingScream();

                float impactSpeed = collision.relativeVelocity.magnitude;
                _synthesizer.PlayImpactBonk(_personality, impactSpeed / 12f);

                _currentNoiseLevel = 0.70f; // Impact noise
                _noiseDecayTimer = 1.5f;

                // A hard landing hurts: the relic cries instead of complaining.
                if (IsAuthority && impactSpeed >= _hurtImpactSpeed && !_temper.IsInGrace && !_carryable.IsLockedInAltar)
                {
                    StartCrying();
                }
                else if (!IsCrying)
                {
                    TriggerDialogue(RelicDialogueCategory.LandedImpact);
                }
            }
        }

        private void UpdateCarriedState()
        {
            // 1. Sprinting detection
            if (_carrierController != null && _carrierController.IsSprinting && _carrierController.CurrentSpeed > 4f)
            {
                _sprintTimer += Time.deltaTime;
                if (_sprintTimer >= _sprintRoastDuration)
                {
                    _sprintTimer = -10f; // Cooldown after roast
                    TriggerDialogue(RelicDialogueCategory.Sprinting);
                }
            }
            else
            {
                _sprintTimer = Mathf.Max(0f, _sprintTimer - Time.deltaTime);
            }

            // 2. Microphone noise detection
            if (_carrierVoiceSource != null && _carrierVoiceSource.Level01 >= _micRoastThreshold)
            {
                _micTimer += Time.deltaTime;
                if (_micTimer >= _micRoastDuration)
                {
                    _micTimer = -8f; // Cooldown
                    _currentNoiseLevel = 0.60f;
                    _noiseDecayTimer = 2.5f;
                    TriggerDialogue(RelicDialogueCategory.MicReaction);
                }
            }
            else
            {
                _micTimer = Mathf.Max(0f, _micTimer - Time.deltaTime);
            }

            // 3. Monster proximity detection
            if (_monsterReactionCooldown <= 0f)
            {
                CheckMonsterProximity();
            }

            // 4. Random witty chatter
            _chatterTimer -= Time.deltaTime;
            if (_chatterTimer <= 0f)
            {
                _chatterTimer = Random.Range(_carryChatterInterval * 0.8f, _carryChatterInterval * 1.3f);
                TriggerDialogue(RelicDialogueCategory.CarryChatter);
            }
        }

        private void UpdateUnattendedState()
        {
            // Sleepy murmurs when left on the ground
            _idleSleepTimer -= Time.deltaTime;
            if (_idleSleepTimer <= 0f)
            {
                _idleSleepTimer = Random.Range(_idleSleepInterval * 0.8f, _idleSleepInterval * 1.3f);
                TriggerDialogue(RelicDialogueCategory.IdleSleep);
            }
        }

        private void CheckMonsterProximity()
        {
            var enemies = EnemyBrain.ActiveEnemies;
            if (enemies == null || enemies.Count == 0) return;

            Vector3 myPos = transform.position;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;

                float dist = Vector3.Distance(myPos, enemy.transform.position);
                if (dist <= _monsterAlertDistance && enemy.State != EnemyState.Idle)
                {
                    _monsterReactionCooldown = 15f; // Don't spam monster lines
                    TriggerDialogue(RelicDialogueCategory.MonsterNear);
                    break;
                }
            }
        }

        /// <summary>
        /// Selects a line and synchronizes across all players.
        /// </summary>
        public void TriggerDialogue(RelicDialogueCategory category, int lineIndex = -1, bool isScream = false)
        {
            int totalLines = RelicPersonality.GetLineCount(_personality, category);
            if (totalLines == 0) return;

            int selectedIndex = lineIndex >= 0 ? lineIndex : Random.Range(0, totalLines);

            if (IsSpawned)
            {
                // Observers RPCs can only be sent by the server; clients receive the line from it.
                if (IsServerStarted) RpcPlayDialogue(_personality, category, selectedIndex, isScream);
            }
            else
            {
                // Local / offline play
                PlayDialogueLocal(_personality, category, selectedIndex, isScream);
            }
        }

        [ObserversRpc]
        private void RpcPlayDialogue(RelicPersonalityType personality, RelicDialogueCategory category, int lineIndex, bool isScream)
        {
            PlayDialogueLocal(personality, category, lineIndex, isScream);
        }

        private void PlayDialogueLocal(RelicPersonalityType personality, RelicDialogueCategory category, int lineIndex, bool isScream)
        {
            if (Muted) return;

            string line = RelicPersonality.GetLine(personality, category, lineIndex);

            // Display in High on Life subtitle UI & 3D speech bubble
            if (_dialogueUI != null)
            {
                _dialogueUI.DisplayLine(personality, line, _synthesizer);
            }

            // Set noise level if not already screaming
            if (!isScream && _currentNoiseLevel < 0.15f)
            {
                _currentNoiseLevel = 0.08f;
                _noiseDecayTimer = 3f;
            }
        }

        /// <summary>
        /// Initiates the symphony choir on this relic when docked in the Altar.
        /// </summary>
        public void StartSymphonyChoir()
        {
            _isChoirActive = true;
            _currentNoiseLevel = 0.6f;

            if (IsSpawned)
            {
                if (IsServerStarted) RpcStartSymphonyChoir();
            }
            else
            {
                _synthesizer.StartSymphonyChoir(_personality);
            }
        }

        [ObserversRpc]
        private void RpcStartSymphonyChoir()
        {
            _isChoirActive = true;
            _currentNoiseLevel = 0.6f;
            if (_synthesizer != null)
            {
                _synthesizer.StartSymphonyChoir(_personality);
            }
        }

        /// <summary>
        /// Stops the symphony choir drone.
        /// </summary>
        public void StopSymphonyChoir()
        {
            _isChoirActive = false;
            if (_synthesizer != null)
            {
                _synthesizer.StopSymphonyChoir();
            }
        }

        private void HandleDebugKeys()
        {

            // [T] Key: Cycle personality archetype (on a networked relic only the server may change it)
            if (DebugKeys.WasPressed(Key.T) && (!IsSpawned || IsServerStarted))
            {
                int next = ((int)_personality + 1) % 4;
                SetPersonality((RelicPersonalityType)next);
                TriggerDialogue(RelicDialogueCategory.Pickup);
            }

            // [Y] Key: Force comedy line
            if (DebugKeys.WasPressed(Key.Y))
            {
                RelicDialogueCategory[] cats = { RelicDialogueCategory.CarryChatter, RelicDialogueCategory.Sprinting, RelicDialogueCategory.MicReaction, RelicDialogueCategory.MonsterNear };
                var randomCat = cats[Random.Range(0, cats.Length)];
                TriggerDialogue(randomCat);
            }
        }
    }
}
