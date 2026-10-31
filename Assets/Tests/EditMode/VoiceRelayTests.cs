using EchoProtocol.Voice;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>What the server checks before relaying a voice packet: distance and the speaker's packet rate.</summary>
    public class VoiceRelayTests
    {
        [Test]
        public void InRange_RelaysWithinHearing_PlusSlackForLatency()
        {
            Vector3 speaker = Vector3.zero;
            Assert.IsTrue(VoiceRelay.InRange(speaker, new Vector3(10f, 0f, 0f), 28f));
            Assert.IsTrue(VoiceRelay.InRange(speaker, new Vector3(28f + VoiceRelay.RangeSlackMetres - 0.1f, 0f, 0f), 28f),
                "a friend walking into range keeps the first words");
            Assert.IsFalse(VoiceRelay.InRange(speaker, new Vector3(40f, 0f, 0f), 28f), "too far to hear: not worth the bandwidth");
        }

        [Test]
        public void DownsampleFactor_ReachesTheVoiceRate_AndNeverDropsBelowOne()
        {
            Assert.AreEqual(8000, VoiceCodec.SampleRate);
            Assert.AreEqual(2, VoiceRelay.DownsampleFactor(16000));
            Assert.AreEqual(6, VoiceRelay.DownsampleFactor(48000));
            Assert.AreEqual(1, VoiceRelay.DownsampleFactor(8000));
            Assert.AreEqual(1, VoiceRelay.DownsampleFactor(4000), "a slow microphone still sends");
        }

        [Test]
        public void RateLimiter_AllowsABurst_ThenDropsTheFlood()
        {
            var limiter = new PacketRateLimiter(capacity: 5f, refillPerSecond: 25f);

            for (int i = 0; i < 5; i++) Assert.IsTrue(limiter.TryConsume(0f), $"burst packet {i}");
            Assert.IsFalse(limiter.TryConsume(0f), "a sixth packet in the same instant is a flood");
            Assert.AreEqual(1, limiter.Dropped);
        }

        [Test]
        public void RateLimiter_NormalTalking_IsNeverDropped()
        {
            // One packet every 40 ms (25 a second) for ten seconds, against a limit of 37.5 a second.
            var limiter = new PacketRateLimiter(capacity: 9f, refillPerSecond: 37.5f);
            for (int i = 0; i < 250; i++) Assert.IsTrue(limiter.TryConsume(i * 0.04f), $"packet {i}");
            Assert.AreEqual(0, limiter.Dropped);
        }

        [Test]
        public void RateLimiter_RefillsOverTime_ButNeverAboveItsCapacity()
        {
            var limiter = new PacketRateLimiter(capacity: 3f, refillPerSecond: 10f);
            for (int i = 0; i < 3; i++) limiter.TryConsume(0f);
            Assert.IsFalse(limiter.TryConsume(0f));

            Assert.IsTrue(limiter.TryConsume(0.1f), "one packet back after a tenth of a second");

            // A long silence refills only up to the burst size.
            int sent = 0;
            for (int i = 0; i < 10; i++) if (limiter.TryConsume(100f)) sent++;
            Assert.AreEqual(3, sent);
        }
    }
}
