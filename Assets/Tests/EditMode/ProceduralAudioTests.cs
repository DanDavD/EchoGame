using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>The generated sounds: never clipping, never silent, seamless when they loop, the same on every machine.</summary>
    public class ProceduralAudioTests
    {
        private static float Peak(float[] samples)
        {
            float max = 0f;
            foreach (float s in samples) max = Mathf.Max(max, Mathf.Abs(s));
            return max;
        }

        private static float Rms(float[] samples)
        {
            double sum = 0;
            foreach (float s in samples) sum += s * s;
            return (float)System.Math.Sqrt(sum / samples.Length);
        }

        private static float[][] AllSounds() => new[]
        {
            ProceduralAudio.RoomToneSamples(8f),
            ProceduralAudio.DripSamples(0),
            ProceduralAudio.DripSamples(3),
            ProceduralAudio.RumbleSamples(),
            ProceduralAudio.MonsterBreathSamples(),
            ProceduralAudio.MonsterRoarSamples(),
            ProceduralAudio.MonsterGrowlSamples(),
            ProceduralAudio.StepSamples(seed: 31, pitch: 55f, length: 0.28f, grit: 0.35f),
            ProceduralAudio.StepSamples(seed: 11, pitch: 120f, length: 0.12f, grit: 0.6f),
        };

        [Test]
        public void EverySound_StaysBelowFullScale_AndIsNotSilent()
        {
            foreach (float[] sound in AllSounds())
            {
                Assert.LessOrEqual(Peak(sound), 1f, "a sample above full scale would clip");
                Assert.Greater(Rms(sound), 0.01f, "the sound must be audible");
                foreach (float s in sound) Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s));
            }
        }

        [Test]
        public void RoomTone_LoopsWithoutAClick()
        {
            float[] loop = ProceduralAudio.RoomToneSamples(8f);
            Assert.AreEqual(Mathf.RoundToInt(ProceduralAudio.SampleRate * 8f), loop.Length);

            // The last sample flows into the first: the jump is no bigger than between two normal neighbours.
            float seam = Mathf.Abs(loop[loop.Length - 1] - loop[0]);
            float typical = 0f;
            for (int i = 1; i < loop.Length; i++) typical = Mathf.Max(typical, Mathf.Abs(loop[i] - loop[i - 1]));
            Assert.LessOrEqual(seam, typical + 1e-4f);
        }

        [Test]
        public void TheRoomTone_IsQuieterThanTheMonster()
        {
            // The background must never cover the roar that tells you to run.
            Assert.Less(Rms(ProceduralAudio.RoomToneSamples(8f)), Rms(ProceduralAudio.MonsterRoarSamples()));
        }

        [Test]
        public void Generators_AreDeterministic()
        {
            CollectionAssert.AreEqual(ProceduralAudio.MonsterRoarSamples(), ProceduralAudio.MonsterRoarSamples());
            CollectionAssert.AreEqual(ProceduralAudio.DripSamples(2), ProceduralAudio.DripSamples(2));
        }

        [Test]
        public void Normalize_ScalesToThePeak_AndLeavesSilenceAlone()
        {
            float[] scaled = ProceduralAudio.Normalize(new[] { 0.1f, -0.4f, 0.2f }, 0.8f);
            Assert.AreEqual(0.8f, Peak(scaled), 1e-5f);
            Assert.AreEqual(0.2f, scaled[0], 1e-5f);

            float[] silence = ProceduralAudio.Normalize(new float[4], 0.8f);
            Assert.AreEqual(0f, Peak(silence));
        }
    }
}
