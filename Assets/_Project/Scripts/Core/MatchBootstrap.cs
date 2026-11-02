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
    /// just open any scene and press Play. (A scene named "MainMenu" is skipped.)
    /// </summary>
    public static class MatchBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name == "MainMenu") return;

            ClearOldSceneObjects(scene);
            Physics2D.gravity = Vector2.zero;
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
            human.gameObject.AddComponent<HumanPlayerInput>().Init(hud.Joystick);

            manager.Init(ball, possession, roster, hud);
        }

        // Removes leftovers from hand-built scenes. Keeps cameras, lights and the EventSystem.
        private static void ClearOldSceneObjects(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool keep = root.GetComponentInChildren<Camera>(true) != null
                            || root.GetComponentInChildren<EventSystem>(true) != null
                            || root.name.Contains("Light");
                if (!keep) UnityEngine.Object.Destroy(root);
            }
        }

        private static void SetUpCamera()
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

            if (cam.GetComponent<CameraFit>() == null) cam.gameObject.AddComponent<CameraFit>();
        }

        private static Ball CreateBall(Transform root)
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
            SpriteLibrary.Create(go.transform, "Shadow", "shadow", ballSize * 1.7f, Vector2.zero, 2, Color.black, true, true);
            SpriteRenderer sprite = SpriteLibrary.Create(go.transform, "Sprite", "ball", ballSize, Vector2.zero, 6, Color.white, true);
            ball.SetVisual(sprite.transform);
            return ball;
        }

        private static PlayerController CreatePlayer(
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
            string spriteName = team == Team.Blue ? "player_blue" : "player_red";

            SpriteLibrary.Create(go.transform, "Shadow", "shadow", size * 1.5f, Vector2.zero, 1, Color.black, true, true);

            // Boots sit under the body so they tuck in and poke out as he runs
            Color bootColor = new Color(0.1f, 0.1f, 0.1f);
            Vector2 bootSize = new Vector2(0.2f, 0.3f);
            SpriteRenderer leftBoot = SpriteLibrary.Create(go.transform, "BootLeft", "boot", bootSize,
                new Vector2(-PlayerAnimator.BootSpread, PlayerAnimator.BootRestY), 4, bootColor);
            SpriteRenderer rightBoot = SpriteLibrary.Create(go.transform, "BootRight", "boot", bootSize,
                new Vector2(PlayerAnimator.BootSpread, PlayerAnimator.BootRestY), 4, bootColor);
            SpriteRenderer bodySprite = SpriteLibrary.Create(go.transform, "Sprite", spriteName, size, Vector2.zero, 5, teamColor, true);

            PlayerAnimator animator = go.AddComponent<PlayerAnimator>();
            animator.Init(leftBoot.transform, rightBoot.transform, bodySprite.transform);

            Dribbler dribbler = go.AddComponent<Dribbler>();
            dribbler.Init(ball, possession, animator);
            return controller;
        }
    }
}
