using System.IO;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.EditorTools
{
    /// <summary>
    /// The single place that defines how every EchoReveal material looks (surface pattern, tint, edge glow, emission).
    /// Menu: Echo Protocol > Build > Materials. The level builders call <see cref="ApplyLook"/> so a rebuilt scene never
    /// brings the old placeholder look back, and a test checks the result.
    /// </summary>
    public static class EchoMaterials
    {
        public const string ShaderPath = "Assets/Shaders/EchoReveal.shader";
        private const string Folder = "Assets/Art/Materials/";

        /// <summary>Surface pattern ids understood by the EchoReveal shader.</summary>
        public const float PatternBricks = 0f;
        public const float PatternSlabs = 1f;
        public const float PatternRock = 2f;
        public const float PatternFlesh = 3f;

        public readonly struct Look
        {
            public readonly string Name;
            public readonly float Pattern;
            public readonly Color Tint;
            public readonly Color Edge;
            public readonly Color Emission;
            public readonly float PulseSpeed;

            public Look(string name, float pattern, Color tint, Color edge, Color emission = default, float pulseSpeed = 0f)
            {
                Name = name;
                Pattern = pattern;
                Tint = tint;
                Edge = edge;
                Emission = emission;
                PulseSpeed = pulseSpeed;
            }
        }

        private static readonly Color Cyan = new Color(0.3f, 1.35f, 1.5f, 1f);
        private static readonly Color Amber = new Color(1.5f, 1.2f, 0.3f, 1f);
        private static readonly Color Blood = new Color(1.6f, 0.25f, 0.2f, 1f);

        /// <summary>Every material of the game and its look.</summary>
        public static readonly Look[] Looks =
        {
            new Look("Echo_Wall", PatternBricks, new Color(0.62f, 0.56f, 0.58f), Cyan),
            new Look("Echo_Floor", PatternSlabs, new Color(0.5f, 0.52f, 0.6f), Cyan),
            new Look("Echo_Prop", PatternRock, new Color(0.66f, 0.62f, 0.58f), Cyan),
            new Look("Echo_Enemy", PatternFlesh, Color.white, Blood, new Color(0.3f, 0.02f, 0.02f), 2.4f),
            new Look("Echo_Relic", PatternRock, new Color(1f, 0.78f, 0.3f), Amber, new Color(0.9f, 0.55f, 0.08f), 2f),
            new Look("Echo_Player", PatternRock, new Color(0.4f, 0.6f, 1f), Cyan)
        };

        /// <summary>Applies the look to every material of the game (creating the missing ones).</summary>
        [MenuItem("Echo Protocol/Build/Materials")]
        public static void ApplyLook()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[EchoMaterials] Shader not found at {ShaderPath}.");
                return;
            }

            Directory.CreateDirectory(Folder.TrimEnd('/'));
            foreach (Look look in Looks)
            {
                string path = $"{Folder}{look.Name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool isNew = material == null;
                if (isNew) material = new Material(shader);

                material.shader = shader;
                if (isNew) AssetDatabase.CreateAsset(material, path);
                ApplyTo(material);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EchoMaterials] Applied the look to {Looks.Length} materials.");
        }

        /// <summary>Sets the look of <paramref name="material"/> if its name is one of the game's materials. Returns true when it was.</summary>
        public static bool ApplyTo(Material material)
        {
            if (material == null) return false;

            foreach (Look look in Looks)
            {
                if (look.Name != material.name) continue;

                material.SetFloat("_Pattern", look.Pattern);
                material.SetFloat("_TexScale", 1f);
                material.SetColor("_Tint", look.Tint);
                material.SetColor("_EdgeColor", look.Edge);
                material.SetColor("_Emission", look.Emission);
                material.SetFloat("_PulseSpeed", look.PulseSpeed);
                material.SetFloat("_EmissionInDark", 0f);
                EditorUtility.SetDirty(material);
                return true;
            }
            return false;
        }
    }
}
