using NUnit.Framework;

namespace EchoProtocol.Tests
{
    public class HearingMathTests
    {
        private const float Range = 40f;

        [Test]
        public void PerceivedLevel_AtSource_IsFullNoiseLevel()
        {
            Assert.AreEqual(0.8f, HearingSensor.ComputePerceivedLevel(0.8f, 0f, Range), 1e-5f);
        }

        [Test]
        public void PerceivedLevel_AtMaxRange_IsSilent()
        {
            Assert.AreEqual(0f, HearingSensor.ComputePerceivedLevel(1f, Range, Range), 1e-5f);
        }

        [Test]
        public void PerceivedLevel_BeyondMaxRange_IsSilent()
        {
            Assert.AreEqual(0f, HearingSensor.ComputePerceivedLevel(1f, Range * 3f, Range), 1e-5f);
        }

        [Test]
        public void PerceivedLevel_HalfwayAway_IsHalved()
        {
            Assert.AreEqual(0.5f, HearingSensor.ComputePerceivedLevel(1f, Range * 0.5f, Range), 1e-5f);
        }

        [Test]
        public void PerceivedLevel_ClosingIn_NeverGetsQuieter()
        {
            float previous = -1f;
            for (float d = Range; d >= 0f; d -= 1f)
            {
                float level = HearingSensor.ComputePerceivedLevel(0.7f, d, Range);
                Assert.GreaterOrEqual(level, previous, $"level dropped when closing in at {d} m");
                previous = level;
            }
        }

        [Test]
        public void PerceivedLevel_ClampsNoiseLevelToOne()
        {
            Assert.AreEqual(1f, HearingSensor.ComputePerceivedLevel(5f, 0f, Range), 1e-5f);
        }

        [Test]
        public void PerceivedLevel_WithInvalidRange_IsSilentInsteadOfNaN()
        {
            Assert.AreEqual(0f, HearingSensor.ComputePerceivedLevel(1f, 1f, 0f));
            Assert.AreEqual(0f, HearingSensor.ComputePerceivedLevel(1f, 1f, -5f));
        }
    }
}
