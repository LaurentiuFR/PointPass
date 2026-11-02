using System;
using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Trigger area inside a goal. Fires when the ball is fully over the line.</summary>
    public class GoalZone : MonoBehaviour
    {
        private Team scoringTeam;
        private Action<Team> onGoal;

        public void Init(Team scoringTeam, Action<Team> onGoal)
        {
            this.scoringTeam = scoringTeam;
            this.onGoal = onGoal;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<Ball>() != null)
            {
                onGoal?.Invoke(scoringTeam);
            }
        }
    }
}
