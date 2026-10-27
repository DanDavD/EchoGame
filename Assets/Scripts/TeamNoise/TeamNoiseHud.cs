using UnityEngine;

namespace EchoProtocol.TeamNoise
{
    /// <summary>
    /// On-screen view of the <see cref="TeamNoiseDirector"/>: the team noise meter, the alarm and the
    /// push-to-talk echo indicator. Reads only replicated state, so it shows the same thing on every peer.
    /// </summary>
    [RequireComponent(typeof(TeamNoiseDirector))]
    public class TeamNoiseHud : MonoBehaviour
    {
        private TeamNoiseDirector _director;
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _small;

        private void Awake()
        {
            _director = GetComponent<TeamNoiseDirector>();
        }

        private void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        }

        private void OnGUI()
        {
            if (_director == null) return;
            EnsureStyles();

            DrawMeter();
            DrawEchoIndicator();
        }

        // ---------------------------------------------------------------- meter and alarm

        private void DrawMeter()
        {
            float w = 320f, h = 14f;
            float x = (Screen.width - w) * 0.5f;
            float y = 88f;

            if (_director.IsAlarmed)
            {
                float flash = (Mathf.Sin(Time.time * 9f) + 1f) * 0.5f;
                Rect box = new Rect(x - 90f, y - 4f, w + 180f, 62f);
                GUI.color = Color.Lerp(new Color(0.45f, 0f, 0f, 0.92f), new Color(0.85f, 0.05f, 0.05f, 0.92f), flash);
                GUI.DrawTexture(box, Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUI.Label(new Rect(box.x, box.y + 4f, box.width, 26f), "THE BEAST HEARD YOU! EVERYONE, STAY SILENT!", _title);

                Rect calmBar = new Rect(x, box.y + 38f, w, h);
                DrawBar(calmBar, _director.Calm01, new Color(0.4f, 0.9f, 1f, 1f));
                GUI.color = Color.white;
                GUI.Label(new Rect(calmBar.x, calmBar.y - 1f, calmBar.width, calmBar.height + 2f), "silence", _small);
                return;
            }

            float fill = _director.Fill01;
            Rect bar = new Rect(x, y, w, h);
            Color color = fill < 0.5f ? Color.Lerp(new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.85f, 0.2f), fill * 2f)
                                      : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.2f, 0.15f), (fill - 0.5f) * 2f);
            DrawBar(bar, fill, color);

            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x, bar.y + h, bar.width, 18f), fill > 0.75f ? "TEAM NOISE - TOO LOUD, HUSH!" : "TEAM NOISE", _label);
        }

        private static void DrawBar(Rect rect, float value, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- push to talk

        private void DrawEchoIndicator()
        {
            PlayerController local = LocalPlayer();
            if (local == null) return;

            var echo = local.GetComponent<EchoVisionController>();
            bool echoing = echo != null && echo.IsEchoing;

            Rect rect = new Rect(Screen.width - 190f, Screen.height - 58f, 170f, 36f);
            GUI.color = echoing ? new Color(0.3f, 0.85f, 1f, 0.9f) : new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = echoing ? Color.black : new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(rect, echoing ? "ECHOING..." : "Hold [V] to echo", _label);
            GUI.color = Color.white;
        }

        private static PlayerController LocalPlayer()
        {
            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController p = players[i];
                if (p != null && (!p.IsSpawned || p.IsOwner)) return p;
            }
            return null;
        }
    }
}
