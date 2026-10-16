using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EchoProtocol.Visuals
{
    /// <summary>
    /// The game's post-processing look, built in code so it is versioned, reviewable and identical in every scene and
    /// build: a glowing bloom on the echo edges, a heavy vignette, film grain and a touch of chromatic aberration.
    /// A global <see cref="Volume"/> is created when a scene loads (unless one already exists) and every gameplay camera is
    /// told to render post-processing through <see cref="CameraPolish"/>.
    /// </summary>
    public static class PostProcessingRig
    {
        private const string VolumeName = "[EchoPostProcessing]";

        private static GameObject _volumeObject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _volumeObject = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        /// <summary>Creates the global look volume if it does not exist yet, and polishes every camera.</summary>
        public static void Ensure()
        {
            if (_volumeObject == null)
            {
                _volumeObject = new GameObject(VolumeName);
                Object.DontDestroyOnLoad(_volumeObject);

                var volume = _volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100f;
                volume.weight = 1f;
                volume.sharedProfile = CreateProfile();
            }

            foreach (Camera camera in Camera.allCameras) CameraPolish.Apply(camera);
        }

        /// <summary>Builds the look: every component is active with its values overridden.</summary>
        public static VolumeProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "EchoLook";

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.3f);
            grain.response.Override(0.6f);

            var aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0.08f);

            var colors = profile.Add<ColorAdjustments>(true);
            colors.contrast.Override(10f);
            colors.saturation.Override(6f);

            return profile;
        }
    }
}
