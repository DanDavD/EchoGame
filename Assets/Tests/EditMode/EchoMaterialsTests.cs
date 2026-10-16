using EchoProtocol.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Tests
{
    /// <summary>The look of every material is defined in code; the assets in the repo must match it.</summary>
    public class EchoMaterialsTests
    {
        [Test]
        public void EveryGameMaterial_UsesTheEchoShaderAndItsDefinedPattern()
        {
            foreach (EchoMaterials.Look look in EchoMaterials.Looks)
            {
                string path = $"Assets/Art/Materials/{look.Name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                Assert.IsNotNull(material, $"{path} is missing. Run Echo Protocol > Build > Materials.");
                Assert.AreEqual("EchoProtocol/EchoReveal", material.shader.name, $"{look.Name} shader");
                Assert.AreEqual(look.Pattern, material.GetFloat("_Pattern"), $"{look.Name} pattern");
                Assert.AreEqual(1f, material.GetFloat("_TexScale"), 1e-4f, $"{look.Name} pattern scale");
            }
        }

        [Test]
        public void ApplyTo_IgnoresMaterialsThatAreNotPartOfTheGame()
        {
            var stranger = new Material(AssetDatabase.LoadAssetAtPath<Shader>(EchoMaterials.ShaderPath)) { name = "SomethingElse" };
            try
            {
                Assert.IsFalse(EchoMaterials.ApplyTo(stranger));
                Assert.IsFalse(EchoMaterials.ApplyTo(null));
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void TheShader_CompilesAndOffersTheLookProperties()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(EchoMaterials.ShaderPath);
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported, "the shader must compile on this platform");

            foreach (string property in new[] { "_Pattern", "_Tint", "_EdgeColor", "_Emission", "_TexScale" })
            {
                Assert.GreaterOrEqual(shader.FindPropertyIndex(property), 0, $"shader property {property}");
            }
        }
    }
}
