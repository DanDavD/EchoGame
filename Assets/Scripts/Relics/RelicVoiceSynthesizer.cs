using System.Collections.Generic;
using UnityEngine;

namespace EchoProtocol.Relics
{
    /// <summary>
    /// Procedural speech and comedic SFX synthesizer for living relics.
    /// Generates custom waveforms at runtime with zero external audio assets,
    /// providing Banjo-Kazooie style speech blips, 3D Doppler flying screams, and cartoon impact thuds.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RelicVoiceSynthesizer : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _voiceSource;
        [SerializeField] private AudioSource _screamSource;
        [SerializeField] private AudioSource _choirSource;
        [SerializeField] private AudioSource _cryingSource;

        [Header("3D Spatial Settings")]
        [SerializeField] private float _minDistance = 2f;
        [SerializeField] private float _maxDistance = 55f;
        [SerializeField] private float _dopplerLevel = 1.4f;

        private Rigidbody _rb;
        private RelicPersonalityType _currentPersonality = RelicPersonalityType.SarcasticCritic;
        private bool _isScreaming;

        // Pre-cached procedural clips for each personality to avoid runtime GC
        private readonly Dictionary<RelicPersonalityType, AudioClip[]> _cachedBlipClips =
            new Dictionary<RelicPersonalityType, AudioClip[]>();

        private readonly Dictionary<RelicPersonalityType, AudioClip> _cachedScreamClips =
            new Dictionary<RelicPersonalityType, AudioClip>();

        private readonly Dictionary<RelicPersonalityType, AudioClip> _cachedImpactClips =
            new Dictionary<RelicPersonalityType, AudioClip>();

        private readonly Dictionary<RelicPersonalityType, AudioClip> _cachedChoirClips =
            new Dictionary<RelicPersonalityType, AudioClip>();

        private readonly Dictionary<RelicPersonalityType, AudioClip> _cachedCryClips =
            new Dictionary<RelicPersonalityType, AudioClip>();

        private readonly Dictionary<RelicPersonalityType, AudioClip> _cachedBeatClips =
            new Dictionary<RelicPersonalityType, AudioClip>();

        private AudioClip _turnTickClip;
        private AudioClip _successClip;
        private AudioClip _failClip;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (_voiceSource == null) _voiceSource = GetComponent<AudioSource>();
            if (_voiceSource == null) _voiceSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(_voiceSource, false);

            if (_screamSource == null)
            {
                var screamGo = new GameObject("ScreamAudioSource");
                screamGo.transform.SetParent(transform, false);
                _screamSource = screamGo.AddComponent<AudioSource>();
            }
            ConfigureSource(_screamSource, true);

            if (_choirSource == null)
            {
                var choirGo = new GameObject("ChoirAudioSource");
                choirGo.transform.SetParent(transform, false);
                _choirSource = choirGo.AddComponent<AudioSource>();
            }
            ConfigureSource(_choirSource, true);

            if (_cryingSource == null)
            {
                var cryGo = new GameObject("CryingAudioSource");
                cryGo.transform.SetParent(transform, false);
                _cryingSource = cryGo.AddComponent<AudioSource>();
            }
            ConfigureSource(_cryingSource, true);

