using System;
using System.Collections.Generic;

namespace EchoProtocol.Rituals
{
    /// <summary>
    /// The "human echo" ritual's rules: the relic plays a short rhythm and the players repeat it with their voice.
    /// A pattern is a list of beat times in seconds, the first beat at 0. Matching compares the time between beats,
    /// never the words, and forgives a player who echoes the whole rhythm a little slower or faster.
    /// Pure rules: no Unity objects and no networking.
    /// </summary>
    public static class EchoRhythm
    {
        /// <summary>Fewest and most beats a pattern can have.</summary>
        public const int MinBeats = 2;
        public const int MaxBeats = 4;

        /// <summary>Seconds of one short step between beats at tempo 1. A long step is two of them.</summary>
        public const float StepSeconds = 0.34f;

        /// <summary>How far each beat may land from where it should, in seconds, after the tempo is evened out.</summary>
        public const float DefaultTolerance = 0.17f;

        /// <summary>How much slower or faster than the relic a whole echo may be and still count.</summary>
        public const float MinTempoScale = 0.7f;
        public const float MaxTempoScale = 1.45f;

        // Steps between consecutive beats, 1 = short and 2 = long, indexed by beat count.
        private static readonly int[][][] Templates =
        {
            null,
            null,
            new[] { new[] { 1 }, new[] { 2 } },
            new[] { new[] { 1, 1 }, new[] { 1, 2 }, new[] { 2, 1 } },
            new[] { new[] { 1, 1, 1 }, new[] { 1, 1, 2 }, new[] { 2, 1, 1 }, new[] { 1, 2, 1 } },
        };

        /// <summary>
        /// A random pattern of <paramref name="beats"/> beats. <paramref name="tempo"/> stretches it: 1 is normal, above 1
        /// slower (a pompous relic), below 1 faster (a nervous one). The same seed always gives the same pattern.
        /// </summary>
        public static float[] CreatePattern(int seed, int beats, float tempo)
        {
            beats = Math.Max(MinBeats, Math.Min(MaxBeats, beats));
            tempo = Math.Max(0.5f, Math.Min(2f, tempo));

            var random = new Random(seed);
            int[][] options = Templates[beats];
            int[] steps = options[random.Next(options.Length)];

            var pattern = new float[beats];
            for (int i = 1; i < beats; i++)
            {
                pattern[i] = pattern[i - 1] + steps[i - 1] * StepSeconds * tempo;
            }
            return pattern;
        }

        /// <summary>Seconds from the first beat to the last.</summary>
        public static float Duration(IReadOnlyList<float> pattern)
        {
            return pattern == null || pattern.Count == 0 ? 0f : pattern[pattern.Count - 1] - pattern[0];
        }

        /// <summary>
        /// True when <paramref name="onsets"/> (syllable start times in any clock) echo <paramref name="pattern"/>: the same
        /// number of beats, and once the echo's overall speed is matched to the pattern, every beat within
        /// <paramref name="tolerance"/> seconds of its place. <paramref name="worstError"/> is the furthest beat, in seconds.
        /// </summary>
        public static bool Matches(IReadOnlyList<float> pattern, IReadOnlyList<float> onsets, float tolerance, out float worstError)
        {
            worstError = float.PositiveInfinity;
            if (pattern == null || onsets == null || pattern.Count == 0 || onsets.Count != pattern.Count) return false;

            worstError = 0f;
            if (pattern.Count == 1) return true;

            float expectedSpan = pattern[pattern.Count - 1] - pattern[0];
            float actualSpan = onsets[onsets.Count - 1] - onsets[0];
            if (expectedSpan <= 0f || actualSpan <= 0f) return false;

            float tempoScale = actualSpan / expectedSpan;
            if (tempoScale < MinTempoScale || tempoScale > MaxTempoScale)
            {
                worstError = Math.Abs(actualSpan - expectedSpan);
                return false;
            }

            for (int i = 1; i < pattern.Count; i++)
            {
                float expected = pattern[i] - pattern[0];
                float actual = (onsets[i] - onsets[0]) / tempoScale;
                worstError = Math.Max(worstError, Math.Abs(actual - expected));
            }
            return worstError <= tolerance;
        }

        /// <summary>Beats for the next try: the first try uses <paramref name="startBeats"/>, and each failure makes it one beat easier.</summary>
        public static int BeatsForAttempt(int startBeats, int failedAttempts)
        {
            return Math.Max(MinBeats, Math.Min(MaxBeats, startBeats - Math.Max(0, failedAttempts)));
        }
    }
}
