using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoProtocol.Networking.Core;

namespace EchoProtocol.EditorTools
{
    /// <summary>
    /// Puts the <c>NetworkRig</c> prefab into a scene. Scene builders call <see cref="Ensure"/> so a regenerated scene can
    /// never lose multiplayer again (that is exactly how it was lost before).
    /// </summary>
    public static class NetworkRigInstaller
    {
        /// <summary>True when <paramref name="scene"/> already contains a networking rig.</summary>
        public static bool HasRig(Scene scene)
        {
            return FindRig(scene) != null;
        }

        /// <summary>Returns the rig in <paramref name="scene"/>, instantiating the prefab first if there is none.</summary>
        public static GameObject Ensure(Scene scene)
        {
            GameObject existing = FindRig(scene);
            if (existing != null) return existing;

            GameObject prefab = NetworkRigBuilder.EnsurePrefab();
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = Vector3.zero;
            return instance;
        }

        [MenuItem("Echo Protocol/Build/Install Network Rig In Open Scene")]
        public static void InstallInOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            if (HasRig(scene))
            {
                Debug.Log($"[NetworkRigInstaller] '{scene.name}' already has a network rig.");
                return;
            }

            if (Ensure(scene) != null)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[NetworkRigInstaller] Installed the network rig in '{scene.name}'. Save the scene.");
            }
        }

        private static GameObject FindRig(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var bootstrap = root.GetComponentInChildren<NetworkBootstrap>(true);
                if (bootstrap != null) return bootstrap.gameObject;
            }
            return null;
        }
    }
}
