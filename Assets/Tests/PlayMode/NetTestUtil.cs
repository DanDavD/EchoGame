using System;
using System.Collections;
using System.Collections.Generic;
using EchoProtocol.Networking.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EchoProtocol.Tests
{
    /// <summary>Shared helpers for tests that load a scene, host a real local server and check what the network spawned.</summary>
    internal static class NetTestUtil
    {
        public static IEnumerator LoadScene(string sceneName)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            // Let Awake/Start run and physics settle.
            for (int i = 0; i < 5; i++) yield return null;
            yield return new WaitForFixedUpdate();
        }

        /// <summary>Waits (in frames) until <paramref name="condition"/> holds; fails the test with <paramref name="what"/> on timeout.</summary>
        public static IEnumerator WaitUntil(Func<bool> condition, string what, float timeoutSeconds = 20f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"Timed out after {timeoutSeconds:F0}s waiting for: {what}");
                yield return null;
            }
        }

        public static NetworkBootstrap FindBootstrap()
        {
            return UnityEngine.Object.FindAnyObjectByType<NetworkBootstrap>(FindObjectsInactive.Include);
        }

        public static PlayerController FindLocalPlayer()
        {
            foreach (PlayerController player in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude))
            {
                if (player.IsSpawned && player.IsOwner) return player;
            }
            return null;
        }

        /// <summary>Stops networking and removes the persistent rig so the next test starts from a clean slate.</summary>
        public static IEnumerator DestroyRig()
        {
            NetworkBootstrap bootstrap = FindBootstrap();
            if (bootstrap == null) yield break;

            bootstrap.Disconnect();
            for (int i = 0; i < 10; i++) yield return null;
            UnityEngine.Object.Destroy(bootstrap.gameObject);
            for (int i = 0; i < 3; i++) yield return null;
        }
    }

    /// <summary>
    /// Collects every error, exception and assert logged while a test runs. Failing the test on those is strict about
    /// the game's own code but tolerates Steam: the game must keep working on a machine without the Steam client.
    /// </summary>
    internal sealed class ErrorCollector : IDisposable
    {
        private readonly List<string> _errors = new List<string>();

        public ErrorCollector()
        {
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
        }

        public IReadOnlyList<string> Errors => _errors;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition.IndexOf("steam", StringComparison.OrdinalIgnoreCase) >= 0) return;
            _errors.Add($"[{type}] {condition}");
        }

        public void Dispose()
        {
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = false;
        }
    }
}
