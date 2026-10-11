using System.Collections;
using UnityEngine;

namespace EchoProtocol.Relics
{
    /// <summary>
    /// Renders High on Life style dialogue UI:
    /// 1. Screen-space HUD dialogue card at the bottom of the screen with typewriter effect and neon accents.
    /// 2. Floating 3D world-space speech bubble above the relic so co-op players see where the voice comes from.
    /// </summary>
    public class RelicDialogueUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _charactersPerSecond = 46f;
        [SerializeField] private float _displayLingerSeconds = 3.6f;
        [SerializeField] private float _worldBubbleMaxDistance = 30f;
        [SerializeField] private Vector3 _worldBubbleOffset = new Vector3(0f, 0.55f, 0f);

        private RelicPersonalityType _currentPersonality;
        private string _targetLine = "";
        private string _currentVisibleText = "";
        private float _typewriterProgress;
        private float _fadeAlpha;
        private float _lingerTimer;
        private bool _isDisplaying;

        private Coroutine _typewriterCoroutine;
        private RelicVoiceSynthesizer _voiceSynthesizer;

        // Custom GUI textures
        private Texture2D _whiteTex;
        private Texture2D _darkTex;

        private void Awake()
        {
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            _darkTex = new Texture2D(1, 1);
            _darkTex.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.08f, 0.92f));
            _darkTex.Apply();
        }

        private void OnDestroy()
        {
            if (_whiteTex != null) Destroy(_whiteTex);
            if (_darkTex != null) Destroy(_darkTex);
        }

        /// <summary>
        /// Displays a dialogue line with animated typewriter text and procedural speech blips.
        /// </summary>
        public void DisplayLine(RelicPersonalityType personality, string line, RelicVoiceSynthesizer voice)
        {
            _currentPersonality = personality;
            _targetLine = line;
            _voiceSynthesizer = voice;

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
            }

            _typewriterCoroutine = StartCoroutine(TypewriterRoutine());
        }

        /// <summary>
        /// Instantly hides any active dialogue.
        /// </summary>
        public void ClearDialogue()
        {
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }

            _isDisplaying = false;
            _targetLine = "";
            _currentVisibleText = "";
            _fadeAlpha = 0f;
        }

        private IEnumerator TypewriterRoutine()
        {
            _isDisplaying = true;
            _currentVisibleText = "";
            _fadeAlpha = 1f;

            int totalChars = _targetLine.Length;
            float charDelay = 1f / Mathf.Max(5f, _charactersPerSecond);

            int blipCounter = 0;

            for (int i = 0; i <= totalChars; i++)
            {
                _currentVisibleText = _targetLine.Substring(0, i);

                // Play procedural speech blip every 2-3 characters (skip spaces/punctuation)
                if (i > 0 && i < totalChars)
                {
                    char c = _targetLine[i - 1];
                    if (char.IsLetterOrDigit(c))
                    {
                        blipCounter++;
                        if (blipCounter % 2 == 0 && _voiceSynthesizer != null)
                        {
                            _voiceSynthesizer.PlaySpeechBlip(_currentPersonality);
                        }
                    }
                }

                yield return new WaitForSeconds(charDelay);
            }

            // Linger once fully revealed
            _lingerTimer = _displayLingerSeconds;
            while (_lingerTimer > 0f)
            {
                _lingerTimer -= Time.deltaTime;
                yield return null;
            }

            // Smooth fade out
            float fadeSpeed = 3.5f;
            while (_fadeAlpha > 0.01f)
            {
                _fadeAlpha -= Time.deltaTime * fadeSpeed;
                yield return null;
            }

            _fadeAlpha = 0f;
            _isDisplaying = false;
            _typewriterCoroutine = null;
        }

        private void OnGUI()
        {
            if (!_isDisplaying || _fadeAlpha <= 0.01f || string.IsNullOrEmpty(_currentVisibleText)) return;

            var profile = RelicPersonality.GetProfile(_currentPersonality);
            Color accentColor = profile.PrimaryColor;
            accentColor.a *= _fadeAlpha;

            Color bgColor = profile.BackgroundColor;
            bgColor.a *= _fadeAlpha;

            DrawScreenHudCard(profile, accentColor, bgColor);
            Draw3DWorldSpeechBubble(profile, accentColor, bgColor);
        }

        private void DrawScreenHudCard(RelicPersonality.Profile profile, Color accentColor, Color bgColor)
        {
            int screenW = Screen.width;
            int screenH = Screen.height;

            float cardW = Mathf.Min(740f, screenW - 40f);

            GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            // Pre-calculate needed text height with generous padding to prevent vertical truncation
            float textH = bodyStyle.CalcHeight(new GUIContent(_targetLine), cardW - 44f);
            float cardH = Mathf.Max(104f, 44f + textH + 22f);
            float cardX = (screenW - cardW) * 0.5f;
            float cardY = screenH - cardH - 24f;

            Rect cardRect = new Rect(cardX, cardY, cardW, cardH);

            // 1. Dark background panel
            Color prevColor = GUI.color;
            GUI.color = bgColor;
            GUI.DrawTexture(cardRect, _darkTex);

            // 2. Neon border outline
            DrawBorder(cardRect, accentColor, 2f);

            // 3. Header title bar
            Rect headerRect = new Rect(cardX + 18f, cardY + 12f, cardW - 36f, 22f);
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = accentColor;

            string headerText = $"[ {profile.Name}  •  {profile.Title} ]";
            GUI.Label(headerRect, headerText, headerStyle);

            // 4. Subtitle typewriter body
            Rect bodyRect = new Rect(cardX + 20f, cardY + 38f, cardW - 40f, textH + 10f);
            bodyStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, _fadeAlpha);

            // Cursor blink effect
            string cursor = (Time.time % 0.4f < 0.2f && _currentVisibleText.Length < _targetLine.Length) ? " ▌" : "";
            GUI.Label(bodyRect, _currentVisibleText + cursor, bodyStyle);

            GUI.color = prevColor;
        }

        private void Draw3DWorldSpeechBubble(RelicPersonality.Profile profile, Color accentColor, Color bgColor)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = transform.position + _worldBubbleOffset;
            float dist = Vector3.Distance(cam.transform.position, worldPos);
            if (dist > _worldBubbleMaxDistance) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0.1f) return; // Behind camera

            // Screen coordinates in Unity GUI have Y inverted
            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;

            GUIStyle textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            textStyle.normal.textColor = new Color(1f, 1f, 1f, _fadeAlpha);

            // Generous bubble width (up to 480px) so long comedy sentences wrap naturally without clipping
            float maxBubbleWidth = Mathf.Min(480f, Screen.width - 60f);
            float innerPadX = 18f;
            float innerPadY = 14f;

            // Compute ideal width based on single-line length, clamped between 220px and 480px
            Vector2 unconstrained = textStyle.CalcSize(new GUIContent(_targetLine));
            float textW = Mathf.Clamp(unconstrained.x + 10f, 220f, maxBubbleWidth - innerPadX * 2f);
            float textH = textStyle.CalcHeight(new GUIContent(_targetLine), textW) + 4f;

            float boxW = textW + innerPadX * 2f;
            float boxH = textH + innerPadY * 2f;

            // Stable bubble rect based on target line so it never twitches or jitters during typing
            Rect bubbleRect = new Rect(guiX - boxW * 0.5f, guiY - boxH - 12f, boxW, boxH);

            Color prevColor = GUI.color;

            // Background
            GUI.color = bgColor;
            GUI.DrawTexture(bubbleRect, _darkTex);

            // Border
            DrawBorder(bubbleRect, accentColor, 1.5f);

            // Text: display the visible typed portion with full word-wrapping and exact inner rect
            GUI.color = Color.white;
            Rect textInnerRect = new Rect(bubbleRect.x + innerPadX, bubbleRect.y + innerPadY, textW, textH);
            GUI.Label(textInnerRect, $"\"{_currentVisibleText}\"", textStyle);

            // Small indicator tick pointing downward towards the relic
            Rect tickRect = new Rect(guiX - 4f, guiY - 12f, 8f, 7f);
            GUI.color = accentColor;
            GUI.DrawTexture(tickRect, _whiteTex);

            GUI.color = prevColor;
        }

        private void DrawBorder(Rect rect, Color color, float thickness)
        {
            Color prev = GUI.color;
            GUI.color = color;

            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _whiteTex);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), _whiteTex);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _whiteTex);
            // Right
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), _whiteTex);

            GUI.color = prev;
        }
    }
}
