using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace EchoProtocol.UI
{
    /// <summary>
    /// Modern UGUI Main Menu & Calibration Screen for Echo Protocol:
    /// - Game launch buttons: Solo Expedition, Host Steam Lobby, Join Local, Training Sandbox.
    /// - Live Microphone VU Calibration Meter with real-time level fill and threshold markers.
    /// - Headphone warning and quick controls briefing.
    /// - Seamless Esc toggle in-game for pause/options/disconnect.
    /// Inversion of Control: delegates networking actions via static events to preserve assembly boundaries.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        public static MainMenuController Instance { get; private set; }

        public static event System.Action RequestSolo;
        public static event System.Action RequestSteamHost;
        public static event System.Action RequestLocalClient;
        public static event System.Action RequestDisconnect;

        /// <summary>Set by the network rig when connected to a host/server.</summary>
        public static bool IsSessionActive { get; set; }

        private Canvas _canvas;
        private CanvasScaler _scaler;

        private GameObject _menuRoot;
        private Image _menuBg;

        // Title
        private Text _titleText;
        private Text _taglineText;

        // Mode Buttons
        private Button _soloBtn;
        private Button _hostSteamBtn;
        private Button _joinLocalBtn;
        private Button _sandboxBtn;
        private Button _resumeBtn;
        private Button _disconnectBtn;

        // Microphone Calibration Widget
        private RectTransform _micBarFill;
        private Image _micBarFillImg;
        private Text _micLevelLabel;

        private MicrophoneLoudness _micLoudness;

        private bool _isOpen = true;
        private Sprite _roundedBoxSprite;

        // Not created automatically any more. The network menu (LobbyMenuUI) is the main menu: it owns the waiting room,
        // Steam lobbies, errors and the match menu, while this screen started hosts behind the session's back, drew over
        // that menu and fought it for Esc and the cursor. Add this component to a scene by hand to bring it back once it
        // goes through NetworkSession.

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

            BuildSprite();
            BuildUI();

            _micLoudness = FindAnyObjectByType<MicrophoneLoudness>();

            // Auto-hide menu if match starts directly or in a pre-connected session
            if (IsSessionActive)
            {
                SetMenuOpen(false);
            }
        }

        private void BuildSprite()
        {
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
            _roundedBoxSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
        }

        private void BuildUI()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200; // Above HUD

            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920, 1080);
            _scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

            _menuRoot = new GameObject("MenuContainer");
            _menuRoot.transform.SetParent(transform, false);
            var rootRect = _menuRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _menuBg = _menuRoot.AddComponent<Image>();
            _menuBg.color = new Color(0.02f, 0.03f, 0.05f, 0.94f);

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_menuRoot.transform, false);
            var tRect = titleGo.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 0.85f);
            tRect.anchorMax = new Vector2(0.5f, 0.85f);
            tRect.sizeDelta = new Vector2(900, 70);
            _titleText = titleGo.AddComponent<Text>();
            _titleText.font = defaultFont;
            _titleText.fontSize = 52;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = new Color(0.3f, 0.95f, 1f, 1f);
            _titleText.text = "E C H O   P R O T O C O L";

            var tagGo = new GameObject("Tagline");
            tagGo.transform.SetParent(_menuRoot.transform, false);
            var tagRect = tagGo.AddComponent<RectTransform>();
            tagRect.anchorMin = new Vector2(0.5f, 0.79f);
            tagRect.anchorMax = new Vector2(0.5f, 0.79f);
            tagRect.sizeDelta = new Vector2(900, 35);
            _taglineText = tagGo.AddComponent<Text>();
            _taglineText.font = defaultFont;
            _taglineText.fontSize = 18;
            _taglineText.fontStyle = FontStyle.Italic;
            _taglineText.alignment = TextAnchor.MiddleCenter;
            _taglineText.color = new Color(0.85f, 0.88f, 0.92f, 0.85f);
            _taglineText.text = "\"In the dark, your voice reveals... and your voice attracts.\"";

            // Left Panel: Mode Buttons
            float startY = 180f;
            _soloBtn = CreateButton("SoloBtn", "SOLO EXPEDITION (OFFLINE)", new Vector2(-280, startY), () => LaunchSolo());
            _hostSteamBtn = CreateButton("HostSteamBtn", "HOST STEAM EXPEDITION", new Vector2(-280, startY - 70), () => LaunchSteamHost());
            _joinLocalBtn = CreateButton("JoinLocalBtn", "JOIN LOCAL CLIENT", new Vector2(-280, startY - 140), () => LaunchLocalClient());
            _sandboxBtn = CreateButton("SandboxBtn", "TRAINING SANDBOX", new Vector2(-280, startY - 210), () => LaunchSandbox());
            _resumeBtn = CreateButton("ResumeBtn", "RESUME EXPEDITION [ESC]", new Vector2(-280, startY - 280), () => SetMenuOpen(false));
            _disconnectBtn = CreateButton("DisconnectBtn", "DISCONNECT / LEAVE", new Vector2(-280, startY - 350), () => Disconnect());

            // Right Panel: Microphone Live VU & Calibration
            var micPanel = new GameObject("MicPanel");
            micPanel.transform.SetParent(_menuRoot.transform, false);
            var mRect = micPanel.AddComponent<RectTransform>();
            mRect.anchoredPosition = new Vector2(280, -20);
            mRect.sizeDelta = new Vector2(460, 480);
            var mBg = micPanel.AddComponent<Image>();
            mBg.sprite = _roundedBoxSprite;
            mBg.type = Image.Type.Sliced;
            mBg.color = new Color(0.05f, 0.08f, 0.12f, 0.9f);

            var mHeader = CreateLabel("MicHeader", micPanel.transform, new Vector2(0, 195), new Vector2(420, 40), "MICROPHONE CALIBRATION", 18, FontStyle.Bold, Color.cyan);
            mHeader.alignment = TextAnchor.MiddleCenter;

            // VU Bar Background
            var barBgGo = new GameObject("VuBarBg");
            barBgGo.transform.SetParent(micPanel.transform, false);
            var barBgRect = barBgGo.AddComponent<RectTransform>();
            barBgRect.anchoredPosition = new Vector2(0, 140);
            barBgRect.sizeDelta = new Vector2(380, 26);
            var barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.sprite = _roundedBoxSprite;
            barBgImg.type = Image.Type.Sliced;
            barBgImg.color = new Color(0.12f, 0.14f, 0.18f, 1f);

            // VU Bar Fill
            var barFillGo = new GameObject("VuBarFill");
            barFillGo.transform.SetParent(barBgGo.transform, false);
            _micBarFill = barFillGo.AddComponent<RectTransform>();
            _micBarFill.anchorMin = new Vector2(0, 0);
            _micBarFill.anchorMax = new Vector2(0, 1);
            _micBarFill.pivot = new Vector2(0, 0.5f);
            _micBarFill.offsetMin = new Vector2(2, 2);
            _micBarFill.offsetMax = new Vector2(2, -2);
            _micBarFill.sizeDelta = new Vector2(0, 0);
            _micBarFillImg = barFillGo.AddComponent<Image>();
            _micBarFillImg.sprite = _roundedBoxSprite;
            _micBarFillImg.type = Image.Type.Sliced;
            _micBarFillImg.color = Color.green;

            _micLevelLabel = CreateLabel("MicLabel", micPanel.transform, new Vector2(0, 100), new Vector2(380, 28), "LEVEL: Speaking Test", 14, FontStyle.Normal, Color.white);
            _micLevelLabel.alignment = TextAnchor.MiddleCenter;

            // Headphone Advisory Box
            var hpBox = new GameObject("HeadphoneBox");
            hpBox.transform.SetParent(micPanel.transform, false);
            var hpRect = hpBox.AddComponent<RectTransform>();
            hpRect.anchoredPosition = new Vector2(0, 30);
            hpRect.sizeDelta = new Vector2(410, 80);
            var hpImg = hpBox.AddComponent<Image>();
            hpImg.sprite = _roundedBoxSprite;
            hpImg.type = Image.Type.Sliced;
            hpImg.color = new Color(0.45f, 0.25f, 0.05f, 0.85f);

            var hpText = CreateLabel("HpText", hpBox.transform, Vector2.zero, new Vector2(390, 70), "⚠ HEADPHONES STRONGLY RECOMMENDED\nOpen desktop speakers will feed back into your microphone, creating voice echo that attracts the beast!", 13, FontStyle.Bold, new Color(1f, 0.95f, 0.4f));
            hpText.alignment = TextAnchor.MiddleCenter;

            // Controls Briefing Box
            var ctrlBox = new GameObject("ControlsBox");
            ctrlBox.transform.SetParent(micPanel.transform, false);
            var cRect = ctrlBox.AddComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(0, -115);
            cRect.sizeDelta = new Vector2(410, 180);
            var cImg = ctrlBox.AddComponent<Image>();
            cImg.sprite = _roundedBoxSprite;
            cImg.type = Image.Type.Sliced;
            cImg.color = new Color(0.04f, 0.05f, 0.08f, 0.85f);

            string controlsInfo = "EXPEDITION CONTROLS:\n" +
                                  "• [WASD] Move   |   [Shift] Sprint (Loud!)\n" +
                                  "• [V] HOLD TO ECHO: Flash voice light\n" +
                                  "• [E] Pick up & dock relics into Central Altar\n" +
                                  "• [G] Drop Relic   |   [T] Switch Archetype\n" +
                                  "• [Esc] Toggle Pause & Settings";
            var ctrlText = CreateLabel("CtrlText", ctrlBox.transform, Vector2.zero, new Vector2(390, 160), controlsInfo, 13, FontStyle.Normal, new Color(0.85f, 0.9f, 0.95f));
            ctrlText.alignment = TextAnchor.MiddleLeft;
        }

        private Button CreateButton(string name, string text, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_menuRoot.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(380, 52);

            var img = go.AddComponent<Image>();
            img.sprite = _roundedBoxSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.2f, 0.45f, 0.75f, 1f);
            colors.pressedColor = new Color(0.08f, 0.3f, 0.55f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lRect = labelGo.AddComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.offsetMin = Vector2.zero;
            lRect.offsetMax = Vector2.zero;
            var t = labelGo.AddComponent<Text>();
            t.font = font;
            t.fontSize = 16;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = text;

            return btn;
        }

        private Text CreateLabel(string name, Transform parent, Vector2 pos, Vector2 size, string text, int fontSize, FontStyle style, Color color)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.text = text;
            return t;
        }

        private void Update()
        {
            // Toggle menu with Escape
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsSessionActive)
                {
                    SetMenuOpen(!_isOpen);
                }
            }

            if (!_isOpen) return;

            UpdateMicVU();
            UpdateButtonStates();
        }

        private void UpdateMicVU()
        {
            if (_micLoudness == null) _micLoudness = FindAnyObjectByType<MicrophoneLoudness>();
            if (_micLoudness == null || _micBarFill == null) return;

            float level = Mathf.Clamp01(_micLoudness.Level01);
            _micBarFill.sizeDelta = new Vector2(376f * level, 0);

            if (level < 0.12f)
            {
                _micBarFillImg.color = new Color(0.2f, 0.7f, 0.3f);
                _micLevelLabel.text = $"[SILENT / AMBIENT] Level: {level:F2}";
            }
            else if (level < 0.40f)
            {
                _micBarFillImg.color = new Color(0.9f, 0.8f, 0.2f);
                _micLevelLabel.text = $"[WHISPER / TALK] Level: {level:F2}";
            }
            else
            {
                _micBarFillImg.color = new Color(0.95f, 0.25f, 0.2f);
                _micLevelLabel.text = $"[LOUD / SCREAM] Level: {level:F2} (Attracting Monster!)";
            }
        }

        private void UpdateButtonStates()
        {
            bool inSession = IsSessionActive;

            _soloBtn.gameObject.SetActive(!inSession);
            _hostSteamBtn.gameObject.SetActive(!inSession);
            _joinLocalBtn.gameObject.SetActive(!inSession);
            _sandboxBtn.gameObject.SetActive(!inSession);

            _resumeBtn.gameObject.SetActive(inSession);
            _disconnectBtn.gameObject.SetActive(inSession);
        }

        public void SetMenuOpen(bool open)
        {
            _isOpen = open;
            if (_menuRoot != null) _menuRoot.SetActive(open);

            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void LaunchSolo()
        {
            SetMenuOpen(false);
            RequestSolo?.Invoke();
        }

        private void LaunchSteamHost()
        {
            SetMenuOpen(false);
            RequestSteamHost?.Invoke();
        }

        private void LaunchLocalClient()
        {
            SetMenuOpen(false);
            RequestLocalClient?.Invoke();
        }

        private void LaunchSandbox()
        {
            SetMenuOpen(false);
            UnityEngine.SceneManagement.SceneManager.LoadScene("Sandbox_Test");
        }

        private void Disconnect()
        {
            RequestDisconnect?.Invoke();
            SetMenuOpen(true);
        }
    }
}
