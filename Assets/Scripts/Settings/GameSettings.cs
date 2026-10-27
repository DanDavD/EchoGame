using System;
using UnityEngine;

namespace EchoProtocol.Settings
{
    /// <summary>
    /// The player's options: audio volumes, mouse, microphone and display. Plain data with its limits and defaults,
    /// so it can be tested without Unity; <see cref="SettingsManager"/> loads, saves and applies it.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const float MinMouseSensitivity = 0.25f;
        public const float MaxMouseSensitivity = 3f;
        public const float MinEchoSensitivity = 0.5f;
        public const float MaxEchoSensitivity = 3f;

        /// <summary>Volume of everything, 0..1.</summary>
        public float MasterVolume = 0.8f;
        /// <summary>Volume of the cave ambience and the music, 0..1 (on top of the master volume).</summary>
        public float AmbienceVolume = 1f;
        /// <summary>Multiplier on the mouse look speed (1 = the designed feel).</summary>
        public float MouseSensitivity = 1f;
        /// <summary>Moving the mouse up looks down.</summary>
        public bool InvertMouseY;
        /// <summary>How easily the voice lights the cave (the echo's voice sensitivity; 1.25 lets a normal voice light about 7 m).</summary>
        public float EchoSensitivity = 1.25f;
        /// <summary>Name of the microphone to capture; empty means the system default.</summary>
        public string MicrophoneDevice = string.Empty;
        /// <summary>Full screen (true) or a window (false).</summary>
        public bool Fullscreen = true;

        public static GameSettings Defaults() => new GameSettings();

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>A copy with every value inside its range (values from a store or a slider can be anything).</summary>
        public GameSettings Clamped()
        {
            GameSettings copy = Clone();
            copy.MasterVolume = Clamp01OrDefault(MasterVolume, 0.8f);
            copy.AmbienceVolume = Clamp01OrDefault(AmbienceVolume, 1f);
            copy.MouseSensitivity = ClampOrDefault(MouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity, 1f);
            copy.EchoSensitivity = ClampOrDefault(EchoSensitivity, MinEchoSensitivity, MaxEchoSensitivity, 1.25f);
            copy.MicrophoneDevice = MicrophoneDevice ?? string.Empty;
            return copy;
        }

        public bool SameAs(GameSettings other)
        {
            return other != null
                && Mathf.Approximately(MasterVolume, other.MasterVolume)
                && Mathf.Approximately(AmbienceVolume, other.AmbienceVolume)
                && Mathf.Approximately(MouseSensitivity, other.MouseSensitivity)
                && InvertMouseY == other.InvertMouseY
                && Mathf.Approximately(EchoSensitivity, other.EchoSensitivity)
                && (MicrophoneDevice ?? string.Empty) == (other.MicrophoneDevice ?? string.Empty)
                && Fullscreen == other.Fullscreen;
        }

        // ------------------------------------------------------------------ storage

        private const string Prefix = "echo.settings.";

        /// <summary>
        /// Version of the saved settings. Version 4 set the default echo sensitivity to 1.25 (versions 2 and 3 tried 1.6 and 1.4, both too sensitive): settings saved
        /// before it get the new default once, instead of keeping a value the player never chose.
        /// </summary>
        public const int CurrentVersion = 4;

        public static GameSettings Load(ISettingsStore store)
        {
            GameSettings d = Defaults();
            if (store == null) return d;

            var loaded = new GameSettings
            {
                MasterVolume = store.GetFloat(Prefix + "masterVolume", d.MasterVolume),
                AmbienceVolume = store.GetFloat(Prefix + "ambienceVolume", d.AmbienceVolume),
                MouseSensitivity = store.GetFloat(Prefix + "mouseSensitivity", d.MouseSensitivity),
                InvertMouseY = store.GetInt(Prefix + "invertMouseY", d.InvertMouseY ? 1 : 0) != 0,
                EchoSensitivity = store.GetFloat(Prefix + "echoSensitivity", d.EchoSensitivity),
                MicrophoneDevice = store.GetString(Prefix + "microphone", d.MicrophoneDevice),
                Fullscreen = store.GetInt(Prefix + "fullscreen", d.Fullscreen ? 1 : 0) != 0,
            };

            if (store.GetInt(Prefix + "version", 1) < CurrentVersion) loaded.EchoSensitivity = d.EchoSensitivity;
            return loaded.Clamped();
        }

        public void Save(ISettingsStore store)
        {
            if (store == null) return;
            GameSettings s = Clamped();
            store.SetFloat(Prefix + "masterVolume", s.MasterVolume);
            store.SetFloat(Prefix + "ambienceVolume", s.AmbienceVolume);
            store.SetFloat(Prefix + "mouseSensitivity", s.MouseSensitivity);
            store.SetInt(Prefix + "invertMouseY", s.InvertMouseY ? 1 : 0);
            store.SetFloat(Prefix + "echoSensitivity", s.EchoSensitivity);
            store.SetString(Prefix + "microphone", s.MicrophoneDevice);
            store.SetInt(Prefix + "fullscreen", s.Fullscreen ? 1 : 0);
            store.SetInt(Prefix + "version", CurrentVersion);
            store.Flush();
        }

        /// <summary>
        /// The microphone to capture: a device chosen in the Inspector wins, then the one saved in the settings if it is
        /// still connected, otherwise the system default (null).
        /// </summary>
        public static string ResolveMicrophone(string inspectorDevice, string savedDevice, string[] connected)
        {
            if (!string.IsNullOrEmpty(inspectorDevice)) return inspectorDevice;
            if (string.IsNullOrEmpty(savedDevice) || connected == null) return null;
            return Array.IndexOf(connected, savedDevice) >= 0 ? savedDevice : null;
        }

        private static float Clamp01OrDefault(float value, float fallback) => ClampOrDefault(value, 0f, 1f, fallback);

        private static float ClampOrDefault(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Mathf.Clamp(value, min, max);
        }
    }

    /// <summary>Where the settings are kept. <see cref="PlayerPrefsSettingsStore"/> in the game, a memory store in tests.</summary>
    public interface ISettingsStore
    {
        float GetFloat(string key, float fallback);
        int GetInt(string key, int fallback);
        string GetString(string key, string fallback);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void SetString(string key, string value);
        void Flush();
    }

    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Flush() => PlayerPrefs.Save();
    }

    public sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly System.Collections.Generic.Dictionary<string, object> _values =
            new System.Collections.Generic.Dictionary<string, object>();

        public int Flushes { get; private set; }

        public float GetFloat(string key, float fallback) => _values.TryGetValue(key, out object v) && v is float f ? f : fallback;
        public int GetInt(string key, int fallback) => _values.TryGetValue(key, out object v) && v is int i ? i : fallback;
        public string GetString(string key, string fallback) => _values.TryGetValue(key, out object v) && v is string s ? s : fallback;
        public void SetFloat(string key, float value) => _values[key] = value;
        public void SetInt(string key, int value) => _values[key] = value;
        public void SetString(string key, string value) => _values[key] = value;
        public void Flush() => Flushes++;
    }
}
