using System;

namespace EchoProtocol.Voice
{
    /// <summary>
    /// A small jitter buffer for one remote voice. Network packets arrive in bursts; the audio thread reads at a steady
    /// rate. Playback only starts once a little audio has piled up (the pre-buffer), so short network hiccups do not turn
    /// into stutter; when it runs dry it goes silent and waits for the pre-buffer again. If it ever overflows (a long
    /// stall followed by a flood) the oldest audio is dropped, so the voice never lags further and further behind.
    /// Thread-safe: the network writes on the main thread and Unity reads on the audio thread.
    /// </summary>
    public sealed class VoicePlaybackBuffer
    {
        private readonly float[] _ring;
        private readonly int _prebuffer;
        private readonly object _lock = new object();

        private int _read;
        private int _count;
        private bool _playing;

        /// <param name="capacity">Most samples it can hold; older ones are dropped beyond it.</param>
        /// <param name="prebuffer">Samples that must be waiting before playback starts (or restarts after running dry).</param>
        public VoicePlaybackBuffer(int capacity, int prebuffer)
        {
            _ring = new float[Math.Max(1, capacity)];
            _prebuffer = Math.Max(0, Math.Min(prebuffer, _ring.Length));
        }

        /// <summary>Samples waiting to be played.</summary>
        public int Buffered
        {
            get { lock (_lock) return _count; }
        }

        /// <summary>True while audio is coming out (pre-buffer reached and not run dry since).</summary>
        public bool IsPlaying
        {
            get { lock (_lock) return _playing; }
        }

        /// <summary>Adds the first <paramref name="count"/> samples of <paramref name="samples"/>.</summary>
        public void Write(float[] samples, int count)
        {
            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_count == _ring.Length)
                    {
                        // Full: drop the oldest sample so the voice stays close to real time.
                        _read = (_read + 1) % _ring.Length;
                        _count--;
                    }

                    _ring[(_read + _count) % _ring.Length] = samples[i];
                    _count++;
                }

                if (!_playing && _count >= _prebuffer && _count > 0) _playing = true;
            }
        }

        /// <summary>
        /// Fills <paramref name="output"/> with the next samples, or silence when there are none to play. Returns how many
        /// real samples were written.
        /// </summary>
        public int Read(float[] output)
        {
            lock (_lock)
            {
                int written = 0;
                for (int i = 0; i < output.Length; i++)
                {
                    if (_playing && _count > 0)
                    {
                        output[i] = _ring[_read];
                        _read = (_read + 1) % _ring.Length;
                        _count--;
                        written++;
                    }
                    else
                    {
                        _playing = false;
                        output[i] = 0f;
                    }
                }
                return written;
            }
        }

        /// <summary>Forgets everything that was waiting.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _read = 0;
                _count = 0;
                _playing = false;
            }
        }
    }
}
