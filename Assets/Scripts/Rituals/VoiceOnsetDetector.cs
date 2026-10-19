using System.Collections.Generic;

namespace EchoProtocol.Rituals
{
    /// <summary>
    /// Finds the start of each syllable ("ba", "da", a clap) in a stream of voice levels, so a ritual can compare the
    /// player's rhythm with the relic's. It only looks at loudness over time, never at words, so it works in any
    /// language. Pure rules: feed it the raw (unsmoothed) 0 to 1 level every frame.
    /// </summary>
    public sealed class VoiceOnsetDetector
    {
        private readonly float _onLevel;
        private readonly float _offLevel;
        private readonly float _releaseFraction;
        private readonly float _minGapSeconds;
        private readonly List<float> _onsets = new List<float>();

        private bool _inSyllable;
        private float _peak;
        private float _valley;
        private float _lastOnset = float.NegativeInfinity;

        /// <param name="onLevel">Level a syllable has to reach to count.</param>
        /// <param name="offLevel">Level below which a syllable is always over.</param>
        /// <param name="releaseFraction">A syllable also ends when the level falls below this fraction of its peak, so a
        /// loud voice that never drops to silence between syllables still splits into beats.</param>
        /// <param name="minGapSeconds">Shortest time between two onsets; anything closer is the same syllable.</param>
        public VoiceOnsetDetector(float onLevel = 0.2f, float offLevel = 0.1f, float releaseFraction = 0.55f, float minGapSeconds = 0.09f)
        {
            _onLevel = onLevel;
            _offLevel = offLevel;
            _releaseFraction = releaseFraction;
            _minGapSeconds = minGapSeconds;
        }

        /// <summary>Times (in the caller's clock) at which each syllable started, oldest first.</summary>
        public IReadOnlyList<float> Onsets => _onsets;

        /// <summary>Forgets every onset and starts listening again.</summary>
        public void Reset()
        {
            _onsets.Clear();
            _inSyllable = false;
            _peak = 0f;
            _valley = 0f;
            _lastOnset = float.NegativeInfinity;
        }

        /// <summary>Feeds one level reading taken at <paramref name="time"/>. Returns true when it starts a new syllable.</summary>
        public bool Sample(float time, float level)
        {
            if (_inSyllable)
            {
                if (level > _peak) _peak = level;
                if (level <= _offLevel || level <= _peak * _releaseFraction)
                {
                    _inSyllable = false;
                    _valley = level;
                }
                return false;
            }

            if (level < _valley) _valley = level;

            // A new syllable has to rise clearly above the dip that ended the last one, or a loud voice hovering between
            // syllables would count as a fresh beat on every frame.
            float needed = _releaseFraction > 0f ? System.Math.Max(_onLevel, _valley / _releaseFraction) : _onLevel;
            if (level < needed || time - _lastOnset < _minGapSeconds) return false;

            _inSyllable = true;
            _peak = level;
            _lastOnset = time;
            _onsets.Add(time);
            return true;
        }
    }
}
