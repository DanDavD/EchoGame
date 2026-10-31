using EchoProtocol.EditorTools;
using EchoProtocol.Player;
using EchoProtocol.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>Floating name tags: cleaning up a player's name and how visible the tag is.</summary>
    public class PlayerNameTests
    {
        [Test]
        public void Sanitize_KeepsANormalSteamName()
        {
            Assert.AreEqual("DanDavD", PlayerName.Sanitize("DanDavD", 0));
            Assert.AreEqual("Señor Eco", PlayerName.Sanitize("Señor Eco", 0), "accents and spaces are fine");
        }

        [Test]
        public void Sanitize_StripsMarkup_ControlCharacters_AndExtraSpaces()
        {
            Assert.AreEqual("Daniel Dev", PlayerName.Sanitize("  <b>Daniel</b>   Dev\n ", 0));
            Assert.AreEqual("bad", PlayerName.Sanitize("<color=red>bad</color>", 0), "no rich text injected into everyone's HUD");
            Assert.AreEqual("a b", PlayerName.Sanitize("a\tb\u0007", 0));
        }

        [Test]
        public void Sanitize_CutsLongNames()
        {
            string name = PlayerName.Sanitize("AVeryVeryLongSteamNameThatGoesOn", 0);
            Assert.AreEqual(PlayerName.MaxLength, name.Length);
        }

        [Test]
        public void Sanitize_FallsBackToPlayerN_WhenNothingIsLeft()
        {
            Assert.AreEqual("Player 1", PlayerName.Sanitize(null, 0), "the host without Steam");
            Assert.AreEqual("Player 3", PlayerName.Sanitize("   ", 2));
            Assert.AreEqual("Player 2", PlayerName.Sanitize("<i></i>", 1));
        }

        [Test]
        public void TagAlpha_FullUpClose_FadesFarAway_AndHidesBehindWalls()
        {
            Assert.AreEqual(1f, NameTagRules.Alpha(5f, 0));
            Assert.AreEqual(1f, NameTagRules.Alpha(NameTagRules.FullDistance, 0));
            float mid = NameTagRules.Alpha((NameTagRules.FullDistance + NameTagRules.MaxDistance) * 0.5f, 0);
            Assert.That(mid, Is.EqualTo(0.5f).Within(0.01f));
            Assert.AreEqual(0f, NameTagRules.Alpha(NameTagRules.MaxDistance + 1f, 0));
            Assert.AreEqual(0f, NameTagRules.Alpha(3f, 1), "a wall in between hides the tag: the cave stays scary");
        }

        [Test]
        public void TagScale_ShrinksWithDistance_ButStaysReadable()
        {
            Assert.AreEqual(1f, NameTagRules.Scale(0f));
            Assert.That(NameTagRules.Scale(NameTagRules.MaxDistance * 2f), Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void PlayerPrefab_HasPlayerName()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkRigBuilder.PlayerPrefabPath);
            Assert.IsNotNull(prefab.GetComponent<PlayerName>(),
                "the player prefab needs PlayerName (Echo Protocol > Build > Add Player Name To Player)");
        }
    }
}
