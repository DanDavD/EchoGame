using System.Collections;
using EchoProtocol.Relics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>Two relics talking at once must not draw their subtitle cards on top of each other.</summary>
    public class RelicDialogueUITests
    {
        private GameObject _first;
        private GameObject _second;

        [SetUp]
        public void SetUp()
        {
            _first = new GameObject("RelicA");
            _second = new GameObject("RelicB");
        }

        [TearDown]
        public void TearDown()
        {
            if (_first != null) Object.Destroy(_first);
            if (_second != null) Object.Destroy(_second);
        }

        [UnityTest]
        public IEnumerator TheRelicThatSpokeLast_OwnsTheScreenCard()
        {
            var a = _first.AddComponent<RelicDialogueUI>();
            var b = _second.AddComponent<RelicDialogueUI>();

            a.DisplayLine(RelicPersonalityType.SarcasticCritic, "Just lying here listening to you.", null);
            yield return null;
            Assert.IsTrue(a.OwnsScreenCard, "the only relic talking shows its card");

            b.DisplayLine(RelicPersonalityType.AnxiousCoward, "Is it gone?! Don't leave me alone!", null);
            yield return null;
            Assert.IsTrue(b.OwnsScreenCard, "the newest line takes the card");
            Assert.IsFalse(a.OwnsScreenCard, "the older line keeps only its world bubble");
        }

        [UnityTest]
        public IEnumerator ClearingTheLine_ReleasesTheScreenCard()
        {
            var a = _first.AddComponent<RelicDialogueUI>();
            var b = _second.AddComponent<RelicDialogueUI>();

            a.DisplayLine(RelicPersonalityType.DumbGloop, "Boing!", null);
            b.DisplayLine(RelicPersonalityType.GrumpyElder, "Unhand me, serf.", null);
            yield return null;

            b.ClearDialogue();
            Assert.IsFalse(b.OwnsScreenCard);
            Assert.IsFalse(a.OwnsScreenCard, "a cleared card is not handed back to an older, half-finished line");
        }

        [UnityTest]
        public IEnumerator DestroyingTheSpeaker_ReleasesTheScreenCard()
        {
            var a = _first.AddComponent<RelicDialogueUI>();
            a.DisplayLine(RelicPersonalityType.SarcasticCritic, "Three stars out of five.", null);
            yield return null;

            Object.Destroy(_first);
            yield return null;

            var b = _second.AddComponent<RelicDialogueUI>();
            Assert.IsFalse(b.OwnsScreenCard, "nobody owns the card until a relic speaks again");
            b.DisplayLine(RelicPersonalityType.AnxiousCoward, "Hello?", null);
            Assert.IsTrue(b.OwnsScreenCard);
        }
    }
}
