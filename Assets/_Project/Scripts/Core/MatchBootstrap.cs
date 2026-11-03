using System.Collections.Generic;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace PointPass.Core
{
    /// <summary>
    /// Builds the whole match when you press Play. No setup in the editor needed:
    /// just open any scene and press Play. The runtime main menu opens before the match.
    /// </summary>
    public static class MatchBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (PointPass.Networking.NetworkDevelopmentBootstrap.TryStart()) return;
            Scene scene = SceneManager.GetActiveScene();

            ClearOldSceneObjects(scene);
            Physics2D.gravity = Vector2.zero;
            QualitySettings.antiAliasing = 4;
            SetUpCamera();

            Transform root = new GameObject("Match").transform;
            MatchManager manager = root.gameObject.AddComponent<MatchManager>();

            PitchBuilder.Build(root, manager.OnGoal);
            Ball ball = CreateBall(root);
            PossessionSystem possession = new PossessionSystem();

            // The roster: every player in the match. For now, one human-controlled blue player.
            PlayerController human = CreatePlayer(root, Team.Blue, ball, possession, new Vector2(-3f, 0f), -90f);
            List<PlayerController> roster = new List<PlayerController> { human };

            MatchHud hud = MatchHud.Create();
            human.gameObject.AddComponent<HumanPlayerInput>().Init(hud);
            human.gameObject.AddComponent<PlayerAimGuide>().Init(ball);

            manager.Init(ball, possession, roster, hud);
            root.gameObject.AddComponent<PointPass.Audio.FootballAudio>().Init(ball, roster, manager, possession);
            manager.SetMenu(MatchMenu.Create());
        }

        // Removes leftovers from hand-built scenes. Keeps cameras, lights and the EventSystem.
        internal static void ClearOldSceneObjects(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool keep = root.GetComponentInChildren<Camera>(true) != null
                            || root.GetComponentInChildren<EventSystem>(true) != null
                            || root.name.Contains("Light");
                if (!keep) UnityEngine.Object.Destroy(root);
            }
        }

        internal static void SetUpCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MatchConfig.Background;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.allowMSAA = true;
            cam.allowDynamicResolution = false;

            if (cam.GetComponent<CameraFit>() == null) cam.gameObject.AddComponent<CameraFit>();
        }

        internal static Ball CreateBall(Transform root)
        {
            GameObject go = new GameObject("Ball");
            go.transform.SetParent(root, false);

            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 0.2f;
            body.linearDamping = 1.2f;
            body.angularDamping = 1f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = MatchConfig.BallDiameter * 0.5f;
            collider.sharedMaterial = new PhysicsMaterial2D("Ball") { bounciness = 0.6f, friction = 0f };

            Ball ball = go.AddComponent<Ball>();

            Vector2 ballSize = Vector2.one * MatchConfig.BallDiameter;
            go.AddComponent<StadiumShadow>().Init(0.4f, 0.12f, 2);
            SpriteRenderer sprite = SpriteLibrary.Create(go.transform, "Sprite", "ball_polished", ballSize * 1.12f, Vector2.zero, 6, Color.white, true);
            ball.SetVisual(sprite.transform);
            go.AddComponent<StadiumActorLighting>().Init(root);
            return ball;
        }

        internal static PlayerController CreatePlayer(
            Transform root, Team team, Ball ball, PossessionSystem possession, Vector2 kickoffPosition, float kickoffAngle)
        {
            GameObject go = new GameObject("Player");
            go.transform.SetParent(root, false);

            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 1f;
            body.linearDamping = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = MatchConfig.PlayerColliderRadius;

            go.AddComponent<PlayerMovement>();
            PlayerController controller = go.AddComponent<PlayerController>();
            controller.Init(team, kickoffPosition, kickoffAngle);
            go.transform.position = kickoffPosition;

            Vector2 size = Vector2.one * MatchConfig.PlayerDiameter;
            Color teamColor = team == Team.Blue ? MatchConfig.BlueTeam : MatchConfig.RedTeam;
            string spriteName = team == Team.Blue ? "player_blue_rigged" : "player_red_rigged";

            go.AddComponent<StadiumShadow>().Init(0.7f, 0.38f, 1);

            // Boots sit under the body so they tuck in and poke out as he runs
            Color bootColor = new Color(0.1f, 0.1f, 0.1f);
            Vector2 bootSize = new Vector2(0.32f, 0.36f);
            SpriteRenderer leftBoot = SpriteLibrary.Create(go.transform, "BootLeft", "boot_polished", bootSize,
                new Vector2(-PlayerAnimator.BootSpread, PlayerAnimator.BootRestY), 4, bootColor);
            SpriteRenderer rightBoot = SpriteLibrary.Create(go.transform, "BootRight", "boot_polished", bootSize,
                new Vector2(PlayerAnimator.BootSpread, PlayerAnimator.BootRestY), 4, bootColor);
            SpriteRenderer bodySprite = SpriteLibrary.Create(go.transform, "Sprite", spriteName, size, Vector2.zero, 5, teamColor, true);
            Transform leftArm = CreateArm(go.transform, true);
            Transform rightArm = CreateArm(go.transform, false);

            PlayerAnimator animator = go.AddComponent<PlayerAnimator>();
            animator.Init(leftBoot.transform, rightBoot.transform, bodySprite.transform, leftArm, rightArm);

            Dribbler dribbler = go.AddComponent<Dribbler>();
            dribbler.Init(ball, possession, animator);
            PlayerKicker kicker = go.AddComponent<PlayerKicker>();
            kicker.Init(ball, possession);
            kicker.Kicked += animator.PlayKick;
            go.AddComponent<StadiumActorLighting>().Init(root);
            return controller;
        }

        private static Transform CreateArm(Transform player, bool left)
        {
            Transform pivot = new GameObject(left ? "LeftArm" : "RightArm").transform;
            pivot.SetParent(player, false);
            pivot.localPosition = new Vector3(left ? -0.30f : 0.30f, -0.035f, 0);
            CreateArmPart(pivot, "UpperArm", new Rect(0, 0.5f, 1, 0.5f), new Vector2(0.25f, 0.145f), new Vector2(0, -0.065f), left);
            Transform elbow = new GameObject("Elbow").transform;
            elbow.SetParent(pivot, false);
            elbow.localPosition = new Vector3(0, -0.115f, 0);
            CreateArmPart(elbow, "Forearm", new Rect(0, 0, 1, 0.53f), new Vector2(0.25f, 0.145f), new Vector2(0, -0.06f), left);
            return pivot;
        }

        private static void CreateArmPart(Transform parent, string name, Rect region, Vector2 size, Vector2 position, bool left)
        {
            SpriteRenderer part = SpriteLibrary.Create(parent, name, "arm_runner", size, position, 4, Color.white);
            part.sprite = SpriteLibrary.GetRegion("arm_runner", region);
            Vector2 native = part.sprite.bounds.size;
            part.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1);
            part.flipX = left;
        }
    }
}