            GenerateAllProceduralClips();
        }

        private void ConfigureSource(AudioSource src, bool loop)
        {
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 1.0f; // Full 3D spatialization
            src.dopplerLevel = _dopplerLevel;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = _minDistance;
            src.maxDistance = _maxDistance;
            src.spread = 45f;
        }

        public void SetPersonality(RelicPersonalityType personality)
        {
            _currentPersonality = personality;
        }

        private void Update()
        {
            if (_isScreaming && _screamSource != null && _screamSource.isPlaying)
            {
                // Modulate scream pitch dynamically with actual flying velocity for exaggerated cartoon Doppler
                float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
                float profileBase = RelicPersonality.GetProfile(_currentPersonality).BasePitch;
                float velocityPitchFactor = Mathf.Clamp(1.0f + (speed / 35f) * 0.45f, 0.75f, 2.1f);
                _screamSource.pitch = profileBase * velocityPitchFactor;
            }
        }

        private float _lastBlipTime;

        /// <summary>
        /// Plays a short procedural Banjo-Kazooie/Animal Crossing style speech blip.
        /// </summary>
        public void PlaySpeechBlip(RelicPersonalityType personality, float pitchOffset = 0f)
        {
            if (_voiceSource == null) return;
            if (Time.time - _lastBlipTime < 0.075f) return;
            _lastBlipTime = Time.time;

            var profile = RelicPersonality.GetProfile(personality);
            if (!_cachedBlipClips.TryGetValue(personality, out var clips) || clips.Length == 0) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            float pitch = profile.BasePitch + Random.Range(-profile.PitchVariance, profile.PitchVariance) + pitchOffset;
            _voiceSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.5f);
            _voiceSource.PlayOneShot(clip, 0.8f);
        }

        /// <summary>
        /// Starts the continuous procedural Doppler scream while airborne.
        /// </summary>
        public void StartFlyingScream(RelicPersonalityType personality)
        {
            if (_screamSource == null) return;
            _currentPersonality = personality;
            _isScreaming = true;

            if (_cachedScreamClips.TryGetValue(personality, out var clip))
            {
                _screamSource.clip = clip;
                _screamSource.pitch = RelicPersonality.GetProfile(personality).BasePitch;
                _screamSource.volume = 1.0f;
                _screamSource.Play();
            }
        }

        /// <summary>
        /// Stops the airborne flying scream.
        /// </summary>
        public void StopFlyingScream()
        {
            _isScreaming = false;
            if (_screamSource != null && _screamSource.isPlaying)
            {
                _screamSource.Stop();
            }
        }

        /// <summary>
        /// Starts the continuous harmonious alien choir drone when docked in the Altar.
        /// </summary>
        public void StartSymphonyChoir(RelicPersonalityType personality)
        {
            if (_choirSource == null) return;
            _currentPersonality = personality;

            if (_cachedChoirClips.TryGetValue(personality, out var clip))
            {
                _choirSource.clip = clip;
                _choirSource.volume = 0.85f;
                _choirSource.pitch = 1.0f;
                _choirSource.Play();
            }
        }

        /// <summary>
        /// Stops the altar choir playback.
        /// </summary>
        public void StopSymphonyChoir()
        {
            if (_choirSource != null && _choirSource.isPlaying)
            {
                _choirSource.Stop();
            }
        }

        /// <summary>
        /// Plays a procedural cartoon impact thud/clonk upon landing.
        /// </summary>
        public void PlayImpactBonk(RelicPersonalityType personality, float volume = 1f)
        {
            if (_voiceSource == null) return;

            if (_cachedImpactClips.TryGetValue(personality, out var clip))
            {
                _voiceSource.pitch = RelicPersonality.GetProfile(personality).BasePitch * Random.Range(0.9f, 1.1f);
                _voiceSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            }
        }

        /// <summary>True while the crying loop is playing.</summary>
        public bool IsCrying => _cryingSource != null && _cryingSource.isPlaying;

        /// <summary>Starts the looping baby-like wail of a crying relic. Safe to call again while it already cries.</summary>
        public void StartCrying(RelicPersonalityType personality)
        {
            if (_cryingSource == null) return;
            if (!_cachedCryClips.TryGetValue(personality, out var clip)) return;
            if (_cryingSource.isPlaying && _cryingSource.clip == clip) return;

            _cryingSource.clip = clip;
            _cryingSource.pitch = 1f;
            _cryingSource.volume = 0.9f;
            _cryingSource.Play();
        }

        /// <summary>Stops the crying loop.</summary>
        public void StopCrying()
        {
            if (_cryingSource != null && _cryingSource.isPlaying) _cryingSource.Stop();
        }

        /// <summary>One beat of a ritual rhythm, in the relic's own timbre. <paramref name="accent"/> plays the first beat louder.</summary>
        public void PlayRitualBeat(RelicPersonalityType personality, bool accent)
        {
            if (_voiceSource == null || !_cachedBeatClips.TryGetValue(personality, out var clip)) return;
            _voiceSource.pitch = accent ? 1.12f : 1f;
            _voiceSource.PlayOneShot(clip, accent ? 1f : 0.8f);
        }

        /// <summary>A short tick that tells the next player it is their turn to echo.</summary>
        public void PlayTurnTick()
        {
            if (_voiceSource == null || _turnTickClip == null) return;
            _voiceSource.pitch = 1f;
            _voiceSource.PlayOneShot(_turnTickClip, 0.7f);
        }

        /// <summary>A rising jingle for a ritual the team got right, or a sad trombone for one it got wrong.</summary>
        public void PlayRitualResult(bool success)
        {
            AudioClip clip = success ? _successClip : _failClip;
            if (_voiceSource == null || clip == null) return;
            _voiceSource.pitch = 1f;
            _voiceSource.PlayOneShot(clip, 0.9f);
        }

        // =========================================================================
        // PROCEDURAL WAVEFORM GENERATION
        // =========================================================================

        private void GenerateAllProceduralClips()
        {
            foreach (RelicPersonalityType type in (RelicPersonalityType[])System.Enum.GetValues(typeof(RelicPersonalityType)))
            {
                // 1. Generate speech blips (3 variations per personality)
                var blips = new AudioClip[3];
                for (int i = 0; i < 3; i++)
                {
                    blips[i] = GenerateSpeechBlipClip(type, i);
                }
                _cachedBlipClips[type] = blips;

                // 2. Generate seamless looping scream clip (1.2 seconds)
                _cachedScreamClips[type] = GenerateScreamClip(type);

                // 3. Generate impact bonk clip
                _cachedImpactClips[type] = GenerateImpactClip(type);

                // 4. Generate seamless looping symphony choir clip (2.0 seconds)
                _cachedChoirClips[type] = GenerateChoirClip(type);

                // 5. Crying loop and ritual beat
                _cachedCryClips[type] = GenerateCryClip(type);
                _cachedBeatClips[type] = GenerateBeatClip(type);
            }

            _turnTickClip = GenerateToneSequence("TurnTick", new[] { 1320f }, 0.06f, 0.5f);
            _successClip = GenerateToneSequence("RitualSuccess", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, 0.55f);
            _failClip = GenerateToneSequence("RitualFail", new[] { 392f, 369.99f, 349.23f, 293.66f }, 0.2f, 0.5f);
        }

        /// <summary>Base pitch (Hz) of each relic's cry and beat, matching its speaking voice.</summary>
        private static float VoiceFrequency(RelicPersonalityType type) => type switch
        {
            RelicPersonalityType.SarcasticCritic => 330f,
            RelicPersonalityType.AnxiousCoward => 700f,
            RelicPersonalityType.DumbGloop => 440f,
            RelicPersonalityType.GrumpyElder => 165f,
            _ => 330f
        };

        private AudioClip GenerateCryClip(RelicPersonalityType type)
        {
            // A cartoon baby wail: a falling "WAAAH", two sobs per loop.
            float duration = 1.6f;
            int totalSamples = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            float baseFreq = VoiceFrequency(type);
            float phase = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float progress = (float)i / totalSamples;
                float sob = (progress * 2f) % 1f;
                float freq = baseFreq * Mathf.Lerp(1.35f, 0.85f, sob) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 6f * progress * duration));
                phase += 2f * Mathf.PI * freq / SampleRate;

                float wah = Mathf.Clamp01(sob * 12f) * Mathf.Clamp01((1f - sob) * 3f); // sharp attack, fading tail
                float voice = Mathf.Sin(phase) + 0.45f * Mathf.Sin(phase * 2f) + 0.2f * Mathf.Sin(phase * 3f);
                if (type == RelicPersonalityType.DumbGloop) voice = Mathf.Sign(Mathf.Sin(phase)) * 0.6f + 0.3f * Mathf.Sin(phase * 2f);

                float edgeFade = Mathf.Clamp01(Mathf.Sin(progress * Mathf.PI) * 30f);
                samples[i] = voice * wah * edgeFade * 0.42f;
            }

            var clip = AudioClip.Create($"Cry_{type}", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateBeatClip(RelicPersonalityType type)
        {
            float duration = 0.16f;
            int totalSamples = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];
            float freq = VoiceFrequency(type) * 1.5f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / totalSamples;
                float envelope = Mathf.Clamp01(progress * 40f) * Mathf.Exp(-progress * 6f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                samples[i] = tone * envelope * 0.6f;
            }

            var clip = AudioClip.Create($"Beat_{type}", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateToneSequence(string name, float[] notes, float noteSeconds, float volume)
        {
            int perNote = Mathf.RoundToInt(SampleRate * noteSeconds);
            float[] samples = new float[perNote * notes.Length];

            for (int n = 0; n < notes.Length; n++)
            {
                for (int i = 0; i < perNote; i++)
                {
                    float t = (float)i / SampleRate;
                    float progress = (float)i / perNote;
                    float envelope = Mathf.Clamp01(progress * 30f) * Mathf.Exp(-progress * 3f);
                    float tone = Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * t);
                    samples[n * perNote + i] = tone * envelope * volume;
                }
            }

            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateSpeechBlipClip(RelicPersonalityType type, int variant)
        {
            float duration = 0.065f;
            int totalSamples = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];

            float baseFreq = type switch
            {
                RelicPersonalityType.SarcasticCritic => 260f + variant * 35f,
                RelicPersonalityType.AnxiousCoward => 680f + variant * 80f,
                RelicPersonalityType.DumbGloop => 200f + variant * 120f,
                RelicPersonalityType.GrumpyElder => 125f + variant * 20f,
                _ => 300f
            };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / totalSamples;

                // Envelope: quick attack (5ms), exponential decay
                float attack = Mathf.Clamp01(progress * 15f);
                float envelope = attack * Mathf.Exp(-progress * 4.5f);

                float sample = 0f;

                switch (type)
                {
                    case RelicPersonalityType.SarcasticCritic:
                        // FM Synthesis: slightly cynical buzzing tone
                        float mod = Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * t) * 1.5f;
                        sample = Mathf.Sin(2f * Mathf.PI * baseFreq * t + mod);
                        break;

                    case RelicPersonalityType.AnxiousCoward:
                        // High sine with rapid jitter/tremolo
                        float vibrato = Mathf.Sin(2f * Mathf.PI * 25f * t) * 35f;
                        sample = Mathf.Sin(2f * Mathf.PI * (baseFreq + vibrato) * t);
                        // Add slight falsetto harmonic
                        sample += 0.35f * Mathf.Sin(2f * Mathf.PI * (baseFreq * 2f) * t);
                        break;

                    case RelicPersonalityType.DumbGloop:
                        // Bouncy cartoon frequency sweep (boing/bloop)
                        float sweepFreq = Mathf.Lerp(baseFreq * 0.7f, baseFreq * 2.2f, Mathf.Sin(progress * Mathf.PI));
                        sample = Mathf.Sin(2f * Mathf.PI * sweepFreq * t);
                        // Add goofy square overtone
                        sample += 0.2f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * sweepFreq * 0.5f * t));
                        break;

                    case RelicPersonalityType.GrumpyElder:
                        // Low saw/organ pompous hum
                        float saw = (2f * (t * baseFreq - Mathf.Floor(t * baseFreq + 0.5f)));
                        sample = saw * 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                        break;
                }

                samples[i] = sample * envelope * 0.8f;
            }

            var clip = AudioClip.Create($"Blip_{type}_{variant}", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateScreamClip(RelicPersonalityType type)
        {
            float duration = 1.4f;
            int totalSamples = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / totalSamples;

                // Loop smoothing envelope (fade in at start, fade out at end for seamless looping)
                float loopFade = Mathf.Sin(progress * Mathf.PI);

                float sample = 0f;

                switch (type)
                {
                    case RelicPersonalityType.SarcasticCritic:
                        // Mid-range dramatic shouting wail with chaotic flutter
                        float freqBob = 360f + 60f * Mathf.Sin(2f * Mathf.PI * 5f * t);
                        float flutter = Mathf.Sin(2f * Mathf.PI * 30f * t) * 0.2f;
                        sample = Mathf.Sin(2f * Mathf.PI * freqBob * t) + flutter;
                        sample += 0.3f * Mathf.Sin(2f * Mathf.PI * freqBob * 1.5f * t);
                        break;

                    case RelicPersonalityType.AnxiousCoward:
                        // Pure panicked high-pitched siren screech
                        float freqNelly = 880f + 180f * Mathf.Sin(2f * Mathf.PI * 8f * t);
                        sample = Mathf.Sin(2f * Mathf.PI * freqNelly * t) * 0.7f;
                        sample += 0.3f * Mathf.Sin(2f * Mathf.PI * (freqNelly * 1.33f) * t);
                        // Add hyperventilating pulse
                        sample *= (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 12f * t));
                        break;

                    case RelicPersonalityType.DumbGloop:
                        // Goofy cartoon siren / kazoo oscillator ("WHEEEE-NYOOOOM-WHEEE")
                        float freqGloop = 440f + 260f * Mathf.Sin(2f * Mathf.PI * 3.5f * t);
                        // Buzz / kazoo timbre
                        sample = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freqGloop * t)) * 0.65f;
                        sample += 0.35f * Mathf.Sin(2f * Mathf.PI * (freqGloop * 2f) * t);
                        break;

                    case RelicPersonalityType.GrumpyElder:
                        // Deep booming majestic war scream / indignant roar
                        float freqElder = 140f + 30f * Mathf.Sin(2f * Mathf.PI * 2.5f * t);
                        float saw = (2f * (t * freqElder - Mathf.Floor(t * freqElder + 0.5f)));
                        sample = saw * 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * freqElder * 2f * t);
                        break;
                }

                samples[i] = sample * loopFade * 0.9f;
            }

            var clip = AudioClip.Create($"Scream_{type}", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateImpactClip(RelicPersonalityType type)
        {
            float duration = 0.25f;
            int totalSamples = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / totalSamples;

                // Thud base (heavy low-frequency thump)
                float thudFreq = Mathf.Lerp(110f, 35f, progress);
                float thud = Mathf.Sin(2f * Mathf.PI * thudFreq * t) * Mathf.Exp(-progress * 18f);

                // Personality accent
                float accent = 0f;
                switch (type)
                {
                    case RelicPersonalityType.SarcasticCritic:
                        // Metallic clink
                        accent = Mathf.Sin(2f * Mathf.PI * 620f * t) * Mathf.Exp(-progress * 28f) * 0.4f;
                        break;

                    case RelicPersonalityType.AnxiousCoward:
                        // High squeaky pop
                        accent = Mathf.Sin(2f * Mathf.PI * 850f * t) * Mathf.Exp(-progress * 35f) * 0.5f;
                        break;

                    case RelicPersonalityType.DumbGloop:
                        // Bouncy cartoon boing
                        float boingFreq = Mathf.Lerp(300f, 650f, progress);
                        accent = Mathf.Sin(2f * Mathf.PI * boingFreq * t) * Mathf.Exp(-progress * 14f) * 0.6f;
                        break;

                    case RelicPersonalityType.GrumpyElder:
                        // Resonant heavy ancient stone gong
                        accent = Mathf.Sin(2f * Mathf.PI * 210f * t) * Mathf.Exp(-progress * 10f) * 0.5f;
                        break;
                }

                samples[i] = (thud * 0.7f + accent * 0.5f);
            }

            var clip = AudioClip.Create($"Impact_{type}", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateChoirClip(RelicPersonalityType type)
        {
            float duration = 2.0f;
            int totalSamples = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / totalSamples;

                // Seamless loop envelope window at edges to prevent clipping clicks
                float edgeFade = Mathf.Clamp01(Mathf.Sin(progress * Mathf.PI) * 40f);

                float sample = 0f;
                switch (type)
                {
                    case RelicPersonalityType.SarcasticCritic:
                        // C4 (261.63 Hz) & E4 (329.63 Hz) dry monotone synthesizer hum
                        float c4 = Mathf.Sin(2f * Mathf.PI * 261.63f * t);
                        float e4 = Mathf.Sin(2f * Mathf.PI * 329.63f * t);
                        sample = (c4 * 0.6f + e4 * 0.4f);
                        break;

                    case RelicPersonalityType.AnxiousCoward:
                        // High A4 (440.0 Hz) soprano alien with nervous 7Hz vibrato
                        float vib = Mathf.Sin(2f * Mathf.PI * 7.5f * t) * 12f;
                        sample = Mathf.Sin(2f * Mathf.PI * (440f + vib) * t) * 0.7f;
                        sample += Mathf.Sin(2f * Mathf.PI * (880f + vib * 1.5f) * t) * 0.25f;
                        break;

                    case RelicPersonalityType.DumbGloop:
                        // E3 (164.81 Hz) goofy bouncy choir chanting with subtle pulsing volume tremolo
                        float tremolo = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 3f * t);
                        float gloopWave = Mathf.Sin(2f * Mathf.PI * 164.81f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 329.62f * t);
                        sample = gloopWave * tremolo * 0.75f;
                        break;

                    case RelicPersonalityType.GrumpyElder:
                        // A2 (110.0 Hz) deep cosmic Gregorian drone with odd throat harmonics (330Hz, 550Hz)
                        float a2 = Mathf.Sin(2f * Mathf.PI * 110f * t);
                        float h3 = Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.45f;
                        float h5 = Mathf.Sin(2f * Mathf.PI * 550f * t) * 0.25f;
                        sample = (a2 * 0.6f + h3 + h5) * 0.8f;
                        break;
                }

                samples[i] = sample * edgeFade * 0.65f;
            }

            var clip = AudioClip.Create($"Choir_{type}", totalSamples, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
