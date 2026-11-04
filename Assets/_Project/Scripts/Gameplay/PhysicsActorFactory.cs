using PointPass.Core;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Shared physical actors for rendered matches and the dedicated match worker.</summary>
    public static class PhysicsActorFactory
    {
        public static Ball CreateBall(Transform root)
        {
            GameObject go = new GameObject("Ball");
            go.transform.SetParent(root, false);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = 0.2f;
            body.linearDamping = MatchConfig.PitchBallDamping;
            body.angularDamping = 1f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = MatchConfig.BallDiameter * 0.5f;
            collider.sharedMaterial = new PhysicsMaterial2D("Ball") { bounciness = 0.6f, friction = 0f };
            return go.AddComponent<Ball>();
        }

        public static PlayerController CreatePlayer(Transform root, Team team, Vector2 kickoffPosition, float kickoffAngle)
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
            go.AddComponent<CircleCollider2D>().radius = MatchConfig.PlayerColliderRadius;
            go.AddComponent<PlayerMovement>();
            PlayerController controller = go.AddComponent<PlayerController>();
            controller.Init(team, kickoffPosition, kickoffAngle);
            go.transform.position = kickoffPosition;
            return controller;
        }
    }
}
