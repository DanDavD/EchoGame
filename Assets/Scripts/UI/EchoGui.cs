using UnityEngine;

namespace EchoProtocol.UI
{
    /// <summary>
    /// The shared look of the game's IMGUI screens, following docs/VISUAL_DESIGN.md: a dark card like the relic dialogue,
    /// cold cyan for the world, warm gold for the main action and red for problems. Screens lay out in a virtual space
    /// 1080 units high that is scaled to the real screen, so they keep their proportions at any resolution.
    /// </summary>
    public static class EchoGui
    {
        public const float ReferenceHeight = 1080f;

        public static readonly Color CardColor = new Color(0.06f, 0.06f, 0.08f, 0.94f);
        public static readonly Color Cyan = new Color(0.35f, 0.85f, 1f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.32f, 1f);
        public static readonly Color Red = new Color(1f, 0.42f, 0.36f, 1f);
        public static readonly Color TextColor = new Color(0.93f, 0.95f, 0.97f, 1f);
        public static readonly Color MutedColor = new Color(0.58f, 0.64f, 0.7f, 1f);
        public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.55f);

        public enum ButtonKind { Normal, Primary, Danger }

        private static Texture2D _white;
        private static GUIStyle _title;
        private static GUIStyle _heading;
        private static GUIStyle _body;
        private static GUIStyle _muted;
        private static GUIStyle _buttonText;
        private static GUIStyle _field;
        private static Matrix4x4 _savedMatrix;

        public static GUIStyle Title => _title;
        public static GUIStyle Heading => _heading;
        public static GUIStyle Body => _body;
        public static GUIStyle Muted => _muted;

        public static float Scale => Mathf.Max(0.5f, Screen.height / ReferenceHeight);

        /// <summary>
        /// True while the mouse is over an in-match panel. The player controller then does not grab the cursor on a
        /// click, so the panel's buttons can be pressed.
        /// </summary>
        public static bool PointerOverPanel { get; set; }

        /// <summary>Animated "." to "..." for screens that wait on something.</summary>
        public static string Dots => new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);

        /// <summary>Starts drawing in the virtual space and returns its size. Call from OnGUI and pair with <see cref="End"/>.</summary>
        public static Vector2 Begin()
        {
            EnsureStyles();
            _savedMatrix = GUI.matrix;
            float scale = Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            return new Vector2(Screen.width / scale, Screen.height / scale);
        }

        public static void End()
        {
            GUI.matrix = _savedMatrix;
            GUI.color = Color.white;
        }

        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = previous;
        }

        public static void Border(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>A dark card with a faint border in the accent colour and an accent bar along the top.</summary>
        public static void Card(Rect rect, Color accent)
        {
            Fill(rect, CardColor);
            Border(rect, WithAlpha(accent, 0.3f), 1f);
            Fill(new Rect(rect.x, rect.y, rect.width, 3f), accent);
        }

        /// <summary>A thin line with a small caption in the middle, to separate sections of a card.</summary>
        public static void Divider(Rect rect, string caption)
        {
            float mid = rect.y + rect.height * 0.5f;
            Vector2 size = _muted.CalcSize(new GUIContent(caption));
            float gap = size.x + 24f;
            float side = (rect.width - gap) * 0.5f;
            Fill(new Rect(rect.x, mid, side, 1f), WithAlpha(MutedColor, 0.4f));
            Fill(new Rect(rect.xMax - side, mid, side, 1f), WithAlpha(MutedColor, 0.4f));
            Label(new Rect(rect.x + side, rect.y, gap, rect.height), caption, _muted, MutedColor, TextAnchor.MiddleCenter);
        }

        public static void Label(Rect rect, string text, GUIStyle style)
        {
            GUI.Label(rect, text, style);
        }

        public static void Label(Rect rect, string text, GUIStyle style, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var styled = new GUIStyle(style) { alignment = anchor };
            styled.normal.textColor = color;
            GUI.Label(rect, text, styled);
        }

        /// <summary>A small coloured dot, used for status lines and player lists.</summary>
        public static void Dot(Vector2 centre, Color color, float size = 8f)
        {
            Fill(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), color);
        }

        /// <summary>Draws a button and returns true when it is clicked. Disabled buttons are drawn faded and never click.</summary>
        public static bool Button(Rect rect, string text, ButtonKind kind = ButtonKind.Normal, bool enabled = true)
        {
            Color accent = kind == ButtonKind.Primary ? Gold : kind == ButtonKind.Danger ? Red : Cyan;
            bool hover = enabled && rect.Contains(Event.current.mousePosition);
            Color textColor;

            if (!enabled)
            {
                Fill(rect, new Color(1f, 1f, 1f, 0.03f));
                Border(rect, WithAlpha(MutedColor, 0.35f), 1f);
                textColor = WithAlpha(MutedColor, 0.7f);
            }
            else if (kind == ButtonKind.Primary)
            {
                Fill(rect, hover ? Color.Lerp(Gold, Color.white, 0.25f) : Gold);
                textColor = new Color(0.1f, 0.08f, 0.04f, 1f);
            }
            else
            {
                Fill(rect, WithAlpha(accent, hover ? 0.2f : 0.06f));
                Border(rect, WithAlpha(accent, hover ? 1f : 0.75f), 1.5f);
                textColor = hover ? Color.Lerp(accent, Color.white, 0.4f) : accent;
            }

            Label(rect, text, _buttonText, textColor, TextAnchor.MiddleCenter);
            return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        public static string TextField(Rect rect, string value)
        {
            Fill(rect, new Color(1f, 1f, 1f, 0.06f));
            Border(rect, WithAlpha(Cyan, 0.45f), 1f);
            return GUI.TextField(new Rect(rect.x + 10f, rect.y, rect.width - 20f, rect.height), value ?? string.Empty, 64, _field);
        }

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static void EnsureStyles()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            if (_title != null) return;

            _title = Make(44, FontStyle.Bold, TextColor);
            _heading = Make(22, FontStyle.Bold, Cyan);
            _body = Make(18, FontStyle.Normal, TextColor);
            _body.wordWrap = true;
            _muted = Make(16, FontStyle.Normal, MutedColor);
            _muted.wordWrap = true;
            _buttonText = Make(17, FontStyle.Bold, TextColor);
            _field = Make(18, FontStyle.Normal, TextColor);
            _field.alignment = TextAnchor.MiddleLeft;
            _field.clipping = TextClipping.Clip;
            _field.hover.textColor = TextColor;
            _field.focused.textColor = TextColor;
            _field.active.textColor = TextColor;
        }

        private static GUIStyle Make(int size, FontStyle fontStyle, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, richText = false };
            style.normal.textColor = color;
            return style;
        }
    }
}
