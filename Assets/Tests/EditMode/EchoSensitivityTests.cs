using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// The echo's voice sensitivity: a proportional reduction of the noise needed for every radius, applied only to the echo.
    /// </summary>
    public class EchoSensitivityTests
    {
        private const float MaxRadius = 60f;
        private const float Gate = 0.08f;

        // Same shape as the serialized default: whisper ~2.5 m, talk ~12 m, scream = the whole cavern.
        private static AnimationCurve DefaultCurve() => new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 0.04f),
            new Keyframe(0.65f, 0.2f),
            new Keyframe(1f, 1f));

        private static float Radius(float level, float sensitivity) =>
            EchoVisionController.TargetRadius(level, sensitivity, Gate, MaxRadius, DefaultCurve());

        [Test]
        public void SensitivityOfOne_KeepsTheOriginalMapping()
        {
            AnimationCurve curve = DefaultCurve();
            foreach (float level in new[] { 0.1f, 0.25f, 0.4f, 0.65f, 0.9f, 1f })
            {
                Assert.AreEqual(MaxRadius * curve.Evaluate(level), Radius(level, 1f), 1e-4f, $"level {level}");
            }
        }

        [Test]
        public void EveryRadius_NeedsTheSameFractionLessNoise()
        {
            const float sensitivity = 1.3f;

            // Any radius the old mapping reached at level L is now reached at L / sensitivity: a proportional cut.
            foreach (float oldLevel in new[] { 0.3f, 0.5f, 0.65f, 0.8f, 0.95f })
            {
                float before = Radius(oldLevel, 1f);
                float after = Radius(oldLevel / sensitivity, sensitivity);
                Assert.AreEqual(before, after, 1e-3f, $"the radius at level {oldLevel} must be reached with {1f / sensitivity:P0} of the loudness");
            }
        }

        [Test]
        public void SameNoise_NowLightsMoreOfTheWorld_AtEveryPoint()
        {
            foreach (float level in new[] { 0.2f, 0.35f, 0.5f, 0.65f, 0.8f })
            {
                Assert.Greater(Radius(level, 1.3f), Radius(level, 1f), $"level {level}");
            }
        }

        [Test]
        public void ALoudEnoughVoice_StillCapsAtTheMaximumRadius()
        {
            Assert.AreEqual(MaxRadius, Radius(1f, 1.3f), 1e-4f);
            Assert.AreEqual(MaxRadius, Radius(0.8f, 1.3f), 1e-4f, "0.8 x 1.3 already reaches the top of the curve");
            Assert.LessOrEqual(Radius(1f, 3f), MaxRadius, "never beyond the maximum");
        }

        [Test]
        public void TheNoiseGate_LooksAtTheRealLevel_SoBackgroundNoiseIsNotAmplified()
        {
            Assert.AreEqual(0f, Radius(0.05f, 3f), "breathing stays below the gate even with a high sensitivity");
            Assert.Greater(Radius(0.1f, 1.3f), 0f);
        }

        [Test]
        public void RadiusGrowsWithTheVoice()
        {
            float previous = -1f;
            for (float level = 0f; level <= 1.0001f; level += 0.05f)
            {
                float radius = Radius(level, 1.3f);
                Assert.GreaterOrEqual(radius, previous, $"level {level}");
                previous = radius;
            }
        }

        [Test]
        public void ANonPositiveSensitivity_RevealsNothingInsteadOfBreaking()
        {
            Assert.AreEqual(0f, Radius(0.9f, 0f));
            Assert.AreEqual(0f, Radius(0.9f, -2f));
        }
    }
}
