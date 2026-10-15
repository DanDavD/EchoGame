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

        private RelicCarryable _carryable;
        private RelicVoiceSynthesizer _synthesizer;
        private RelicDialogueUI _dialogueUI;
        private Rigidbody _rb;

        // INoiseSource implementation
        private float _currentNoiseLevel;
        Transform INoiseSource.Owner => transform;
        public float NoiseLevel01 => _currentNoiseLevel;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Muted = false;

        /// <summary>Silences or restores all relics; silencing also hides the line being shown right now.</summary>
        public static void ToggleMuted()
        {
            Muted = !Muted;
            Debug.Log($"<color=yellow>[RelicBrain]</color> Relics muted: {Muted}");
            if (!Muted) return;

            foreach (RelicDialogueUI ui in FindObjectsByType<RelicDialogueUI>(FindObjectsSortMode.None)) ui.ClearDialogue();
        }

        // Cached carrier components
        private PlayerController _carrierController;
        private IVoiceLevelSource _carrierVoiceSource;

        /// <summary>The personality chosen by the server, so every peer speaks and sings with the same voice.</summary>
        private readonly SyncVar<int> _syncedPersonality = new SyncVar<int>();

        public RelicPersonalityType Personality => _personality;

        private void Awake()
        {
            _carryable = GetComponent<RelicCarryable>();
            _synthesizer = GetComponent<RelicVoiceSynthesizer>();
            if (_synthesizer == null) _synthesizer = gameObject.AddComponent<RelicVoiceSynthesizer>();
            _dialogueUI = GetComponent<RelicDialogueUI>();
            if (_dialogueUI == null) _dialogueUI = gameObject.AddComponent<RelicDialogueUI>();
            _rb = GetComponent<Rigidbody>();

            _synthesizer.SetPersonality(_personality);
            _chatterTimer = Random.Range(6f, _carryChatterInterval);
            _idleSleepTimer = Random.Range(10f, _idleSleepInterval);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // The value that arrived with the spawn; later changes come through OnPersonalityChanged.
            if (!IsServerStarted) ApplyPersonality((RelicPersonalityType)_syncedPersonality.Value);
        }

        private void OnEnable()
        {
            NoiseRegistry.Register(this);
            _syncedPersonality.OnChange += OnPersonalityChanged;

            if (_carryable != null)
            {
                _carryable.OnPickedUp += HandlePickedUp;
                _carryable.OnDropped += HandleDropped;
            }
        }

        private void OnDisable()
        {
            NoiseRegistry.Unregister(this);
            _syncedPersonality.OnChange -= OnPersonalityChanged;

            if (_carryable != null)
            {
                _carryable.OnPickedUp -= HandlePickedUp;
                _carryable.OnDropped -= HandleDropped;
            }

            if (_synthesizer != null)
            {
                _synthesizer.StopFlyingScream();
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

            TriggerDialogue(RelicDialogueCategory.Pickup);
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

            // Docking in the altar is not a drop: the altar's own line plays instead.
            if (_carryable.IsLockedInAltar) return;

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

            if (_carryable.IsCarried)
            {
                UpdateCarriedState();
            }
            else if (!_carryable.IsLockedInAltar)
            {
                UpdateUnattendedState();
            }
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

                TriggerDialogue(RelicDialogueCategory.LandedImpact);
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
