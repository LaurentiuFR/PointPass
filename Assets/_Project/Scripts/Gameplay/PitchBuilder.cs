using System;
using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>
    /// Creates the pitch art, the walls (with goal gaps), the goals and the goal triggers.
    /// Walls and goals sit exactly on the painted white line.
    /// </summary>
    public static class PitchBuilder
    {
        public static void Build(Transform root, Action<Team> onGoal)
        {
            Transform pitch = new GameObject("Pitch").transform;
            pitch.SetParent(root, false);

            float fw = MatchConfig.FieldHalfWidth;   // x of the side lines
            float fh = MatchConfig.FieldHalfHeight;  // y of the touchlines
            float goalWidth = MatchConfig.GoalWidth;
            float goalDepth = MatchConfig.GoalDepth;
            float t = 1f; // wall thickness

            // Surrounds and markings are presentation only; the existing colliders below are unchanged.
            StadiumBuilder.Build(pitch);
            SpriteRenderer turf = SpriteLibrary.Create(pitch, "Surface", "grass_stadium",
                new Vector2(MatchConfig.PitchWidth, MatchConfig.PitchHeight), Vector2.zero, -10, new Color(0.2f, 0.5f, 0.24f));
            turf.color = new Color(0.72f, 0.88f, 0.78f);

            // Goals: the open side of the art is its edge, which sits on the goal line
            Color goalPlaceholder = new Color(1f, 1f, 1f, 0.25f);
            Vector2 goalSize = new Vector2(goalDepth, goalWidth);
            SpriteLibrary.Create(pitch, "GoalLeft", "goal_left", goalSize,
                new Vector2(-(fw + goalDepth * 0.5f), 0f), -5, goalPlaceholder);
            SpriteRenderer rightGoal = SpriteLibrary.Create(pitch, "GoalRight", "goal_left", goalSize,
                new Vector2(fw + goalDepth * 0.5f, 0f), -5, goalPlaceholder);
            rightGoal.flipX = true;

            // Top and bottom walls (inner face on the touchline)
            float topWidth = 2f * (fw + goalDepth + t);
            AddWall(pitch, "WallTop", new Vector2(0f, fh + t * 0.5f), new Vector2(topWidth, t));
            AddWall(pitch, "WallBottom", new Vector2(0f, -(fh + t * 0.5f)), new Vector2(topWidth, t));

            // Side walls (inner face on the goal line), split to leave a gap for each goal
            float sideLength = fh + t - goalWidth * 0.5f;
            float sideY = goalWidth * 0.5f + sideLength * 0.5f;
            float sideX = fw + goalDepth * 0.5f; // these also form the sides of the goal
            AddWall(pitch, "WallLeftUpper", new Vector2(-sideX, sideY), new Vector2(goalDepth, sideLength));
            AddWall(pitch, "WallLeftLower", new Vector2(-sideX, -sideY), new Vector2(goalDepth, sideLength));
            AddWall(pitch, "WallRightUpper", new Vector2(sideX, sideY), new Vector2(goalDepth, sideLength));
            AddWall(pitch, "WallRightLower", new Vector2(sideX, -sideY), new Vector2(goalDepth, sideLength));

            // Back of each goal
            float backX = fw + goalDepth + 0.15f;
            AddWall(pitch, "GoalBackLeft", new Vector2(-backX, 0f), new Vector2(0.3f, goalWidth + 0.6f));
            AddWall(pitch, "GoalBackRight", new Vector2(backX, 0f), new Vector2(0.3f, goalWidth + 0.6f));

            // Goal triggers: the whole ball must be over the line to count
            float zoneDepth = goalDepth - MatchConfig.BallDiameter;
            float zoneX = fw + MatchConfig.BallDiameter + zoneDepth * 0.5f;
            AddGoalZone(pitch, "GoalZoneLeft", new Vector2(-zoneX, 0f), new Vector2(zoneDepth, goalWidth), Team.Red, onGoal);
            AddGoalZone(pitch, "GoalZoneRight", new Vector2(zoneX, 0f), new Vector2(zoneDepth, goalWidth), Team.Blue, onGoal);
        }

        private static void AddWall(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static void AddGoalZone(Transform parent, string objectName, Vector2 position, Vector2 size, Team scoringTeam, Action<Team> onGoal)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = true;
            go.AddComponent<GoalZone>().Init(scoringTeam, onGoal);
        }
    }
}
