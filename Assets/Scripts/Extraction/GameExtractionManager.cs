using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
using EchoProtocol.Altar;
using EchoProtocol.Level;
using EchoProtocol.Player;
using EchoProtocol.Relics;

namespace EchoProtocol.Extraction
{
    public enum ExtractionState
    {
        ObjectiveInProgress,
        ExtractionActive,
        Victory,
        Defeat
    }

    /// <summary>
    /// Manages the primary match flow: Objective -> Extraction Countdown -> Victory/Defeat.
    /// The server decides every transition and replicates the state, the countdown and the end-screen quote to the
    /// clients. Without a network (the sandbox) the same flow runs locally. Provides HUD banners during the heist and
    /// High on Life comedic post-game screens.
    /// </summary>
    public class GameExtractionManager : NetworkBehaviour
    {
        public static GameExtractionManager Instance { get; private set; }

        [Header("Extraction Settings")]
        [SerializeField] private float _extractionTimeLimit = 45f;
        [SerializeField] private AltarReceptacle _altar;

        // The state as the server holds it; the SyncVars below mirror it for the clients.
        private ExtractionState _localState = ExtractionState.ObjectiveInProgress;
        private float _localRemaining;
        private int _localQuoteIndex;
        private float _lastSentRemaining;

        private readonly SyncVar<int> _stateSync = new SyncVar<int>();
        private readonly SyncVar<float> _remainingSync = new SyncVar<float>();
        private readonly SyncVar<int> _quoteSync = new SyncVar<int>();

        private static readonly string[] VictoryQuotes = new[]
        {
            "\"WAIT, WE ACTUALLY LIVED?! I had already uninstalled myself in protest!\" - Nervous Nelly",
            "\"Don't get cocky. My specular highlight is permanently scratched. 3 stars out of 5.\" - Obsidian Bob",
            "\"A passable retreat, serf. You may now polish my royal casing with micro-fiber silk.\" - Lord Malakor",
            "\"YAY! WE WON! Can we go back inside and pet the monster again?! Please?!\" - Gloop"
        };

        private static readonly string[] DefeatQuotes = new[]
        {
            "\"You lost to a monster WITH NO EYEBALLS?! Swear on your entire Steam inventory you won't uninstall!\"",
            "\"I TOLD YOU! I LITERALLY WARNED YOU 47 TIMES! My vertices are in the monster's digestive tract!\" - Nervous Nelly",
            "\"Incompetence of cosmic proportions. My grandmother was an untextured cube and she ran faster!\" - Lord Malakor",
            "\"Crunchy! He bit my head off! 10 out of 10 teeth texture work!\" - Gloop"
        };

        /// <summary>
        /// Raised on the server when a networked match restarts. The network rig listens to it to respawn the monster
        /// and relics; this assembly cannot reference the networking code directly.
        /// </summary>
        public event System.Action ServerRestarting;

        /// <summary>True when this peer decides the match: the server, or a machine without a network.</summary>
        private bool IsAuthority => !IsSpawned || IsServerStarted;

        /// <summary>Current state of the extraction loop, as seen by this peer.</summary>
        public ExtractionState State => IsSpawned && !IsServerStarted ? (ExtractionState)_stateSync.Value : _localState;

        /// <summary>Remaining seconds before extraction failure, as seen by this peer.</summary>
        public float RemainingTime => IsSpawned && !IsServerStarted ? _remainingSync.Value : _localRemaining;

        private int QuoteIndex => IsSpawned && !IsServerStarted ? _quoteSync.Value : _localQuoteIndex;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_altar == null)
            {
                _altar = FindAnyObjectByType<AltarReceptacle>();
            }

