using UnityEngine;

namespace EchoProtocol.Voice
{
    /// <summary>
    /// Pure rules the server applies before relaying a voice packet: who is close enough to hear it, and how many
    /// packets one speaker may send. No networking.
    /// </summary>
    public static class VoiceRelay
    {
        /// <summary>
        /// Metres added to the hearing distance before a listener is skipped: both positions arrive with network delay,
        /// so a friend walking into range must not lose the first words.
        /// </summary>
        public const float RangeSlackMetres = 4f;

        /// <summary>
        /// True when a listener at <paramref name="listener"/> should get the voice of a speaker at
        /// <paramref name="speaker"/>. Beyond <paramref name="maxDistance"/> the 3D source is silent anyway, so the packet
        /// would only cost bandwidth.
        /// </summary>
        public static bool InRange(Vector3 speaker, Vector3 listener, float maxDistance)
        {
            float range = maxDistance + RangeSlackMetres;
            return (listener - speaker).sqrMagnitude <= range * range;
        }

        /// <summary>
        /// The capture is divided by this to reach <see cref="VoiceCodec.SampleRate"/>. A microphone that does not
        /// capture a multiple of it still sends something sensible (never below 1).
        /// </summary>
        public static int DownsampleFactor(int captureRate) => Mathf.Max(1, captureRate / VoiceCodec.SampleRate);
    }

    /// <summary>
    /// A token bucket: one speaker may send <c>refillPerSecond</c> packets a second on average, with short bursts up to
    /// <c>capacity</c> (the pre-roll leaves in one go when a player starts talking). Packets beyond that are dropped,
    /// so one modified client cannot flood the host.
    /// </summary>
    public sealed class PacketRateLimiter
    {
        private readonly float _capacity;
        private readonly float _refillPerSecond;
        private float _tokens;
        private float _lastTime = float.NegativeInfinity;

        public PacketRateLimiter(float capacity, float refillPerSecond)
        {
            _capacity = Mathf.Max(1f, capacity);
            _refillPerSecond = Mathf.Max(0f, refillPerSecond);
            _tokens = _capacity;
        }

        /// <summary>Packets dropped so far (for tests and debugging).</summary>
        public int Dropped { get; private set; }

        /// <summary>Spends one packet at time <paramref name="now"/> (seconds). False means drop it.</summary>
        public bool TryConsume(float now)
        {
            if (!float.IsNegativeInfinity(_lastTime))
            {
                _tokens = Mathf.Min(_capacity, _tokens + (now - _lastTime) * _refillPerSecond);
            }
            _lastTime = now;

            if (_tokens >= 1f)
            {
                _tokens -= 1f;
                return true;
            }

            Dropped++;
            return false;
        }
    }
}
