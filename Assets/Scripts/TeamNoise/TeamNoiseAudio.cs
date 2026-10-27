using UnityEngine;

namespace EchoProtocol.TeamNoise
{
    /// <summary>
    /// The music of the team noise meter, made at runtime with no audio assets: a low drone that swells as the team gets
    /// louder, and a pounding, dissonant alarm loop while the beast is charging. Plays on every peer from the
    /// <see cref="TeamNoiseDirector"/>'s replicated state.
    /// </summary>
    [RequireComponent(typeof(TeamNoiseDirector))]
    public class TeamNoiseAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField, Range(0f, 1f)] private float _tensionVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _alarmVolume = 0.75f;
        [Tooltip("Seconds to fade the alarm music in and out.")]
        [SerializeField] private float _fadeSeconds = 0.6f;

        private TeamNoiseDirector _director;
        private AudioSource _tension;
        private AudioSource _alarm;

        private void Awake()
        {
            _director = GetComponent<TeamNoiseDirector>();
            _tension = CreateLoop("TensionDrone", GenerateTensionClip());
            _alarm = CreateLoop("AlarmMusic", GenerateAlarmClip());
        }

        private AudioSource CreateLoop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // music: same volume everywhere
            source.volume = 0f;
            source.Play();
            return source;
        }

        private void Update()
        {
            if (_director == null) return;

            bool alarmed = _director.IsAlarmed;
            float step = Time.deltaTime / Mathf.Max(0.01f, _fadeSeconds);
            // The "Ambience and music" setting scales the music too (the master volume scales everything).
            float music = EchoProtocol.Settings.SettingsManager.Current.AmbienceVolume;

            float alarmTarget = alarmed ? _alarmVolume * music : 0f;
            _alarm.volume = Mathf.MoveTowards(_alarm.volume, alarmTarget, step * _alarmVolume);

            // The drone grows with the square of the meter: barely there at half, unsettling near the top.
            float fill = _director.Fill01;
            float tensionTarget = alarmed ? 0f : _tensionVolume * fill * fill * music;
            _tension.volume = Mathf.MoveTowards(_tension.volume, tensionTarget, step * _tensionVolume);
        }

        private static AudioClip GenerateTensionClip()
        {
            float duration = 6f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            var random = new System.Random(7);
            float noise = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                // Whole cycles of every frequency in 6 s, so the loop has no seam.
                float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / duration);
                float drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.3f
                            + Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.25f;
                noise = noise * 0.995f + ((float)random.NextDouble() * 2f - 1f) * 0.005f; // low rumble
                samples[i] = (drone * swell + noise * 3f) * 0.45f;
            }

            var clip = AudioClip.Create("TeamNoiseTension", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateAlarmClip()
        {
            // 4 seconds at 120 BPM: a kick on every beat, a clashing stab every two beats, a falling siren on top.
            float duration = 4f;
            float beat = 0.5f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float inBeat = t % beat;
                float inBar = t % (beat * 2f);

                float kickFreq = Mathf.Lerp(140f, 42f, Mathf.Clamp01(inBeat / 0.12f));
                float kick = Mathf.Sin(2f * Mathf.PI * kickFreq * inBeat) * Mathf.Exp(-inBeat * 14f);

                float saw1 = 2f * (t * 110f - Mathf.Floor(t * 110f + 0.5f));
                float saw2 = 2f * (t * 116.5f - Mathf.Floor(t * 116.5f + 0.5f));
                float stab = (saw1 + saw2) * 0.5f * Mathf.Exp(-inBar * 5f);

                float drone = (Mathf.Sin(2f * Mathf.PI * 55f * t) + Mathf.Sin(2f * Mathf.PI * 58.27f * t)) * 0.25f;

                float sirenPhase = (t % 2f) / 2f;
                float siren = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(880f, 620f, sirenPhase) * t) * 0.12f;

                float edge = Mathf.Clamp01(Mathf.Min(t, duration - t) * 200f);
                samples[i] = (kick * 0.9f + stab * 0.45f + drone + siren) * 0.5f * edge;
            }

            var clip = AudioClip.Create("TeamNoiseAlarm", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
