using System.Collections.Generic;
using EchoProtocol.Networking.Core;
using EchoProtocol.Networking.Steam;
using EchoProtocol.UI;
using UnityEngine;

namespace EchoProtocol.Networking.UI
{
    /// <summary>
    /// The game's menus: host or join (Steam or LAN), the friends' games list, the waiting room with invites, the waiting
    /// screens, errors, the settings, and the match menu that Esc opens during a match (resume, settings, controls,
    /// leave). It only reads <see cref="NetworkSession"/> and calls it; all the networking happens there.
    /// </summary>
    [RequireComponent(typeof(NetworkSession))]
    public class LobbyMenuUI : MonoBehaviour
    {
        private const float CardWidth = 640f;
        private const float Pad = 40f;
        private const float LobbyRefreshSeconds = 5f;
        private const float FriendsRefreshSeconds = 3f;

        private NetworkSession _session;
        private bool _joinScreen;
        private bool _friendsPanel;
        private string _address = "127.0.0.1";
        private string _lanAddress;
        private float _nextLobbyRefresh;
        private float _nextFriendsRefresh;
        private List<SteamFriend> _friends = new List<SteamFriend>();
        private readonly Dictionary<ulong, float> _invitedAt = new Dictionary<ulong, float>();
        private Vector2 _lobbyScroll;
        private Vector2 _friendScroll;

        // The photo mode photographs the match as a player sees it, without this panel.
        private bool _photoMode;

        private readonly SettingsPanel _settings = new SettingsPanel();
        private bool _mainMenuSettings;

        // Drawn on top of every other IMGUI screen (HUD, subtitles, end screen): lower depth is drawn later.
        private const int MenuGuiDepth = -100;

        private static readonly string[,] Controls =
        {
            { "WASD", "Move" },
            { "Mouse", "Look around" },
            { "Space", "Jump" },
            { "Shift", "Sprint (relics hate it)" },
            { "V (hold)", "Echo: your voice lights the cave" },
            { "E", "Pick up, dock in the altar, pass" },
            { "G", "Drop the relic" },
            { "Esc", "This menu" },
            { "R", "Restart after the match ends" },
        };

        private void Awake()
        {
            _session = GetComponent<NetworkSession>();
            _photoMode = EchoProtocol.Visuals.PhotoMode.Requested(System.Environment.GetCommandLineArgs(), out _);
        }

        private void Update()
        {
            SessionState state = _session.State;

            // The menus need the mouse; the player locks it again when the match starts.
            if (state != SessionState.InMatch)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (state != SessionState.Offline)
            {
                _joinScreen = false;
                _mainMenuSettings = false;
            }
            if (state != SessionState.WaitingRoom) _friendsPanel = false;

            // The match menu exists only during a match (and never in the photo mode's pictures).
            MatchMenu.SetAvailable(state == SessionState.InMatch && !_photoMode);

            // One settings screen for both places; it opens its microphone test while it is on screen and saves on close.
            bool wantSettings = (state == SessionState.Offline && _mainMenuSettings)
                             || (state == SessionState.InMatch && MatchMenu.Current == MatchMenu.Page.Settings);
            if (wantSettings && !_settings.IsOpen) _settings.Open();
            else if (!wantSettings && _settings.IsOpen) _settings.Close();

            if (_joinScreen && Time.unscaledTime >= _nextLobbyRefresh)
            {
                _session.Lobby.RefreshFriendLobbies();
                _nextLobbyRefresh = Time.unscaledTime + LobbyRefreshSeconds;
            }

            if (_friendsPanel && Time.unscaledTime >= _nextFriendsRefresh)
            {
                _friends = _session.Lobby.GetOnlineFriends();
                _nextFriendsRefresh = Time.unscaledTime + FriendsRefreshSeconds;
            }
        }

        private void OnDisable()
        {
            MatchMenu.SetAvailable(false);
            _settings.Close();
        }

