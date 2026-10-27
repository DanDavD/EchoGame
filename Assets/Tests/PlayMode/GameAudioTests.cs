using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>The cave ambience starts on its own, and footsteps follow movement.</summary>
    public class GameAudioTests
    {
        private GameObject _walker;

        [TearDown]
        public void TearDown()
        {
            if (_walker != null) Object.Destroy(_walker);
        }

        [UnityTest]
        public IEnumerator TheCaveAmbience_StartsByItself_AndKeepsItsRoomTonePlaying()
        {
            yield return null;
            Assert.IsNotNull(CaveAmbience.Instance, "the ambience boots after the first scene loads");

            AudioSource roomTone = CaveAmbience.Instance.GetComponent<AudioSource>();
            Assert.IsNotNull(roomTone);
            Assert.IsTrue(roomTone.loop);
            Assert.IsNotNull(roomTone.clip, "the room tone clip is generated");
            Assert.AreEqual(0f, roomTone.spatialBlend, "the room tone is the air of the cave, heard everywhere");
        }

        [UnityTest]
        public IEnumerator Walking_PlaysFootsteps_AndStandingStillDoesNot()
        {
            _walker = new GameObject("Walker");
            var steps = _walker.AddComponent<FootstepAudio>();
            Assert.IsNotNull(_walker.GetComponentInChildren<AudioSource>(), "the footsteps have their own 3D source");

            // Still for a while: no step. (Waits in game time: with no graphics a frame can last microseconds.)
            yield return WaitGameSeconds(0.6f);
            Assert.AreEqual(0, steps.StepsPlayed, "standing still makes no sound");

            float end = Time.time + 2f;
            while (Time.time < end)
            {
                _walker.transform.position += Vector3.forward * (3.5f * Time.deltaTime);
                yield return null;
            }
            // 2 s at 3.5 m/s with a 1.9 m stride is about 3 or 4 steps.
            Assert.That(steps.StepsPlayed, Is.InRange(2, 6), "walking at 3.5 m/s plays footsteps at a walking pace");
        }

        [UnityTest]
        public IEnumerator Walls_CountOnlySolidGeometry()
        {
            var a = new Vector3(100f, 1f, 0f);
            var b = new Vector3(110f, 1f, 0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(105f, 1f, 0f);
            var player = new GameObject("TeammateInTheWay", typeof(CharacterController));
            player.transform.position = new Vector3(103f, 1f, 0f);
            yield return null;
            Physics.SyncTransforms(); // new colliders reach the physics scene at the next step; queries need them now
            try
            {
                Assert.AreEqual(1, SoundOcclusion.CountWalls(a, b, null), "a wall counts, a player standing in the way does not");
                Assert.AreEqual(0, SoundOcclusion.CountWalls(a, b, wall.transform), "a sound's own body never muffles it");

                var second = GameObject.CreatePrimitive(PrimitiveType.Cube);
                second.transform.position = new Vector3(107f, 1f, 0f);
                Physics.SyncTransforms();
                Assert.AreEqual(2, SoundOcclusion.CountWalls(a, b, null), "every wall in between counts");
                Object.Destroy(second);
            }
            finally
            {
                Object.Destroy(wall);
                Object.Destroy(player);
            }
        }

        [UnityTest]
        public IEnumerator ASoundBehindAWall_IsMuffled_AndClearInTheOpen()
        {
            var listener = new GameObject("Listener", typeof(AudioListener));
            listener.transform.position = new Vector3(200f, 1f, 0f);
            var silenced = new System.Collections.Generic.List<AudioListener>();
            foreach (AudioListener other in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
            {
                if (other.gameObject == listener || other.gameObject == CaveAmbience.Instance.gameObject) continue;
                other.enabled = false;
                silenced.Add(other);
            }

            _walker = new GameObject("Sound", typeof(AudioSource));
            _walker.transform.position = new Vector3(212f, 1f, 0f);
            var occlusion = _walker.AddComponent<SoundOcclusion>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(206f, 1f, 0f);
            wall.transform.localScale = new Vector3(0.5f, 4f, 4f);
            try
            {
                yield return WaitGameSeconds(1f);
                Assert.AreEqual(1, occlusion.Walls);
                Assert.Less(occlusion.Cutoff, 3000f, "behind a wall the sound is muffled");

                Object.Destroy(wall);
                yield return WaitGameSeconds(1f);
                Assert.AreEqual(0, occlusion.Walls);
                Assert.Greater(occlusion.Cutoff, 15000f, "in the open it is clear again");
            }
            finally
            {
                if (wall != null) Object.Destroy(wall);
                Object.Destroy(listener);
                foreach (AudioListener other in silenced) if (other != null) other.enabled = true;
            }
        }

        [Test]
        public void MoreWalls_MuffleMore()
        {
            Assert.AreEqual(SoundOcclusion.ClearCutoff, SoundOcclusion.CutoffFor(0));
            Assert.Greater(SoundOcclusion.CutoffFor(1), SoundOcclusion.CutoffFor(2));
            Assert.AreEqual(SoundOcclusion.CutoffFor(2), SoundOcclusion.CutoffFor(5));
        }

        private static IEnumerator WaitGameSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }
    }
}
