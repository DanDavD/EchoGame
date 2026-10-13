using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EchoProtocol.EditorTools
{
    /// <summary>
    /// One-click Windows builds. Menu: Echo Protocol > Build > Windows (Release | Development).
    /// Also callable headless for CI / smoke tests:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod EchoProtocol.EditorTools.EchoBuildPipeline.BuildWindowsRelease
    /// Output goes to Builds/Windows[-Dev]/EchoProtocol.exe (the Builds/ folder is git-ignored).
    /// </summary>
    public static class EchoBuildPipeline
    {
        private const string ExecutableName = "EchoProtocol.exe";

        [MenuItem("Echo Protocol/Build/Windows (Release)")]
        public static void BuildWindowsRelease() => BuildWindows(development: false);

        [MenuItem("Echo Protocol/Build/Windows (Development)")]
        public static void BuildWindowsDevelopment() => BuildWindows(development: true);

        [MenuItem("Echo Protocol/Build/Open Builds Folder")]
        public static void OpenBuildsFolder()
        {
            string path = Path.GetFullPath("Builds");
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static void BuildWindows(bool development)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                FailBuild("No enabled scenes in Build Settings.");
                return;
            }

            string outputDir = Path.GetFullPath(development ? "Builds/Windows-Dev" : "Builds/Windows");
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputDir, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            Debug.Log($"[Build] Starting {(development ? "development" : "release")} build -> {options.locationPathName}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                FailBuild($"Build {summary.result}: {summary.totalErrors} error(s), {summary.totalWarnings} warning(s). See the Console.");
                return;
            }

            CopySteamAppId(outputDir);
            Debug.Log($"[Build] Succeeded in {summary.totalTime.TotalSeconds:F0}s, {summary.totalSize / (1024f * 1024f):F1} MB -> {outputDir}");
        }

        // steam_appid.txt must sit next to the exe so Steamworks works when launching outside Steam.
        private static void CopySteamAppId(string outputDir)
        {
            const string source = "steam_appid.txt";
            if (File.Exists(source)) File.Copy(source, Path.Combine(outputDir, source), overwrite: true);
        }

        private static void FailBuild(string message)
        {
            Debug.LogError($"[Build] {message}");
            // Non-zero exit code when running headless.
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
