using UnityEngine;

/// <summary>
/// The rules that turn movement into sound, kept apart from the audio components so they can be tested.
/// Everything works from the horizontal speed alone, so it gives the same result on every network peer.
/// </summary>
public static class MovementSounds
{
    /// <summary>Below this speed (m/s) nobody is walking, so there are no steps.</summary>
    public const float MinStepSpeed = 0.6f;

    /// <summary>
    /// Seconds between two steps at <paramref name="speed"/> m/s for a stride of <paramref name="strideMetres"/>,
    /// or 0 when standing still. Faster means shorter intervals, with a floor so a sprint never becomes a drum roll.
    /// </summary>
    public static float StepInterval(float speed, float strideMetres)
    {
        if (speed < MinStepSpeed || strideMetres <= 0f) return 0f;
        return Mathf.Max(0.22f, strideMetres / speed);
    }

    /// <summary>Volume 0..1 of a step at <paramref name="speed"/>: soft when walking, loud when sprinting.</summary>
    public static float StepVolume(float speed, float walkSpeed, float sprintSpeed)
    {
        if (speed < MinStepSpeed) return 0f;
        float t = Mathf.InverseLerp(walkSpeed, sprintSpeed, speed);
        return Mathf.Lerp(0.45f, 1f, t);
    }

    /// <summary>
    /// Decides when the monster roars: when it starts moving at hunting speed after a calm spell, and not again before
    /// a cooldown. Fed with the speed every frame; works on clients too, where only the movement is replicated.
    /// </summary>
    public sealed class RoarTrigger
    {
        public const float HuntSpeed = 4.5f;
        public const float CalmSpeed = 3.5f;
        public const float CalmSecondsNeeded = 2f;
        public const float FastSecondsNeeded = 0.25f;
        public const float CooldownSeconds = 8f;

        private float _calmFor = CalmSecondsNeeded; // a monster that spawns already hunting may roar at once
        private float _fastFor;
        private float _cooldown;
        private bool _armed = true;

        /// <summary>Returns true on the one update where the monster should roar.</summary>
        public bool Update(float speed, float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return false;
            _cooldown = Mathf.Max(0f, _cooldown - deltaSeconds);

            if (speed <= CalmSpeed)
            {
                _calmFor += deltaSeconds;
                _fastFor = 0f;
                if (_calmFor >= CalmSecondsNeeded) _armed = true;
                return false;
            }

            _calmFor = 0f;
            if (speed < HuntSpeed) return false;

            _fastFor += deltaSeconds;
            if (!_armed || _cooldown > 0f || _fastFor < FastSecondsNeeded) return false;

            _armed = false;
            _cooldown = CooldownSeconds;
            return true;
        }
    }
}
