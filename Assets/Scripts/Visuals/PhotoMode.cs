using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using EchoProtocol.Altar;
using EchoProtocol.Extraction;
using EchoProtocol.Level;

namespace EchoProtocol.Visuals
{
    /// <summary>
    /// Takes a fixed set of screenshots of the level and quits: <c>EchoProtocol.exe -echo-photo [folder]</c>.
    /// Used to review the look of the game with real frames instead of guesses, and to produce press material.
    /// The shots come from the real player camera, so post-processing, HUD and shaders are exactly what a player sees.
    /// Together with <c>-echo-host</c> (implied by the launch options) it needs no input.
    /// </summary>
    public sealed class PhotoMode : MonoBehaviour
    {
        public const string Flag = "-echo-photo";

        /// <summary>One screenshot: where to stand, what to look at and how much of the level the echo reveals.</summary>
        public readonly struct Shot
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 LookAt;
            public readonly float EchoRadius;
            public readonly bool FullLight;

            public Shot(string name, Vector3 position, Vector3 lookAt, float echoRadius, bool fullLight = false)
            {
                Name = name;
                Position = position;
                LookAt = lookAt;
                EchoRadius = echoRadius;
                FullLight = fullLight;
            }
        }

        private string _folder;

        /// <summary>
        /// True when the photo flag is present. <paramref name="folder"/> is the argument after the flag when it does not
        /// look like another flag, otherwise null (the default folder is used).
        /// </summary>
        public static bool Requested(string[] args, out string folder)
        {
            folder = null;
            if (args == null) return false;

            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], Flag, System.StringComparison.OrdinalIgnoreCase)) continue;

                if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]) && !args[i + 1].StartsWith("-"))
                {
                    folder = args[i + 1];
                }
                return true;
            }
            return false;
        }

        /// <summary>Yaw and pitch (degrees) that make a camera at <paramref name="from"/> look at <paramref name="to"/>.</summary>
        public static void AimAt(Vector3 from, Vector3 to, out float yaw, out float pitch)
        {
            Vector3 direction = to - from;
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            yaw = flat.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Atan2(direction.y, Mathf.Max(flat.magnitude, 1e-4f)) * Mathf.Rad2Deg;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Requested(System.Environment.GetCommandLineArgs(), out string folder)) return;

            var go = new GameObject("[PhotoMode]");
            DontDestroyOnLoad(go);
            go.AddComponent<PhotoMode>()._folder = folder;
        }

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(_folder)) _folder = Path.Combine(Application.persistentDataPath, "photos");
            Directory.CreateDirectory(_folder);

            PlayerController player = null;
            float timeout = Time.realtimeSinceStartup + 40f;
            while (player == null && Time.realtimeSinceStartup < timeout)
            {
                player = FindLocalPlayer();
                yield return null;
            }

            if (player == null)
            {
                Debug.LogError("[PhotoMode] No local player appeared; no photos taken.");
                Application.Quit(1);
                yield break;
            }

            yield return new WaitForSeconds(1.5f);

            DebugOverlay.Visible = false;
            EnemyBrain.SetFrozen(true);

            var vision = player.GetComponent<EchoVisionController>();
            List<Shot> shots = BuildShots();
            Debug.Log($"[PhotoMode] Taking {shots.Count} photos into {_folder}");

            for (int i = 0; i < shots.Count; i++)
            {
                Shot shot = shots[i];
                AimAt(shot.Position, shot.LookAt, out float yaw, out float pitch);
                player.SetView(shot.Position, yaw, pitch);
                if (vision != null) vision.ForceRadius(shot.EchoRadius);
                EchoShaderPublisher.SetFullIllumination(shot.FullLight);
                if (shot.Name == "portal_open") EscapePortal.ActivateAll();

                // Let the light grow and the camera settle.
                yield return new WaitForSeconds(1.2f);

                string path = Path.Combine(_folder, $"{i + 1:00}_{shot.Name}.png");
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSeconds(0.6f);
                Debug.Log($"[PhotoMode] Saved {path}");
            }

            EchoShaderPublisher.SetFullIllumination(false);
            Debug.Log("[PhotoMode] Done.");
            Application.Quit();
        }

        private static PlayerController FindLocalPlayer()
        {
            foreach (PlayerController candidate in PlayerController.ActivePlayers)
            {
                if (candidate != null && (!candidate.IsSpawned || candidate.IsOwner)) return candidate;
            }
            return null;
        }

        /// <summary>The standard set of shots, placed from the level's own markers.</summary>
        private static List<Shot> BuildShots()
        {
            var shots = new List<Shot>();
            SpawnPoint start = SpawnRegistry.First(SpawnKind.Player);
            Vector3 startPos = start != null ? start.transform.position : Vector3.up;
            Vector3 forward = start != null ? start.transform.forward : Vector3.forward;
            Vector3 ahead = startPos + forward * 12f;

            shots.Add(new Shot("spawn_dark", startPos, ahead, 0f));
            shots.Add(new Shot("spawn_whisper", startPos, ahead, 3f));
            shots.Add(new Shot("spawn_talk", startPos, ahead, 12f));
            shots.Add(new Shot("corridor_scream", startPos, ahead, 60f));

            var altar = Object.FindAnyObjectByType<AltarReceptacle>();
            if (altar != null) shots.Add(Viewpoint("altar_talk", altar.transform.position + Vector3.up * 1.4f, 7f, 14f, false));

            SpawnPoint shrine = SpawnRegistry.First(SpawnKind.Relic);
            if (shrine != null) shots.Add(Viewpoint("shrine_whisper", shrine.transform.position, 3.5f, 6f, false));

            var portal = Object.FindAnyObjectByType<EscapePortal>();
            if (portal != null) shots.Add(Viewpoint("portal_scream", portal.transform.position, 8f, 40f, false));

            var monster = Object.FindAnyObjectByType<EnemyBrain>();
            if (monster != null) shots.Add(Viewpoint("monster_scream", monster.transform.position + Vector3.up * 0.5f, 7f, 25f, false));
            if (monster != null) shots.Add(Viewpoint("monster_closeup", monster.transform.position + Vector3.up * 0.9f, 3.8f, 10f, false));

            if (altar != null) shots.Add(Viewpoint("altar_full_light", altar.transform.position + Vector3.up * 1.4f, 9f, 0f, true));
            if (portal != null) shots.Add(Viewpoint("portal_open", portal.transform.position, 7f, 0f, false));
            return shots;
        }

        /// <summary>
        /// A shot of <paramref name="target"/> from about <paramref name="distance"/> metres away, standing somewhere walkable
        /// with a clear line of sight (so the camera never ends up inside a wall).
        /// </summary>
        private static Shot Viewpoint(string name, Vector3 target, float distance, float radius, bool fullLight)
        {
            for (float d = distance; d >= 2f; d -= 1f)
            {
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * 22.5f * Mathf.Deg2Rad;
                    Vector3 candidate = target + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * d;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;

                    Vector3 stand = new Vector3(hit.position.x, 1.1f, hit.position.z);
                    Vector3 eye = stand + Vector3.up * 0.7f;
                    if (Physics.Linecast(eye, target, out RaycastHit blocked) && Vector3.Distance(blocked.point, target) > 1.6f) continue;

                    return new Shot(name, stand, target, radius, fullLight);
                }
            }
            return new Shot(name, target + Vector3.back * distance + Vector3.up, target, radius, fullLight);
        }
    }
}
