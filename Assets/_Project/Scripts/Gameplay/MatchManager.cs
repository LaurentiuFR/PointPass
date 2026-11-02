using System.Collections;
using System.Collections.Generic;
using PointPass.Core;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PointPass.Gameplay
{
    /// <summary>
    /// Runs the match: two 90s halves, goals, a 60s golden-goal overtime if tied, then full time.
    /// It controls every player in the roster, so pauses and resets apply to all of them.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        private enum Phase { MainMenu, Paused, Playing, FullTime }

        private Ball ball;
        private PossessionSystem possession;
        private List<PlayerController> players;
        private MatchHud hud;

        private Phase phase = Phase.Paused;
        private int blueScore;
        private int redScore;
        private int half = 1;
        private bool overtime;
        private float clock;
        private MatchMenu menu;
        private bool userPaused;
        private float previousTimeScale = 1f;

        public bool IsUserPaused => userPaused;
        public bool IsInMainMenu => phase == Phase.MainMenu;

        public void Init(Ball ball, PossessionSystem possession, List<PlayerController> players, MatchHud hud)
        {
            if (this.hud != null) this.hud.PauseRequested -= PauseMatch;
            this.ball = ball;
            this.possession = possession;
            this.players = players;
            this.hud = hud;
            hud.PauseRequested += PauseMatch;
        }

        public void SetMenu(MatchMenu menu)
        {
            UnsubscribeMenu();
            this.menu = menu;
            if (menu == null) return;
            menu.PlayRequested += StartMatch;
            menu.ResumeRequested += ResumeMatch;
            menu.RestartRequested += StartMatch;
            menu.MainMenuRequested += OpenMainMenu;
        }

        private void UnsubscribeMenu()
        {
            if (menu == null) return;
            menu.PlayRequested -= StartMatch;
            menu.ResumeRequested -= ResumeMatch;
            menu.RestartRequested -= StartMatch;
            menu.MainMenuRequested -= OpenMainMenu;
        }

        private void Start()
        {
            if (menu != null) OpenMainMenu();
            else StartMatch();
        }

        public void StartMatch()
        {
            StopAllCoroutines();
            RestoreGameTime();
            menu?.Hide();
            hud.gameObject.SetActive(true);
            SetPlayerInput(true);
            phase = Phase.Paused;
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
            if (phase != Phase.Playing || userPaused) return;

            if (scoringTeam == Team.Blue) blueScore++;
            else redScore++;

            phase = Phase.Paused;
            hud.SetScore(blueScore, redScore);
            StartCoroutine(GoalRoutine());
        }

        private void Update()
        {
            bool pausePress = (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (pausePress && menu != null)
            {
                if (userPaused) ResumeMatch();
                else PauseMatch();
                return;
            }
            if (userPaused) return;
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
        }

        public void OpenMainMenu()
        {
            StopAllCoroutines();
            RestoreGameTime();
            phase = Phase.MainMenu;
            SetPlayerInput(false);
            // Clear momentum while bodies are simulated, then freeze the abandoned match.
            SetPlaying(true);
            ResetPositions();
            SetPlaying(false);
            hud.ShowMessage(string.Empty);
            hud.gameObject.SetActive(false);
            menu?.ShowMain();
        }

        public void PauseMatch()
        {
            if (menu == null || userPaused || (phase != Phase.Playing && phase != Phase.Paused)) return;
            previousTimeScale = Time.timeScale;
            userPaused = true;
            Time.timeScale = 0f;
            SetPlayerInput(false);
            hud.gameObject.SetActive(false);
            menu.ShowPause();
        }

        public void ResumeMatch()
        {
            if (!userPaused) return;
            RestoreGameTime();
            menu?.Hide();
            hud.gameObject.SetActive(true);
            SetPlayerInput(true);
        }

        private void RestoreGameTime()
        {
            if (userPaused) Time.timeScale = previousTimeScale;
            userPaused = false;
        }

        private void SetPlayerInput(bool enabled)
        {
            foreach (PlayerController player in players)
            {
                if (player == null) continue;
                player.SetInputEnabled(enabled);
                player.GetComponent<HumanPlayerInput>()?.SuppressUntilRelease();
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
            RestoreGameTime();
            phase = Phase.FullTime;
            SetPlayerInput(false);
            SetPlaying(false);
            hud.SetClockText("FULL TIME");
            if (menu != null)
            {
                hud.gameObject.SetActive(false);
                menu.ShowResults(blueScore, redScore);
            }
            else hud.ShowMessage("FULL TIME\n" + MatchHud.ScoreLine(blueScore, redScore));
        }

        // Every player returns to his kick-off spot and the ball becomes free.
        private void ResetPositions()
        {
            possession.Clear();
            ball.ResetTo(Vector2.zero);
            foreach (PlayerController player in players)
            {
                if (player == null) continue;
                player.ResetToKickoff();
            }
        }

        // Freezes or unfreezes the ball and the whole roster together.
        private void SetPlaying(bool playing)
        {
            ball.Body.simulated = playing;
            foreach (PlayerController player in players)
            {
                if (player == null) continue;
                player.SetActive(playing);
            }
        }

        private void UpdateClock()
        {
            string label = overtime ? "GOLDEN GOAL" : (half == 1 ? "1ST HALF" : "2ND HALF");
            hud.SetClock(label, clock);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PauseMatch();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) PauseMatch();
        }

        private void OnDisable()
        {
            RestoreGameTime();
            if (players != null) SetPlayerInput(false);
            if (ball != null && players != null) SetPlaying(false);
        }

        private void OnDestroy()
        {
            RestoreGameTime();
            UnsubscribeMenu();
            if (hud != null) hud.PauseRequested -= PauseMatch;
        }
    }
}
