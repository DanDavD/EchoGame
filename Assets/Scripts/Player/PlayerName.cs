using System.Text;
using System.Text.RegularExpressions;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace EchoProtocol.Player
{
    /// <summary>
    /// The name everyone sees above this player (their Steam name, or "Player N" without Steam). The owner sends it once
    /// when the player spawns; the server cleans it up and replicates it to every peer, including players who join later.
    /// <see cref="EchoProtocol.UI.NameTagOverlay"/> draws it. Offline it is never shown (there are no teammates).
    /// </summary>
    public class PlayerName : NetworkBehaviour
    {
        /// <summary>Longest name shown; longer ones are cut so a tag never covers the screen.</summary>
        public const int MaxLength = 20;

        /// <summary>
        /// Where the local player's name comes from. The networking assembly sets it to the Steam name; this assembly
        /// cannot reference Steam. Returning null or empty falls back to "Player N".
        /// </summary>
        public static System.Func<string> LocalNameProvider { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LocalNameProvider = null;

        private static readonly Regex RichTextTag = new Regex("<[^>]*>");

        private readonly SyncVar<string> _name = new SyncVar<string>();

        /// <summary>The name to show for this player, as this peer knows it.</summary>
        public string DisplayName => string.IsNullOrEmpty(_name.Value) ? Fallback(IsSpawned ? OwnerId : 0) : _name.Value;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) ServerSetName(LocalNameProvider?.Invoke());
        }

        [ServerRpc]
        private void ServerSetName(string requested)
        {
            _name.Value = Sanitize(requested, OwnerId);
        }

        /// <summary>
        /// A name safe to draw: no rich-text tags or control characters, single spaces, at most <see cref="MaxLength"/>
        /// characters, and "Player N" when nothing is left. The server runs it, so a modified client cannot inject markup.
        /// </summary>
        public static string Sanitize(string raw, int ownerId)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Fallback(ownerId);

            string text = RichTextTag.Replace(raw, string.Empty);
            var builder = new StringBuilder(text.Length);
            bool lastWasSpace = false;
            foreach (char c in text)
            {
                bool space = char.IsWhiteSpace(c); // tabs and line breaks count as spaces, not as junk
                if (!space && (char.IsControl(c) || c == '<' || c == '>')) continue;
                if (space && (lastWasSpace || builder.Length == 0)) continue;
                builder.Append(space ? ' ' : c);
                lastWasSpace = space;
            }

            string clean = builder.ToString().Trim();
            if (clean.Length > MaxLength) clean = clean.Substring(0, MaxLength).TrimEnd();
            return clean.Length == 0 ? Fallback(ownerId) : clean;
        }

        /// <summary>The name of a player without one: client 0 (the host) is "Player 1".</summary>
        public static string Fallback(int ownerId) => $"Player {Mathf.Max(0, ownerId) + 1}";
    }
}
