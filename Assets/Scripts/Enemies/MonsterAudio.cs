using UnityEngine;

namespace EchoProtocol.Enemies
{
    /// <summary>
    /// Procedural 3D audio for the blind monster:
    /// - Continuous raspy breathing when idling or investigating.
    /// - Terrifying guttural screech/roar when entering Hunt or Enraged mode.
    /// - Heavy rhythmic footsteps when moving through the dungeon.
    /// Fully procedural: synthesized at runtime with zero external audio assets.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _breathSource;
        private AudioSource _roarSource;
        private AudioSource _stepSource;

        private EnemyBrain _brain;
        private Vector3 _lastPos;
        private float _stepTimer;
        private EnemyState _lastState = EnemyState.Idle;

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _lastPos = transform.position;

            _breathSource = CreateAudioSource("Monster_Breathing", true, 0.45f, 2f, 22f);
            _breathSource.clip = GenerateBreathingClip();
            _breathSource.Play();

            _roarSource = CreateAudioSource("Monster_Roar", false, 0.9f, 4f, 45f);
            _roarSource.clip = GenerateRoarClip();

            _stepSource = CreateAudioSource("Monster_Footsteps", false, 0.55f, 2f, 20f);
            _stepSource.clip = GenerateFootstepClip();
        }

        private AudioSource CreateAudioSource(string name, bool loop, float volume, float minDistance, float maxDistance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            var source = go.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.volume = volume;
            source.loop = loop;
            source.playOnAwake = false;
            return source;
        }

        private void Update()
        {
            float speed = (transform.position - _lastPos).magnitude / Mathf.Max(0.001f, Time.deltaTime);
            _lastPos = transform.position;

            EnemyState state = _brain != null ? _brain.State : EnemyState.Idle;

            // Trigger roar on transition to Hunt
            if (state == EnemyState.Hunt && _lastState != EnemyState.Hunt)
            {
                TriggerRoar();
            }
            _lastState = state;

            // Adjust breathing intensity
            if (_breathSource != null)
            {
                float targetVol = state == EnemyState.Hunt ? 0.7f : 0.45f;
                float targetPitch = state == EnemyState.Hunt ? 1.35f : 1.0f;
                _breathSource.volume = Mathf.MoveTowards(_breathSource.volume, targetVol, Time.deltaTime * 0.5f);
                _breathSource.pitch = Mathf.MoveTowards(_breathSource.pitch, targetPitch, Time.deltaTime * 0.4f);
            }

            // Footsteps when moving
            if (speed > 0.4f)
            {
                float stepInterval = state == EnemyState.Hunt ? 0.32f : 0.65f;
                _stepTimer += Time.deltaTime;
                if (_stepTimer >= stepInterval)
                {
                    _stepTimer = 0f;
                    PlayFootstep(state == EnemyState.Hunt ? 1.15f : 0.9f);
                }
            }
            else
            {
                _stepTimer = 0f;
            }
        }

        /// <summary>Plays a terrifying screech roar.</summary>
        public void TriggerRoar()
        {
            if (_roarSource == null) return;
            _roarSource.pitch = Random.Range(0.92f, 1.08f);
            _roarSource.Play();
        }

        private void PlayFootstep(float pitch)
        {
            if (_stepSource == null) return;
            _stepSource.pitch = pitch * Random.Range(0.9f, 1.1f);
            _stepSource.PlayOneShot(_stepSource.clip);
        }

        private static AudioClip GenerateBreathingClip()
        {
            float duration = 4.0f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            var rng = new System.Random(42);
            float noiseFilter = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float cycle = (t % 2.0f) / 2.0f;
                float breathEnv = Mathf.Sin(Mathf.PI * cycle);
                breathEnv = Mathf.Pow(breathEnv, 2.2f);

                float white = (float)rng.NextDouble() * 2f - 1f;
                noiseFilter = noiseFilter * 0.94f + white * 0.06f; // low-pass rasp

                float throatTone = Mathf.Sin(2f * Mathf.PI * 72f * t) * 0.25f;
                samples[i] = (noiseFilter * 1.8f + throatTone) * breathEnv * 0.3f;
            }

            var clip = AudioClip.Create("MonsterBreathing", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateRoarClip()
        {
            float duration = 2.4f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            var rng = new System.Random(99);
            float noise = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration));
                env = Mathf.Pow(env, 0.7f);

                float pitchSlide = Mathf.Lerp(420f, 160f, t / duration);
                float f1 = Mathf.Sin(2f * Mathf.PI * pitchSlide * t);
                float f2 = Mathf.Sin(2f * Mathf.PI * (pitchSlide * 1.52f) * t) * 0.6f;
                float sub = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.45f;

                float white = (float)rng.NextDouble() * 2f - 1f;
                noise = noise * 0.90f + white * 0.10f;

                float screech = (f1 + f2 + sub + noise * 1.5f) * env * 0.45f;
                // Soft distortion clipping
                screech = Mathf.Clamp(screech * 1.8f, -0.95f, 0.95f);
                samples[i] = screech;
            }

            var clip = AudioClip.Create("MonsterRoar", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateFootstepClip()
        {
            float duration = 0.28f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 22f);
                float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(95f, 38f, t / duration) * t);
                samples[i] = thump * env * 0.65f;
            }

            var clip = AudioClip.Create("MonsterFootstep", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
