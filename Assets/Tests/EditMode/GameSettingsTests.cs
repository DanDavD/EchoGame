using EchoProtocol.Settings;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>The player's settings: defaults, limits, saving and loading, and which microphone is used.</summary>
    public class GameSettingsTests
    {
        [TearDown]
        public void TearDown()
        {
            // Back to the real store, so no test leaves its memory store behind.
            SettingsManager.UseStore(new PlayerPrefsSettingsStore());
        }

        [Test]
        public void Defaults_AreTheDesignedFeel()
        {
            GameSettings d = GameSettings.Defaults();
            Assert.AreEqual(0.8f, d.MasterVolume);
            Assert.AreEqual(1f, d.AmbienceVolume);
            Assert.AreEqual(1f, d.MouseSensitivity);
            Assert.IsFalse(d.InvertMouseY);
            Assert.AreEqual(1.25f, d.EchoSensitivity, "the echo sensitivity the game was tuned with");
            Assert.AreEqual(string.Empty, d.MicrophoneDevice, "the system default microphone");
        }

        [Test]
        public void Clamped_KeepsEveryValueInItsRange_AndRepairsBrokenOnes()
        {
            var wild = new GameSettings
            {
                MasterVolume = 3f,
                AmbienceVolume = -1f,
                MouseSensitivity = 50f,
                EchoSensitivity = float.NaN,
                MicrophoneDevice = null,
            };
            GameSettings c = wild.Clamped();

            Assert.AreEqual(1f, c.MasterVolume);
            Assert.AreEqual(0f, c.AmbienceVolume);
            Assert.AreEqual(GameSettings.MaxMouseSensitivity, c.MouseSensitivity);
            Assert.AreEqual(1.25f, c.EchoSensitivity, "a broken value falls back to the default");
            Assert.AreEqual(string.Empty, c.MicrophoneDevice);
            Assert.AreEqual(3f, wild.MasterVolume, "clamping returns a copy and leaves the original alone");
        }

        [Test]
        public void SaveThenLoad_GivesBackTheSameSettings()
        {
            var store = new MemorySettingsStore();
            var saved = new GameSettings
            {
                MasterVolume = 0.35f,
                AmbienceVolume = 0.6f,
                MouseSensitivity = 1.75f,
                InvertMouseY = true,
                EchoSensitivity = 2.2f,
                MicrophoneDevice = "Headset Microphone",
                Fullscreen = false,
            };

            saved.Save(store);
            GameSettings loaded = GameSettings.Load(store);

            Assert.IsTrue(loaded.SameAs(saved));
            Assert.AreEqual(1, store.Flushes, "saving writes to disk once");
        }

        [Test]
        public void OlderSettings_GetTheNewEchoSensitivity_OnlyOnce()
        {
            // Saved by an older build: no version key, the old 1.3 default and a volume the player chose.
            var store = new MemorySettingsStore();
            store.SetFloat("echo.settings.echoSensitivity", 1.3f);
            store.SetFloat("echo.settings.masterVolume", 0.4f);

            GameSettings migrated = GameSettings.Load(store);
            Assert.AreEqual(1.25f, migrated.EchoSensitivity, 1e-5f, "an old save gets the new, easier echo");
            Assert.AreEqual(0.4f, migrated.MasterVolume, 1e-5f, "everything else the player chose is kept");

            // From now on the slider wins, even if the player goes back to 1.3.
            migrated.EchoSensitivity = 1.3f;
            migrated.Save(store);
            Assert.AreEqual(1.3f, GameSettings.Load(store).EchoSensitivity, 1e-5f);
        }

        [Test]
        public void LoadingAnEmptyStore_GivesTheDefaults()
        {
            Assert.IsTrue(GameSettings.Load(new MemorySettingsStore()).SameAs(GameSettings.Defaults()));
            Assert.IsTrue(GameSettings.Load(null).SameAs(GameSettings.Defaults()));
        }

        [Test]
        public void TheMicrophone_IsTheInspectorOne_ThenTheSavedOne_ThenTheDefault()
        {
            string[] connected = { "Laptop Mic", "Headset Microphone" };

            Assert.AreEqual("Studio Mic", GameSettings.ResolveMicrophone("Studio Mic", "Headset Microphone", connected),
                "a device set on the component always wins");
            Assert.AreEqual("Headset Microphone", GameSettings.ResolveMicrophone("", "Headset Microphone", connected));
            Assert.IsNull(GameSettings.ResolveMicrophone("", "USB Mic (unplugged)", connected),
                "a saved microphone that is no longer connected falls back to the default");
            Assert.IsNull(GameSettings.ResolveMicrophone(null, "", connected), "nothing saved means the default");
            Assert.IsNull(GameSettings.ResolveMicrophone(null, "Headset Microphone", null));
        }

        [Test]
        public void Apply_ClampsAppliesTheVolume_AndTellsTheListeners()
        {
            SettingsManager.UseStore(new MemorySettingsStore());
            GameSettings received = null;
            void Listen(GameSettings s) => received = s;
            SettingsManager.Changed += Listen;
            try
            {
                SettingsManager.Apply(new GameSettings { MasterVolume = 0.4f, MouseSensitivity = 99f });

                Assert.IsNotNull(received, "every listener hears about the change");
                Assert.AreEqual(GameSettings.MaxMouseSensitivity, received.MouseSensitivity, "listeners get clamped values");
                Assert.AreEqual(0.4f, AudioListener.volume, 1e-5f, "the master volume is the listener volume");
                Assert.AreEqual(0.4f, SettingsManager.Current.MasterVolume, 1e-5f);
            }
            finally
            {
                SettingsManager.Changed -= Listen;
                AudioListener.volume = 1f;
            }
        }

        [Test]
        public void Save_WritesTheCurrentSettingsToTheStore()
        {
            var store = new MemorySettingsStore();
            SettingsManager.UseStore(store);
            SettingsManager.Apply(new GameSettings { EchoSensitivity = 2f });
            SettingsManager.Save();

            Assert.AreEqual(2f, GameSettings.Load(store).EchoSensitivity, 1e-5f);
            AudioListener.volume = 1f;
        }
    }
}
