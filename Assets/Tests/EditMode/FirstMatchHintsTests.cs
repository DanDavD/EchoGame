using EchoProtocol.UI;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    /// <summary>The first-match hints: one at a time, each gone once the player has done what it asks.</summary>
    public class FirstMatchHintsTests
    {
        [Test]
        public void FirstHint_TeachesTheEcho_UntilThePlayerHasUsedIt()
        {
            Assert.AreEqual(FirstMatchHints.Echo, FirstMatchHints.Pick(0f, hasEchoed: false, carryingRelic: false, relicsOnAltar: 0));
            Assert.AreEqual(FirstMatchHints.Echo, FirstMatchHints.Pick(30f, false, true, 0), "the echo comes first, even while carrying");
        }

        [Test]
        public void AfterTheEcho_ItPointsAtTheRelics_ThenAtTheAltar()
        {
            Assert.AreEqual(FirstMatchHints.FindRelic, FirstMatchHints.Pick(10f, true, false, 0));
            Assert.AreEqual(FirstMatchHints.Carry, FirstMatchHints.Pick(10f, true, true, 0));
        }

        [Test]
        public void Hints_StopAfterTheFirstRelicIsDocked_OrAfterAWhile()
        {
            Assert.IsNull(FirstMatchHints.Pick(10f, false, false, relicsOnAltar: 1), "the team has got it");
            Assert.IsNull(FirstMatchHints.Pick(FirstMatchHints.MaxSeconds + 1f, false, false, 0), "nobody needs them any more");
        }
    }
}
