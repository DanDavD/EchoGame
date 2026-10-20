using EchoProtocol.Relics;
using EchoProtocol.Rituals;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    /// <summary>The human-echo ritual (syllable detection and rhythm matching) and the relic's mood, without a scene.</summary>
    public class RitualRulesTests
    {
        // ---------------------------------------------------------------- syllable detection

        /// <summary>Feeds the detector a voice that says one syllable at each of <paramref name="starts"/>, sampled every 16 ms.</summary>
        private static VoiceOnsetDetector Speak(float peak, float floor, params float[] starts)
        {
            var detector = new VoiceOnsetDetector();
            for (float t = 0f; t < 3f; t += 0.016f)
            {
                float level = floor;
                foreach (float s in starts)
                {
                    if (t >= s && t < s + 0.12f) level = peak;
                }
                detector.Sample(t, level);
            }
            return detector;
        }

        [Test]
        public void Detector_FindsEverySyllable()
        {
            var detector = Speak(0.6f, 0f, 0.2f, 0.55f, 1.2f);

            Assert.AreEqual(3, detector.Onsets.Count);
            Assert.That(detector.Onsets[0], Is.EqualTo(0.2f).Within(0.02f));
            Assert.That(detector.Onsets[2], Is.EqualTo(1.2f).Within(0.02f));
        }

        [Test]
        public void Detector_SplitsLoudSyllables_ThatNeverReachSilence()
        {
            // Shouting "BA-BA": the level dips to 0.3 between syllables but never drops to silence.
            var detector = Speak(0.9f, 0.3f, 0.2f, 0.6f);

            Assert.AreEqual(2, detector.Onsets.Count);
        }

        [Test]
        public void Detector_IgnoresWhispersAndSilence()
        {
            var detector = Speak(0.12f, 0f, 0.2f, 0.6f);

            Assert.AreEqual(0, detector.Onsets.Count);
        }

        [Test]
        public void Detector_Reset_ForgetsOnsets()
        {
            var detector = Speak(0.6f, 0f, 0.2f);

            detector.Reset();

            Assert.AreEqual(0, detector.Onsets.Count);
        }

        // ---------------------------------------------------------------- rhythm

        [Test]
        public void Pattern_StartsAtZero_AndHasTheRequestedBeats()
        {
            for (int beats = EchoRhythm.MinBeats; beats <= EchoRhythm.MaxBeats; beats++)
            {
                float[] pattern = EchoRhythm.CreatePattern(7, beats, 1f);
                Assert.AreEqual(beats, pattern.Length);
                Assert.AreEqual(0f, pattern[0]);
                for (int i = 1; i < pattern.Length; i++) Assert.Greater(pattern[i], pattern[i - 1]);
            }
        }

        [Test]
        public void Pattern_IsTheSameForTheSameSeed()
        {
            CollectionAssert.AreEqual(EchoRhythm.CreatePattern(42, 4, 1f), EchoRhythm.CreatePattern(42, 4, 1f));
        }

        [Test]
        public void Pattern_SlowerTempo_LastsLonger()
        {
            float normal = EchoRhythm.Duration(EchoRhythm.CreatePattern(3, 3, 1f));
            float slow = EchoRhythm.Duration(EchoRhythm.CreatePattern(3, 3, 1.3f));

            Assert.Greater(slow, normal);
        }

        [Test]
        public void Pattern_ClampsTheBeatCount()
        {
            Assert.AreEqual(EchoRhythm.MaxBeats, EchoRhythm.CreatePattern(1, 99, 1f).Length);
            Assert.AreEqual(EchoRhythm.MinBeats, EchoRhythm.CreatePattern(1, 0, 1f).Length);
        }

        [Test]
        public void Echo_InAnyClock_Matches()
        {
            float[] pattern = { 0f, 0.34f, 1.02f };
            float[] echo = { 10f, 10.36f, 11.0f };

            Assert.IsTrue(EchoRhythm.Matches(pattern, echo, EchoRhythm.DefaultTolerance, out _));
        }

        [Test]
        public void Echo_SlightlySlower_StillMatches()
        {
            float[] pattern = { 0f, 0.34f, 1.02f };
            float[] echo = { 0f, 0.42f, 1.25f };

            Assert.IsTrue(EchoRhythm.Matches(pattern, echo, EchoRhythm.DefaultTolerance, out _));
        }

        [Test]
        public void Echo_WithAMissingOrExtraBeat_Fails()
        {
            float[] pattern = { 0f, 0.34f, 1.02f };

            Assert.IsFalse(EchoRhythm.Matches(pattern, new[] { 0f, 1.02f }, EchoRhythm.DefaultTolerance, out _));
            Assert.IsFalse(EchoRhythm.Matches(pattern, new[] { 0f, 0.34f, 0.68f, 1.02f }, EchoRhythm.DefaultTolerance, out _));
        }

        [Test]
        public void Echo_WithTheWrongRhythm_Fails()
        {
            // Short-long asked, long-short answered.
            float[] pattern = { 0f, 0.34f, 1.02f };
            float[] echo = { 0f, 0.68f, 1.02f };

            Assert.IsFalse(EchoRhythm.Matches(pattern, echo, EchoRhythm.DefaultTolerance, out float error));
            Assert.Greater(error, EchoRhythm.DefaultTolerance);
        }

        [Test]
        public void Echo_WayTooSlow_Fails()
        {
            float[] pattern = { 0f, 0.34f, 0.68f };
            float[] echo = { 0f, 1f, 2f };

            Assert.IsFalse(EchoRhythm.Matches(pattern, echo, EchoRhythm.DefaultTolerance, out _));
        }

        [Test]
        public void Echo_Silence_Fails()
        {
            Assert.IsFalse(EchoRhythm.Matches(new[] { 0f, 0.34f }, new float[0], EchoRhythm.DefaultTolerance, out _));
        }

        [Test]
        public void Beats_GetEasierAfterEachFailure()
        {
            Assert.AreEqual(4, EchoRhythm.BeatsForAttempt(4, 0));
            Assert.AreEqual(3, EchoRhythm.BeatsForAttempt(4, 1));
            Assert.AreEqual(EchoRhythm.MinBeats, EchoRhythm.BeatsForAttempt(4, 10));
        }

        [Test]
        public void DetectedSpeech_MatchesThePatternItWasSpokenFrom()
        {
            float[] pattern = EchoRhythm.CreatePattern(11, 3, 1f);
            var detector = Speak(0.6f, 0f, 0.5f + pattern[0], 0.5f + pattern[1], 0.5f + pattern[2]);

            Assert.IsTrue(EchoRhythm.Matches(pattern, detector.Onsets, EchoRhythm.DefaultTolerance, out _));
        }

        // ---------------------------------------------------------------- relic mood

        [Test]
        public void Temper_AWhisperingCarrier_KeepsItCalm()
        {
            var temper = new RelicTemper(loudLevel: 0.4f, strainSeconds: 0.6f, patienceSeconds: 0f);

            for (int i = 0; i < 100; i++) Assert.AreEqual(TemperEvent.None, temper.Tick(0.1f, true, 0.25f, 1));
        }

        [Test]
        public void Temper_ALoudCarrier_MakesItCry()
        {
            var temper = new RelicTemper(loudLevel: 0.4f, strainSeconds: 0.5f, patienceSeconds: 0f);

            Assert.AreEqual(TemperEvent.None, temper.Tick(0.3f, true, 0.7f, 1));
            Assert.AreEqual(TemperEvent.Cry, temper.Tick(0.3f, true, 0.7f, 1));
        }

        [Test]
        public void Temper_AShortShout_IsForgiven()
        {
            var temper = new RelicTemper(loudLevel: 0.4f, strainSeconds: 0.6f, patienceSeconds: 0f);

            temper.Tick(0.4f, true, 0.9f, 1);
            temper.Tick(1f, true, 0.1f, 1);

            Assert.AreEqual(TemperEvent.None, temper.Tick(0.4f, true, 0.9f, 1));
        }

        [Test]
        public void Temper_KeptByOnePlayer_FussesThenCries_WhenTeammatesCouldTakeIt()
        {
            var temper = new RelicTemper(patienceSeconds: 10f, fussyFraction: 0.7f);
            bool fussed = false;
            TemperEvent last = TemperEvent.None;

            for (int i = 0; i < 10 && last != TemperEvent.Cry; i++)
            {
                last = temper.Tick(1f, true, 0f, 2);
                if (last == TemperEvent.Fussy) fussed = true;
            }

            Assert.IsTrue(fussed, "it warns before crying");
            Assert.AreEqual(TemperEvent.Cry, last);
        }

        [Test]
        public void Temper_ASoloPlayer_IsNeverAskedToPassIt()
        {
            var temper = new RelicTemper(patienceSeconds: 5f);

            for (int i = 0; i < 100; i++) Assert.AreEqual(TemperEvent.None, temper.Tick(1f, true, 0f, 1));
        }

        [Test]
        public void Temper_PassingItOn_RestartsThePatience()
        {
            var temper = new RelicTemper(patienceSeconds: 10f);
            temper.Tick(8f, true, 0f, 2);

            temper.ResetPatience();

            Assert.That(temper.Patience01, Is.EqualTo(0f).Within(0.0001f));
            Assert.AreEqual(TemperEvent.None, temper.Tick(5f, true, 0f, 2));
        }

        [Test]
        public void Temper_DroppingIt_DoesNotDodgeThePatience()
        {
            var temper = new RelicTemper(patienceSeconds: 10f);
            temper.Tick(8f, true, 0f, 2);

            temper.Tick(1f, false, 0f, 2);

            Assert.Greater(temper.Patience01, 0.5f, "a short drop only relaxes it a little");
        }

        [Test]
        public void Temper_AfterSoothing_DoesNotCryDuringTheGrace()
        {
            var temper = new RelicTemper(loudLevel: 0.4f, strainSeconds: 0.2f, patienceSeconds: 0f);

            temper.Soothe(3f);

            Assert.IsTrue(temper.IsInGrace);
            for (int i = 0; i < 25; i++) Assert.AreEqual(TemperEvent.None, temper.Tick(0.1f, true, 1f, 1));

            temper.Tick(1f, true, 0f, 1); // the rest of the grace passes in silence
            Assert.IsFalse(temper.IsInGrace);
            Assert.AreEqual(TemperEvent.Cry, temper.Tick(0.3f, true, 1f, 1), "after the grace a scream works again");
        }
    }
}
