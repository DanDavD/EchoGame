using System;

namespace EchoProtocol.Core
{
    /// <summary>
    /// Shared noise meter for the whole team. Every voice fills it and silence drains it. When it is full the
    /// cave raises the alarm, and the alarm only ends after the whole team has stayed quiet for a while.
    /// Pure rules: no Unity objects and no networking. The server feeds it the team's summed loudness each tick.
    /// </summary>
    public sealed class TeamNoiseBudget
    {
        private readonly float _capacity;
        private readonly float _fillPerSecond;
        private readonly float _drainPerSecond;
        private readonly float _calmSeconds;
        private readonly float _quietThreshold;

        private float _level;
        private float _quietSeconds;

        /// <summary>Raised once, when the meter fills up and the alarm starts.</summary>
        public event Action Alarmed;

        /// <summary>Raised once, when the team has been quiet long enough and the alarm ends.</summary>
        public event Action Calmed;

        /// <param name="capacity">Level at which the alarm starts.</param>
        /// <param name="fillPerSecond">How fast one unit of team loudness fills the meter.</param>
        /// <param name="drainPerSecond">How fast the meter empties while the team is quiet.</param>
        /// <param name="calmSeconds">Continuous quiet needed to end the alarm.</param>
        /// <param name="quietThreshold">Summed loudness below this counts as quiet.</param>
        public TeamNoiseBudget(float capacity, float fillPerSecond, float drainPerSecond, float calmSeconds, float quietThreshold = 0.05f)
        {
            _capacity = Math.Max(0.0001f, capacity);
            _fillPerSecond = Math.Max(0f, fillPerSecond);
            _drainPerSecond = Math.Max(0f, drainPerSecond);
            _calmSeconds = Math.Max(0f, calmSeconds);
            _quietThreshold = Math.Max(0f, quietThreshold);
        }

        /// <summary>Current level, between 0 and the capacity.</summary>
        public float Level => _level;

        /// <summary>Level as a 0 to 1 fraction, for the HUD and the music.</summary>
        public float Fill01 => _level / _capacity;

        /// <summary>True while the alarm is active.</summary>
        public bool IsAlarmed { get; private set; }

        /// <summary>Seconds of continuous quiet collected toward calming down. Only meaningful during the alarm.</summary>
        public float QuietSeconds => _quietSeconds;

        /// <summary>
        /// Advances the meter by <paramref name="deltaSeconds"/> given the team's summed loudness
        /// (each player's 0 to 1 loudness added together).
        /// </summary>
        public void Tick(float deltaSeconds, float teamLoudness)
        {
            if (deltaSeconds <= 0f) return;

            float loudness = Math.Max(0f, teamLoudness);
            bool quiet = loudness < _quietThreshold;

            if (IsAlarmed)
            {
                // Any sound during the alarm restarts the count, so the whole team has to stay silent.
                _quietSeconds = quiet ? _quietSeconds + deltaSeconds : 0f;
                if (_quietSeconds >= _calmSeconds)
                {
                    IsAlarmed = false;
                    _level = 0f;
                    _quietSeconds = 0f;
                    Calmed?.Invoke();
                }
                return;
            }

            if (quiet)
            {
                _level = Math.Max(0f, _level - _drainPerSecond * deltaSeconds);
                return;
            }

            _level = Math.Min(_capacity, _level + loudness * _fillPerSecond * deltaSeconds);
            if (_level >= _capacity) RaiseAlarm();
        }

        /// <summary>
        /// Adds <paramref name="amount"/> straight to the meter (the debug key, for example). Raises the alarm when it
        /// fills up. Does nothing during an alarm.
        /// </summary>
        public void Add(float amount)
        {
            if (IsAlarmed || amount <= 0f) return;

            _level = Math.Min(_capacity, _level + amount);
            if (_level >= _capacity) RaiseAlarm();
        }

        /// <summary>Takes <paramref name="amount"/> off the meter (a soothed relic, for example). Does nothing during an alarm.</summary>
        public void Remove(float amount)
        {
            if (IsAlarmed || amount <= 0f) return;
            _level = Math.Max(0f, _level - amount);
        }

        /// <summary>Fills the meter and starts the alarm right away, unless it is already on.</summary>
        public void ForceAlarm()
        {
            if (IsAlarmed) return;
            _level = _capacity;
            RaiseAlarm();
        }

        private void RaiseAlarm()
        {
            IsAlarmed = true;
            _quietSeconds = 0f;
            Alarmed?.Invoke();
        }

        /// <summary>Empties the meter and ends any alarm, for a new match.</summary>
        public void Reset()
        {
            _level = 0f;
            _quietSeconds = 0f;
            IsAlarmed = false;
        }
    }
}
