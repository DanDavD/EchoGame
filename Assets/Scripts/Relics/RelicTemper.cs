namespace EchoProtocol.Relics
{
    /// <summary>What a relic's mood did this tick.</summary>
    public enum TemperEvent
    {
        None,
        /// <summary>It has been in the same hands for too long and starts complaining: pass it on soon.</summary>
        Fussy,
        /// <summary>It bursts into tears: the carrier was too loud, or nobody passed it around.</summary>
        Cry
    }

    /// <summary>
    /// The mood rules of a sensitive relic. It is carried like a baby: the carrier has to whisper, and with two or more
    /// players it wants to be passed from hand to hand. Anything louder than a whisper for a moment, or keeping it too
    /// long, makes it cry. Pure rules: no Unity objects and no networking.
    /// </summary>
    public sealed class RelicTemper
    {
        private readonly float _loudLevel;
        private readonly float _strainSeconds;
        private readonly float _patienceSeconds;
        private readonly float _fussyFraction;

        private float _strain;
        private float _patience;
        private float _grace;
        private bool _warned;

        /// <param name="loudLevel">Carrier voice level (0 to 1) that counts as too loud. Whispers stay below it.</param>
        /// <param name="strainSeconds">How long the carrier may stay too loud before the relic cries.</param>
        /// <param name="patienceSeconds">How long one player may carry it, with teammates around, before it cries.</param>
        /// <param name="fussyFraction">Fraction of the patience at which it starts complaining.</param>
        public RelicTemper(float loudLevel = 0.4f, float strainSeconds = 0.6f, float patienceSeconds = 35f, float fussyFraction = 0.7f)
        {
            _loudLevel = loudLevel;
            _strainSeconds = strainSeconds;
            _patienceSeconds = patienceSeconds;
            _fussyFraction = fussyFraction;
        }

        /// <summary>How much of the current carrier's patience is used, 0 to 1.</summary>
        public float Patience01 => _patienceSeconds <= 0f ? 0f : _patience / _patienceSeconds;

        /// <summary>True while a recent soothing still protects it from crying again.</summary>
        public bool IsInGrace => _grace > 0f;

        /// <summary>
        /// Advances the mood by <paramref name="deltaSeconds"/>. <paramref name="carrierLevel"/> is the carrier's voice
        /// (ignored when not carried) and <paramref name="playerCount"/> how many players are in the match: patience only
        /// runs out when someone else could take the relic.
        /// </summary>
        public TemperEvent Tick(float deltaSeconds, bool carried, float carrierLevel, int playerCount)
        {
            if (deltaSeconds <= 0f) return TemperEvent.None;

            if (_grace > 0f)
            {
                _grace -= deltaSeconds;
                _strain = 0f;
                return TemperEvent.None;
            }

            if (!carried)
            {
                // On the floor it relaxes slowly, so dropping it and picking it straight back up does not dodge passing it on.
                // A different player picking it up is a pass, and the caller resets the patience then.
                _strain = 0f;
                _patience = System.Math.Max(0f, _patience - deltaSeconds * 0.5f);
                if (_patience < _patienceSeconds * _fussyFraction) _warned = false;
                return TemperEvent.None;
            }

            if (carrierLevel >= _loudLevel) _strain += deltaSeconds;
            else _strain = System.Math.Max(0f, _strain - deltaSeconds * 2f);

            if (_strain >= _strainSeconds)
            {
                _strain = 0f;
                ResetPatience();
                return TemperEvent.Cry;
            }

            if (playerCount < 2 || _patienceSeconds <= 0f) return TemperEvent.None;

            _patience += deltaSeconds;
            if (_patience >= _patienceSeconds)
            {
                ResetPatience();
                return TemperEvent.Cry;
            }

            if (!_warned && _patience >= _patienceSeconds * _fussyFraction)
            {
                _warned = true;
                return TemperEvent.Fussy;
            }
            return TemperEvent.None;
        }

        /// <summary>A new pair of hands: the patience starts again (the relic was passed on).</summary>
        public void ResetPatience()
        {
            _patience = 0f;
            _warned = false;
        }

        /// <summary>The relic was calmed down: no crying at all for <paramref name="graceSeconds"/>, then a fresh start.</summary>
        public void Soothe(float graceSeconds)
        {
            _grace = System.Math.Max(0f, graceSeconds);
            _strain = 0f;
            ResetPatience();
        }
    }
}
