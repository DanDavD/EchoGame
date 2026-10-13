using System.Reflection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace EchoProtocol.Core
{
    /// <summary>
    /// Keeps standalone sandbox play working when no FishNet NetworkManager is in the scene.
    /// <list type="bullet">
    /// <item>FishNet deactivates every placed NetworkObject in Start() unless a server/client is running,
    /// which removes the player (and its camera / AudioListener) and leaves a black "No cameras rendering" screen.
    /// Scene NetworkObjects are therefore flagged non-networked before their Start().</item>
    /// <item>NetworkBehaviour only receives its NetworkObject reference during network initialization, so reading
    /// IsSpawned / IsOwner offline throws a NullReferenceException. Every NetworkBehaviour is bound to a
    /// (never-spawned) NetworkObject so those properties safely return false.</item>
    /// </list>
    /// Does nothing in networked sessions.
    /// </summary>
    public static class OfflineSandboxGuard
    {
        private static readonly FieldInfo NetworkObjectCacheField =
            typeof(NetworkBehaviour).GetField("_networkObjectCache", BindingFlags.Instance | BindingFlags.NonPublic);

        private static NetworkObject _anchor;

        /// <summary>True when the scene runs without a FishNet NetworkManager.</summary>
        public static bool IsOffline { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _anchor = null;
            IsOffline = false;
        }

        // AfterSceneLoad runs after Awake but before the first Start(), which is when FishNet deactivates objects.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void DisableNetworkingWhenOffline()
        {
            if (Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include) != null) return;

            IsOffline = true;

            var networkObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include);
            for (int i = 0; i < networkObjects.Length; i++)
            {
                networkObjects[i].SetIsNetworked(false);
            }

            if (networkObjects.Length > 0) _anchor = networkObjects[0];

            var behaviours = Object.FindObjectsByType<NetworkBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Bind(behaviours[i]);
            }

            if (networkObjects.Length > 0)
            {
                Debug.Log($"[OfflineSandboxGuard] No NetworkManager in scene: running {networkObjects.Length} NetworkObject(s) / {behaviours.Length} NetworkBehaviour(s) in offline mode.");
            }
        }

        /// <summary>
        /// Binds every NetworkBehaviour under <paramref name="root"/> so its network properties are safe to read offline.
        /// Call after creating networked components at runtime in an offline session. No-op in networked sessions.
        /// </summary>
        public static void BindOffline(GameObject root)
        {
            if (!IsOffline || root == null) return;

            var behaviours = root.GetComponentsInChildren<NetworkBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Bind(behaviours[i]);
            }
        }

        private static void Bind(NetworkBehaviour behaviour)
        {
            if (behaviour == null || NetworkObjectCacheField == null) return;

            var nob = behaviour.GetComponentInParent<NetworkObject>(true);
            if (nob == null) nob = _anchor;
            if (nob == null) return;

            NetworkObjectCacheField.SetValue(behaviour, nob);
        }
    }
}
