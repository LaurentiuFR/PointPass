using System;
using PointPass.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PointPass.Networking
{
    public static class NetworkDevelopmentBootstrap
    {
        public static bool TestEnabled => Debug.isDebugBuild && HasArgument("--pp-test");
        public static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        public static string Argument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        internal static bool TryStart()
        {
            bool enabled = HasArgument("--pp-network") || HasArgument("--pp-host") || HasArgument("--pp-client");
#if UNITY_EDITOR
            enabled |= UnityEditor.SessionState.GetBool("PointPass.NetworkDevelopment", false);
#endif
            if (!enabled) return false;
            MatchBootstrap.ClearOldSceneObjects(SceneManager.GetActiveScene());
            Physics2D.gravity = Vector2.zero;
            QualitySettings.antiAliasing = 4;
            MatchBootstrap.SetUpCamera();
            Application.targetFrameRate = 60;
            GameObject root = new GameObject("Multiplayer Development");
            NetworkSession session = root.AddComponent<NetworkSession>();
            root.AddComponent<NetworkDevelopmentPanel>().Init(session);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestEnabled) root.AddComponent<NetworkDevelopmentTestDriver>().Init(session);
#endif
            if (ushort.TryParse(Argument("--pp-port", "7777"), out ushort port) && port > 0)
            {
                if (HasArgument("--pp-host")) session.Host(port);
                else if (HasArgument("--pp-client")) session.Join(Argument("--pp-ip", "127.0.0.1"), port);
            }
            return true;
        }
    }
}
