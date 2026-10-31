using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using EchoProtocol.Extraction;
using EchoProtocol.Player;
using EchoProtocol.Altar;

namespace EchoProtocol.UI
{
    /// <summary>
    /// Modern UGUI In-Game HUD for Echo Protocol:
    /// - Top Center: Objective progress ([X/3] Relics secured) & Pulsing Extraction Alarm.
    /// - Center: Minimalist crosshair & dynamic interaction badge ([E] Place Living Relic).
    /// - Bottom Left: Carried relic status badge with keybind hints.
    /// - Full Screen: High-impact Victory (Rank S+) and Defeat end screens with clickable buttons.
    /// Replaces legacy OnGUI() with a responsive, anti-aliased Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameHUDController : MonoBehaviour
    {
        public static GameHUDController Instance { get; private set; }

        private Canvas _canvas;
        private CanvasScaler _scaler;

        // Top Banner
        private GameObject _topBannerRoot;
        private Image _topBannerBg;
        private Text _topBannerTitle;
        private Text _topBannerSubtitle;

        // Crosshair & Interaction
        private Image _crosshairDot;
        private GameObject _interactPromptRoot;
        private Image _interactPromptBg;
        private Text _interactPromptText;

        // Carried Item Status
        private GameObject _carriedStatusRoot;
        private Image _carriedStatusBg;
        private Text _carriedStatusText;

        // End Game Screens
        private GameObject _endScreenRoot;
        private Image _endScreenBg;
        private Text _endScreenTitle;
        private Text _endScreenRank;
        private Text _endScreenQuote;
        private Button _endScreenRestartBtn;
        private Text _endScreenRestartBtnText;
        private GameObject _endScreenMenuBtn;

        // "CAUGHT!" sting in the middle of the screen when the team loses a life
        private Text _caughtToast;
        private float _caughtToastUntil;
        private GameExtractionManager _listenedMatch;
        private AltarReceptacle _altar;

        private const float CaughtToastSeconds = 2.6f;

        // First-match hints under the objective banner
        private Text _hintText;
        private float _matchStartTime = -1f;
        private bool _hasEchoed;

        // Procedural Textures
        private Sprite _panelSprite;
        private Sprite _dotSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureHUD()
        {
            if (FindAnyObjectByType<GameHUDController>() == null)
            {
                var go = new GameObject("UGUI_GameHUD");
                // This runs once per launch, so the HUD has to survive scene loads (the offline restart reloads the scene).
                DontDestroyOnLoad(go);
                go.AddComponent<GameHUDController>();
            }
        }

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

            BuildProceduralSprites();
            BuildCanvasHierarchy();
            EnsureEventSystem();
        }

