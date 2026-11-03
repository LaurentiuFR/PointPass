using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PointPass.EditorTools
{
    public static class MultiplayerDevelopmentBuild
    {
        [MenuItem("PointPass/Multiplayer Development/Run in Editor")]
        public static void RunInEditor()
        {
            SessionState.SetBool("PointPass.NetworkDevelopment", true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("PointPass/Multiplayer Development/Use Offline Mode")]
        public static void UseOfflineMode()
        {
            SessionState.SetBool("PointPass.NetworkDevelopment", false);
            EditorApplication.isPlaying = false;
        }

        [MenuItem("PointPass/Multiplayer Development/Build Windows Test Player")]
        public static void BuildWindows()
        {
            string output = Path.GetFullPath("Builds/Multiplayer/PointPass.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/SampleScene.unity" },
                locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Directory.CreateDirectory("Logs/NetworkVerification");
            File.WriteAllText("Logs/NetworkVerification/build.txt", report.summary.result + "\n" + output + "\nErrors: " + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Windows multiplayer test build failed.");
            Debug.Log("PointPass Windows test build: " + output);
        }
    }
}
