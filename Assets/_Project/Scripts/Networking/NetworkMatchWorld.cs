using System.Collections.Generic;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;

namespace PointPass.Networking
{
    /// <summary>Builds the existing actors. Only the authoritative world enables their physics.</summary>
    public sealed class NetworkMatchWorld
    {
        public Transform Root { get; }
        public Ball Ball { get; }
        public PossessionSystem Possession { get; } = new PossessionSystem();
        public List<PlayerController> Players { get; } = new List<PlayerController>();
        public MatchManager Simulation { get; }
        public MatchHud Hud { get; }
        public NetworkPlayerInput ClientInput { get; }
        private readonly uint[] kickSerials = new uint[2];
        private readonly PlayerKicker.KickType[] lastKicks = new PlayerKicker.KickType[2];
        private readonly ulong remoteId;
        private readonly PointPass.Audio.FootballAudio audio;
        private int presentedReset;
        private int presentedGoals;
        private bool presentedPlaying;
        private bool hasPresented;
        private int lastPresentedBlue;

        public NetworkMatchWorld(NetworkSession session, bool server, ulong remoteId)
        {
            this.remoteId = remoteId;
            Root = new GameObject("NetworkMatch").transform;
            MatchManager manager = server ? Root.gameObject.AddComponent<MatchManager>() : null;
            PitchBuilder.Build(Root, team => { if (manager != null) manager.OnGoal(team); });
            Ball = MatchBootstrap.CreateBall(Root);
            Players.Add(MatchBootstrap.CreatePlayer(Root, Team.Blue, Ball, Possession, new Vector2(-3, 0), -90));
            Players.Add(MatchBootstrap.CreatePlayer(Root, Team.Red, Ball, Possession, new Vector2(3, 0), 90));
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                Players[i].name = i == 0 ? "Blue (host)" : "Red (client)";
                Players[i].GetComponent<PlayerKicker>().Kicked += type => { kickSerials[slot]++; lastKicks[slot] = type; };
            }
            Hud = MatchHud.Create();
            Hud.transform.SetParent(Root, false);
            int localSlot = server ? 0 : 1;
            if (server)
            {
                Simulation = manager;
                manager.Init(Ball, Possession, Players, Hud, authoritativeNetwork: true);
            }
            else
            {
                foreach (Collider2D collider in Root.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                Ball.Body.simulated = false;
                Ball.Body.interpolation = RigidbodyInterpolation2D.None;
                Ball.enabled = false;
                foreach (PlayerController player in Players)
                {
                    player.GetComponent<PlayerMovement>().enabled = false;
                    player.GetComponent<PlayerKicker>().enabled = false;
                    player.GetComponent<PlayerTackler>().enabled = false;
                    player.Dribbler.enabled = false;
                    player.Body.simulated = false;
                    player.Body.interpolation = RigidbodyInterpolation2D.None;
                    player.GetComponent<PlayerAnimator>().SetReplicatedMotion(Vector2.zero);
                }
                ClientInput = new NetworkPlayerInput(session, Players[localSlot]);
                Players[localSlot].CommandSink = ClientInput;
            }
            Players[localSlot].gameObject.AddComponent<PlayerAimGuide>().Init(Ball);
            if (!NetworkDevelopmentBootstrap.TestEnabled)
                Players[localSlot].gameObject.AddComponent<HumanPlayerInput>().Init(Hud);
            // A server match cannot be paused by one client's local UI.
            Hud.PauseRequested += () => Players[localSlot].CancelActions();
            audio = Root.gameObject.AddComponent<PointPass.Audio.FootballAudio>();
            audio.Init(Ball, server ? Players : null, manager, Possession);
        }

        public NetworkMatchState Capture(uint sequence)
        {
            return new NetworkMatchState
            {
                Sequence = sequence, ServerTime = Time.unscaledTimeAsDouble,
                ResetVersion = Simulation.ResetVersion,
                BlueScore = Simulation.BlueScore, RedScore = Simulation.RedScore,
                Half = Simulation.Half, Clock = Simulation.Clock, Overtime = Simulation.IsOvertime,
                Playing = Simulation.IsPlaying, FullTime = Simulation.IsFullTime, Message = Simulation.Message,
                OwnerSlot = Possession.Owner == Players[0] ? 0 : Possession.Owner == Players[1] ? 1 : -1,
                BallPosition = Ball.Body.position, BallVelocity = Ball.Body.linearVelocity,
                BallAngle = Ball.transform.Find("Sprite").localEulerAngles.z, BallCurve = Ball.Curve,
                Blue = CapturePlayer(0, 0), Red = CapturePlayer(1, remoteId)
            };
        }

