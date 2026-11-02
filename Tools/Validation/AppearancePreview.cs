using System;
using System.IO;
using System.Reflection;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.Validation
{
    /// <summary>Renders actual game code/art in a disposable editor scene, without saving it.</summary>
    public static class AppearancePreview
    {
        private static void Call(object target, string name, params object[] arguments)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, arguments);
        }

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (string path in AssetDatabase.GetAllAssetPaths())
                {
                    if (!path.StartsWith("Assets/Resources/Art/") || !path.EndsWith(".png")) continue;
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = path.EndsWith("_polished.png") || path.EndsWith("_rigged.png")
                        || path.EndsWith("arm_runner.png") ? 512 : 2048;
                    importer.SaveAndReimport();
                }
                typeof(MatchBootstrap).GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    Call(component, "Awake");

                MatchManager manager = UnityEngine.Object.FindFirstObjectByType<MatchManager>();
                PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                Ball ball = UnityEngine.Object.FindFirstObjectByType<Ball>();
                MatchHud hud = UnityEngine.Object.FindFirstObjectByType<MatchHud>();
                player.ResetToKickoff();
                player.Body.position = new Vector2(-1.8f, 0);
                player.transform.position = player.Body.position;
                player.transform.rotation = Quaternion.Euler(0, 0, -90);
                ball.ResetTo(player.Body.position + Vector2.right * 0.64f);
                PossessionSystem possession = (PossessionSystem)typeof(MatchManager)
                    .GetField("possession", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
                possession.TryAcquire(player);
                player.BeginKick(PlayerKicker.KickType.Pass);
                typeof(PlayerKicker).GetField("chargeStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(player.GetComponent<PlayerKicker>(), Time.time - 0.4f);
                Call(player.GetComponent<PlayerAimGuide>(), "LateUpdate");
                hud.SetScore(1, 0);
                hud.SetClock("1ST HALF", 78);
                hud.SetPlayerState(true, true, false);
                hud.SetCharge(player.ChargingKick, player.KickCharge);
                Call(hud, "Update");
                // Availability animation is advanced explicitly in this still preview.
                foreach (CanvasGroup group in hud.GetComponentsInChildren<CanvasGroup>())
                    if (group.GetComponent<HoldActionButton>() != null) group.alpha = 1;

                Camera camera = Camera.main;
                camera.aspect = 16f / 9f;
                Call(camera.GetComponent<CameraFit>(), "LateUpdate");
                Directory.CreateDirectory("../AppearanceVerification");
                Capture(camera, hud.GetComponent<Canvas>(), 1920, 1080, "../AppearanceVerification/match-preview.png");

                hud.gameObject.SetActive(false);
                camera.transform.position = new Vector3(-1.25f, 0.03f, -10);
                camera.orthographicSize = 0.95f;
                Capture(camera, null, 1200, 900, "../AppearanceVerification/player-ball-preview.png");
                PlayerAnimator animator = player.GetComponent<PlayerAnimator>();
                animator.PlayKick(PlayerKicker.KickType.Shot);
                Call(animator, "Update");
                Capture(camera, null, 1200, 900, "../AppearanceVerification/kick-preview.png");
                RunningPose(player, animator, Mathf.PI * 0.5f);
                Capture(camera, null, 1200, 900, "../AppearanceVerification/running-left-preview.png");
                RunningPose(player, animator, Mathf.PI * 1.5f);
                Capture(camera, null, 1200, 900, "../AppearanceVerification/running-right-preview.png");
                player.Body.linearVelocity = Vector2.zero;
                Call(animator, "ResetPose");
                player.transform.Find("Sprite").GetComponent<SpriteRenderer>().sprite = SpriteLibrary.Get("player_red_rigged");
                Capture(camera, null, 1200, 900, "../AppearanceVerification/red-player-preview.png");
                player.transform.Find("Sprite").GetComponent<SpriteRenderer>().sprite = SpriteLibrary.Get("player_blue_rigged");

                camera.transform.position = new Vector3(0, 0, -10);
                camera.aspect = 16f / 9f;
                Call(camera.GetComponent<CameraFit>(), "LateUpdate");
                player.CancelKick();
                player.BeginKick(PlayerKicker.KickType.Shot);
                player.SetShotCurve(0.85f);
                typeof(PlayerKicker).GetField("chargeStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(player.GetComponent<PlayerKicker>(), Time.time - 0.75f);
                Call(player.GetComponent<PlayerAimGuide>(), "LateUpdate");
                hud.gameObject.SetActive(true);
                hud.SetPlayerState(true, true, false);
                hud.SetCharge(player.ChargingKick, player.KickCharge, player.ShotCurve);
                Capture(camera, hud.GetComponent<Canvas>(), 1920, 1080, "../AppearanceVerification/curve-shot-preview.png");
                hud.gameObject.SetActive(false);
                player.CancelKick();
                Call(player.GetComponent<PlayerAimGuide>(), "LateUpdate");
                MatchMenu menu = UnityEngine.Object.FindFirstObjectByType<MatchMenu>(FindObjectsInactive.Include);
                menu.ShowMain();
                Capture(camera, menu.GetComponent<Canvas>(), 1920, 1080, "../AppearanceVerification/menu-main.png");
                Capture(camera, menu.GetComponent<Canvas>(), 1440, 1080, "../AppearanceVerification/menu-main-tablet.png");
                Call(menu, "ShowControls", MatchMenu.Screen.Main);
                Capture(camera, menu.GetComponent<Canvas>(), 1920, 1080, "../AppearanceVerification/menu-controls.png");
                menu.ShowPause();
                Capture(camera, menu.GetComponent<Canvas>(), 1920, 1080, "../AppearanceVerification/menu-pause.png");
                menu.ShowResults(2, 1);
                Capture(camera, menu.GetComponent<Canvas>(), 1920, 1080, "../AppearanceVerification/menu-results.png");
                Debug.Log("Appearance previews rendered successfully.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void RunningPose(PlayerController player, PlayerAnimator animator, float phase)
        {
            // Stage two opposite stride phases without advancing gameplay physics.
            player.Body.linearVelocity = Vector2.right * 6.48f;
            typeof(PlayerAnimator).GetField("kickRemaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(animator, 0f);
            float radiansPerUnit = (float)typeof(PlayerAnimator).GetField("radiansPerUnit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(animator);
            typeof(PlayerAnimator).GetProperty("Phase").SetValue(animator,
                phase - player.Body.linearVelocity.magnitude * radiansPerUnit * Time.deltaTime);
            typeof(PlayerAnimator).GetProperty("Intensity").SetValue(animator, 1f);
            Call(animator, "Update");
        }

        private static void Capture(Camera camera, Canvas canvas, int width, int height, string path)
        {
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            if (canvas != null)
            {
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                canvas.scaleFactor = height / 1080f;
                foreach (Text text in canvas.GetComponentsInChildren<Text>())
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                Canvas.ForceUpdateCanvases();
                MatchMenu menu = canvas.GetComponent<MatchMenu>();
                if (menu != null) Call(menu, "ApplyLayout");
                foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>()) graphic.Rebuild(CanvasUpdate.PreRender);
            }
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(pixels);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
