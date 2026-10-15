using EchoProtocol.Altar;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    /// <summary>The altar's docking and completion rules, checked without a scene or a network.</summary>
    public class AltarRulesTests
    {
        [Test]
        public void Count_CountsTheFilledSockets()
        {
            Assert.AreEqual(0, AltarRules.Count(0));
            Assert.AreEqual(1, AltarRules.Count(0b001));
            Assert.AreEqual(3, AltarRules.Count(0b111));
            Assert.AreEqual(2, AltarRules.Count(0b101));
        }

        [Test]
        public void With_FillsOnlyTheRequestedSocket()
        {
            int mask = AltarRules.With(0, 1);
            Assert.AreEqual(0b010, mask);
            Assert.AreEqual(0b110, AltarRules.With(mask, 2));
            Assert.AreEqual(mask, AltarRules.With(mask, -1), "an invalid index changes nothing");
        }

        [Test]
        public void IsFilled_ReadsTheBitOfTheSocket()
        {
            int mask = 0b100;
            Assert.IsTrue(AltarRules.IsFilled(mask, 2));
            Assert.IsFalse(AltarRules.IsFilled(mask, 0));
            Assert.IsFalse(AltarRules.IsFilled(mask, -1));
            Assert.IsFalse(AltarRules.IsFilled(mask, AltarRules.MaxSockets));
        }

        [Test]
        public void ShouldComplete_OnlyWhenTheRequiredSocketsAreFilledAndNotYetComplete()
        {
            Assert.IsFalse(AltarRules.ShouldComplete(0b011, 3, false), "two of three is not enough");
            Assert.IsTrue(AltarRules.ShouldComplete(0b111, 3, false));
            Assert.IsFalse(AltarRules.ShouldComplete(0b111, 3, true), "the symphony starts only once");
            Assert.IsFalse(AltarRules.ShouldComplete(0b111, 0, false), "an altar that needs nothing never completes");
        }

        [Test]
        public void CanDock_AcceptsAFreeSocketInRange()
        {
            Assert.IsTrue(AltarRules.CanDock(0b001, 3, 1, false, false, 2f));
        }

        [Test]
        public void CanDock_RefusesAFilledSocket()
        {
            Assert.IsFalse(AltarRules.CanDock(0b001, 3, 0, false, false, 2f));
        }

        [Test]
        public void CanDock_RefusesAnIndexOutsideTheAltar()
        {
            Assert.IsFalse(AltarRules.CanDock(0, 3, 3, false, false, 2f));
            Assert.IsFalse(AltarRules.CanDock(0, 3, -1, false, false, 2f));
        }

        [Test]
        public void CanDock_RefusesOnceTheAltarIsComplete()
        {
            Assert.IsFalse(AltarRules.CanDock(0, 3, 0, true, false, 2f));
        }

        [Test]
        public void CanDock_RefusesARelicThatIsAlreadyLocked()
        {
            Assert.IsFalse(AltarRules.CanDock(0, 3, 0, false, true, 2f));
        }

        [Test]
        public void CanDock_RefusesAPlayerTooFarFromTheSocket()
        {
            Assert.IsTrue(AltarRules.CanDock(0, 3, 0, false, false, AltarRules.DockRange));
            Assert.IsFalse(AltarRules.CanDock(0, 3, 0, false, false, AltarRules.DockRange + 0.1f));
        }
    }
}
