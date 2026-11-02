using System.Collections;
using PointPass.Core;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PointPass.Gameplay
{
    /// <summary>
    /// Runs the match: two 90s halves, goals, a 60s golden-goal overtime if tied, then full time.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        private enum Phase { Paused, Playing, FullTime }

        private Ball ball;
        private PlayerMovement player;
        private Dribbler dribbler;
        private MatchHud hud;

        private Phase phase = Phase.Paused;
        private int blueScore;
        private int redScore;
        private int half = 1;
        private bool overtime;
        private float clock;
        private float fullTimeAt;

        public void Init(Ball ball, PlayerMovement player, Dribbler dribbler, MatchHud hud)
        {
            this.ball = ball;
            this.player = player;
            this.dribbler = dribbler;
            this.hud = hud;
        }

        private void Start()
        {
            StartMatch();
        }

        private void StartMatch()
        {
            StopAllCoroutines();
            blueScore = 0;
            redScore = 0;
            half = 1;
            overtime = false;
            clock = MatchConfig.HalfSeconds;
            hud.SetScore(0, 0);
            StartCoroutine(KickoffRoutine("KICK OFF"));
        }

        public void OnGoal(Team scoringTeam)
        {
            if (phase != Phase.Playing) return;

            if (scoringTeam == Team.Blue) blueScore++;
            else redScore++;

            phase = Phase.Paused;
            hud.SetScore(blueScore, redScore);
            StartCoroutine(GoalRoutine());
        }

        private void Update()
        {
            if (phase == Phase.Playing)
            {
                clock -= Time.deltaTime;
                if (clock <= 0f)
                {
                    clock = 0f;
                    phase = Phase.Paused;
                    UpdateClock();
                    StartCoroutine(PeriodEndRoutine());
                    return;
                }
                UpdateClock();
            }
            else if (phase == Phase.FullTime && Time.time > fullTimeAt + 1f && AnyPress())
            {
                StartMatch();
            }
        }

        private IEnumerator KickoffRoutine(string message)
        {
            // Reset while the bodies are active so velocities are really cleared, then freeze.
            SetPlaying(true);
            ResetPositions();
            SetPlaying(false);

            UpdateClock();
            hud.ShowMessage(message);
            yield return new WaitForSeconds(MatchConfig.KickoffPauseSeconds);

            hud.ShowMessage(string.Empty);
            SetPlaying(true);
            phase = Phase.Playing;
        }

        private IEnumerator GoalRoutine()
        {
            yield return null; // leave the physics callback before changing physics state
            SetPlaying(false);
            hud.ShowMessage("GOAL!");
            yield return new WaitForSeconds(MatchConfig.GoalPauseSeconds);

            if (overtime)
            {
                EndMatch();
                yield break;
            }
            yield return KickoffRoutine("KICK OFF");
        }

        private IEnumerator PeriodEndRoutine()
        {
            SetPlaying(false);

            if (overtime)
            {
                EndMatch();
                yield break;
            }

            if (half == 1)
            {
                hud.ShowMessage("HALF TIME");
                yield return new WaitForSeconds(MatchConfig.HalfTimePauseSeconds);
                half = 2;
                clock = MatchConfig.HalfSeconds;
                yield return KickoffRoutine("2ND HALF");
                yield break;
            }

            if (blueScore != redScore)
            {
                EndMatch();
                yield break;
            }

            overtime = true;
            clock = MatchConfig.OvertimeSeconds;
            hud.ShowMessage("TIED!");
            yield return new WaitForSeconds(MatchConfig.HalfTimePauseSeconds);
            yield return KickoffRoutine("GOLDEN GOAL");
        }

        private void EndMatch()
        {
            phase = Phase.FullTime;
            fullTimeAt = Time.time;
            SetPlaying(false);
            hud.SetClockText("FULL TIME");
            hud.ShowMessage("FULL TIME\n" + MatchHud.ScoreLine(blueScore, redScore) + "\nTap to play again");
        }

        private void ResetPositions()
        {
            dribbler.Release();
            ball.ResetTo(Vector2.zero);
            player.ResetTo(new Vector2(-3f, 0f), -90f);
        }

        private void SetPlaying(bool playing)
        {
            ball.Body.simulated = playing;
            player.Body.simulated = playing;
            player.InputEnabled = playing;
        }

        private void UpdateClock()
        {
            string label = overtime ? "GOLDEN GOAL" : (half == 1 ? "1ST HALF" : "2ND HALF");
            hud.SetClock(label, clock);
        }

        private static bool AnyPress()
        {
            bool pointer = Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
            bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            return pointer || space;
        }
    }
}
