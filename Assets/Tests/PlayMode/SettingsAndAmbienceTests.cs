using System.Collections;
using EchoProtocol.Settings;
using EchoProtocol.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>The ambience's spare listener for the menus, and the settings screen's microphone test.</summary>
    public class SettingsAndAmbienceTests
    {
        private GameObject _camera;
        private readonly System.Collections.Generic.List<AudioListener> _silenced = new System.Collections.Generic.List<AudioListener>();

        [TearDown]
        public void TearDown()
        {
            if (_camera != null) Object.Destroy(_camera);
            foreach (AudioListener listener in _silenced) if (listener != null) listener.enabled = true;
            _silenced.Clear();
            SettingsManager.UseStore(new PlayerPrefsSettingsStore());
            AudioListener.volume = 1f;
        }

        [UnityTest]
        public IEnumerator TheAmbience_ListensWhenNobodyElseDoes_AndStepsAsideForACamera()
        {
            yield return null;
            foreach (AudioListener existing in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
            {
                if (existing.gameObject == CaveAmbience.Instance.gameObject) continue;
                existing.enabled = false;
                _silenced.Add(existing);
            }
            yield return null;
            Assert.IsTrue(CaveAmbience.Instance.UsingFallbackListener, "with no camera (the menus) the ambience listens");

            _camera = new GameObject("PlayerCamera", typeof(AudioListener));
            yield return null;
            Assert.IsFalse(CaveAmbience.Instance.UsingFallbackListener, "a player's camera takes over at once");
            Assert.AreEqual(_camera.transform.position, CaveAmbience.Instance.transform.position, "and the ambience follows it");

            Object.Destroy(_camera);
            yield return null;
            yield return null;
            Assert.IsTrue(CaveAmbience.Instance.UsingFallbackListener, "back to the menus: the ambience listens again");
        }

        [UnityTest]
        public IEnumerator TheSettingsScreen_BringsItsOwnMicrophoneTest_InTheMenus_AndSavesWhenClosed()
        {
            var store = new MemorySettingsStore();
            SettingsManager.UseStore(store);
            var panel = new SettingsPanel();

            // The menus: no local player exists (a test before this one may have left avatars, so they go first).
            foreach (PlayerController player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include))
            {
                Object.Destroy(player.gameObject);
            }
            yield return null;

            panel.Open();
            yield return null;
            Assert.IsTrue(panel.UsesOwnTestMicrophone, "no player in the menus, so the panel opens a test microphone");
            Assert.IsNotNull(panel.Meter);
            Assert.IsFalse(panel.Meter.ShowDebugBar, "the test microphone never draws the debug bar");
            GameObject testMic = panel.Meter.gameObject;

            panel.Close();
            yield return null;
            Assert.IsTrue(testMic == null, "closing the screen releases the microphone");
            Assert.IsFalse(panel.UsesOwnTestMicrophone);
            Assert.AreEqual(1, store.Flushes, "closing the screen saves the settings");
        }
    }
}
