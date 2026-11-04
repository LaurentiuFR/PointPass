using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PointPass.EditorTools
{
    /// <summary>Reproducible pilot client and presentation-free Linux dedicated-server builds.</summary>
    public static class OnlinePilotBuild
    {
        private const string ServerScene = "Assets/_Project/Scenes/OnlinePilotServer.unity";
        private const string Presentation = "Assets/_Project/Resources";
        private const string ExcludedPresentation = "Assets/_Project/ServerBuildExcludedPresentation";

        [MenuItem("PointPass/Online Pilot/Build Linux Dedicated Server")]
        public static void BuildLinuxServer()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            RecoverPresentation();
            EnsureServerScene();
            bool previousOptimizations = PlayerSettings.dedicatedServerOptimizations;
            ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Server);
            bool moved = false;
            try
            {
                // Resources are otherwise protected from normal dedicated-server asset stripping.
                // Move through AssetDatabase so Unity preserves every asset GUID and meta file.
                if (AssetDatabase.IsValidFolder(Presentation))
                {
                    string error = AssetDatabase.MoveAsset(Presentation, ExcludedPresentation);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    moved = true;
                }
                PlayerSettings.dedicatedServerOptimizations = true;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ServerScene },
                    locationPathName = OutputPath("Builds/OnlinePilot/Server/PointPassServer.x86_64"),
                    target = BuildTarget.StandaloneLinux64,
                    subtarget = (int)StandaloneBuildSubtarget.Server,
                    options = BuildOptions.DetailedBuildReport | BuildOptions.StrictMode
                });
                WriteReport(report, "server-build.json", verifyPresentation: true);
            }
            finally
            {
                PlayerSettings.dedicatedServerOptimizations = previousOptimizations;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, previousBackend);
                if (moved) RecoverPresentation();
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("PointPass/Online Pilot/Build Windows Client")]
        public static void BuildWindowsClient()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            RecoverPresentation();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/SampleScene.unity" },
                locationPathName = OutputPath("Builds/OnlinePilot/Client/PointPass.exe"),
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.Development | BuildOptions.DetailedBuildReport | BuildOptions.StrictMode
            });
            WriteReport(report, "client-build.json", verifyPresentation: false);
        }

        [MenuItem("PointPass/Online Pilot/Restore Presentation After Interrupted Build")]
        public static void RecoverPresentation()
        {
            if (!AssetDatabase.IsValidFolder(ExcludedPresentation)) return;
            if (AssetDatabase.IsValidFolder(Presentation))
                throw new IOException("Both presentation folders exist. Resolve the interrupted build before continuing.");
            string error = AssetDatabase.MoveAsset(ExcludedPresentation, Presentation);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }

        private static void EnsureServerScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ServerScene) != null) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (!EditorSceneManager.SaveScene(scene, ServerScene)) throw new IOException("Could not save the empty server scene.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static string OutputPath(string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-pointPassBuildPath");
            string output = Path.GetFullPath(index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : fallback);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            return output;
        }

        private static void WriteReport(BuildReport report, string file, bool verifyPresentation)
        {
            string[] assets = report.packedAssets.SelectMany(asset => asset.contents)
                .Select(content => content.sourceAssetPath).Where(path => !string.IsNullOrEmpty(path))
                .Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] forbidden = assets.Where(path => path.StartsWith(Presentation + "/", StringComparison.Ordinal)
                || path.StartsWith(ExcludedPresentation + "/", StringComparison.Ordinal)
                || path.StartsWith("Assets/_Project/Art/", StringComparison.Ordinal)
                || path.StartsWith("Assets/_Project/Audio/", StringComparison.Ordinal)).ToArray();
            Directory.CreateDirectory("Logs/OnlinePilotVerification");
            File.WriteAllText("Logs/OnlinePilotVerification/" + file, JsonUtility.ToJson(new ReportData
            {
                result = report.summary.result.ToString(), path = report.summary.outputPath,
                bytes = report.summary.totalSize, errors = report.summary.totalErrors,
                presentationFree = forbidden.Length == 0, packedAssets = assets, forbiddenAssets = forbidden
            }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Pilot build failed: " + report.summary.result);
            if (verifyPresentation && forbidden.Length != 0)
                throw new InvalidOperationException("Presentation assets were included in the dedicated-server build; inspect the build report.");
            Debug.Log("PointPass pilot build succeeded: " + report.summary.outputPath);
        }

        [Serializable]
        private sealed class ReportData
        {
            public string result;
            public string path;
            public ulong bytes;
            public int errors;
            public bool presentationFree;
            public string[] packedAssets;
            public string[] forbiddenAssets;
        }
    }
}
