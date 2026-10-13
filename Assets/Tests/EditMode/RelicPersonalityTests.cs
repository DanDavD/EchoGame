using System;
using System.Collections.Generic;
using EchoProtocol.Relics;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    public class RelicPersonalityTests
    {
        private static readonly RelicPersonalityType[] Types =
            (RelicPersonalityType[])Enum.GetValues(typeof(RelicPersonalityType));

        private static readonly RelicDialogueCategory[] Categories =
            (RelicDialogueCategory[])Enum.GetValues(typeof(RelicDialogueCategory));

        [Test]
        public void EveryPersonality_HasAUniqueNameAndPositivePitch()
        {
            var names = new HashSet<string>();
            foreach (var type in Types)
            {
                var profile = RelicPersonality.GetProfile(type);
                Assert.IsFalse(string.IsNullOrWhiteSpace(profile.Name), $"{type} has no name");
                Assert.IsTrue(names.Add(profile.Name), $"duplicate relic name {profile.Name}");
                Assert.Greater(profile.BasePitch, 0f, $"{type} pitch");
            }
        }

        [Test]
        public void EveryPersonalityAndCategory_HasRealDialogue()
        {
            foreach (var type in Types)
            foreach (var category in Categories)
            {
                var lines = RelicPersonality.GetLines(type, category);
                Assert.IsNotEmpty(lines, $"{type}/{category} has no lines");
                foreach (var line in lines)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line), $"{type}/{category} has an empty line");
                    Assert.AreNotEqual("...", line, $"{type}/{category} falls back to the placeholder");
                }
            }
        }

        [Test]
        public void GetRandomLineIndex_AlwaysStaysInRange()
        {
            foreach (var type in Types)
            foreach (var category in Categories)
            {
                int count = RelicPersonality.GetLineCount(type, category);
                for (int i = 0; i < 20; i++)
                {
                    int index = RelicPersonality.GetRandomLineIndex(type, category);
                    Assert.That(index, Is.InRange(0, Math.Max(0, count - 1)));
                }
            }
        }

        [Test]
        public void GetLine_ClampsOutOfRangeIndexes()
        {
            var type = RelicPersonalityType.SarcasticCritic;
            var category = RelicDialogueCategory.Pickup;
            int count = RelicPersonality.GetLineCount(type, category);

            Assert.AreEqual(RelicPersonality.GetLine(type, category, 0), RelicPersonality.GetLine(type, category, -5));
            Assert.AreEqual(RelicPersonality.GetLine(type, category, count - 1), RelicPersonality.GetLine(type, category, count + 100));
        }
    }
}
