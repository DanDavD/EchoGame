using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace EchoProtocol.Voice
{
    /// <summary>
    /// Proximity voice chat over the game's own network (Fish-Net), so it works the same on a LAN (Tugboat) and over
    /// Steam, with no extra service.
    /// <list type="bullet">
    /// <item>The <b>owner</b> reads its microphone, keeps only the moments it is actually talking (with a short hangover
    /// so word endings are not cut), downsamples and mu-law encodes the audio, and sends small packets to the server on the
    /// unreliable channel: a lost packet is a tiny gap, never a delay.</item>
    /// <item>The <b>server</b> relays each packet to the other players close enough to hear it (beyond the hearing
    /// distance the 3D source is silent anyway), and drops packets from a speaker who sends far more than talking needs.</item>
    /// <item>Every <b>other peer</b> plays it from a 3D audio source on that player's body. Voices fade with distance, so
    /// a friend who falls, wanders off or gets dragged away by the beast fades out too.</item>
    /// </list>
    /// Talking here is only chat: it never lights the cave (that is the V echo). The monster and the team noise meter
    /// already hear the voice through <see cref="VoiceNoiseEmitter"/>. Offline it does nothing.
    /// </summary>
    [RequireComponent(typeof(MicrophoneLoudness))]
    public class ProximityVoice : NetworkBehaviour
    {
        /// <summary>Largest packet the server relays; anything bigger is dropped.</summary>
        public const int MaxPacketBytes = 1024;

        [Header("Hearing")]
        [Tooltip("Distance (m) at which a voice is at full volume.")]
        [SerializeField] private float _minDistance = 2f;
        [Tooltip("Distance (m) beyond which a voice can no longer be heard.")]
        [SerializeField] private float _maxDistance = 28f;
        [SerializeField, Range(0f, 2f)] private float _volume = 1.2f;

        [Header("Sending")]
        [Tooltip("Voice level (0..1) at which the microphone counts as talking.")]
        [SerializeField, Range(0f, 0.5f)] private float _talkGate = 0.06f;
        [Tooltip("Seconds the microphone keeps sending after the voice drops, so word endings are not cut off.")]
        [SerializeField] private float _hangoverSeconds = 0.35f;
        [Tooltip("Seconds of audio kept from just before the voice was detected, so the first syllable is not clipped.")]
        [SerializeField] private float _prerollSeconds = 0.12f;
        [Tooltip("Seconds of audio per packet. Smaller is snappier but costs more packets.")]
        [SerializeField] private float _packetSeconds = 0.04f;

        [Header("Receiving")]
        [Tooltip("Seconds of audio to collect before a remote voice starts playing (hides network jitter).")]
        [SerializeField] private float _prebufferSeconds = 0.1f;
        [Tooltip("Most seconds of audio waiting to play; older audio is dropped beyond it so a voice never lags behind.")]
        [SerializeField] private float _maxBufferSeconds = 0.5f;

        /// <summary>Master volume for every remote voice, for a settings menu (0 mutes everyone).</summary>
        public static float MasterVolume { get; set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => MasterVolume = 1f;

        private MicrophoneLoudness _mic;

        // Owner side.
        private readonly List<float> _captured = new List<float>();
        private readonly List<float> _carry = new List<float>();
        private readonly List<float> _pending = new List<float>();
        private byte[] _packetBuffer;
        private float _hangover;

        // Server side: one limiter per speaker.
        private PacketRateLimiter _relayLimiter;

        // Listener side.
        private VoicePlaybackBuffer _buffer;
        private AudioSource _source;
        private float[] _decoded;
        private float _lastPacketTime = -10f;

        /// <summary>Samples per second the voice travels at (the same on every peer).</summary>
        public int VoiceSampleRate => VoiceCodec.SampleRate;

        /// <summary>Packets the server dropped from this speaker for sending too fast (for tests and debugging).</summary>
        public int RelayDropped => _relayLimiter != null ? _relayLimiter.Dropped : 0;

        private int DownsampleFactor => VoiceRelay.DownsampleFactor(_mic != null ? _mic.CaptureSampleRate : 16000);

        /// <summary>True while this player's voice was heard over the network a moment ago (for a "talking" icon).</summary>
        public bool IsHeardTalking => Time.time - _lastPacketTime < 0.25f;

        /// <summary>True while the local owner is sending voice.</summary>
        public bool IsSending { get; private set; }

        /// <summary>Remote voice audio waiting to be played, in samples (for tests and debugging).</summary>
        public int BufferedSamples => _buffer != null ? _buffer.Buffered : 0;

        private int PacketSamples => Mathf.Max(32, Mathf.RoundToInt(VoiceSampleRate * _packetSeconds));

        private void Awake()
        {
            _mic = GetComponent<MicrophoneLoudness>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) EnsurePlayback();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_buffer != null) _buffer.Clear();
            if (_source != null) _source.Stop();
        }

        // ---------------------------------------------------------------- owner: capture and send

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                CaptureAndSend();
            }
            else if (_source != null)
            {
                _source.volume = _volume * MasterVolume;
            }
        }

        private void CaptureAndSend()
        {
            if (_mic == null || !_mic.isActiveAndEnabled) return;

            _captured.Clear();
            _mic.ReadNewSamples(_captured);

            // Voice activity: only send while talking, plus a short tail.
            if (_mic.Level01 >= _talkGate) _hangover = _hangoverSeconds;
            else _hangover -= Time.deltaTime;

            VoiceCodec.Downsample(_captured, DownsampleFactor, _pending, _carry);

            IsSending = _hangover > 0f;
            if (!IsSending)
            {
                // Silent: send nothing, but keep the last moment so the first syllable survives the detection delay.
                int preroll = Mathf.RoundToInt(VoiceSampleRate * _prerollSeconds);
                if (_pending.Count > preroll) _pending.RemoveRange(0, _pending.Count - preroll);
                return;
            }

            int packetSamples = PacketSamples;
            if (_packetBuffer == null || _packetBuffer.Length != packetSamples)
            {
                _packetBuffer = new byte[packetSamples];
            }

            while (_pending.Count >= packetSamples)
            {
                VoiceCodec.Encode(_pending, packetSamples, _packetBuffer);
                _pending.RemoveRange(0, packetSamples);
                ServerRelayVoice(_packetBuffer);
            }
        }

        [ServerRpc]
        private void ServerRelayVoice(byte[] packet, Channel channel = Channel.Unreliable)
        {
            if (packet == null || packet.Length == 0 || packet.Length > MaxPacketBytes) return;
            if (!RelayLimiter.TryConsume(Time.unscaledTime)) return;

            Vector3 speaker = transform.position;
            foreach (NetworkConnection listener in Observers)
            {
                if (listener == Owner) continue;

                // A connection's first object is its player. Without one (still loading) it gets the voice anyway.
                NetworkObject body = listener.FirstObject;
                if (body != null && !VoiceRelay.InRange(speaker, body.transform.position, _maxDistance)) continue;

                TargetPlayVoice(listener, packet);
            }
        }

        // Talking needs one packet every _packetSeconds; allow half as much again, and the pre-roll leaving in one burst.
        private PacketRateLimiter RelayLimiter
        {
            get
            {
                if (_relayLimiter == null)
                {
                    float perSecond = 1f / Mathf.Max(0.005f, _packetSeconds);
                    float burst = Mathf.Ceil(_prerollSeconds / Mathf.Max(0.005f, _packetSeconds)) + 6f;
                    _relayLimiter = new PacketRateLimiter(burst, perSecond * 1.5f);
                }
                return _relayLimiter;
            }
        }

        // ---------------------------------------------------------------- everyone else: play

        [TargetRpc]
        private void TargetPlayVoice(NetworkConnection target, byte[] packet, Channel channel = Channel.Unreliable)
        {
            ReceivePacket(packet);
        }

        /// <summary>Queues one encoded voice packet for playback on this avatar. Called for every packet that arrives.</summary>
        public void ReceivePacket(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return;
            EnsurePlayback();

            if (_decoded == null || _decoded.Length < packet.Length) _decoded = new float[Mathf.Max(packet.Length, MaxPacketBytes)];
            int count = VoiceCodec.Decode(packet, _decoded);
            _buffer.Write(_decoded, count);
            _lastPacketTime = Time.time;
        }

        private void EnsurePlayback()
        {
            if (_buffer != null) return;

            int rate = VoiceSampleRate;
            _buffer = new VoicePlaybackBuffer(Mathf.RoundToInt(rate * _maxBufferSeconds), Mathf.RoundToInt(rate * _prebufferSeconds));

            var go = new GameObject("ProximityVoice");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.6f, 0f); // roughly the mouth

            _source = go.AddComponent<AudioSource>();
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear; // fully silent at max distance: that is the "proximity"
            _source.minDistance = _minDistance;
            _source.maxDistance = _maxDistance;
            _source.dopplerLevel = 0f;
            _source.spread = 30f;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = _volume * MasterVolume;

            // A streaming clip that pulls audio from the jitter buffer on the audio thread.
            _source.clip = AudioClip.Create($"Voice_{name}", rate, 1, rate, true, OnAudioRead);
            _source.Play();
        }

        // Runs on the audio thread: only touches the thread-safe buffer.
        private void OnAudioRead(float[] data)
        {
            if (_buffer == null)
            {
                System.Array.Clear(data, 0, data.Length);
                return;
            }
            _buffer.Read(data);
        }
    }
}