        private void OnGUI()
        {
            GUI.depth = MenuGuiDepth;
            EchoGui.PointerOverPanel = false;
            SessionState state = _session.State;
            if (state == SessionState.InMatch && _photoMode) return;
            if (state == SessionState.InMatch && !MatchMenu.IsOpen && Cursor.lockState == CursorLockMode.Locked) return;

            Vector2 screen = EchoGui.Begin();
            switch (state)
            {
                case SessionState.Offline:
                    EchoGui.Fill(new Rect(0f, 0f, screen.x, screen.y), EchoGui.Backdrop);
                    if (_mainMenuSettings)
                    {
                        if (_settings.Draw(screen)) _mainMenuSettings = false;
                    }
                    else if (_joinScreen) DrawJoinScreen(screen);
                    else DrawMainMenu(screen);
                    break;
                case SessionState.CreatingLobby:
                case SessionState.JoiningLobby:
                case SessionState.Connecting:
                    EchoGui.Fill(new Rect(0f, 0f, screen.x, screen.y), EchoGui.Backdrop);
                    DrawBusy(screen, state);
                    break;
                case SessionState.WaitingRoom:
                    EchoGui.Fill(new Rect(0f, 0f, screen.x, screen.y), EchoGui.Backdrop);
                    DrawWaitingRoom(screen);
                    break;
                case SessionState.InMatch:
                    DrawMatchMenu(screen);
                    break;
                case SessionState.Error:
                    EchoGui.Fill(new Rect(0f, 0f, screen.x, screen.y), EchoGui.Backdrop);
                    DrawError(screen);
                    break;
            }

            DrawNotice(screen);
            EchoGui.End();
        }

        private static Rect CenteredCard(Vector2 screen, float width, float height)
        {
            return new Rect((screen.x - width) * 0.5f, (screen.y - height) * 0.5f, width, height);
        }

