using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
    /// Provides HUD banners during the heist and High on Life comedic post-game screens.
    /// </summary>
    public class GameExtractionManager : MonoBehaviour
    {
        public static GameExtractionManager Instance { get; private set; }

        [Header("Extraction Settings")]
        [SerializeField] private float _extractionTimeLimit = 45f;
        [SerializeField] private AltarReceptacle _altar;

        private ExtractionState _state = ExtractionState.ObjectiveInProgress;
        private float _remainingTime;
        private string _activeVictoryQuote;
        private string _activeDefeatQuote;

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

        /// <summary>Current state of the extraction loop.</summary>
        public ExtractionState State => _state;

        /// <summary>Remaining seconds before extraction failure.</summary>
        public float RemainingTime => _remainingTime;

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

            _remainingTime = _extractionTimeLimit;
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
            if (_state == ExtractionState.ExtractionActive)
            {
                _remainingTime -= Time.deltaTime;
                if (_remainingTime <= 0f)
                {
                    _remainingTime = 0f;
                    TriggerDefeat("Extraction time expired! Devoured in the dark.");
                }
            }

            // Quick restart with [R] when match is resolved
            if ((_state == ExtractionState.Victory || _state == ExtractionState.Defeat) && Keyboard.current != null)
            {
                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    RestartMatch();
                }
            }
        }

        /// <summary>
        /// Begins the 45-second extraction countdown and unlocks all escape portals.
        /// </summary>
        public void StartExtraction(float duration = 45f)
        {
            if (_state != ExtractionState.ObjectiveInProgress) return;

            _state = ExtractionState.ExtractionActive;
            _remainingTime = duration;

            EscapePortal.ActivateAll();

            Debug.Log($"<color=orange><b>[GameExtractionManager] EXTRACTION ACTIVE! {duration:F0} SECONDS REMAINING!</b></color>");
        }

        /// <summary>
        /// Called when a player reaches an active escape portal.
        /// </summary>
        public void OnPlayerEscaped(PlayerController player)
        {
            if (_state == ExtractionState.Victory || _state == ExtractionState.Defeat) return;

            _state = ExtractionState.Victory;
            _activeVictoryQuote = VictoryQuotes[Random.Range(0, VictoryQuotes.Length)];

            Debug.Log("<color=green><b>[GameExtractionManager] EXTRACTION SUCCESSFUL! VICTORY!</b></color>");
        }

        /// <summary>
        /// Triggers match defeat.
        /// </summary>
        public void TriggerDefeat(string reason)
        {
            if (_state == ExtractionState.Victory || _state == ExtractionState.Defeat) return;

            _state = ExtractionState.Defeat;
            _activeDefeatQuote = DefeatQuotes[Random.Range(0, DefeatQuotes.Length)];

            Debug.Log($"<color=red><b>[GameExtractionManager] EXTRACTION FAILED: {reason}</b></color>");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RestartMatch()
        {
            // The symphony flips a static flag; without this the next match would start fully lit.
            EchoShaderPublisher.SetFullIllumination(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnGUI()
        {
            int screenW = Screen.width;
            int screenH = Screen.height;

            // 1. In-game HUD Banner
            if (_state == ExtractionState.ObjectiveInProgress)
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
            else if (_state == ExtractionState.ExtractionActive)
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
                    $"! CRITICAL EXTRACTION: ESCAPE PORTAL OPEN ! Time: {_remainingTime:F1}s", style);

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
            else if (_state == ExtractionState.Victory)
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
                GUI.Label(new Rect(screenW * 0.15f, screenH * 0.44f, screenW * 0.7f, 70), _activeVictoryQuote, quoteStyle);

                if (GUI.Button(new Rect(screenW * 0.5f - 120, screenH * 0.62f, 240, 48), "PLAY AGAIN [R]"))
                {
                    RestartMatch();
                }
            }
            // 3. Defeat End Screen
            else if (_state == ExtractionState.Defeat)
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
                GUI.Label(new Rect(screenW * 0.15f, screenH * 0.44f, screenW * 0.7f, 70), _activeDefeatQuote, quoteStyle);

                if (GUI.Button(new Rect(screenW * 0.5f - 120, screenH * 0.62f, 240, 48), "TRY AGAIN [R]"))
                {
                    RestartMatch();
                }
            }
        }
    }
}
