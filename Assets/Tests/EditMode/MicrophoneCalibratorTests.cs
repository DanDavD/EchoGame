using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// The automatic microphone calibration, tested against simulated microphones with very different noise floors and
    /// gains: whatever the hardware, silence reveals nothing, normal speech lights about the same area and a scream lights
    /// the whole cavern.
    /// </summary>
    public class MicrophoneCalibratorTests
    {
        private const float Dt = 1f / 60f;
        private const float SpeechLevel = 0.35f;
        private const float Headroom = 26f;
        private const float Sensitivity = 1.25f; // the shipped default
        private const float Gate = 0.08f;
        private const float MaxRadius = 60f;

        private static readonly AnimationCurve Curve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.25f, 0.04f), new Keyframe(0.65f, 0.2f), new Keyframe(1f, 1f));

        // (noise floor dB, normal speech dB): a headset, a very quiet mic, a laptop in a noisy room, a loud laptop, a hot mic.
        private static readonly object[] Microphones =
        {
            new object[] { -60f, -40f },
            new object[] { -72f, -54f },
            new object[] { -45f, -27f },
            new object[] { -50f, -31f },
            new object[] { -38f, -18f }
        };

        private static float Noise(System.Random rng, float floor) => floor + (float)(rng.NextDouble() * 5.0 - 2.5);
        private static float Voice(System.Random rng, float speech) => speech + (float)(rng.NextDouble() * 10.0 - 5.0);

        // The player starts quietly, then talks in bursts (0.6 s of speech, 0.4 s of pause) for the given time.
        private static MicrophoneCalibrator Learn(float floor, float speech, float talkSeconds, int seed = 7)
        {
            var calibrator = new MicrophoneCalibrator();
            var rng = new System.Random(seed);

            for (float t = 0f; t < 3f; t += Dt) calibrator.Update(Noise(rng, floor), Dt);
            for (float t = 0f; t < talkSeconds; t += Dt)
            {
                bool talking = (t % 1f) < 0.6f;
                calibrator.Update(talking ? Voice(rng, speech) : Noise(rng, floor), Dt);
            }
            return calibrator;
        }

        private static float EchoMetres(MicrophoneCalibrator calibrator, float db)
        {
            float level = calibrator.ToLevel01(db, SpeechLevel, Headroom);
            return EchoVisionController.TargetRadius(level, Sensitivity, Gate, MaxRadius, Curve);
        }

        [TestCaseSource(nameof(Microphones))]
        public void Silence_RevealsNothing_OnAnyMicrophone(float floor, float speech)
        {
            MicrophoneCalibrator calibrator = Learn(floor, speech, 40f);
            var rng = new System.Random(99);

            float widest = 0f;
            for (int i = 0; i < 600; i++)
            {
                float db = Noise(rng, floor);
                calibrator.Update(db, Dt);
                widest = Mathf.Max(widest, EchoMetres(calibrator, db));
            }

            Assert.AreEqual(0f, widest, $"the room noise of a {floor} dB microphone must not light anything");
        }

        [TestCaseSource(nameof(Microphones))]
        public void NormalSpeech_LightsAboutSevenMetres_OnAnyMicrophone(float floor, float speech)
        {
            MicrophoneCalibrator calibrator = Learn(floor, speech, 40f);

            Assert.That(EchoMetres(calibrator, speech), Is.InRange(5.5f, 8.5f),
                $"speech at {speech} dB on a {floor} dB microphone");
        }

        [TestCaseSource(nameof(Microphones))]
        public void ALoudShout_LightsTheWholeCavern_OnAnyMicrophone(float floor, float speech)
        {
            MicrophoneCalibrator calibrator = Learn(floor, speech, 40f);
            float shout = calibrator.ScreamDb(Headroom);

            Assert.AreEqual(MaxRadius, EchoMetres(calibrator, shout), 1e-3f, $"a shout at {shout} dB");
        }

        [TestCaseSource(nameof(Microphones))]
        public void TheEchoGrowsAsTheVoiceGetsLouder_OnAnyMicrophone(float floor, float speech)
        {
            MicrophoneCalibrator calibrator = Learn(floor, speech, 40f);

            float quiet = EchoMetres(calibrator, speech - 5f);
            float normal = EchoMetres(calibrator, speech);
            float loud = EchoMetres(calibrator, speech + 6f);

            Assert.Less(quiet, normal);
            Assert.Less(normal, loud);
        }

        [Test]
        public void TheFloor_FollowsTheRoomWhenItGetsNoisier()
        {
            var calibrator = new MicrophoneCalibrator();
            var rng = new System.Random(3);

            for (float t = 0f; t < 10f; t += Dt) calibrator.Update(Noise(rng, -60f), Dt);
            Assert.Less(calibrator.FloorDb, -58f, "learned the quiet room");

            for (float t = 0f; t < 60f; t += Dt) calibrator.Update(Noise(rng, -48f), Dt);
            Assert.That(calibrator.FloorDb, Is.InRange(-53f, -46f), "followed the noisier room within a minute");
        }

        [Test]
        public void Speech_IsNotMistakenForRoomNoise()
        {
            MicrophoneCalibrator calibrator = Learn(-60f, -40f, 20f);
            float floorBefore = calibrator.FloorDb;

            // Two more minutes of talking with the usual pauses.
            var rng = new System.Random(11);
            for (float t = 0f; t < 120f; t += Dt)
            {
                bool talking = (t % 1f) < 0.6f;
                calibrator.Update(talking ? Voice(rng, -40f) : Noise(rng, -60f), Dt);
            }

            Assert.LessOrEqual(calibrator.FloorDb, floorBefore + 3f, "talking must not raise the noise floor");
        }

        [Test]
        public void ALoudSpeaker_AndAQuietOne_BothGetNormalSpeechAtTheSameEcho()
        {
            // Same microphone and room, two people who talk 12 dB apart.
            MicrophoneCalibrator quietTalker = Learn(-60f, -46f, 60f);
            MicrophoneCalibrator loudTalker = Learn(-60f, -34f, 60f);

            Assert.That(EchoMetres(quietTalker, -46f), Is.InRange(5.5f, 8.5f), "the quiet talker");
            Assert.That(EchoMetres(loudTalker, -34f), Is.InRange(5.5f, 8.5f), "the loud talker");
        }

        [Test]
        public void AMutedMicrophone_DoesNotBreakAnything()
        {
            var calibrator = new MicrophoneCalibrator();
            for (float t = 0f; t < 10f; t += Dt) calibrator.Update(-80f, Dt);

            Assert.AreEqual(MicrophoneCalibrator.MinFloorDb, calibrator.FloorDb, 1e-3f);
            Assert.AreEqual(0f, calibrator.ToLevel01(-80f, SpeechLevel, Headroom));
            Assert.IsFalse(float.IsNaN(calibrator.SpeechDb) || float.IsNaN(calibrator.SilenceDb));
        }

        [Test]
        public void TheScreamPoint_NeverExceedsFullScale()
        {
            MicrophoneCalibrator hotMic = Learn(-38f, -8f, 40f);
            Assert.LessOrEqual(hotMic.ScreamDb(Headroom), MicrophoneCalibrator.MaxScreamDb);
        }

        [Test]
        public void BeforeAnySpeech_TheSpeechGuessFollowsTheFloor_EvenWhileItSettles()
        {
            var calibrator = new MicrophoneCalibrator();
            var rng = new System.Random(5);

            // A microphone that opens with a digital-silence reading and then settles at its real room noise.
            calibrator.Update(-80f, Dt);
            for (float t = 0f; t < 5f; t += Dt) calibrator.Update(Noise(rng, -63f), Dt);

            Assert.AreEqual(calibrator.FloorDb + MicrophoneCalibrator.InitialSpeechAboveFloorDb, calibrator.SpeechDb, 1e-3f,
                "nobody has spoken yet, so the guess is the floor plus the usual distance");
            Assert.That(calibrator.FloorDb, Is.InRange(-68f, -60f), "settled at the real room noise");
        }

        [Test]
        public void Seed_StartsFromAKnownRoomNoise()
        {
            var calibrator = new MicrophoneCalibrator();
            calibrator.Seed(-50f);

            Assert.IsTrue(calibrator.IsCalibrated);
            Assert.AreEqual(-50f, calibrator.FloorDb, 1e-3f);
            Assert.AreEqual(-42f, calibrator.SilenceDb, 1e-3f);
        }

        [Test]
        public void TheMapping_IsZeroAtSilence_TheSpeechLevelAtSpeech_AndOneAtTheScream()
        {
            const float silence = -55f, speech = -40f, scream = -14f;

            Assert.AreEqual(0f, MicrophoneCalibrator.Map(-70f, silence, speech, scream, 0.35f));
            Assert.AreEqual(0f, MicrophoneCalibrator.Map(silence, silence, speech, scream, 0.35f));
            Assert.AreEqual(0.35f, MicrophoneCalibrator.Map(speech, silence, speech, scream, 0.35f), 1e-4f);
            Assert.AreEqual(1f, MicrophoneCalibrator.Map(scream, silence, speech, scream, 0.35f), 1e-4f);
            Assert.AreEqual(1f, MicrophoneCalibrator.Map(0f, silence, speech, scream, 0.35f), "never above 1");
        }

        [Test]
        public void TheMapping_GrowsMonotonically_AndSurvivesCollidingPoints()
        {
            float previous = -1f;
            for (float db = -80f; db <= 0f; db += 0.5f)
            {
                float level = MicrophoneCalibrator.Map(db, -55f, -40f, -14f, 0.35f);
                Assert.GreaterOrEqual(level, previous, $"{db} dB");
                previous = level;
            }

            // Estimates that collide (speech under the silence point, scream under speech) must not produce NaN or division by zero.
            float collided = MicrophoneCalibrator.Map(-30f, -40f, -45f, -50f, 0.35f);
            Assert.IsFalse(float.IsNaN(collided) || float.IsInfinity(collided));
            Assert.That(collided, Is.InRange(0f, 1f));
        }

        [Test]
        public void ZeroOrNegativeTimeSteps_AreIgnored()
        {
            var calibrator = new MicrophoneCalibrator();
            calibrator.Seed(-60f);

            calibrator.Update(-20f, 0f);
            calibrator.Update(-20f, -1f);

            Assert.AreEqual(-60f, calibrator.FloorDb, 1e-3f);
        }
    }
}
