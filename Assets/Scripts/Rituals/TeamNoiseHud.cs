using UnityEngine;
using EchoProtocol.Relics;

namespace EchoProtocol.Rituals
{
    /// <summary>
    /// On-screen view of the <see cref="TeamNoiseDirector"/>: the team noise meter, the alarm, the human echo ritual and the
    /// push-to-talk echo indicator. Reads only replicated state, so it shows the same thing on every peer.
    /// </summary>
    [RequireComponent(typeof(TeamNoiseDirector))]
    public class TeamNoiseHud : MonoBehaviour
    {
        private TeamNoiseDirector _director;
        private GUIStyle _label;
        private GUIStyle _title;
        private GUIStyle _big;
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
            _big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 26, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        }

        private void OnGUI()
        {
            if (_director == null) return;
            EnsureStyles();

            DrawMeter();
            DrawEchoIndicator();
            DrawRitual();
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

        // ---------------------------------------------------------------- ritual

        private void DrawRitual()
        {
            RitualPhase phase = _director.Phase;
            if (phase == RitualPhase.None) return;

            float w = 560f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.2f;
            Rect panel = new Rect(x, y, w, 150f);

            GUI.color = new Color(0.05f, 0.04f, 0.1f, 0.85f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            RelicBrain relic = _director.RitualRelic;
            string name = relic != null ? RelicPersonality.GetProfile(relic.Personality).Name : "THE RELIC";
            var pattern = _director.RitualPattern;

            if (phase == RitualPhase.Calling)
            {
                GUI.Label(new Rect(x, y + 8f, w, 28f), $"{name} IS CRYING! Listen to its rhythm...", _title);
                DrawPatternDots(new Rect(x, y + 48f, w, 30f), pattern, _director.PhaseElapsed);
                GUI.Label(new Rect(x, y + 92f, w, 40f), "Then echo it one by one, with any sound: \"ba\", \"da\", \"pato\"...", _small);
                DrawParticipants(new Rect(x, y + 118f, w, 24f));
            }
            else if (phase == RitualPhase.Turn)
            {
                if (_director.IsLocalTurn)
                {
                    GUI.color = new Color(1f, 0.9f, 0.3f, 1f);
                    GUI.Label(new Rect(x, y + 4f, w, 34f), "YOUR TURN! ECHO THE RHYTHM!", _big);
                    GUI.color = Color.white;
                    DrawBeatProgress(new Rect(x, y + 48f, w, 30f), pattern.Count, _director.LocalBeatCount);
                }
                else
                {
                    GUI.Label(new Rect(x, y + 8f, w, 28f), $"{PlayerLabel(CurrentTurnPlayer())} is echoing {name}...", _title);
                    DrawPatternDots(new Rect(x, y + 48f, w, 30f), pattern, float.MaxValue);
                }

                float remaining = 1f - Mathf.Clamp01(_director.PhaseElapsed / Mathf.Max(0.1f, _director.TurnWindow));
                DrawBar(new Rect(x + 80f, y + 90f, w - 160f, 8f), remaining, new Color(1f, 0.8f, 0.3f, 1f));
                DrawParticipants(new Rect(x, y + 118f, w, 24f));
            }
            else if (phase == RitualPhase.Result)
            {
                if (_director.LastRoundSucceeded)
                {
                    GUI.color = new Color(0.5f, 1f, 0.6f, 1f);
                    GUI.Label(new Rect(x, y + 30f, w, 40f), $"{name} CALMED DOWN!", _big);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x, y + 80f, w, 24f), "Team noise dropped. Keep whispering.", _small);
                }
                else if (_director.LastRoundCriedOut)
                {
                    GUI.color = new Color(1f, 0.35f, 0.3f, 1f);
                    GUI.Label(new Rect(x, y + 30f, w, 40f), $"{name} CRIED ITSELF OUT...", _big);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x, y + 80f, w, 24f), "...and the whole cave heard it. RUN, then go silent.", _small);
                }
                else
                {
                    GUI.color = new Color(1f, 0.6f, 0.3f, 1f);
                    GUI.Label(new Rect(x, y + 30f, w, 40f), "WRONG RHYTHM! IT CRIES HARDER", _big);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x, y + 80f, w, 24f), "It will try an easier one...", _small);
                }
                DrawParticipants(new Rect(x, y + 118f, w, 24f));
            }
        }

        /// <summary>One dot per beat; while the relic is calling, each dot lights up as its beat plays.</summary>
        private void DrawPatternDots(Rect area, System.Collections.Generic.IReadOnlyList<float> pattern, float elapsed)
        {
            if (pattern == null || pattern.Count == 0) return;

            float span = Mathf.Max(0.01f, pattern[pattern.Count - 1]);
            float width = Mathf.Min(area.width - 120f, 90f * pattern.Count);
            float left = area.x + (area.width - width) * 0.5f;

            for (int i = 0; i < pattern.Count; i++)
            {
                float px = pattern.Count == 1 ? left + width * 0.5f : left + width * (pattern[i] / span);
                bool lit = elapsed >= pattern[i];
                bool flashing = lit && elapsed - pattern[i] < 0.15f;
                float size = flashing ? 26f : 20f;
                GUI.color = lit ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.25f);
                GUI.DrawTexture(new Rect(px - size * 0.5f, area.y + (area.height - size) * 0.5f, size, size), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        /// <summary>Hollow slots for the beats to say, filled as the microphone hears each syllable.</summary>
        private void DrawBeatProgress(Rect area, int expected, int heard)
        {
            float size = 22f, gap = 14f;
            float width = expected * size + (expected - 1) * gap;
            float left = area.x + (area.width - width) * 0.5f;

            for (int i = 0; i < expected; i++)
            {
                Rect slot = new Rect(left + i * (size + gap), area.y + (area.height - size) * 0.5f, size, size);
                GUI.color = i < heard ? new Color(0.4f, 1f, 0.6f, 1f) : new Color(1f, 1f, 1f, 0.3f);
                GUI.DrawTexture(slot, Texture2D.whiteTexture);
            }
            if (heard > expected)
            {
                GUI.color = new Color(1f, 0.4f, 0.3f, 1f);
                GUI.Label(new Rect(area.x, area.y + area.height, area.width, 18f), "too many beats!", _small);
            }
            GUI.color = Color.white;
        }

        private void DrawParticipants(Rect area)
        {
            var participants = _director.RitualParticipants;
            var results = _director.RitualResults;
            if (participants.Count == 0) return;

            float cell = Mathf.Min(120f, area.width / participants.Count);
            float left = area.x + (area.width - cell * participants.Count) * 0.5f;

            for (int i = 0; i < participants.Count; i++)
            {
                int result = i < results.Count ? results[i] : 0;
                bool active = _director.Phase == RitualPhase.Turn && _director.RitualTurn == i;
                string mark = result > 0 ? " OK" : result < 0 ? " X" : active ? " ..." : "";

                GUI.color = result > 0 ? new Color(0.5f, 1f, 0.6f) : result < 0 ? new Color(1f, 0.4f, 0.35f) : active ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(new Rect(left + i * cell, area.y, cell, area.height), PlayerLabel(participants[i]) + mark, _label);
            }
            GUI.color = Color.white;
        }

        private PlayerController CurrentTurnPlayer()
        {
            int turn = _director.RitualTurn;
            var participants = _director.RitualParticipants;
            return turn >= 0 && turn < participants.Count ? participants[turn] : null;
        }

        private static string PlayerLabel(PlayerController player)
        {
            if (player == null) return "Someone";
            if (!player.IsSpawned || player.IsOwner) return "YOU";
            return $"P{player.OwnerId + 1}";
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
