using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EchoProtocol.Visuals
{
    /// <summary>
    /// Settings every gameplay camera needs for the look: post-processing on (URP leaves it off by default), HDR so the
    /// echo glow can bloom, SMAA against the hard geometry edges and dithering against banding in the dark gradients.
    /// </summary>
    public static class CameraPolish
    {
        /// <summary>Applies the look settings to <paramref name="camera"/>. Safe to call repeatedly.</summary>
        public static void Apply(Camera camera)
        {
            if (camera == null) return;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            data.dithering = true;
            data.stopNaN = true;
            camera.allowHDR = true;
        }
    }
}
