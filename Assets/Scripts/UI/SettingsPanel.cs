using EchoProtocol.Settings;
using UnityEngine;

namespace EchoProtocol.UI
{
    /// <summary>
    /// The settings screen, shared by the main menu and the in-match menu: volumes, mouse, microphone (device, echo
    /// sensitivity and a live test meter) and full screen. Every change applies at once through
    /// <see cref="SettingsManager"/>; closing the screen saves to disk.
    ///
    /// The test meter reads the local player's microphone during a match. In the main menu there is no player yet, so the
    /// panel opens a hidden <see cref="MicrophoneLoudness"/> of its own while it is on screen; it runs the same capture and
    /// calibration as the game, so what you see is what the echo will get.
    /// </summary>
    public sealed class SettingsPanel
    {
        public const float Width = 640f;
        public const float Height = 724f;

        private const float Pad = 40f;
        private const string DefaultDeviceLabel = "System default";

        private GameSettings _draft;
        private MicrophoneLoudness _testMic;
        private GameObject _testMicObject;

        public bool IsOpen { get; private set; }

        /// <summary>The microphone the meter shows (the player's, or the panel's own in the main menu).</summary>
        public MicrophoneLoudness Meter => _testMic;

        /// <summary>True while the panel runs a test microphone of its own (there is no local player to read).</summary>
        public bool UsesOwnTestMicrophone => _testMicObject != null;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _draft = SettingsManager.Current.Clone();
            _draft.Fullscreen = Screen.fullScreen;
            AttachTestMicrophone();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SettingsManager.Save();
            if (_testMicObject != null) Object.Destroy(_testMicObject);
            _testMicObject = null;
            _testMic = null;
        }

        /// <summary>Draws the screen centred in <paramref name="screen"/>. Returns true when the player pressed BACK.</summary>
        public bool Draw(Vector2 screen)
        {
            if (!IsOpen) Open();
            if (_testMic == null) AttachTestMicrophone(); // the player spawned, or left, while the panel was open

            Rect card = new Rect((screen.x - Width) * 0.5f, Mathf.Max(12f, (screen.y - Height) * 0.5f), Width, Height);
            EchoGui.Card(card, EchoGui.Cyan);
            EchoGui.PointerOverPanel |= card.Contains(Event.current.mousePosition);

            float x = card.x + Pad;
            float w = card.width - Pad * 2f;
            float y = card.y + 30f;

            EchoGui.Label(new Rect(x, y, w, 30f), "SETTINGS", EchoGui.Heading);
            y += 46f;

            EchoGui.Divider(new Rect(x, y, w, 24f), "AUDIO");
            y += 34f;
            _draft.MasterVolume = SliderRow(x, ref y, w, "Master volume", _draft.MasterVolume, 0f, 1f, Percent(_draft.MasterVolume));
            _draft.AmbienceVolume = SliderRow(x, ref y, w, "Ambience and music", _draft.AmbienceVolume, 0f, 1f, Percent(_draft.AmbienceVolume));
            y += 8f;

            EchoGui.Divider(new Rect(x, y, w, 24f), "MOUSE");
            y += 34f;
            _draft.MouseSensitivity = SliderRow(x, ref y, w, "Mouse sensitivity", _draft.MouseSensitivity,
                GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity, $"{_draft.MouseSensitivity:F2}x");
            _draft.InvertMouseY = EchoGui.Toggle(new Rect(x, y, w, 34f), _draft.InvertMouseY, "Invert mouse Y");
            y += 50f;

            EchoGui.Divider(new Rect(x, y, w, 24f), "MICROPHONE");
            y += 34f;
            DrawMicrophone(x, ref y, w);

            EchoGui.Divider(new Rect(x, y, w, 24f), "DISPLAY");
            y += 34f;
            _draft.Fullscreen = EchoGui.Toggle(new Rect(x, y, w, 34f), _draft.Fullscreen, "Full screen");
            y += 54f;

            bool back = false;
            const float buttonWidth = 200f;
            if (EchoGui.Button(new Rect(x, y, buttonWidth, 46f), "RESET"))
            {
                GameSettings defaults = GameSettings.Defaults();
                defaults.MicrophoneDevice = _draft.MicrophoneDevice; // a reset keeps your microphone and window
                defaults.Fullscreen = _draft.Fullscreen;
                _draft = defaults;
            }
            if (EchoGui.Button(new Rect(x + w - buttonWidth, y, buttonWidth, 46f), "BACK", EchoGui.ButtonKind.Primary)) back = true;

            if (!_draft.SameAs(SettingsManager.Current))
            {
                SettingsManager.Apply(_draft);
                _draft = SettingsManager.Current.Clone();
            }
            return back;
        }