        private void DrawMainMenu(Vector2 screen)
        {
            bool steam = _session.SteamAvailable;
            Rect card = CenteredCard(screen, CardWidth, 560f);
            EchoGui.Card(card, EchoGui.Cyan);

            float x = card.x + Pad;
            float w = card.width - Pad * 2f;
            float y = card.y + 34f;

            EchoGui.Label(new Rect(x, y, w, 56f), "ECHO PROTOCOL", EchoGui.Title);
            y += 58f;
            EchoGui.Label(new Rect(x, y, w, 26f), "Your voice is your only light.", EchoGui.Muted);
            y += 46f;

            EchoGui.Dot(new Vector2(x + 4f, y + 11f), steam ? EchoGui.Cyan : EchoGui.MutedColor);
            string steamLine = steam
                ? $"Steam: signed in as {_session.Lobby.LocalName}"
                : "Steam is not running. Start Steam to play with friends.";
            EchoGui.Label(new Rect(x + 18f, y, w - 18f, 24f), steamLine, EchoGui.Muted);
            y += 40f;

            if (EchoGui.Button(new Rect(x, y, w, 58f), "HOST MATCH", EchoGui.ButtonKind.Primary, steam)) _session.HostSteam();
            y += 70f;
            if (EchoGui.Button(new Rect(x, y, w, 50f), "JOIN A FRIEND", EchoGui.ButtonKind.Normal, steam))
            {
                _joinScreen = true;
                _nextLobbyRefresh = 0f;
            }
            y += 76f;

            EchoGui.Divider(new Rect(x, y, w, 24f), "LOCAL / LAN");
            y += 40f;

            const float buttonWidth = 160f;
            if (EchoGui.Button(new Rect(x, y, buttonWidth, 46f), "HOST LOCAL")) _session.HostLocal();
            _address = EchoGui.TextField(new Rect(x + buttonWidth + 14f, y, w - buttonWidth * 2f - 28f, 46f), _address);
            if (EchoGui.Button(new Rect(x + w - buttonWidth, y, buttonWidth, 46f), "JOIN IP")) _session.JoinLocal(_address);
            y += 60f;

            EchoGui.Label(new Rect(x, y, w, 44f), "LAN play uses UDP port 7770. Every player needs the same build of the game.", EchoGui.Muted);
            y += 56f;

            if (EchoGui.Button(new Rect(x, y, buttonWidth, 46f), "SETTINGS")) _mainMenuSettings = true;
            if (EchoGui.Button(new Rect(x + w - buttonWidth, y, buttonWidth, 46f), "QUIT", EchoGui.ButtonKind.Danger)) Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DrawJoinScreen(Vector2 screen)
        {
            Rect card = CenteredCard(screen, CardWidth, 534f);
            EchoGui.Card(card, EchoGui.Cyan);

            float x = card.x + Pad;
            float w = card.width - Pad * 2f;
            float y = card.y + 34f;

            EchoGui.Label(new Rect(x, y, w, 30f), "JOIN A FRIEND", EchoGui.Heading);
            y += 36f;
            EchoGui.Label(new Rect(x, y, w, 24f), "Games your Steam friends are hosting.", EchoGui.Muted);
            y += 40f;

            var lobbies = new List<FriendLobby>();
            foreach (FriendLobby lobby in _session.Lobby.FriendLobbies)
            {
                if (lobby.IsEcho) lobbies.Add(lobby);
            }

            Rect listArea = new Rect(x, y, w, 300f);
            EchoGui.Fill(listArea, new Color(1f, 1f, 1f, 0.03f));
            if (lobbies.Count == 0)
            {
                EchoGui.Label(new Rect(x + 20f, y + 100f, w - 40f, 100f),
                    "No friends are hosting right now.\nAsk a friend to host and invite you: accepting the invite in Steam brings you straight in.",
                    EchoGui.Muted, EchoGui.MutedColor, TextAnchor.UpperCenter);
            }
            else
            {
                const float rowHeight = 72f;
                _lobbyScroll = GUI.BeginScrollView(listArea, _lobbyScroll, new Rect(0f, 0f, w - 20f, lobbies.Count * rowHeight));
                for (int i = 0; i < lobbies.Count; i++)
                {
                    DrawLobbyRow(new Rect(0f, i * rowHeight, w - 20f, rowHeight - 8f), lobbies[i]);
                }
                GUI.EndScrollView();
            }
            y += 316f;

            const float buttonWidth = 170f;
            if (EchoGui.Button(new Rect(x, y, buttonWidth, 46f), "REFRESH")) _nextLobbyRefresh = 0f;
            if (EchoGui.Button(new Rect(x + w - buttonWidth, y, buttonWidth, 46f), "BACK")) _joinScreen = false;
            y += 62f;

            EchoGui.Label(new Rect(x, y, w, 44f), "Only games of friends who use the same build of the game can be joined.", EchoGui.Muted);
        }

        private void DrawLobbyRow(Rect row, FriendLobby lobby)
        {
            EchoGui.Fill(row, new Color(1f, 1f, 1f, 0.04f));
            EchoGui.Label(new Rect(row.x + 16f, row.y + 8f, row.width - 160f, 26f), $"{lobby.FriendName}'s game", EchoGui.Body);

            string status;
            Color color;
            if (!lobby.DataReady && lobby.Problem == null)
            {
                status = "Loading" + EchoGui.Dots;
                color = EchoGui.MutedColor;
            }
            else if (lobby.Problem != null)
            {
                status = lobby.Problem;
                color = EchoGui.Red;
            }
            else
            {
                status = $"{lobby.Players}/{lobby.MaxPlayers} players, waiting in the lobby";
                color = EchoGui.Cyan;
            }
            EchoGui.Label(new Rect(row.x + 16f, row.y + 34f, row.width - 160f, 24f), status, EchoGui.Muted, color);

            bool joinable = lobby.DataReady && lobby.Problem == null;
            if (EchoGui.Button(new Rect(row.xMax - 130f, row.y + 12f, 116f, 40f), "JOIN", EchoGui.ButtonKind.Primary, joinable))
            {
                _session.JoinSteamLobby(lobby.Lobby);
            }
        }

        private void DrawBusy(Vector2 screen, SessionState state)
        {
            Rect card = CenteredCard(screen, CardWidth, 222f);
            EchoGui.Card(card, EchoGui.Cyan);

            float x = card.x + Pad;
            float w = card.width - Pad * 2f;
            float y = card.y + 34f;

            string title;
            string detail;
            switch (state)
            {
                case SessionState.CreatingLobby:
                    title = "CREATING LOBBY";
                    detail = "Asking Steam for a friends-only lobby";
                    break;
                case SessionState.JoiningLobby:
                    title = "JOINING";
                    detail = "Entering your friend's lobby";
                    break;
                default:
                    title = "CONNECTING";
                    detail = $"Reaching the host ({Mathf.CeilToInt(_session.ConnectSecondsLeft)} s)";
                    break;
            }

            EchoGui.Label(new Rect(x, y, w, 30f), title, EchoGui.Heading);
            y += 44f;
            EchoGui.Label(new Rect(x, y, w, 28f), detail + EchoGui.Dots, EchoGui.Body);
            y += 62f;

            if (EchoGui.Button(new Rect(x, y, 170f, 46f), "CANCEL")) _session.Leave();
        }

        private void DrawWaitingRoom(Vector2 screen)
        {
            bool host = _session.IsHost;
            bool steam = _session.IsSteamSession;
            float totalWidth = _friendsPanel ? CardWidth + 20f + 380f : CardWidth;
            float left = (screen.x - totalWidth) * 0.5f;
            Rect card = new Rect(left, (screen.y - 504f) * 0.5f, CardWidth, 504f);
            EchoGui.Card(card, EchoGui.Gold);

            float x = card.x + Pad;
            float w = card.width - Pad * 2f;
            float y = card.y + 34f;

            EchoGui.Label(new Rect(x, y, w, 30f), "WAITING ROOM", EchoGui.Heading, EchoGui.Gold);
            y += 38f;

            string subtitle;
            if (!host) subtitle = "Waiting for the host to start the match" + EchoGui.Dots;
            else if (steam) subtitle = "Invite your friends, then start the match when everyone is here.";
            else subtitle = $"Friends on your network join with your address: {LanAddress()}";
            EchoGui.Label(new Rect(x, y, w, 48f), subtitle, EchoGui.Muted);
            y += 58f;

            List<string> names = _session.GetPlayerNames();
            string count = steam || host ? $"  {Mathf.Max(1, names.Count)}/{SteamLobbyManager.MaxMembers}" : string.Empty;
            EchoGui.Label(new Rect(x, y, w, 24f), "PLAYERS" + count, EchoGui.Muted, EchoGui.MutedColor);
            y += 30f;

            if (names.Count == 0)
            {
                // A LAN client does not get the roster; the host does.
                EchoGui.Label(new Rect(x, y, w, 28f), "Connected to the host.", EchoGui.Body);
            }
            else
            {
                for (int i = 0; i < SteamLobbyManager.MaxMembers; i++)
                {
                    Rect row = new Rect(x, y + i * 44f, w, 38f);
                    bool taken = i < names.Count;
                    EchoGui.Fill(row, new Color(1f, 1f, 1f, taken ? 0.05f : 0.02f));
                    EchoGui.Dot(new Vector2(row.x + 18f, row.center.y),
                        !taken ? EchoGui.WithAlpha(EchoGui.MutedColor, 0.4f) : i == 0 ? EchoGui.Gold : EchoGui.Cyan);
                    EchoGui.Label(new Rect(row.x + 34f, row.y + 6f, row.width - 40f, 28f), taken ? names[i] : "Open slot",
                        EchoGui.Body, taken ? EchoGui.TextColor : EchoGui.WithAlpha(EchoGui.MutedColor, 0.6f));
                }
            }
            y += SteamLobbyManager.MaxMembers * 44f + 18f;

            if (host)
            {
                if (EchoGui.Button(new Rect(x, y, w, 56f), "START MATCH", EchoGui.ButtonKind.Primary)) _session.StartMatch();
                y += 68f;
            }
            else
            {
                y += 68f;
            }

            const float buttonWidth = 170f;
            if (steam)
            {
                if (EchoGui.Button(new Rect(x, y, buttonWidth, 46f), "INVITE")) _session.Lobby.OpenInviteOverlay();
                if (EchoGui.Button(new Rect(x + buttonWidth + 12f, y, buttonWidth, 46f), _friendsPanel ? "HIDE FRIENDS" : "FRIENDS"))
                {
                    _friendsPanel = !_friendsPanel;
                    _nextFriendsRefresh = 0f;
                }
            }
            if (EchoGui.Button(new Rect(x + w - buttonWidth, y, buttonWidth, 46f), "LEAVE", EchoGui.ButtonKind.Danger)) _session.Leave();

            if (_friendsPanel)
            {
                DrawFriendsPanel(new Rect(card.xMax + 20f, card.y, 380f, card.height));
            }
        }

        private void DrawFriendsPanel(Rect card)
        {
            EchoGui.Card(card, EchoGui.Cyan);

            float x = card.x + 28f;
            float w = card.width - 56f;
            float y = card.y + 30f;

            EchoGui.Label(new Rect(x, y, w, 30f), "INVITE FRIENDS", EchoGui.Heading);
            y += 36f;
            EchoGui.Label(new Rect(x, y, w, 44f), "Online friends. The Steam overlay (Shift+Tab) also works, except in the Unity Editor.", EchoGui.Muted);
            y += 56f;

            Rect listArea = new Rect(x, y, w, card.yMax - y - 24f);
            if (_friends.Count == 0)
            {
                EchoGui.Label(new Rect(x, y + 20f, w, 60f), "No friends are online.", EchoGui.Muted);
                return;
            }

            const float rowHeight = 62f;
            _friendScroll = GUI.BeginScrollView(listArea, _friendScroll, new Rect(0f, 0f, w - 20f, _friends.Count * rowHeight));
            for (int i = 0; i < _friends.Count; i++)
            {
                SteamFriend friend = _friends[i];
                Rect row = new Rect(0f, i * rowHeight, w - 20f, rowHeight - 6f);
                EchoGui.Fill(row, new Color(1f, 1f, 1f, 0.04f));
                EchoGui.Label(new Rect(row.x + 12f, row.y + 6f, row.width - 120f, 24f), friend.Name, EchoGui.Body);
                EchoGui.Label(new Rect(row.x + 12f, row.y + 30f, row.width - 120f, 22f),
                    friend.PlayingThisGame ? "Playing Echo Protocol" : "Online", EchoGui.Muted,
                    friend.PlayingThisGame ? EchoGui.Cyan : EchoGui.MutedColor);

                Rect button = new Rect(row.xMax - 100f, row.y + 10f, 92f, 36f);
                bool invitedRecently = _invitedAt.TryGetValue(friend.Id.m_SteamID, out float at) && Time.unscaledTime - at < 10f;
                if (friend.InThisLobby)
                {
                    EchoGui.Label(button, "HERE", EchoGui.Muted, EchoGui.Gold, TextAnchor.MiddleCenter);
                }
                else if (invitedRecently)
                {
                    EchoGui.Label(button, "SENT", EchoGui.Muted, EchoGui.Cyan, TextAnchor.MiddleCenter);
                }
                else if (EchoGui.Button(button, "INVITE") && _session.Lobby.InviteFriend(friend.Id))
                {
                    _invitedAt[friend.Id.m_SteamID] = Time.unscaledTime;
                }
            }
            GUI.EndScrollView();
        }

        private void DrawError(Vector2 screen)
        {
            Rect card = CenteredCard(screen, CardWidth, 290f);
            EchoGui.Card(card, EchoGui.Red);

            float x = card.x + Pad;
            float w = card.width - Pad * 2f;
            float y = card.y + 34f;

            EchoGui.Label(new Rect(x, y, w, 30f), "CONNECTION PROBLEM", EchoGui.Heading, EchoGui.Red);
            y += 46f;
            EchoGui.Label(new Rect(x, y, w, 96f), _session.ErrorMessage, EchoGui.Body);
            y += 110f;

            if (EchoGui.Button(new Rect(x, y, 220f, 46f), "BACK TO MENU")) _session.DismissError();
        }

        // During the match. Esc opens the menu; with the menu closed but the cursor free (after alt-tab) only a hint shows.
        private void DrawMatchMenu(Vector2 screen)
        {
            if (!MatchMenu.IsOpen)
            {
                Rect hint = new Rect(screen.x - 420f - 24f, 24f, 420f, 56f);
                EchoGui.Card(hint, EchoGui.Cyan);
                EchoGui.Label(new Rect(hint.x + 20f, hint.y + 4f, hint.width - 40f, hint.height - 4f),
                    "Click the game to resume. Esc opens the menu.", EchoGui.Muted, EchoGui.TextColor, TextAnchor.MiddleLeft);
                return;
            }

            EchoGui.Fill(new Rect(0f, 0f, screen.x, screen.y), EchoGui.Backdrop);
            EchoGui.PointerOverPanel = true; // every click belongs to the menu while it is open

            switch (MatchMenu.Current)
            {
                case MatchMenu.Page.Settings:
                    if (_settings.Draw(screen)) MatchMenu.Show(MatchMenu.Page.Main);
                    break;
                case MatchMenu.Page.Controls:
                    DrawControls(screen);
                    break;
                default:
                    DrawMatchMain(screen);
                    break;
            }
        }

        private void DrawMatchMain(Vector2 screen)
        {
            bool host = _session.IsHost;
            const float width = 460f;
            Rect card = CenteredCard(screen, width, host ? 486f : 456f);
            EchoGui.Card(card, EchoGui.Cyan);

            float x = card.x + Pad;
            float w = card.width - Pad * 2f;
            float y = card.y + 30f;

            EchoGui.Label(new Rect(x, y, w, 30f), "MENU", EchoGui.Heading);
            y += 36f;
            string via = _session.IsSteamSession ? "Steam" : "LAN";
            string role = host ? "Host" : "Client";
            int players = PlayerController.ActivePlayers.Count;
            EchoGui.Label(new Rect(x, y, w, 24f), $"{role} over {via}, {players} {(players == 1 ? "player" : "players")}", EchoGui.Muted);
            y += 26f;
            EchoGui.Label(new Rect(x, y, w, 24f), "The match keeps running while this menu is open.", EchoGui.Muted);
            y += 42f;

            if (EchoGui.Button(new Rect(x, y, w, 56f), "RESUME", EchoGui.ButtonKind.Primary)) MatchMenu.Resume();
            y += 68f;
            if (EchoGui.Button(new Rect(x, y, w, 48f), "SETTINGS")) MatchMenu.Show(MatchMenu.Page.Settings);
            y += 58f;
            if (EchoGui.Button(new Rect(x, y, w, 48f), "CONTROLS")) MatchMenu.Show(MatchMenu.Page.Controls);
            y += 70f;

            if (EchoGui.Button(new Rect(x, y, w, 48f), "LEAVE MATCH", EchoGui.ButtonKind.Danger)) _session.Leave();
            y += 54f;
            if (host)
            {
                EchoGui.Label(new Rect(x, y, w, 24f), "Leaving ends the match for everyone.", EchoGui.Muted, EchoGui.Gold);
                y += 30f;
            }
            EchoGui.Label(new Rect(x, y, w, 24f), "Esc: back to the game", EchoGui.Muted, EchoGui.MutedColor);
        }

        private static void DrawControls(Vector2 screen)
        {
            int rows = Controls.GetLength(0);
            Rect card = CenteredCard(screen, CardWidth, 150f + rows * 46f + 70f);
            EchoGui.Card(card, EchoGui.Cyan);

            float x = card.x + Pad;
            float w = card.width - Pad * 2f;
            float y = card.y + 30f;

            EchoGui.Label(new Rect(x, y, w, 30f), "CONTROLS", EchoGui.Heading);
            y += 36f;
            EchoGui.Label(new Rect(x, y, w, 44f), "Find the three relics, dock them in the altar and escape through the portal. Stay quiet: the monster hunts by sound.", EchoGui.Muted);
            y += 64f;

            const float keyWidth = 120f;
            for (int i = 0; i < rows; i++)
            {
                Rect key = new Rect(x, y + 4f, keyWidth - 16f, 32f);
                EchoGui.Fill(key, EchoGui.WithAlpha(EchoGui.Cyan, 0.08f));
                EchoGui.Border(key, EchoGui.WithAlpha(EchoGui.Cyan, 0.5f), 1f);
                EchoGui.Label(key, Controls[i, 0], EchoGui.Muted, EchoGui.Cyan, TextAnchor.MiddleCenter);
                EchoGui.Label(new Rect(x + keyWidth, y, w - keyWidth, 40f), Controls[i, 1], EchoGui.Body, EchoGui.TextColor, TextAnchor.MiddleLeft);
                y += 46f;
            }

            y += 10f;
            if (EchoGui.Button(new Rect(x + w - 200f, y, 200f, 46f), "BACK", EchoGui.ButtonKind.Primary)) MatchMenu.Show(MatchMenu.Page.Main);
        }

        private void DrawNotice(Vector2 screen)
        {
            string notice = _session.Notice;
            if (notice == null) return;

            Rect card = new Rect((screen.x - 640f) * 0.5f, screen.y - 110f, 640f, 64f);
            EchoGui.Card(card, EchoGui.Gold);
            EchoGui.Label(new Rect(card.x + 24f, card.y + 6f, card.width - 48f, card.height - 6f), notice, EchoGui.Body,
                EchoGui.TextColor, TextAnchor.MiddleLeft);
        }

        private string LanAddress()
        {
            if (_lanAddress != null) return _lanAddress;

            _lanAddress = "this PC's IP address";
            try
            {
                foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                    {
                        _lanAddress = ip.ToString();
                        break;
                    }
                }
            }
            catch (System.Exception)
            {
                // No network name resolution: keep the generic hint.
            }
            return _lanAddress;
        }
    }
}
