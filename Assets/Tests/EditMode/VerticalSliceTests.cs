using NUnit.Framework;
using UnityEngine;
using EchoProtocol.Extraction;
using EchoProtocol.Voice;

namespace EchoProtocol.Tests
{
    /// <summary>
    /// Tests for the polished vertical slice features:
    /// - Monster Sonic Stun Climax
    /// - Procedural Escape Portal audio & activation logic
    /// - Match rules and extraction transitions
    /// </summary>
    public class VerticalSliceTests
    {
        [Test]
        public void MatchRules_Transitions_FollowStrictFlow()
        {
            Assert.IsTrue(MatchRules.CanStartExtraction(ExtractionState.ObjectiveInProgress));
            Assert.IsFalse(MatchRules.CanStartExtraction(ExtractionState.ExtractionActive));
            Assert.IsFalse(MatchRules.CanStartExtraction(ExtractionState.Victory));

            Assert.IsTrue(MatchRules.CanEscape(ExtractionState.ExtractionActive));
            Assert.IsFalse(MatchRules.CanEscape(ExtractionState.ObjectiveInProgress));

            Assert.IsTrue(MatchRules.CanRestart(ExtractionState.Victory));
            Assert.IsTrue(MatchRules.CanRestart(ExtractionState.Defeat));
            Assert.IsFalse(MatchRules.CanRestart(ExtractionState.ExtractionActive));
        }

        [Test]
        public void MatchRules_Tick_CountsDownToZero()
        {
            float remaining = 10f;
            bool expired = MatchRules.Tick(ref remaining, 4f);
            Assert.IsFalse(expired);
            Assert.AreEqual(6f, remaining, 0.001f);

            expired = MatchRules.Tick(ref remaining, 8f);
            Assert.IsTrue(expired);
            Assert.AreEqual(0f, remaining, 0.001f);
        }

        [Test]
        public void VoiceCodec_DestinationBuffer_EncodesIdenticalToAllocated()
        {
            var samples = new System.Collections.Generic.List<float> { -0.8f, -0.2f, 0f, 0.35f, 0.72f };
            byte[] allocated = VoiceCodec.Encode(samples, samples.Count);

            byte[] preallocated = new byte[samples.Count + 4];
            int written = VoiceCodec.Encode(samples, samples.Count, preallocated);

            Assert.AreEqual(samples.Count, written);
            for (int i = 0; i < samples.Count; i++)
            {
                Assert.AreEqual(allocated[i], preallocated[i]);
            }
        }

        [Test]
        public void EscapePortal_ActivateAndDeactivate_TogglesStateCleanly()
        {
            var go = new GameObject("TestPortal");
            try
            {
                var portal = go.AddComponent<EscapePortal>();
                Assert.IsFalse(portal.IsActive);

                portal.ActivatePortal();
                Assert.IsTrue(portal.IsActive);

                portal.DeactivatePortal();
                Assert.IsFalse(portal.IsActive);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
