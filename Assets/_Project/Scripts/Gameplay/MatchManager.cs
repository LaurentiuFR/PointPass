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
        private enum Phase { MainMenu, Paused, Playing, FullTime, Practice }

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
        private StadiumCrowd crowd;
        private GoalConfetti confetti;
        private SidelineReferee referee;
        private StadiumPhotographers photographers;
        private GoalReplayRecorder replay;
        private Team lastScoringTeam;
        private bool wasOwnGoal;
        private PassingPractice practice;
        private PracticeHud practiceHud;

        public bool IsUserPaused => userPaused;
        public bool IsInMainMenu => phase == Phase.MainMenu;
        public bool IsPracticing => practice != null && practice.IsRunning;
        public bool IsPlaying => phase == Phase.Playing;
        public bool IsFullTime => phase == Phase.FullTime;
        public int BlueScore => blueScore;
        public int RedScore => redScore;
        public int Half => half;
        public bool IsOvertime => overtime;
        public float Clock => clock;
        public string Message { get; private set; } = string.Empty;
        public int ResetVersion { get; private set; }
        public event System.Action<Team> GoalScored;
        public event System.Action MatchWhistle;

        public void Init(Ball ball, PossessionSystem possession, List<PlayerController> players, MatchHud hud,
            bool authoritativeNetwork = false)
        {
            if (this.hud != null) this.hud.PauseRequested -= PauseMatch;
            this.ball = ball;
            this.possession = possession;
            this.players = players;
            this.hud = hud;
            crowd = GetComponentInChildren<StadiumCrowd>();
            confetti = GetComponentInChildren<GoalConfetti>();
            referee = GetComponentInChildren<SidelineReferee>();
            photographers = GetComponentInChildren<StadiumPhotographers>();
            // Network clients receive match state; only the server runs these rules.
            // Offline menu, practice and highlight capture remain an offline concern.
            if (authoritativeNetwork) return;
            replay = GetComponent<GoalReplayRecorder>();
            if (replay == null) replay = gameObject.AddComponent<GoalReplayRecorder>();
            replay.Init(ball,players);
            replay.ClipReady += OnReplayReady;
            hud.PauseRequested += PauseMatch;
            practice = GetComponent<PassingPractice>();
            if (practice == null) practice = gameObject.AddComponent<PassingPractice>();
            if (players.Count > 0)
            {
                practice.Init(ball, possession, players[0]);
                practiceHud = PracticeHud.Create(hud.transform.Find("SafeArea"), practice);
            }
        }

        public void SetMenu(MatchMenu menu)
        {
            UnsubscribeMenu();
            this.menu = menu;
            if (menu == null) return;
            menu.PlayRequested += StartMatch;
            menu.PracticeRequested += StartPractice;
            menu.ResumeRequested += ResumeMatch;
            menu.RestartRequested += RestartSession;
            menu.MainMenuRequested += OpenMainMenu;
            menu.SetReplay(replay?.Latest);
        }

        private void UnsubscribeMenu()
        {
            if (menu == null) return;
            menu.PlayRequested -= StartMatch;
            menu.PracticeRequested -= StartPractice;
            menu.ResumeRequested -= ResumeMatch;
            menu.RestartRequested -= RestartSession;
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
            EndPractice();
            menu?.Hide();
            hud?.gameObject.SetActive(true);
            SetPlayerInput(true);
            phase = Phase.Paused;
            blueScore = 0;
            redScore = 0;
            confetti?.ResetMatch();
            half = 1;
            overtime = false;
            clock = MatchConfig.HalfSeconds;
            hud?.SetScore(0, 0);
            StartCoroutine(KickoffRoutine("KICK OFF"));
        }

        public void StartPractice()
        {
            if (practice == null || players.Count == 0) return;
            StopAllCoroutines();
            RestoreGameTime();
            SetPlaying(true);
            ResetPositions();
            replay?.SetRecording(false);
            menu?.SetPracticeMode(true);
            menu?.Hide();
            hud?.gameObject.SetActive(true);
            hud?.SetPracticeMode(true);
            ShowMessage(string.Empty);
            phase = Phase.Practice;
            SetPlayerInput(true);
            practice.BeginSession();
            practiceHud?.SetVisible(true);
        }

        private void RestartSession()
        {
            if (IsPracticing) StartPractice();
            else StartMatch();
        }

        private void EndPractice()
        {
            practice?.EndSession();
            practiceHud?.SetVisible(false);
            hud?.SetPracticeMode(false);
            menu?.SetPracticeMode(false);
        }

        public void OnGoal(Team scoringTeam)
        {
            if (phase != Phase.Playing || userPaused) return;

            if (scoringTeam == Team.Blue) blueScore++;
            else redScore++;

            phase = Phase.Paused;
            GoalScored?.Invoke(scoringTeam);
            hud?.SetScore(blueScore, redScore);
            crowd?.Celebrate(scoringTeam);
            confetti?.Celebrate(scoringTeam);
            referee?.SignalGoal(scoringTeam);
            photographers?.Celebrate();
            PlayerController scorer=ball.LastTouch;
            lastScoringTeam=scoringTeam;
            wasOwnGoal=scorer!=null && scorer.Team!=scoringTeam;
            if (scorer!=null && scorer.Team==scoringTeam) scorer.GetComponent<PlayerAnimator>()?.CelebrateGoal();
            replay?.BeginGoal(scoringTeam);
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
            replay?.SetRecording(phase == Phase.Playing);
            if (phase == Phase.Playing)
            {
                clock -= Time.deltaTime;
                if (clock <= 0f)
                {
                    clock = 0f;
                    phase = Phase.Paused;
                    MatchWhistle?.Invoke();
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
            EndPractice();
            phase = Phase.MainMenu;
            SetPlayerInput(false);
            // Clear momentum while bodies are simulated, then freeze the abandoned match.
            SetPlaying(true);
            ResetPositions();
            SetPlaying(false);
            ShowMessage(string.Empty);
            hud?.gameObject.SetActive(false);
            menu?.ShowMain();
        }

        public void PauseMatch()
        {
            if (menu == null || userPaused || (phase != Phase.Playing && phase != Phase.Paused && phase != Phase.Practice)) return;
            previousTimeScale = Time.timeScale;
            userPaused = true;
            Time.timeScale = 0f;
            SetPlayerInput(false);
            hud?.gameObject.SetActive(false);
            menu.ShowPause();
        }

        public void ResumeMatch()
        {
            if (!userPaused) return;
            RestoreGameTime();
            menu?.Hide();
            hud?.gameObject.SetActive(true);
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
            ShowMessage(message);
            yield return new WaitForSeconds(MatchConfig.KickoffPauseSeconds);

            ShowMessage(string.Empty);
            SetPlaying(true);
            phase = Phase.Playing;
            MatchWhistle?.Invoke();
        }

        private IEnumerator GoalRoutine()
        {
            yield return null; // leave the physics callback before changing physics state
            // Stop player control and possession, but let the shot finish against the net.
            // The match phase already locks scoring and the clock during this celebration.
            possession.Clear();
            foreach (PlayerController player in players)
                if (player != null) player.SetActive(false);
            ball.SetCurve(0f);
            Message = lastScoringTeam == Team.Blue ? "BLUE GOAL!" : "RED GOAL!";
            hud?.ShowGoalMessage(lastScoringTeam,wasOwnGoal);
            yield return new WaitForSeconds(MatchConfig.GoalPauseSeconds);
            replay?.CompleteGoalCapture();

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
                ShowMessage("HALF TIME");
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
            ShowMessage("TIED!");
            yield return new WaitForSeconds(MatchConfig.HalfTimePauseSeconds);
            yield return KickoffRoutine("GOLDEN GOAL");
        }

        private void EndMatch()
        {
            RestoreGameTime();
            phase = Phase.FullTime;
            SetPlayerInput(false);
            SetPlaying(false);
            hud?.SetClockText("FULL TIME");
            if (menu != null)
            {
                hud?.gameObject.SetActive(false);
                menu.ShowResults(blueScore, redScore);
            }
            else ShowMessage("FULL TIME\n" + MatchHud.ScoreLine(blueScore, redScore));
        }

        // Every player returns to his kick-off spot and the ball becomes free.
        private void ResetPositions()
        {
            ResetVersion++;
            replay?.ResetHistory();
            photographers?.ResetFlashes();
            hud?.ClearGoalReplayCue();
            crowd?.ResetCelebration();
            confetti?.Clear();
            referee?.ResetSignal();
            foreach (GoalNet net in GetComponentsInChildren<GoalNet>()) net.ResetNet();
            possession.Clear();
            ball.ResetTo(Vector2.zero);
            foreach (PlayerController player in players)
            {
                if (player == null) continue;
                player.GetComponent<PlayerAnimator>()?.ClearCelebration();
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

        private void ShowMessage(string text)
        {
            Message = text;
            hud?.ShowMessage(text);
        }

        private void UpdateClock()
        {
            string label = overtime ? "GOLDEN GOAL" : (half == 1 ? "1ST HALF" : "2ND HALF");
            hud?.SetClock(label, clock);
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
            practice?.EndSession();
            if (players != null) SetPlayerInput(false);
            if (ball != null && players != null) SetPlaying(false);
        }

        private void OnDestroy()
        {
            if (replay != null) replay.ClipReady -= OnReplayReady;
            RestoreGameTime();
            UnsubscribeMenu();
            if (hud != null) hud.PauseRequested -= PauseMatch;
        }

        private void OnReplayReady(GoalReplayClip clip)
        {
            menu?.SetReplay(clip);
            hud?.ShowGoalReplayCue(replay.SavedToDisk);
        }
    }
}
