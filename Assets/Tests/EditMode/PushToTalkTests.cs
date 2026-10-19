using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>Push-to-talk echo: the voice only lights the cave while V is held, and echoing is louder than chatting.</summary>
    public class PushToTalkTests
    {
        private static readonly AnimationCurve Curve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.25f, 0.04f), new Keyframe(0.65f, 0.2f), new Keyframe(1f, 1f));

        [Test]
        public void WithoutTheEchoKey_EvenAScream_RevealsNothing()
        {
            Assert.AreEqual(0f, EchoVisionController.TargetRadius(1f, 1.3f, 0.08f, 60f, Curve, echoHeld: false));
        }

        [Test]
        public void WithTheEchoKey_TheVoiceLightsAsBefore()
        {
            float pushToTalk = EchoVisionController.TargetRadius(0.5f, 1.3f, 0.08f, 60f, Curve, echoHeld: true);
            float classic = EchoVisionController.TargetRadius(0.5f, 1.3f, 0.08f, 60f, Curve);

            Assert.Greater(pushToTalk, 0f);
            Assert.AreEqual(classic, pushToTalk);
        }

        [Test]
        public void HoldingTheKeyInSilence_StillRevealsNothing()
        {
            Assert.AreEqual(0f, EchoVisionController.TargetRadius(0.02f, 1.3f, 0.08f, 60f, Curve, echoHeld: true));
        }

        [Test]
        public void Echoing_IsLouderForTheMonster_ThanChatting()
        {
            float chat = VoiceNoiseEmitter.Weigh(0.5f, 1f, echoing: false, chatScale: 0.6f, echoScale: 1.25f);
            float echo = VoiceNoiseEmitter.Weigh(0.5f, 1f, echoing: true, chatScale: 0.6f, echoScale: 1.25f);

            Assert.Greater(chat, 0f, "plain chat still makes some noise");
            Assert.Greater(echo, chat);
        }

        [Test]
        public void Noise_StaysBetweenZeroAndOne()
        {
            Assert.AreEqual(1f, VoiceNoiseEmitter.Weigh(1f, 2f, true, 0.6f, 1.25f));
            Assert.AreEqual(0f, VoiceNoiseEmitter.Weigh(-1f, 1f, true, 0.6f, 1.25f));
        }
    }
}