            _localRemaining = _extractionTimeLimit;
            _stateSync.OnChange += OnStateSynced;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // A client that joins into a running match gets the current state with the spawn.
            if (!IsServerStarted) ApplyPortalState(State);
        }

        private void Start()
        {
            SpawnRuntimeRelics();
        }

        /// <summary>
        /// Where each relic appears: the level's relic spawn points, or the sandbox arena defaults when the scene has none.
        /// </summary>
        private static (Vector3 pos, RelicPersonalityType personality, string name)[] BuildRelicSpawnData()
        {
            var points = SpawnRegistry.Get(SpawnKind.Relic);
            if (points.Count == 0)
            {
                return new[]
                {
                    (new Vector3(-14f, 0.6f, 12f), RelicPersonalityType.SarcasticCritic, "Relic_ObsidianBob"),
                    (new Vector3(16f, 0.6f, 18f), RelicPersonalityType.AnxiousCoward, "Relic_NervousNelly"),
                    (new Vector3(-4f, 0.6f, 32f), RelicPersonalityType.DumbGloop, "Relic_DumbGloop")
                };
            }

            var data = new (Vector3, RelicPersonalityType, string)[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                data[i] = (points[i].transform.position, points[i].RelicPersonality, $"Relic_{points[i].RelicPersonality}");
            }
            return data;
        }

        private void SpawnRuntimeRelics()
        {
            var existing = FindObjectsByType<RelicBrain>(FindObjectsInactive.Exclude);
            if (existing != null && existing.Length >= 3) return;

            // With a FishNet NetworkManager in the scene the server owns every relic (NetworkBootstrap spawns them),
            // so spawning local copies here would create NetworkObjects that are never spawned: picking one up fails.
            // Only the offline sandbox builds its relics locally, from plain primitives.
            if (FindAnyObjectByType<FishNet.Managing.NetworkManager>() != null) return;

            (Vector3 pos, RelicPersonalityType personality, string name)[] relicData = BuildRelicSpawnData();

            for (int i = 0; i < relicData.Length; i++)
            {
                var data = relicData[i];
                GameObject relicObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                // Build inactive so Awake/OnEnable never read FishNet properties before they are bound.
                relicObj.SetActive(false);
                relicObj.transform.position = data.pos;
                relicObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                relicObj.AddComponent<Rigidbody>();
                relicObj.AddComponent<EchoProtocol.Interaction.RelicCarryable>();
                relicObj.AddComponent<RelicVoiceSynthesizer>();
                relicObj.AddComponent<RelicDialogueUI>();
                relicObj.AddComponent<RelicBrain>();
                EchoProtocol.Core.OfflineSandboxGuard.BindOffline(relicObj);
                relicObj.SetActive(true);

                relicObj.name = data.name;
                var brain = relicObj.GetComponent<RelicBrain>();
                if (brain != null)
                {
                    brain.SetPersonality(data.personality);
                }
            }
        }

        private void Update()
        {
            if (State == ExtractionState.ExtractionActive && IsAuthority)
            {
                if (MatchRules.Tick(ref _localRemaining, Time.deltaTime))
                {
                    SetRemaining(0f);
                    TriggerDefeat("Extraction time expired! Devoured in the dark.");
                }
                else if (IsSpawned && MatchRules.ShouldSync(_lastSentRemaining, _localRemaining))
                {
                    SetRemaining(_localRemaining);
                }
            }

            // Quick restart with [R] when match is resolved
            if (MatchRules.IsResolved(State) && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartMatch();
            }
        }

        /// <summary>
        /// Begins the extraction countdown and unlocks the escape portals. Called by the altar on the server, or
        /// locally without a network.
        /// </summary>
        public void StartExtraction(float duration = 45f)
        {
            if (!IsAuthority || !MatchRules.CanStartExtraction(State)) return;

            SetRemaining(duration);
            SetState(ExtractionState.ExtractionActive);
            ApplyPortalState(ExtractionState.ExtractionActive);

            Debug.Log($"<color=orange><b>[GameExtractionManager] EXTRACTION ACTIVE! {duration:F0} SECONDS REMAINING!</b></color>");
        }

        /// <summary>
        /// Called on every peer when a local player reaches an escape portal. A client asks the server; the server
        /// checks the state and ends the match for everybody.
        /// </summary>
        public void OnPlayerEscaped(PlayerController player)
        {
            if (player == null) return;

            if (IsSpawned && !IsServerStarted)
            {
                ServerRequestEscape(player.NetworkObject);
                return;
            }

            ResolveVictory();
        }

        // The portal trigger runs on the owner's client, so the server trusts that a spawned player reached it.
        [ServerRpc(RequireOwnership = false)]
        private void ServerRequestEscape(NetworkObject playerNetObj)
        {
            if (playerNetObj == null || !playerNetObj.IsSpawned) return;
            ResolveVictory();
        }

        private void ResolveVictory()
        {
            if (!IsAuthority || !MatchRules.CanEscape(State)) return;

            SetQuote(Random.Range(0, VictoryQuotes.Length));
            SetState(ExtractionState.Victory);

            Debug.Log("<color=green><b>[GameExtractionManager] EXTRACTION SUCCESSFUL! VICTORY!</b></color>");
        }

        /// <summary>
        /// Triggers match defeat. Only the server (or the offline sandbox) decides it.
        /// </summary>
        public void TriggerDefeat(string reason)
        {
            if (!IsAuthority || !MatchRules.CanDefeat(State)) return;

            SetQuote(Random.Range(0, DefeatQuotes.Length));
            SetState(ExtractionState.Defeat);

            Debug.Log($"<color=red><b>[GameExtractionManager] EXTRACTION FAILED: {reason}</b></color>");
        }

        private void OnDestroy()
        {
            _stateSync.OnChange -= OnStateSynced;
            if (Instance == this) Instance = null;
        }

        /// <summary>Starts a new match. Without a network the scene is reloaded; on a network the server resets the world.</summary>
        public void RestartMatch()
        {
            if (!MatchRules.CanRestart(State)) return;

            if (!IsSpawned)
            {
                // The symphony flips a static flag; without this the next match would start fully lit.
                EchoShaderPublisher.SetFullIllumination(false);
                // Fully qualified: NetworkBehaviour exposes its own SceneManager property.
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (IsServerStarted)
            {
                ServerRestartMatch();
            }
            else
            {
                ServerRequestRestart();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRequestRestart()
        {
            if (MatchRules.CanRestart(State)) ServerRestartMatch();
        }

        /// <summary>
        /// Server only: empties the altar, puts the countdown back, and has the network rig respawn the monster and the
        /// relics and send every player back to a spawn point. A scene reload would drop the network session instead.
        /// </summary>
        private void ServerRestartMatch()
        {
            if (_altar != null) _altar.ResetAltar();

            SetRemaining(_extractionTimeLimit);
            SetQuote(0);
            SetState(ExtractionState.ObjectiveInProgress);
            ApplyPortalState(ExtractionState.ObjectiveInProgress);

            ServerRestarting?.Invoke();

            Debug.Log("<color=cyan><b>[GameExtractionManager] Match restarted.</b></color>");
        }

        private void OnStateSynced(int prev, int next, bool asServer)
        {
            // The host already applied the change as the server.
            if (IsServerStarted) return;
            ApplyPortalState((ExtractionState)next);
        }

        /// <summary>Opens or closes the escape portals for a state. Runs on every peer.</summary>
        private static void ApplyPortalState(ExtractionState state)
        {
            if (state == ExtractionState.ExtractionActive) EscapePortal.ActivateAll();
            else if (state == ExtractionState.ObjectiveInProgress) EscapePortal.DeactivateAll();
        }

        private void SetState(ExtractionState state)
        {
            _localState = state;
            if (IsSpawned && IsServerStarted) _stateSync.Value = (int)state;
        }

        private void SetRemaining(float seconds)
        {
            _localRemaining = seconds;
            if (IsSpawned && IsServerStarted)
            {
                _remainingSync.Value = seconds;
                _lastSentRemaining = seconds;
            }
        }

        private void SetQuote(int index)
        {
            _localQuoteIndex = index;
            if (IsSpawned && IsServerStarted) _quoteSync.Value = index;
        }

        private static bool HasLocalPlayer()
        {
            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].IsSpawned && players[i].IsOwner) return true;
            }
            return false;
        }

        private string ActiveVictoryQuote => VictoryQuotes[MatchRules.ClampQuote(QuoteIndex, VictoryQuotes.Length)];
        private string ActiveDefeatQuote => DefeatQuotes[MatchRules.ClampQuote(QuoteIndex, DefeatQuotes.Length)];

        private void OnGUI()
        {
            // In a networked level the match HUD waits until this player is in the match (not in the waiting room).
            if (IsSpawned && !HasLocalPlayer()) return;

            int screenW = Screen.width;
            int screenH = Screen.height;
            ExtractionState state = State;

            // 1. In-game HUD Banner
            if (state == ExtractionState.ObjectiveInProgress)
            {
                int filled = _altar != null ? _altar.FilledSocketsCount : 0;
                int total = _altar != null ? _altar.RequiredSocketsCount : 3;

                GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
                GUI.Box(new Rect(screenW * 0.5f - 240, 18, 480, 42), GUIContent.none);

                GUI.color = Color.cyan;
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(new Rect(screenW * 0.5f - 240, 18, 480, 42),
                    $"OBJECTIVE: Place Living Relics into Central Altar [{filled}/{total}]", style);
            }
            else if (state == ExtractionState.ExtractionActive)
            {
                // Flashing red/yellow alert box
                float flash = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                Color boxColor = Color.Lerp(new Color(0.6f, 0.1f, 0.1f, 0.9f), new Color(0.9f, 0.5f, 0.05f, 0.9f), flash);

                GUI.color = boxColor;
                GUI.Box(new Rect(screenW * 0.5f - 290, 16, 580, 60), GUIContent.none);

                GUI.color = Color.white;
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 17,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(new Rect(screenW * 0.5f - 290, 20, 580, 26),
                    $"! CRITICAL EXTRACTION: ESCAPE PORTAL OPEN ! Time: {RemainingTime:F1}s", style);

                var subStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Normal
                };
                GUI.color = Color.yellow;
                GUI.Label(new Rect(screenW * 0.5f - 290, 46, 580, 24),
                    "The Symphony Choir has enraged the Beast! Sprint to the North Wall Portal!", subStyle);
            }
            // 2. Victory End Screen
            else if (state == ExtractionState.Victory)
            {
                GUI.color = new Color(0.02f, 0.08f, 0.04f, 0.92f);
                GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none);

                GUI.color = Color.green;
                var titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 32,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(new Rect(0, screenH * 0.28f, screenW, 50), "EXTRACTION SUCCESSFUL!", titleStyle);

                var rankStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                GUI.color = Color.yellow;
                GUI.Label(new Rect(0, screenH * 0.36f, screenW, 35), "RANK: S+ (Escaped with living symphony relics)", rankStyle);

                var quoteStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Italic,
                    wordWrap = true
                };
                GUI.color = Color.white;
                GUI.Label(new Rect(screenW * 0.15f, screenH * 0.44f, screenW * 0.7f, 70), ActiveVictoryQuote, quoteStyle);

                if (GUI.Button(new Rect(screenW * 0.5f - 120, screenH * 0.62f, 240, 48), "PLAY AGAIN [R]"))
                {
                    RestartMatch();
                }
            }
            // 3. Defeat End Screen
            else if (state == ExtractionState.Defeat)
            {
                GUI.color = new Color(0.12f, 0.02f, 0.02f, 0.94f);
                GUI.Box(new Rect(0, 0, screenW, screenH), GUIContent.none);

                GUI.color = Color.red;
                var titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 32,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(new Rect(0, screenH * 0.28f, screenW, 50), "EXTRACTION FAILED", titleStyle);

                var rankStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                };
                GUI.color = new Color(1f, 0.4f, 0.4f);
                GUI.Label(new Rect(0, screenH * 0.36f, screenW, 35), "STATUS: Devoured by the Echo Beast", rankStyle);

                var quoteStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Italic,
                    wordWrap = true
                };
                GUI.color = Color.white;
                GUI.Label(new Rect(screenW * 0.15f, screenH * 0.44f, screenW * 0.7f, 70), ActiveDefeatQuote, quoteStyle);

                if (GUI.Button(new Rect(screenW * 0.5f - 120, screenH * 0.62f, 240, 48), "TRY AGAIN [R]"))
                {
                    RestartMatch();
                }
            }
        }
    }
}
