using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// The calibration contract of the shipped player, read from Player.prefab, so changing it without meaning to fails here.
    /// The player calibrates its microphone automatically; the fixed dB values below are the manual fallback (auto calibration
    /// off). Reference microphone for the fixed values: idles around -60 dB, normal speech sits between -45 and -35 dB.
    /// </summary>
    public class EchoCalibrationTests
    {
        private const string PlayerPath = "Assets/Prefabs/Player.prefab";

        private static float EchoMetres(float db)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Assert.IsNotNull(player, $"{PlayerPath} is missing");

            var mic = new SerializedObject(player.GetComponent<MicrophoneLoudness>());
            float silenceDb = mic.FindProperty("_silenceDb").floatValue;
            float screamDb = mic.FindProperty("_screamDb").floatValue;

            var echo = new SerializedObject(player.GetComponent<EchoVisionController>());
            float level = MicrophoneLoudness.DbToLevel01(db, silenceDb, screamDb);
            return EchoVisionController.TargetRadius(
                level,
                echo.FindProperty("_voiceSensitivity").floatValue,
                echo.FindProperty("_noiseGate").floatValue,
                echo.FindProperty("_maxRadius").floatValue,
                echo.FindProperty("_radiusCurve").animationCurveValue);
        }

        [Test]
        public void TheShippedPlayer_CalibratesItsMicrophoneAutomatically_AndSpeechLightsAboutSevenMetres()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var mic = new SerializedObject(player.GetComponent<MicrophoneLoudness>());
            Assert.IsTrue(mic.FindProperty("_autoCalibrate").boolValue, "auto calibration must be on in the shipped player");

            var echo = new SerializedObject(player.GetComponent<EchoVisionController>());
            float metres = EchoVisionController.TargetRadius(
                mic.FindProperty("_speechLevel").floatValue,
                echo.FindProperty("_voiceSensitivity").floatValue,
                echo.FindProperty("_noiseGate").floatValue,
                echo.FindProperty("_maxRadius").floatValue,
                echo.FindProperty("_radiusCurve").animationCurveValue);

            Assert.That(metres, Is.InRange(6f, 8.5f), "normal speech maps to the speech level, which lights about 7 m");
        }

        [Test]
        public void AnIdleMicrophone_RevealsNothing()
        {
            Assert.AreEqual(0f, EchoMetres(-60f), "-60 dB is the room, not a voice");
            Assert.AreEqual(0f, EchoMetres(-58f), "a couple of dB of mic noise must not light the dark");
        }

        [Test]
        public void NormalSpeech_ReachesAboutSevenMetres_AtTheMiddleOfItsRange()
        {
            Assert.That(EchoMetres(-40f), Is.InRange(6f, 8.5f), "speaking at -40 dB should light about 7 m");
        }

        [Test]
        public void NormalSpeech_GrowsAcrossItsWholeRange()
        {
            float quiet = EchoMetres(-45f);
            float middle = EchoMetres(-40f);
            float loud = EchoMetres(-35f);

            Assert.That(quiet, Is.InRange(2f, 4.5f), "-45 dB");
            Assert.That(loud, Is.InRange(9f, 13f), "-35 dB");
            Assert.Less(quiet, middle);
            Assert.Less(middle, loud);
        }

        [Test]
        public void ALoudShout_StillLightsTheWholeCavern()
        {
            Assert.AreEqual(60f, EchoMetres(-12f), 1e-3f);
        }

        [Test]
        public void TheEchoNeverShrinksAsTheVoiceGetsLouder()
        {
            float previous = -1f;
            for (float db = -70f; db <= 0f; db += 1f)
            {
                float metres = EchoMetres(db);
                Assert.GreaterOrEqual(metres, previous, $"{db} dB");
                previous = metres;
            }
        }
    }
}
