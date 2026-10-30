using UnityEngine;

namespace EchoProtocol.Audio
{
    /// <summary>
    /// Generates an oppressive, subterranean ambient audio drone for the catacombs:
    /// - 42 Hz deep earth resonance
    /// - Low cavern air drafts and subtle frequency phasing
    /// Self-contained and procedural: builds at runtime on scene load without external audio clips.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbienceAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.42f;

        private AudioSource _ambienceSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachToCamera()
        {
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener != null && listener.GetComponent<AmbienceAudio>() == null)
            {
                listener.gameObject.AddComponent<AmbienceAudio>();
            }
        }

        private void Awake()
        {
            _ambienceSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource.spatialBlend = 0f; // 2D ambient background
            _ambienceSource.loop = true;
            _ambienceSource.volume = _masterVolume;
            _ambienceSource.playOnAwake = false;
            _ambienceSource.clip = GenerateCatacombAmbience();
            _ambienceSource.Play();
        }

        private static AudioClip GenerateCatacombAmbience()
        {
            float duration = 8.0f; // 8s seamless cycle
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            var rng = new System.Random(1337);
            float airDraft = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;

                // Seamless looping oscillations
                float sub1 = Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.45f;
                float sub2 = Mathf.Sin(2f * Mathf.PI * 56f * t) * 0.25f;
                float phaseSwell = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * (1f / duration) * t);

                float white = (float)rng.NextDouble() * 2f - 1f;
                airDraft = airDraft * 0.992f + white * 0.008f; // very low wind / rumble

                samples[i] = (sub1 * phaseSwell + sub2 + airDraft * 3.2f) * 0.35f;
            }

            var clip = AudioClip.Create("CatacombAmbience", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
