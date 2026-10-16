using UnityEngine;

namespace EchoProtocol.Visuals
{
    /// <summary>
    /// Specks of dust drifting around the player. They are drawn with the EchoReveal shader, so they only exist where the
    /// echo lights the world: a scream fills the air with floating dust, silence leaves only darkness. Purely cosmetic.
    /// </summary>
    public sealed class DustMotes : MonoBehaviour
    {
        private const string ShaderName = "EchoProtocol/EchoReveal";

        private static Mesh _cube;

        /// <summary>Adds the dust to a camera (once). Safe to call repeatedly.</summary>
        public static void Attach(Camera camera)
        {
            if (camera == null || camera.GetComponentInChildren<DustMotes>(true) != null) return;

            var go = new GameObject("DustMotes");
            go.transform.SetParent(camera.transform, false);
            go.AddComponent<DustMotes>();
        }

        private void Awake()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) return; // never block the game for a cosmetic effect

            var material = new Material(shader) { name = "EchoDust", hideFlags = HideFlags.DontSave };
            material.SetFloat("_Pattern", 2f);
            material.SetColor("_Tint", new Color(0.9f, 0.95f, 1f));
            material.SetColor("_EdgeColor", new Color(0.3f, 1.35f, 1.5f));

            var system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.maxParticles = 260;
            main.startLifetime = 9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.04f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 30f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 5f, 18f);

            // A slow, wandering drift, and a fade in and out (the shader ignores vertex colour, so the size does it).
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.15f;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.18f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

            var particleRenderer = GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = CubeMesh();
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            system.Play();
        }

        // Primitive meshes are the one mesh source that is always present in a player build.
        private static Mesh CubeMesh()
        {
            if (_cube != null) return _cube;

            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            return _cube;
        }
    }
}
