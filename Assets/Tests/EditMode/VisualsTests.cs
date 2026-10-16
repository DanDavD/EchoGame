using EchoProtocol.Visuals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EchoProtocol.Tests
{
    public class VisualsTests
    {
        [Test]
        public void LookProfile_HasEveryEffectOfTheLook_ActiveAndOverridden()
        {
            VolumeProfile profile = PostProcessingRig.CreateProfile();
            try
            {
                AssertActive<Tonemapping>(profile);
                AssertActive<Bloom>(profile);
                AssertActive<Vignette>(profile);
                AssertActive<FilmGrain>(profile);
                AssertActive<ChromaticAberration>(profile);
                AssertActive<ColorAdjustments>(profile);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static void AssertActive<T>(VolumeProfile profile) where T : VolumeComponent
        {
            Assert.IsTrue(profile.TryGet(out T component), $"{typeof(T).Name} is missing from the look");
            Assert.IsTrue(component.active, $"{typeof(T).Name} is not active");
        }

        [Test]
        public void LookProfile_BloomIsStrongEnoughToMakeTheEchoEdgesGlow()
        {
            VolumeProfile profile = PostProcessingRig.CreateProfile();
            try
            {
                profile.TryGet(out Bloom bloom);
                Assert.IsTrue(bloom.intensity.overrideState);
                Assert.Greater(bloom.intensity.value, 0.3f);
                Assert.Less(bloom.threshold.value, 1f, "HDR echo edges are brighter than 1, so the threshold must sit below that");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CameraPolish_TurnsOnPostProcessingHdrAndAntialiasing()
        {
            var go = new GameObject("TestCamera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.allowHDR = false;

                CameraPolish.Apply(camera);

                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                Assert.IsTrue(data.renderPostProcessing, "post-processing");
                Assert.IsTrue(camera.allowHDR, "HDR");
                Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, data.antialiasing);
                Assert.IsTrue(data.dithering, "dithering");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CameraPolish_IgnoresNull()
        {
            Assert.DoesNotThrow(() => CameraPolish.Apply(null));
        }

        [Test]
        public void PhotoFlag_IsDetected_WithAndWithoutAFolder()
        {
            Assert.IsFalse(PhotoMode.Requested(null, out _));
            Assert.IsFalse(PhotoMode.Requested(new[] { "game.exe", "-echo-host" }, out _));

            Assert.IsTrue(PhotoMode.Requested(new[] { "game.exe", "-echo-photo" }, out string none));
            Assert.IsNull(none);

            Assert.IsTrue(PhotoMode.Requested(new[] { "-echo-photo", "C:\\shots" }, out string folder));
            Assert.AreEqual("C:\\shots", folder);

            Assert.IsTrue(PhotoMode.Requested(new[] { "-echo-photo", "-screen-width", "960" }, out string flagNext));
            Assert.IsNull(flagNext, "the next flag is not a folder");
        }

        [Test]
        public void AimAt_PointsTheCameraAtTheTarget()
        {
            PhotoMode.AimAt(Vector3.zero, new Vector3(0f, 0f, 10f), out float yaw, out float pitch);
            Assert.AreEqual(0f, yaw, 1e-3f);
            Assert.AreEqual(0f, pitch, 1e-3f);

            PhotoMode.AimAt(Vector3.zero, new Vector3(10f, 0f, 0f), out yaw, out _);
            Assert.AreEqual(90f, yaw, 1e-3f, "east is +90 degrees");

            PhotoMode.AimAt(Vector3.zero, new Vector3(0f, 10f, 10f), out _, out pitch);
            Assert.AreEqual(-45f, pitch, 1e-3f, "looking up is a negative pitch");

            PhotoMode.AimAt(Vector3.zero, new Vector3(0f, -10f, 10f), out _, out pitch);
            Assert.AreEqual(45f, pitch, 1e-3f, "looking down is a positive pitch");
        }

        [Test]
        public void AimAt_StraightUpOrDownDoesNotProduceNaN()
        {
            PhotoMode.AimAt(Vector3.zero, Vector3.up * 5f, out float yaw, out float pitch);
            Assert.IsFalse(float.IsNaN(yaw) || float.IsNaN(pitch));
        }
    }
}
