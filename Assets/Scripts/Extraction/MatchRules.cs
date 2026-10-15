using UnityEngine;

namespace EchoProtocol.Extraction
{
    /// <summary>
    /// Pure match-flow rules: which state changes are allowed and how the extraction countdown ticks. No networking.
    /// <see cref="GameExtractionManager"/> applies them on the server, or locally when there is no network.
    /// </summary>
    public static class MatchRules
    {
        /// <summary>Smallest change in remaining seconds worth sending to the clients.</summary>
        public const float SyncStepSeconds = 0.25f;

        public static bool IsResolved(ExtractionState state) => state == ExtractionState.Victory || state == ExtractionState.Defeat;

        public static bool CanStartExtraction(ExtractionState state) => state == ExtractionState.ObjectiveInProgress;

        /// <summary>Only an active extraction can be won by reaching the portal.</summary>
        public static bool CanEscape(ExtractionState state) => state == ExtractionState.ExtractionActive;

        public static bool CanDefeat(ExtractionState state) => !IsResolved(state);

        /// <summary>A match can be restarted only once it has been won or lost.</summary>
        public static bool CanRestart(ExtractionState state) => IsResolved(state);

        /// <summary>Counts the countdown down by <paramref name="deltaSeconds"/>. Returns true once it has run out.</summary>
        public static bool Tick(ref float remainingSeconds, float deltaSeconds)
        {
            remainingSeconds -= deltaSeconds;
            if (remainingSeconds > 0f) return false;

            remainingSeconds = 0f;
            return true;
        }

        /// <summary>True when the clients' copy of the countdown is stale enough to be worth a network update.</summary>
        public static bool ShouldSync(float lastSent, float current) => Mathf.Abs(lastSent - current) >= SyncStepSeconds;

        /// <summary>Keeps a quote index inside a list of <paramref name="count"/> quotes.</summary>
        public static int ClampQuote(int index, int count) => count <= 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
    }
}