        private NetworkPlayerState CapturePlayer(int slot, ulong clientId)
        {
            PlayerController player = Players[slot];
            return new NetworkPlayerState
            {
                ClientId = clientId, Position = player.Body.position,
                Velocity = player.Body.simulated ? player.Body.linearVelocity : Vector2.zero,
                Angle = player.Body.rotation, Charge = player.KickCharge, Charging = player.ChargingKick,
                Curve = player.ShotCurve, KickSerial = kickSerials[slot], LastKick = lastKicks[slot],
                Celebrating = player.GetComponent<PlayerAnimator>().IsCelebrating,
                Stamina = player.Stamina.Remaining, Exhausted = player.Stamina.Exhausted,
                Sprinting = player.Stamina.IsSprinting, TackleSerial = player.Tackler.ResultSerial,
                TackleResult = player.Tackler.LastResult, TackleRecovery = player.Tackler.RecoveryRemaining,
                Tackling = player.Tackler.IsChallenging
            };
        }

        public void Present(NetworkMatchState state)
        {
            if (state.ResetVersion != presentedReset)
            {
                presentedReset = state.ResetVersion;
                Players[0].GetComponent<PlayerMovement>().ResetTo(state.Blue.Position, state.Blue.Angle);
                Players[1].GetComponent<PlayerMovement>().ResetTo(state.Red.Position, state.Red.Angle);
                Root.GetComponentInChildren<StadiumCrowd>()?.ResetCelebration();
                Root.GetComponentInChildren<GoalConfetti>()?.Clear();
                Root.GetComponentInChildren<SidelineReferee>()?.ResetSignal();
                Root.GetComponentInChildren<StadiumPhotographers>()?.ResetFlashes();
                foreach (GoalNet net in Root.GetComponentsInChildren<GoalNet>()) net.ResetNet();
            }
            int goals = state.BlueScore + state.RedScore;
            if (hasPresented && goals > presentedGoals)
            {
                Team team = state.BlueScore > lastPresentedBlue ? Team.Blue : Team.Red;
                Root.GetComponentInChildren<StadiumCrowd>()?.Celebrate(team);
                Root.GetComponentInChildren<GoalConfetti>()?.Celebrate(team);
                Root.GetComponentInChildren<SidelineReferee>()?.SignalGoal(team);
                Root.GetComponentInChildren<StadiumPhotographers>()?.Celebrate();
                audio.PlayGoal(team);
            }
            if (hasPresented && state.Playing && !presentedPlaying) audio.Play(PointPass.Audio.FootballAudio.Cue.Whistle);
            presentedGoals = goals;
            lastPresentedBlue = state.BlueScore;
            presentedPlaying = state.Playing;
            hasPresented = true;
            SetPose(Ball.Body, state.BallPosition, 0);
            Ball.transform.Find("Sprite").localRotation = Quaternion.Euler(0, 0, state.BallAngle);
            TrailRenderer trail = Ball.GetComponentInChildren<TrailRenderer>();
            if (trail != null) trail.emitting = state.BallVelocity.sqrMagnitude > 90.25f;
            PresentPlayer(0, state.Blue);
            PresentPlayer(1, state.Red);
            PlayerController owner = state.OwnerSlot >= 0 ? Players[state.OwnerSlot] : null;
            if (Possession.Owner != owner) { Possession.Clear(); if (owner != null) Possession.TryAcquire(owner); }
            ClientInput.IsActive = state.Playing;
            ClientInput.SetState(state.Red);
            Hud.SetScore(state.BlueScore, state.RedScore);
            if (state.FullTime) Hud.SetClockText("FULL TIME");
            else Hud.SetClock(state.Overtime ? "GOLDEN GOAL" : state.Half == 1 ? "1ST HALF" : "2ND HALF", state.Clock);
            Hud.ShowMessage(state.Message.ToString());
        }

        private void PresentPlayer(int slot, NetworkPlayerState state)
        {
            PlayerController player = Players[slot];
            SetPose(player.Body, state.Position, state.Angle);
            player.Stamina.SetReplicated(state.Stamina,state.Exhausted,state.Sprinting);
            player.Tackler.SetReplicated(state.TackleSerial,state.TackleResult,state.TackleRecovery,state.Tackling);
            PlayerAnimator animator = player.GetComponent<PlayerAnimator>();
            animator.SetReplicatedMotion(state.Velocity);
            if (kickSerials[slot] != state.KickSerial)
            {
                kickSerials[slot] = state.KickSerial;
                animator.PlayKick(state.LastKick);
                audio.Play(state.LastKick == PlayerKicker.KickType.Pass
                    ? PointPass.Audio.FootballAudio.Cue.Pass : PointPass.Audio.FootballAudio.Cue.Shot);
            }
            if (state.Celebrating && !animator.IsCelebrating) animator.CelebrateGoal();
            else if (!state.Celebrating && animator.IsCelebrating) animator.ClearCelebration();
        }

        private static void SetPose(Rigidbody2D body, Vector2 position, float angle)
        {
            body.position = position;
            body.rotation = angle;
            body.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
        }

        public void Dispose()
        {
            foreach (PlayerController player in Players) { player.SetInputEnabled(false); player.CommandSink = null; player.SetActive(false); }
            Ball.Body.simulated = false;
            Object.Destroy(Ball.Collider.sharedMaterial);
            Object.Destroy(Root.gameObject);
        }
    }
}
