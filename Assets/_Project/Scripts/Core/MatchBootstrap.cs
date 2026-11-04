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
            if (PointPass.Server.OnlineMatchWorker.TryStart()) return;
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

            PlayerController human = CreatePlayer(root, Team.Blue, ball, possession, new Vector2(-3f, 0f), -90f);
            List<PlayerController> roster = new List<PlayerController> { human };
            foreach (Team team in new[] { Team.Blue, Team.Red })
            {
                float side = team == Team.Blue ? -1 : 1;
                roster.Add(CreateGoalkeeper(root, team, ball, possession,
                    new Vector2(side * (MatchConfig.FieldHalfWidth - 0.65f), 0), side > 0 ? 90 : -90));
            }
            foreach (PlayerController player in roster)
                player.GetComponent<GoalkeeperBrain>()?.Init(ball, possession, roster, manager);

            MatchHud hud = MatchHud.Create();
            human.gameObject.AddComponent<HumanPlayerInput>().Init(hud);
            human.gameObject.AddComponent<PlayerAimGuide>().Init(ball);

            manager.Init(ball, possession, roster, hud);
            root.gameObject.AddComponent<PointPass.Audio.FootballAudio>().Init(ball, roster, manager, possession);
            MatchMenu menu = MatchMenu.Create();
            manager.SetMenu(menu);
            PointPass.Online.OnlinePilotClient.Attach(root.gameObject, menu);
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
            Ball ball = PhysicsActorFactory.CreateBall(root);
            GameObject go = ball.gameObject;

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
            PlayerController controller = PhysicsActorFactory.CreatePlayer(root, team, kickoffPosition, kickoffAngle);
            GameObject go = controller.gameObject;

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

        internal static PlayerController CreateGoalkeeper(Transform root, Team team, Ball ball,
            PossessionSystem possession, Vector2 position, float angle)
        {
            PlayerController keeper = CreatePlayer(root, team, ball, possession, position, angle);
            keeper.name = team + " Goalkeeper";
            keeper.GetComponent<CircleCollider2D>().radius = 0.34f;
            keeper.Body.mass = 1.5f;
            Material original = SpriteLibrary.KitMaterial(team);
            Material kit = original != null ? new Material(original) { name = team + " goalkeeper kit" } : null;
            if (kit != null)
            {
                kit.SetColor("_ClubColor", team == Team.Blue ? new Color(0.93f, 0.72f, 0.28f) : new Color(0.22f, 0.72f, 0.66f));
                keeper.transform.Find("Sprite").GetComponent<SpriteRenderer>().sharedMaterial = kit;
            }
            foreach (string arm in new[] { "LeftArm", "RightArm" })
            {
                Transform elbow = keeper.transform.Find(arm + "/Elbow");
                GameObject glove = new GameObject("Glove", typeof(SpriteRenderer));
                glove.transform.SetParent(elbow, false);
                glove.transform.localPosition = new Vector3(0, -0.115f, 0);
                glove.transform.localScale = new Vector3(0.14f, 0.16f, 1);
                SpriteRenderer renderer = glove.GetComponent<SpriteRenderer>();
                renderer.sprite = SpriteLibrary.WhiteCircle(); renderer.color = MatchPalette.Paper; renderer.sortingOrder = 6;
            }
            keeper.gameObject.AddComponent<GoalkeeperBrain>();
            keeper.gameObject.AddComponent<GoalkeeperVisual>().Init(kit);
            return keeper;
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
