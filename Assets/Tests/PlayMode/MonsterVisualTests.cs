using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>The monster's procedural body: it builds itself, adds no physics and animates with its movement.</summary>
    public class MonsterVisualTests
    {
        private GameObject _monster;

        [SetUp]
        public void SetUp()
        {
            _monster = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _monster.name = "TestMonster";
            _monster.transform.position = new Vector3(0f, 1f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_monster != null) Object.Destroy(_monster);
        }

        [UnityTest]
        public IEnumerator Builds_ABodyWithEars_AndRetiresThePlaceholderCapsule()
        {
            _monster.AddComponent<MonsterVisual>();
            yield return null;

            Assert.IsFalse(_monster.GetComponent<MeshRenderer>().enabled, "the placeholder capsule must be hidden");
            Assert.IsNotNull(_monster.transform.Find("MonsterRig"), "rig");
            foreach (string part in new[] { "Torso", "Skull", "EarDishLeft", "EarDishRight", "ArmLeft", "ArmRight", "LegLeftMesh", "LegRightMesh" })
            {
                Assert.IsNotNull(FindDeep(_monster.transform, part), $"body part {part}");
            }
        }

        [UnityTest]
        public IEnumerator AddsNoColliders_ThePlaceholderKeepsDoingThePhysics()
        {
            _monster.AddComponent<MonsterVisual>();
            yield return null;

            var colliders = _monster.GetComponentsInChildren<Collider>();
            Assert.AreEqual(1, colliders.Length, "only the original capsule collider may exist");
            Assert.AreSame(_monster.GetComponent<Collider>(), colliders[0]);
        }

        [UnityTest]
        public IEnumerator Walking_SwingsTheLegs_AndStandingStillDoesNot()
        {
            _monster.AddComponent<MonsterVisual>();
            Transform leg = FindDeep(_monster.transform, "LegLeft");
            yield return null;

            // Still: the legs stay straight. (Waits in game time: with no graphics a frame can last microseconds.)
            yield return WaitGameSeconds(0.5f);
            Assert.AreEqual(0f, Mathf.Abs(Mathf.DeltaAngle(0f, leg.localEulerAngles.x)), 1f, "legs at rest");

            // Walking at 3 m/s: the legs swing.
            float maxSwing = 0f;
            float end = Time.time + 2f;
            while (Time.time < end)
            {
                _monster.transform.position += Vector3.forward * (3f * Time.deltaTime);
                yield return null;
                maxSwing = Mathf.Max(maxSwing, Mathf.Abs(Mathf.DeltaAngle(0f, leg.localEulerAngles.x)));
            }
            Assert.Greater(maxSwing, 10f, "the legs must swing while the monster moves");
        }

        [UnityTest]
        public IEnumerator Hunting_LeansTheBodyIntoTheChase()
        {
            _monster.AddComponent<MonsterVisual>();
            Transform hips = FindDeep(_monster.transform, "Hips");
            yield return null;

            float end = Time.time + 2f;
            while (Time.time < end)
            {
                _monster.transform.position += Vector3.forward * (6f * Time.deltaTime);
                yield return null;
            }

            Assert.Greater(Mathf.DeltaAngle(0f, hips.localEulerAngles.x), 10f, "a hunting monster leans forward");
        }

        private static IEnumerator WaitGameSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }
    }
}
