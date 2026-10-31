namespace EchoProtocol.UI
{
    /// <summary>
    /// Pure rules for the hints a new player sees at the start of a match: one line at a time, each gone as soon as the
    /// player has done what it asks. No hints once the first relic is on the altar or after <see cref="MaxSeconds"/>.
    /// </summary>
    public static class FirstMatchHints
    {
        /// <summary>Seconds into the match after which nobody needs the hints any more.</summary>
        public const float MaxSeconds = 150f;

        public const string Echo = "Hold [V] and talk: your voice lights the cave. Louder lights more, and the beast hears it.";
        public const string FindRelic = "The relics talk. Follow their voices, look at one and press [E].";
        public const string Carry = "Take it to the altar and whisper: loud voices make a relic cry.";

        /// <summary>The hint to show now, or null for none.</summary>
        public static string Pick(float secondsInMatch, bool hasEchoed, bool carryingRelic, int relicsOnAltar)
        {
            if (relicsOnAltar > 0 || secondsInMatch > MaxSeconds) return null;
            if (!hasEchoed) return Echo;
            return carryingRelic ? Carry : FindRelic;
        }
    }
}