        // A Canvas button only receives clicks through an EventSystem, and this project reads input with the new Input
        // System only, so the module has to be the Input System one.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private void BuildProceduralSprites()
        {
            // 32x32 rounded box texture
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 10f);
                    float dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 10f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(5.5f - dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _panelSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));

            // Small dot texture
            var dotTex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    float a = Mathf.Clamp01(3.5f - d);
                    dotTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            dotTex.Apply();
            _dotSprite = Sprite.Create(dotTex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));
        }

        private void BuildCanvasHierarchy()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920, 1080);
            _scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            // 1. Top Banner
            _topBannerRoot = new GameObject("TopBanner");
            _topBannerRoot.transform.SetParent(transform, false);
            var bannerRect = _topBannerRoot.AddComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0, -20);
            bannerRect.sizeDelta = new Vector2(580, 64);

            _topBannerBg = _topBannerRoot.AddComponent<Image>();
            _topBannerBg.sprite = _panelSprite;
            _topBannerBg.type = Image.Type.Sliced;
            _topBannerBg.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);

            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_topBannerRoot.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.4f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            _topBannerTitle = titleObj.AddComponent<Text>();
            _topBannerTitle.font = defaultFont;
            _topBannerTitle.fontSize = 20;
            _topBannerTitle.fontStyle = FontStyle.Bold;
            _topBannerTitle.alignment = TextAnchor.MiddleCenter;
            _topBannerTitle.color = Color.cyan;

            var subObj = new GameObject("Subtitle");
            subObj.transform.SetParent(_topBannerRoot.transform, false);
            var subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0, 0);
            subRect.anchorMax = new Vector2(1, 0.45f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
            _topBannerSubtitle = subObj.AddComponent<Text>();
            _topBannerSubtitle.font = defaultFont;
            _topBannerSubtitle.fontSize = 13;
            _topBannerSubtitle.alignment = TextAnchor.MiddleCenter;
            _topBannerSubtitle.color = new Color(0.85f, 0.85f, 0.9f, 0.9f);

            // 2. Crosshair Dot & Interaction Prompt
            var crosshairObj = new GameObject("CrosshairDot");
            crosshairObj.transform.SetParent(transform, false);
            var crosshairRect = crosshairObj.AddComponent<RectTransform>();
            crosshairRect.sizeDelta = new Vector2(6, 6);
            _crosshairDot = crosshairObj.AddComponent<Image>();
            _crosshairDot.sprite = _dotSprite;
            _crosshairDot.color = new Color(1f, 1f, 1f, 0.65f);

            _interactPromptRoot = new GameObject("InteractPrompt");
            _interactPromptRoot.transform.SetParent(transform, false);
            var promptRect = _interactPromptRoot.AddComponent<RectTransform>();
            promptRect.anchoredPosition = new Vector2(0, -65);
            promptRect.sizeDelta = new Vector2(420, 38);
            _interactPromptBg = _interactPromptRoot.AddComponent<Image>();
            _interactPromptBg.sprite = _panelSprite;
            _interactPromptBg.type = Image.Type.Sliced;
            _interactPromptBg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

            var promptTextObj = new GameObject("PromptText");
            promptTextObj.transform.SetParent(_interactPromptRoot.transform, false);
            var pTextRect = promptTextObj.AddComponent<RectTransform>();
            pTextRect.anchorMin = Vector2.zero;
            pTextRect.anchorMax = Vector2.one;
            pTextRect.offsetMin = new Vector2(10, 0);
            pTextRect.offsetMax = new Vector2(-10, 0);
            _interactPromptText = promptTextObj.AddComponent<Text>();
            _interactPromptText.font = defaultFont;
            _interactPromptText.fontSize = 16;
            _interactPromptText.fontStyle = FontStyle.Bold;
            _interactPromptText.alignment = TextAnchor.MiddleCenter;
            _interactPromptText.color = new Color(1f, 0.92f, 0.35f, 1f);

            // 3. Carried Status Widget
            _carriedStatusRoot = new GameObject("CarriedStatus");
            _carriedStatusRoot.transform.SetParent(transform, false);
            var carriedRect = _carriedStatusRoot.AddComponent<RectTransform>();
            carriedRect.anchorMin = new Vector2(0, 0);
            carriedRect.anchorMax = new Vector2(0, 0);
            carriedRect.pivot = new Vector2(0, 0);
            carriedRect.anchoredPosition = new Vector2(25, 25);
            carriedRect.sizeDelta = new Vector2(460, 48);

            _carriedStatusBg = _carriedStatusRoot.AddComponent<Image>();
            _carriedStatusBg.sprite = _panelSprite;
            _carriedStatusBg.type = Image.Type.Sliced;
            _carriedStatusBg.color = new Color(0.04f, 0.06f, 0.08f, 0.88f);

            var carriedTextObj = new GameObject("CarriedText");
            carriedTextObj.transform.SetParent(_carriedStatusRoot.transform, false);
            var cTextRect = carriedTextObj.AddComponent<RectTransform>();
            cTextRect.anchorMin = Vector2.zero;
            cTextRect.anchorMax = Vector2.one;
            cTextRect.offsetMin = new Vector2(16, 0);
            cTextRect.offsetMax = new Vector2(-16, 0);
            _carriedStatusText = carriedTextObj.AddComponent<Text>();
            _carriedStatusText.font = defaultFont;
            _carriedStatusText.fontSize = 14;
            _carriedStatusText.fontStyle = FontStyle.Bold;
            _carriedStatusText.alignment = TextAnchor.MiddleLeft;
            _carriedStatusText.color = new Color(0.45f, 1f, 0.6f);

            // 4. End Game Screen (Victory / Defeat)
            _endScreenRoot = new GameObject("EndGameScreen");
            _endScreenRoot.transform.SetParent(transform, false);
            var endRect = _endScreenRoot.AddComponent<RectTransform>();
            endRect.anchorMin = Vector2.zero;
            endRect.anchorMax = Vector2.one;
            endRect.offsetMin = Vector2.zero;
            endRect.offsetMax = Vector2.zero;

            _endScreenBg = _endScreenRoot.AddComponent<Image>();
            _endScreenBg.color = new Color(0.02f, 0.04f, 0.05f, 0.95f);

            var endTitleObj = new GameObject("EndTitle");
            endTitleObj.transform.SetParent(_endScreenRoot.transform, false);
            var etRect = endTitleObj.AddComponent<RectTransform>();
            etRect.anchorMin = new Vector2(0, 0.62f);
            etRect.anchorMax = new Vector2(1, 0.74f);
            etRect.offsetMin = Vector2.zero;
            etRect.offsetMax = Vector2.zero;
            _endScreenTitle = endTitleObj.AddComponent<Text>();
            _endScreenTitle.font = defaultFont;
            _endScreenTitle.fontSize = 38;
            _endScreenTitle.fontStyle = FontStyle.Bold;
            _endScreenTitle.alignment = TextAnchor.MiddleCenter;

            var endRankObj = new GameObject("EndRank");
            endRankObj.transform.SetParent(_endScreenRoot.transform, false);
            var erRect = endRankObj.AddComponent<RectTransform>();
            erRect.anchorMin = new Vector2(0, 0.54f);
            erRect.anchorMax = new Vector2(1, 0.62f);
            erRect.offsetMin = Vector2.zero;
            erRect.offsetMax = Vector2.zero;
            _endScreenRank = endRankObj.AddComponent<Text>();
            _endScreenRank.font = defaultFont;
            _endScreenRank.fontSize = 22;
            _endScreenRank.fontStyle = FontStyle.Bold;
            _endScreenRank.alignment = TextAnchor.MiddleCenter;

            var endQuoteObj = new GameObject("EndQuote");
            endQuoteObj.transform.SetParent(_endScreenRoot.transform, false);
            var eqRect = endQuoteObj.AddComponent<RectTransform>();
            eqRect.anchorMin = new Vector2(0.15f, 0.38f);
            eqRect.anchorMax = new Vector2(0.85f, 0.52f);
            eqRect.offsetMin = Vector2.zero;
            eqRect.offsetMax = Vector2.zero;
            _endScreenQuote = endQuoteObj.AddComponent<Text>();
            _endScreenQuote.font = defaultFont;
            _endScreenQuote.fontSize = 17;
            _endScreenQuote.fontStyle = FontStyle.Italic;
            _endScreenQuote.alignment = TextAnchor.MiddleCenter;
            _endScreenQuote.color = new Color(0.9f, 0.9f, 0.95f);

            // Restart Button
            var btnObj = new GameObject("RestartButton");
            btnObj.transform.SetParent(_endScreenRoot.transform, false);
            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.22f);
            btnRect.anchorMax = new Vector2(0.5f, 0.22f);
            btnRect.sizeDelta = new Vector2(260, 52);

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.sprite = _panelSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = new Color(0.15f, 0.45f, 0.85f, 0.95f);

            _endScreenRestartBtn = btnObj.AddComponent<Button>();
            _endScreenRestartBtn.onClick.AddListener(OnRestartClicked);

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btRect = btnTextObj.AddComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            _endScreenRestartBtnText = btnTextObj.AddComponent<Text>();
            _endScreenRestartBtnText.font = defaultFont;
            _endScreenRestartBtnText.fontSize = 18;
            _endScreenRestartBtnText.fontStyle = FontStyle.Bold;
            _endScreenRestartBtnText.alignment = TextAnchor.MiddleCenter;
            _endScreenRestartBtnText.color = Color.white;
            _endScreenRestartBtnText.text = "PLAY AGAIN [R]";

            // Back to the main menu (only where there is a network session to leave)
            _endScreenMenuBtn = new GameObject("MenuButton");
            _endScreenMenuBtn.transform.SetParent(_endScreenRoot.transform, false);
            var menuRect = _endScreenMenuBtn.AddComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.5f, 0.14f);
            menuRect.anchorMax = new Vector2(0.5f, 0.14f);
            menuRect.sizeDelta = new Vector2(260, 44);
            var menuImg = _endScreenMenuBtn.AddComponent<Image>();
            menuImg.sprite = _panelSprite;
            menuImg.type = Image.Type.Sliced;
            menuImg.color = new Color(0.18f, 0.2f, 0.26f, 0.95f);
            _endScreenMenuBtn.AddComponent<Button>().onClick.AddListener(MatchMenu.Leave);
            var menuTextObj = new GameObject("Text");
            menuTextObj.transform.SetParent(_endScreenMenuBtn.transform, false);
            var mtRect = menuTextObj.AddComponent<RectTransform>();
            mtRect.anchorMin = Vector2.zero;
            mtRect.anchorMax = Vector2.one;
            mtRect.offsetMin = Vector2.zero;
            mtRect.offsetMax = Vector2.zero;
            var menuText = menuTextObj.AddComponent<Text>();
            menuText.font = defaultFont;
            menuText.fontSize = 16;
            menuText.fontStyle = FontStyle.Bold;
            menuText.alignment = TextAnchor.MiddleCenter;
            menuText.color = Color.white;
            menuText.text = "BACK TO MENU";

            _endScreenRoot.SetActive(false);

            // 5. Caught sting
            var toastObj = new GameObject("CaughtToast");
            toastObj.transform.SetParent(transform, false);
            var toastRect = toastObj.AddComponent<RectTransform>();
            toastRect.anchoredPosition = new Vector2(0, 120);
            toastRect.sizeDelta = new Vector2(900, 90);
            _caughtToast = toastObj.AddComponent<Text>();
            _caughtToast.font = defaultFont;
            _caughtToast.fontSize = 34;
            _caughtToast.fontStyle = FontStyle.Bold;
            _caughtToast.alignment = TextAnchor.MiddleCenter;
            _caughtToast.color = new Color(1f, 0.25f, 0.2f);
            toastObj.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            toastObj.SetActive(false);

            // 6. First-match hint, just under the objective banner
            var hintObj = new GameObject("FirstMatchHint");
            hintObj.transform.SetParent(transform, false);
            var hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0, -92);
            hintRect.sizeDelta = new Vector2(900, 30);
            _hintText = hintObj.AddComponent<Text>();
            _hintText.font = defaultFont;
            _hintText.fontSize = 16;
            _hintText.alignment = TextAnchor.MiddleCenter;
            _hintText.color = new Color(1f, 0.92f, 0.6f, 0.95f);
            hintObj.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            hintObj.SetActive(false);

            // 7. Teammates' names floating above their heads (behind everything else on this canvas)
            gameObject.AddComponent<NameTagOverlay>().Initialise((RectTransform)transform, defaultFont);
        }

        private void Update()
        {
            ListenToMatch();
            UpdateTopBanner();
            UpdateInteractionAndCarried();
            UpdateEndScreen();
            UpdateCaughtToast();
            UpdateHint();
        }

        /// <summary>The first-match hint on screen, or null (for tests).</summary>
        public string HintText => _hintText != null && _hintText.gameObject.activeSelf ? _hintText.text : null;

        private void UpdateHint()
        {
            var mgr = GameExtractionManager.Active;
            var player = FindLocalPlayer();
            string hint = null;

            if (mgr != null && player != null && mgr.State == ExtractionState.ObjectiveInProgress)
            {
                // The clock starts when this player is in the match (not in the waiting room).
                if (_matchStartTime < 0f) _matchStartTime = Time.time;
                if (player.IsEchoHeld) _hasEchoed = true;

                var pi = player.GetComponent<PlayerInteraction>();
                int onAltar = _altar != null ? _altar.FilledSocketsCount : 0;
                hint = FirstMatchHints.Pick(Time.time - _matchStartTime, _hasEchoed, pi != null && pi.CarriedRelic != null, onAltar);
            }

            bool show = hint != null && !MatchMenu.IsOpen;
            if (_hintText.gameObject.activeSelf != show) _hintText.gameObject.SetActive(show);
            if (show && _hintText.text != hint) _hintText.text = hint;
        }

        private void OnDestroy()
        {
            if (_listenedMatch != null) _listenedMatch.LifeLost -= HandleLifeLost;
            if (Instance == this) Instance = null;
        }

        // The match flow can be replaced (a scene reload), so follow whichever instance is current.
        private void ListenToMatch()
        {
            var mgr = GameExtractionManager.Active;
            if (mgr == _listenedMatch) return;
            if (_listenedMatch != null) _listenedMatch.LifeLost -= HandleLifeLost;
            _listenedMatch = mgr;
            if (_listenedMatch != null) _listenedMatch.LifeLost += HandleLifeLost;
        }

        private void HandleLifeLost(int livesLeft)
        {
            _caughtToast.text = livesLeft > 0
                ? $"CAUGHT!  {livesLeft} {(livesLeft == 1 ? "life" : "lives")} left for the team"
                : "CAUGHT!  No lives left";
            _caughtToastUntil = Time.time + CaughtToastSeconds;
        }

        /// <summary>The text of the "caught" sting while it is on screen, or null (for tests).</summary>
        public string CaughtToastText => _caughtToast != null && _caughtToast.gameObject.activeSelf ? _caughtToast.text : null;

        private void UpdateCaughtToast()
        {
            float left = _caughtToastUntil - Time.time;
            bool show = left > 0f;
            if (_caughtToast.gameObject.activeSelf != show) _caughtToast.gameObject.SetActive(show);
            if (!show) return;

            Color c = _caughtToast.color;
            c.a = Mathf.Clamp01(left / 0.6f); // fades out over the last moment
            _caughtToast.color = c;
        }

        private void UpdateTopBanner()
        {
            var mgr = GameExtractionManager.Active;
            if (mgr == null) return;

            if (_altar == null) _altar = FindAnyObjectByType<AltarReceptacle>();
            var altar = _altar;
            ExtractionState state = mgr.State;

            if (state == ExtractionState.ObjectiveInProgress)
            {
                _topBannerRoot.SetActive(true);
                int filled = altar != null ? altar.FilledSocketsCount : 0;
                int total = altar != null ? altar.RequiredSocketsCount : 3;

                _topBannerBg.color = new Color(0.04f, 0.06f, 0.10f, 0.88f);
                _topBannerTitle.color = Color.cyan;
                _topBannerTitle.text = $"PRIMARY OBJECTIVE: Secure Living Relics [{filled}/{total}]";
                _topBannerSubtitle.color = new Color(0.85f, 0.85f, 0.9f, 0.9f);
                _topBannerSubtitle.text = $"Carry them to the Central Altar. Keep quiet.   TEAM LIVES {mgr.TeamLives}/{mgr.MaxTeamLives}";
            }
            else if (state == ExtractionState.ExtractionActive)
            {
                _topBannerRoot.SetActive(true);
                float flash = (Mathf.Sin(Time.time * 7f) + 1f) * 0.5f;
                _topBannerBg.color = Color.Lerp(new Color(0.55f, 0.08f, 0.08f, 0.94f), new Color(0.85f, 0.42f, 0.04f, 0.94f), flash);

                _topBannerTitle.color = Color.white;
                _topBannerTitle.text = $"⚠ CRITICAL EXTRACTION: ESCAPE PORTAL UNLOCKED — {mgr.RemainingTime:F1}s";
                _topBannerSubtitle.color = Color.yellow;
                _topBannerSubtitle.text = $"The Beast is enraged! SPRINT TO THE EXIT PORTAL!   TEAM LIVES {mgr.TeamLives}/{mgr.MaxTeamLives}";
            }
            else
            {
                _topBannerRoot.SetActive(false);
            }
        }

        private void UpdateInteractionAndCarried()
        {
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                _interactPromptRoot.SetActive(false);
                _carriedStatusRoot.SetActive(false);
                return;
            }

            var pi = localPlayer.GetComponent<PlayerInteraction>();
            if (pi == null) return;

            // Prompt: handing the carried relic to the teammate in view wins over anything else under the crosshair.
            var interactable = pi.HoveredInteractable;
            if (pi.CarriedRelic != null && pi.HoveredTeammate != null)
            {
                bool canPass = pi.CarriedRelic.CanPassTo(localPlayer, pi.HoveredTeammate);
                _interactPromptRoot.SetActive(true);
                _interactPromptText.text = canPass ? $"[E] Pass {pi.CarriedRelic.RelicName}" : "Their hands are full (or they are too far)";
            }
            else if (interactable != null && interactable.CanInteract(localPlayer))
            {
                _interactPromptRoot.SetActive(true);
                _interactPromptText.text = $"[E] {interactable.PromptText}";
            }
            else
            {
                _interactPromptRoot.SetActive(false);
            }

            // Carried Relic
            if (pi.CarriedRelic != null)
            {
                _carriedStatusRoot.SetActive(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _carriedStatusText.text = $"Carrying: {pi.CarriedRelic.RelicName}   [G] Drop   [E] Pass   [T] Personality   [Y] Roast";
#else
                _carriedStatusText.text = $"Carrying: {pi.CarriedRelic.RelicName}   [G] Drop   [E] Pass to a teammate";
#endif
            }
            else
            {
                _carriedStatusRoot.SetActive(false);
            }
        }

        private void UpdateEndScreen()
        {
            var mgr = GameExtractionManager.Active;
            if (mgr == null) return;

            ExtractionState state = mgr.State;
            if (state == ExtractionState.Victory)
            {
                _endScreenRoot.SetActive(true);
                _endScreenBg.color = new Color(0.01f, 0.08f, 0.04f, 0.95f);
                _endScreenTitle.color = Color.green;
                _endScreenTitle.text = "EXTRACTION SUCCESSFUL!";
                _endScreenRank.color = Color.yellow;
                _endScreenRank.text = "RANK: S+ (Expedition Master)";
                _endScreenQuote.text = mgr.ActiveVictoryQuote;
                SetEndButtons(mgr, "PLAY AGAIN [R]");
            }
            else if (state == ExtractionState.Defeat)
            {
                _endScreenRoot.SetActive(true);
                _endScreenBg.color = new Color(0.12f, 0.02f, 0.02f, 0.95f);
                _endScreenTitle.color = Color.red;
                _endScreenTitle.text = "EXPEDITION FAILED";
                _endScreenRank.color = new Color(1f, 0.4f, 0.4f);
                _endScreenRank.text = mgr.TeamLives <= 0
                    ? "STATUS: Caught one time too many by the Blind Echo Beast"
                    : "STATUS: The portal closed. Devoured in the dark.";
                _endScreenQuote.text = mgr.ActiveDefeatQuote;
                SetEndButtons(mgr, "TRY AGAIN [R]");
            }
            else
            {
                _endScreenRoot.SetActive(false);
            }
        }

        // Only the host restarts a networked match; everyone can go back to the menu.
        private void SetEndButtons(GameExtractionManager mgr, string restartLabel)
        {
            bool canRestart = mgr.CanRestartHere;
            _endScreenRestartBtn.interactable = canRestart;
            _endScreenRestartBtnText.text = canRestart ? restartLabel : "WAITING FOR THE HOST...";
            if (_endScreenMenuBtn.activeSelf != MatchMenu.CanLeave) _endScreenMenuBtn.SetActive(MatchMenu.CanLeave);
        }

        private void OnRestartClicked()
        {
            if (GameExtractionManager.Active != null)
            {
                GameExtractionManager.Active.RestartMatch();
            }
        }

        private PlayerController FindLocalPlayer()
        {
            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && (!players[i].IsSpawned || players[i].IsOwner))
                {
                    return players[i];
                }
            }
            return null;
        }
    }
}
