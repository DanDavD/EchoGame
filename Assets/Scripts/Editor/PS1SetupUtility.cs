using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Linq;

[InitializeOnLoad]
public static class PS1SetupUtility
{
    static PS1SetupUtility()
    {
        EditorApplication.delayCall += AutoSetup;
    }

    private static void AutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += AutoSetup;
            return;
        }

        // 1. Create the PS1 Material if it doesn't exist
        string matPath = "Assets/Art/Materials/PS1_PostProcess_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Hidden/EchoProtocol/PS1PostProcess");
            if (shader == null) return; // Wait for shader to compile

            mat = new Material(shader);
            System.IO.Directory.CreateDirectory("Assets/Art/Materials");
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log("[PS1 Setup] Created Post-Process Material.");
        }

        // 2. Add Feature to PC_Renderer
        string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData PC_Renderer");
        if (guids.Length == 0) return;

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (rendererData != null && !rendererData.rendererFeatures.OfType<PS1RetroFeature>().Any())
        {
            var feature = ScriptableObject.CreateInstance<PS1RetroFeature>();
            feature.name = "PS1 Retro Feature";
            feature.settings.postProcessMaterial = mat;
            
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
            
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            Debug.Log("[PS1 Setup] Added PS1 Retro Feature to URP PC_Renderer automatically.");
        }
    }
}
