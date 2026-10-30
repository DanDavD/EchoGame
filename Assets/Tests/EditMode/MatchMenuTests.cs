using EchoProtocol.UI;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>The menu that Esc opens during a match, and the slider of the settings screen.</summary>
    public class MatchMenuTests
    {
        [TearDown]
        public void TearDown()
        {
            MatchMenu.SetAvailable(false);
        }

        [Test]
        public void WithoutAMatch_EscapeDoesNothing()
        {
            MatchMenu.SetAvailable(false);
            MatchMenu.Escape();
            Assert.IsFalse(MatchMenu.IsOpen, "the sandbox has no menu: Esc only frees the cursor there");
        }

        [Test]
        public void Escape_OpensTheMenu_AndEscapeAgainGoesBackToTheGame()
        {
            MatchMenu.SetAvailable(true);

            MatchMenu.Escape();
            Assert.AreEqual(MatchMenu.Page.Main, MatchMenu.Current);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "the menu needs the mouse");

            MatchMenu.Escape();
            Assert.IsFalse(MatchMenu.IsOpen);
        }

        [Test]
        public void Escape_InASubPage_GoesBackToTheMainPage()
        {
            MatchMenu.SetAvailable(true);
            MatchMenu.Show(MatchMenu.Page.Settings);

            MatchMenu.Escape();
            Assert.AreEqual(MatchMenu.Page.Main, MatchMenu.Current, "Esc in the settings is 'back', not 'close'");

            MatchMenu.Show(MatchMenu.Page.Controls);
            MatchMenu.Escape();
            Assert.AreEqual(MatchMenu.Page.Main, MatchMenu.Current);
        }

        [Test]
        public void Resume_ClosesTheMenu()
        {
            MatchMenu.SetAvailable(true);
            MatchMenu.Open();
            MatchMenu.Resume();
            Assert.IsFalse(MatchMenu.IsOpen);
        }

        [Test]
        public void LeavingTheMatch_ClosesTheMenu()
        {
            MatchMenu.SetAvailable(true);
            MatchMenu.Show(MatchMenu.Page.Settings);

            MatchMenu.SetAvailable(false);
            Assert.IsFalse(MatchMenu.IsOpen);

            MatchMenu.Open();
            Assert.IsFalse(MatchMenu.IsOpen, "nothing opens while no screen can draw the menu");
        }

        [Test]
        public void TheSlider_FollowsTheMouse_AndStopsAtItsEnds()
        {
            var rect = new Rect(100f, 0f, 200f, 30f);
            Assert.AreEqual(0f, EchoGui.SliderValueAt(100f, rect, 0f, 1f), 1e-5f);
            Assert.AreEqual(0.5f, EchoGui.SliderValueAt(200f, rect, 0f, 1f), 1e-5f);
            Assert.AreEqual(3f, EchoGui.SliderValueAt(300f, rect, 0.25f, 3f), 1e-5f);
            Assert.AreEqual(0.25f, EchoGui.SliderValueAt(20f, rect, 0.25f, 3f), 1e-5f, "left of the track is the minimum");
            Assert.AreEqual(3f, EchoGui.SliderValueAt(900f, rect, 0.25f, 3f), 1e-5f, "right of the track is the maximum");
        }
    }
}
