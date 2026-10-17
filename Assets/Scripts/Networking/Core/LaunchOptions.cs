namespace EchoProtocol.Networking.Core
{
    /// <summary>What the game should do on startup, as requested on the command line.</summary>
    public enum LaunchMode
    {
        /// <summary>No request: wait for the player to choose.</summary>
        None = 0,

        /// <summary>Start a local host (server + client).</summary>
        Host = 1,

        /// <summary>Join a host as a client.</summary>
        Join = 2
    }

    /// <summary>
    /// Command-line switches that start networking without clicking the panel, used by the packaged game, dedicated
    /// test runs and tooling: <c>-echo-host</c>, <c>-echo-join</c> and <c>-echo-join &lt;address&gt;</c>.
    /// Pure parsing, so it is unit-tested without Unity.
    /// </summary>
    public static class LaunchOptions
    {
        public const string HostFlag = "-echo-host";
        public const string JoinFlag = "-echo-join";
        public const string NoSteamFlag = "-echo-no-steam";

        /// <summary>Added by Steam when a player accepts an invite while the game is closed: <c>+connect_lobby &lt;id&gt;</c>.</summary>
        public const string ConnectLobbyFlag = "+connect_lobby";

        /// <summary>The Steam lobby to join from <c>+connect_lobby &lt;id&gt;</c>, or 0 when there is none.</summary>
        public static ulong ConnectLobby(string[] args)
        {
            if (args == null) return 0;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], ConnectLobbyFlag, System.StringComparison.OrdinalIgnoreCase)
                    && ulong.TryParse(args[i + 1], out ulong lobby))
                {
                    return lobby;
                }
            }
            return 0;
        }

        /// <summary>
        /// True when <c>-echo-no-steam</c> is present: the game then never touches the Steam client. Automated tests use
        /// it so they do not depend on (or fight over) a running Steam session; it also exercises the play-without-Steam path.
        /// </summary>
        public static bool SteamDisabled(string[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], NoSteamFlag, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Reads the launch mode (and the optional join address) from <paramref name="args"/>.
        /// Hosting wins when both flags are present. The address is the argument right after <c>-echo-join</c>
        /// when it does not look like another flag.
        /// </summary>
        public static LaunchMode Parse(string[] args, out string joinAddress)
        {
            joinAddress = null;
            if (args == null) return LaunchMode.None;

            bool host = false;
            bool join = false;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, HostFlag, System.StringComparison.OrdinalIgnoreCase))
                {
                    host = true;
                }
                else if (string.Equals(arg, JoinFlag, System.StringComparison.OrdinalIgnoreCase))
                {
                    join = true;
                    if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]) && !args[i + 1].StartsWith("-"))
                    {
                        joinAddress = args[i + 1];
                    }
                }
            }

            if (host) return LaunchMode.Host;
            if (join) return LaunchMode.Join;

            // The photo mode needs a running match to photograph, so it implies hosting one.
            return EchoProtocol.Visuals.PhotoMode.Requested(args, out _) ? LaunchMode.Host : LaunchMode.None;
        }
    }
}
