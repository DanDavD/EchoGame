using NUnit.Framework;

namespace EchoProtocol.Tests
{
    /// <summary>How movement turns into footsteps and into the monster's roar.</summary>
    public class MovementSoundsTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void StandingStill_MakesNoSteps()
        {
            Assert.AreEqual(0f, MovementSounds.StepInterval(0f, 1.9f));
            Assert.AreEqual(0f, MovementSounds.StepInterval(0.3f, 1.9f));
            Assert.AreEqual(0f, MovementSounds.StepVolume(0.3f, 3.5f, 5.5f));
        }

        [Test]
        public void Sprinting_StepsFasterAndLouder_ThanWalking()
        {
            float walk = MovementSounds.StepInterval(3.5f, 1.9f);
            float sprint = MovementSounds.StepInterval(5.5f, 1.9f);
            Assert.Greater(walk, sprint);
            Assert.AreEqual(1.9f / 3.5f, walk, 1e-4f);

            Assert.Greater(MovementSounds.StepVolume(5.5f, 3.5f, 5.5f), MovementSounds.StepVolume(3.5f, 3.5f, 5.5f));
            Assert.AreEqual(1f, MovementSounds.StepVolume(9f, 3.5f, 5.5f), 1e-4f);
        }

        [Test]
        public void VeryFastMovement_NeverBecomesADrumRoll()
        {
            Assert.GreaterOrEqual(MovementSounds.StepInterval(40f, 1.9f), 0.22f);
        }

        [Test]
        public void TheMonster_RoarsOnce_WhenItStartsToHunt()
        {
            var roar = new MovementSounds.RoarTrigger();
            int roars = Run(roar, speed: 1.2f, seconds: 3f); // wandering
            Assert.AreEqual(0, roars, "a wandering monster does not roar");

            roars = Run(roar, speed: 6f, seconds: 5f); // a long hunt
            Assert.AreEqual(1, roars, "one roar when the hunt starts, not one every frame");
        }

        [Test]
        public void TheMonster_DoesNotRoarAgain_UntilItCalmedDown_AndTheCooldownPassed()
        {
            var roar = new MovementSounds.RoarTrigger();
            Assert.AreEqual(1, Run(roar, speed: 6f, seconds: 1f));

            // A short pause in the chase (an obstacle, a turn) is still the same hunt.
            Run(roar, speed: 2f, seconds: 1f);
            Assert.AreEqual(0, Run(roar, speed: 6f, seconds: 2f));

            // Calm for a while and past the cooldown: a new hunt roars again.
            Run(roar, speed: 1f, seconds: 9f);
            Assert.AreEqual(1, Run(roar, speed: 6f, seconds: 1f));
        }

        [Test]
        public void ABriefSpeedSpike_IsNotAHunt()
        {
            var roar = new MovementSounds.RoarTrigger();
            Run(roar, speed: 1f, seconds: 3f);
            Assert.AreEqual(0, Run(roar, speed: 6f, seconds: 0.1f), "a tenth of a second of speed is a stumble, not a hunt");
        }

        private static int Run(MovementSounds.RoarTrigger roar, float speed, float seconds)
        {
            int count = 0;
            for (float t = 0f; t < seconds; t += Dt)
            {
                if (roar.Update(speed, Dt)) count++;
            }
            return count;
        }
    }
}
