using NUnit.Framework;

namespace EchoProtocol.Tests
{
    public class VoiceLoudnessTests
    {
        // Same defaults as MicrophoneLoudness in the inspector.
        private const float Silence = -35f, Scream = -12f;
        private const float Whisper = 0.12f, Talk = 0.40f, ScreamThreshold = 0.78f, Hysteresis = 0.04f;

        private static VoiceLoudness Resolve(VoiceLoudness current, float level) =>
            MicrophoneLoudness.ResolveState(current, level, Whisper, Talk, ScreamThreshold, Hysteresis);

        [Test]
        public void DbToLevel_AtOrBelowSilenceFloor_IsZero()
        {
            Assert.AreEqual(0f, MicrophoneLoudness.DbToLevel01(-35f, Silence, Scream));
            Assert.AreEqual(0f, MicrophoneLoudness.DbToLevel01(-80f, Silence, Scream));
        }

        [Test]
        public void DbToLevel_AtOrAboveScream_IsOne()
        {
            Assert.AreEqual(1f, MicrophoneLoudness.DbToLevel01(-12f, Silence, Scream), 1e-5f);
            Assert.AreEqual(1f, MicrophoneLoudness.DbToLevel01(0f, Silence, Scream), 1e-5f);
        }

        [Test]
        public void DbToLevel_Midpoint_IsHalf()
        {
            Assert.AreEqual(0.5f, MicrophoneLoudness.DbToLevel01(-23.5f, Silence, Scream), 1e-4f);
        }

        [Test]
        public void ResolveState_ZeroLevel_IsSilent()
        {
            Assert.AreEqual(VoiceLoudness.Silent, Resolve(VoiceLoudness.Silent, 0f));
        }

        [TestCase(0.13f, VoiceLoudness.Whisper)]
        [TestCase(0.45f, VoiceLoudness.Talk)]
        [TestCase(0.90f, VoiceLoudness.Scream)]
        public void ResolveState_ClimbsToMatchingBucket(float level, VoiceLoudness expected)
        {
            Assert.AreEqual(expected, Resolve(VoiceLoudness.Silent, level));
        }

        [Test]
        public void ResolveState_SuddenScream_SkipsStraightToScream()
        {
            Assert.AreEqual(VoiceLoudness.Scream, Resolve(VoiceLoudness.Silent, 1f));
        }

        [Test]
        public void ResolveState_JustBelowThreshold_StaysInBucketBecauseOfHysteresis()
        {
            // 0.38 is under the Talk threshold (0.40) but within the 0.04 hysteresis band.
            Assert.AreEqual(VoiceLoudness.Talk, Resolve(VoiceLoudness.Talk, 0.38f));
        }

        [Test]
        public void ResolveState_ClearlyBelowThreshold_FallsBack()
        {
            Assert.AreEqual(VoiceLoudness.Whisper, Resolve(VoiceLoudness.Talk, 0.30f));
        }

        [Test]
        public void ResolveState_FromScream_CanFallAllTheWayToSilent()
        {
            Assert.AreEqual(VoiceLoudness.Silent, Resolve(VoiceLoudness.Scream, 0f));
        }
    }
}
