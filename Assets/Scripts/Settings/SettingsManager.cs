using System;
using UnityEngine;

namespace EchoProtocol.Settings
{
    /// <summary>
    /// The settings of this player, loaded once from <see cref="PlayerPrefs"/> and shared by the whole game. Changes apply
    /// at once: the master volume and full screen are set here, and every other system (mouse look, microphone, echo,
    /// ambience) reads <see cref="Current"/> or listens to <see cref="Changed"/>.
    /// </summary>
    public static class SettingsManager
    {
        private static ISettingsStore _store;
        private static GameSettings _current;

        /// <summary>Raised after the settings changed, with the new values.</summary>
        public static event Action<GameSettings> Changed;

        /// <summary>The settings in use (a copy is never needed to read them; use <see cref="Apply"/> to change them).</summary>
        public static GameSettings Current
        {
            get
            {
                if (_current == null) _current = GameSettings.Load(Store);
                return _current;
            }
        }

        private static ISettingsStore Store => _store ?? (_store = new PlayerPrefsSettingsStore());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _store = null;
            _current = null;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStart()
        {
            // Only the volume at start: the window mode comes from the launch (and the photo mode runs in a small window).
            AudioListener.volume = Current.MasterVolume;
        }

        /// <summary>Uses another store (tests). Reloads the settings from it.</summary>
        public static void UseStore(ISettingsStore store)
        {
            _store = store;
            _current = GameSettings.Load(Store);
        }

        /// <summary>Makes <paramref name="settings"/> the current settings, applies them and tells every listener.</summary>
        public static void Apply(GameSettings settings)
        {
            if (settings == null) return;
            GameSettings next = settings.Clamped();
            _current = next;

            AudioListener.volume = next.MasterVolume;
            // Compared with the real window, not the saved value: the game may have been launched in a window.
            if (!Application.isEditor && next.Fullscreen != Screen.fullScreen)
            {
                Screen.fullScreenMode = next.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }

            Changed?.Invoke(next);
        }

        /// <summary>Writes the current settings to disk (call when a settings screen closes).</summary>
        public static void Save() => Current.Save(Store);
    }
}