        private void DrawMicrophone(float x, ref float y, float w)
        {
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                EchoGui.Label(new Rect(x, y, w, 52f),
                    "No microphone found. Connect one and open this screen again: the echo needs your voice.",
                    EchoGui.Body, EchoGui.Red);
                y += 66f;
                return;
            }

            // Index 0 is the system default, then every connected device.
            int index = string.IsNullOrEmpty(_draft.MicrophoneDevice) ? 0 : System.Array.IndexOf(devices, _draft.MicrophoneDevice) + 1;
            bool missing = index == 0 && !string.IsNullOrEmpty(_draft.MicrophoneDevice);
            string name = index == 0 ? DefaultDeviceLabel : devices[index - 1];
            if (missing) name = DefaultDeviceLabel + " (saved one not found)";

            int step = EchoGui.Stepper(new Rect(x, y, w, 42f), Shorten(name, 44));
            if (step != 0)
            {
                int count = devices.Length + 1;
                index = ((index + step) % count + count) % count;
                _draft.MicrophoneDevice = index == 0 ? string.Empty : devices[index - 1];
            }
            y += 52f;

            _draft.EchoSensitivity = SliderRow(x, ref y, w, "Echo sensitivity", _draft.EchoSensitivity,
                GameSettings.MinEchoSensitivity, GameSettings.MaxEchoSensitivity, $"{_draft.EchoSensitivity:F2}x");

            float level = _testMic != null ? _testMic.Level01 : 0f;
            Color color = level > 0.78f ? EchoGui.Red : level > 0.12f ? EchoGui.Cyan : EchoGui.MutedColor;
            EchoGui.Label(new Rect(x, y, w * 0.36f, 30f), "Test", EchoGui.Body, EchoGui.TextColor, TextAnchor.MiddleLeft);
            EchoGui.Meter(new Rect(x + w * 0.36f, y + 6f, w * 0.64f, 18f), level, color);
            y += 36f;
            EchoGui.Label(new Rect(x, y, w, 44f),
                "Speak: the bar should move. It learns your room in the first seconds, so stay quiet for a moment first.",
                EchoGui.Muted);
            y += 54f;
        }

        private static float SliderRow(float x, ref float y, float w, string caption, float value, float min, float max, string valueText)
        {
            EchoGui.Label(new Rect(x, y, w * 0.36f, 30f), caption, EchoGui.Body, EchoGui.TextColor, TextAnchor.MiddleLeft);
            float result = EchoGui.Slider(new Rect(x + w * 0.36f, y, w * 0.48f, 30f), value, min, max);
            EchoGui.Label(new Rect(x + w * 0.86f, y, w * 0.14f, 30f), valueText, EchoGui.Body, EchoGui.MutedColor, TextAnchor.MiddleRight);
            y += 42f;
            return result;
        }

        // The meter follows the local player's microphone in a match; in the menus the panel brings its own.
        private void AttachTestMicrophone()
        {
            MicrophoneLoudness playerMic = FindLocalPlayerMicrophone();
            if (playerMic != null)
            {
                if (_testMicObject != null) Object.Destroy(_testMicObject);
                _testMicObject = null;
                _testMic = playerMic;
                return;
            }

            if (_testMicObject != null) return;
            _testMicObject = new GameObject("[MicrophoneTest]");
            _testMic = _testMicObject.AddComponent<MicrophoneLoudness>();
            _testMic.ShowDebugBar = false;
        }

        private static MicrophoneLoudness FindLocalPlayerMicrophone()
        {
            foreach (PlayerController player in PlayerController.ActivePlayers)
            {
                if (player == null || (player.IsSpawned && !player.IsOwner)) continue;
                var mic = player.GetComponent<MicrophoneLoudness>();
                if (mic != null && mic.isActiveAndEnabled) return mic;
            }
            return null;
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 3) + "...";
    }
}
