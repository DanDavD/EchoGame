using EchoProtocol.Core;
using NUnit.Framework;

namespace EchoProtocol.Tests
{
    /// <summary>The shared team noise meter, checked without a scene or a network.</summary>
    public class TeamNoiseBudgetTests
    {
        // Capacity 1, fills 1 per unit of loudness per second, drains 0.5 per second, 2 seconds of quiet to calm.
        private static TeamNoiseBudget CreateBudget() => new TeamNoiseBudget(1f, 1f, 0.5f, 2f);

        [Test]
        public void Silence_DoesNotFillTheMeter()
        {
            var budget = CreateBudget();

            budget.Tick(10f, 0f);

            Assert.That(budget.Level, Is.EqualTo(0f).Within(0.0001f));
            Assert.IsFalse(budget.IsAlarmed);
        }

        [Test]
        public void Talking_FillsTheMeterByLoudnessAndTime()
        {
            var budget = CreateBudget();

            budget.Tick(0.25f, 0.5f);

            Assert.That(budget.Level, Is.EqualTo(0.125f).Within(0.0001f));
        }

        [Test]
        public void LouderTeam_FillsFaster()
        {
            var quiet = CreateBudget();
            var loud = CreateBudget();

            quiet.Tick(0.5f, 0.25f);
            loud.Tick(0.5f, 1f);

            Assert.That(loud.Level, Is.GreaterThan(quiet.Level));
        }

        [Test]
        public void FullMeter_RaisesTheAlarmOnce()
        {
            var budget = CreateBudget();
            int alarms = 0;
            budget.Alarmed += () => alarms++;

            budget.Tick(1f, 1f);
            budget.Tick(1f, 1f);

            Assert.IsTrue(budget.IsAlarmed);
            Assert.AreEqual(1, alarms);
        }

        [Test]
        public void Silence_DrainsTheMeterWhenNoAlarm()
        {
            var budget = CreateBudget();
            budget.Tick(0.5f, 1f);

            budget.Tick(0.5f, 0f);

            Assert.That(budget.Level, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Drain_NeverGoesBelowZero()
        {
            var budget = CreateBudget();
            budget.Tick(0.25f, 1f);

            budget.Tick(10f, 0f);

            Assert.That(budget.Level, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Alarm_DoesNotEndUntilTheWholeTeamIsQuietLongEnough()
        {
            var budget = CreateBudget();
            budget.Tick(1f, 1f);

            budget.Tick(1f, 0f);
            Assert.IsTrue(budget.IsAlarmed, "one second of quiet is not enough");

            budget.Tick(1f, 0f);
            Assert.IsFalse(budget.IsAlarmed, "two seconds of quiet calms the team");
        }

        [Test]
        public void Speaking_DuringTheAlarm_RestartsTheQuietCount()
        {
            var budget = CreateBudget();
            budget.Tick(1f, 1f);
            budget.Tick(1.5f, 0f);

            budget.Tick(0.1f, 1f);
            budget.Tick(1.5f, 0f);

            Assert.IsTrue(budget.IsAlarmed, "the quiet count started again after the sound");
        }

        [Test]
        public void Calming_ResetsTheLevelAndRaisesTheEvent()
        {
            var budget = CreateBudget();
            int calms = 0;
            budget.Calmed += () => calms++;
            budget.Tick(1f, 1f);

            budget.Tick(2f, 0f);

            Assert.IsFalse(budget.IsAlarmed);
            Assert.AreEqual(1, calms);
            Assert.That(budget.Level, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Reset_ClearsTheAlarmAndTheLevel()
        {
            var budget = CreateBudget();
            budget.Tick(1f, 1f);

            budget.Reset();

            Assert.IsFalse(budget.IsAlarmed);
            Assert.That(budget.Level, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ZeroDelta_ChangesNothing()
        {
            var budget = CreateBudget();

            budget.Tick(0f, 1f);

            Assert.That(budget.Level, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void NegativeLoudness_CountsAsSilence()
        {
            var budget = CreateBudget();
            budget.Tick(0.5f, 1f);

            budget.Tick(0.5f, -5f);

            Assert.That(budget.Level, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Add_FillsTheMeter_AndCanRaiseTheAlarm()
        {
            var budget = CreateBudget();
            int alarms = 0;
            budget.Alarmed += () => alarms++;

            budget.Add(0.4f);
            Assert.That(budget.Level, Is.EqualTo(0.4f).Within(0.0001f));

            budget.Add(0.7f);
            Assert.IsTrue(budget.IsAlarmed);
            Assert.AreEqual(1, alarms);
        }

        [Test]
        public void Remove_EmptiesTheMeter_ButNotBelowZero()
        {
            var budget = CreateBudget();
            budget.Add(0.3f);

            budget.Remove(0.5f);

            Assert.That(budget.Level, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Remove_DuringTheAlarm_DoesNotEndIt()
        {
            var budget = CreateBudget();
            budget.ForceAlarm();

            budget.Remove(1f);

            Assert.IsTrue(budget.IsAlarmed, "only silence ends the alarm");
        }

        [Test]
        public void ForceAlarm_StartsTheAlarmOnce()
        {
            var budget = CreateBudget();
            int alarms = 0;
            budget.Alarmed += () => alarms++;

            budget.ForceAlarm();
            budget.ForceAlarm();

            Assert.IsTrue(budget.IsAlarmed);
            Assert.AreEqual(1, alarms);
            Assert.That(budget.Fill01, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void RoomNoise_FromAnIdleMicrophone_AddsNothingToTheMeter()
        {
            const float gate = 0.15f;
            Assert.AreEqual(0f, EchoProtocol.TeamNoise.TeamNoiseDirector.CountedNoise(0.08f, gate), "the hum of a room or a fan");
            Assert.AreEqual(0f, EchoProtocol.TeamNoise.TeamNoiseDirector.CountedNoise(0.14f, gate), "a whisper in plain chat");
            Assert.AreEqual(0.21f, EchoProtocol.TeamNoise.TeamNoiseDirector.CountedNoise(0.21f, gate), 1e-5f, "normal chat counts in full");
        }

        [Test]
        public void Fill01_IsTheLevelAsAFractionOfTheCapacity()
        {
            var budget = new TeamNoiseBudget(4f, 1f, 0.5f, 2f);

            budget.Tick(1f, 2f);

            Assert.That(budget.Fill01, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
