using EchoProtocol.Extraction;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    /// <summary>The match flow's transitions and countdown, checked without a scene or a network.</summary>
    public class MatchRulesTests
    {
        [Test]
        public void Extraction_StartsOnlyFromTheObjective()
        {
            Assert.IsTrue(MatchRules.CanStartExtraction(ExtractionState.ObjectiveInProgress));
            Assert.IsFalse(MatchRules.CanStartExtraction(ExtractionState.ExtractionActive));
            Assert.IsFalse(MatchRules.CanStartExtraction(ExtractionState.Victory));
        }

        [Test]
        public void Escape_WinsOnlyDuringAnActiveExtraction()
        {
            Assert.IsTrue(MatchRules.CanEscape(ExtractionState.ExtractionActive));
            Assert.IsFalse(MatchRules.CanEscape(ExtractionState.ObjectiveInProgress), "the exit is closed before the symphony");
            Assert.IsFalse(MatchRules.CanEscape(ExtractionState.Defeat));
        }

        [Test]
        public void Defeat_CannotHappenTwice()
        {
            Assert.IsTrue(MatchRules.CanDefeat(ExtractionState.ExtractionActive));
            Assert.IsFalse(MatchRules.CanDefeat(ExtractionState.Victory));
            Assert.IsFalse(MatchRules.CanDefeat(ExtractionState.Defeat));
        }

        [Test]
        public void Restart_OnlyAfterTheMatchIsResolved()
        {
            Assert.IsFalse(MatchRules.CanRestart(ExtractionState.ObjectiveInProgress));
            Assert.IsFalse(MatchRules.CanRestart(ExtractionState.ExtractionActive));
            Assert.IsTrue(MatchRules.CanRestart(ExtractionState.Victory));
            Assert.IsTrue(MatchRules.CanRestart(ExtractionState.Defeat));
        }

        [Test]
        public void Tick_CountsDownAndReportsWhenTheTimeIsUp()
        {
            float remaining = 1f;

            Assert.IsFalse(MatchRules.Tick(ref remaining, 0.4f));
            Assert.AreEqual(0.6f, remaining, 1e-5f);

            Assert.IsTrue(MatchRules.Tick(ref remaining, 1f));
            Assert.AreEqual(0f, remaining, "the countdown never goes below zero");
        }

        [Test]
        public void ShouldSync_OnlySendsAStepLargeEnough_ToBeSeen()
        {
            Assert.IsFalse(MatchRules.ShouldSync(10f, 9.9f));
            Assert.IsTrue(MatchRules.ShouldSync(10f, 9.7f));
        }

        [Test]
        public void ClampQuote_KeepsTheIndexInsideTheList()
        {
            Assert.AreEqual(0, MatchRules.ClampQuote(0, 4));
            Assert.AreEqual(3, MatchRules.ClampQuote(9, 4));
            Assert.AreEqual(0, MatchRules.ClampQuote(-2, 4));
            Assert.AreEqual(0, MatchRules.ClampQuote(5, 0), "an empty list still gives a valid index");
        }

        [Test]
        public void LoseLife_TakesOneSharedLife_AndTheLastOneIsDefeat()
        {
            int lives = MatchRules.DefaultTeamLives;
            Assert.AreEqual(3, lives, "three catches survive, the fourth loses");

            Assert.IsFalse(MatchRules.LoseLife(ExtractionState.ObjectiveInProgress, ref lives));
            Assert.IsFalse(MatchRules.LoseLife(ExtractionState.ExtractionActive, ref lives), "catches count during the escape too");
            Assert.AreEqual(1, lives);
            Assert.IsTrue(MatchRules.LoseLife(ExtractionState.ObjectiveInProgress, ref lives), "the last life ends the match");
            Assert.AreEqual(0, lives);
        }

        [Test]
        public void LoseLife_DoesNothing_OnceTheMatchIsOver_OrTheLivesAreGone()
        {
            int lives = 2;
            Assert.IsFalse(MatchRules.LoseLife(ExtractionState.Victory, ref lives));
            Assert.IsFalse(MatchRules.LoseLife(ExtractionState.Defeat, ref lives));
            Assert.AreEqual(2, lives, "a finished match loses nothing");

            lives = 0;
            Assert.IsFalse(MatchRules.LoseLife(ExtractionState.ObjectiveInProgress, ref lives), "no second defeat");
            Assert.AreEqual(0, lives);
        }
    }
}
