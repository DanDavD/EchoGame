using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests
{
    public class NoiseRegistryTests
    {
        private sealed class FakeNoise : INoiseSource
        {
            public Transform Owner => null;
            public float NoiseLevel01 => 1f;
        }

        private FakeNoise _a;
        private FakeNoise _b;

        [SetUp]
        public void SetUp()
        {
            _a = new FakeNoise();
            _b = new FakeNoise();
        }

        [TearDown]
        public void TearDown()
        {
            NoiseRegistry.Unregister(_a);
            NoiseRegistry.Unregister(_b);
        }

        [Test]
        public void Register_AddsSource()
        {
            int before = NoiseRegistry.Sources.Count;
            NoiseRegistry.Register(_a);
            Assert.AreEqual(before + 1, NoiseRegistry.Sources.Count);
            Assert.IsTrue(NoiseRegistry.Sources.Contains(_a));
        }

        [Test]
        public void Register_SameSourceTwice_IsStoredOnce()
        {
            int before = NoiseRegistry.Sources.Count;
            NoiseRegistry.Register(_a);
            NoiseRegistry.Register(_a);
            Assert.AreEqual(before + 1, NoiseRegistry.Sources.Count);
        }

        [Test]
        public void Register_Null_IsIgnored()
        {
            int before = NoiseRegistry.Sources.Count;
            NoiseRegistry.Register(null);
            Assert.AreEqual(before, NoiseRegistry.Sources.Count);
        }

        [Test]
        public void Unregister_RemovesOnlyThatSource()
        {
            NoiseRegistry.Register(_a);
            NoiseRegistry.Register(_b);
            int withBoth = NoiseRegistry.Sources.Count;

            NoiseRegistry.Unregister(_a);

            Assert.AreEqual(withBoth - 1, NoiseRegistry.Sources.Count);
            Assert.IsFalse(NoiseRegistry.Sources.Contains(_a));
            Assert.IsTrue(NoiseRegistry.Sources.Contains(_b));
        }

        [Test]
        public void Unregister_UnknownSource_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => NoiseRegistry.Unregister(_a));
        }
    }
}
