using System;
using System.Collections.Generic;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Networking;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Server
{
    /// <summary>Authoritative football composition; contains no input, networking or presentation components.</summary>
    public sealed class ServerMatchWorld : IDisposable
    {
        public Transform Root { get; }
        public Ball Ball { get; }
        public PossessionSystem Possession { get; } = new PossessionSystem();
        public List<PlayerController> Players { get; } = new List<PlayerController>(2);
        public MatchManager Simulation { get; }
        private readonly ulong[] clientIds;
        private readonly uint[] kickSerials = new uint[2];
        private readonly PlayerKicker.KickType[] lastKicks = new PlayerKicker.KickType[2];
        private float ballAngle;
        private float previousCaptureTime;
        private float rollDirection = 1f;
        private int resetVersion = -1;
        private Team? celebratingTeam;
        private float goalAt;
        private bool disposed;

        public ServerMatchWorld(ulong blueClientId, ulong redClientId)
        {
            clientIds = new[] { blueClientId, redClientId };
            Physics2D.gravity = Vector2.zero;
            Root = new GameObject("AuthoritativeMatch").transform;
            Simulation = Root.gameObject.AddComponent<MatchManager>();
            PitchBuilder.BuildPhysics(Root, Simulation.OnGoal);
            Ball = PhysicsActorFactory.CreateBall(Root);
            CreatePlayer(Team.Blue, new Vector2(-3, 0), -90);
            CreatePlayer(Team.Red, new Vector2(3, 0), 90);
            Simulation.Init(Ball, Possession, Players, null, authoritativeNetwork: true);
            Simulation.GoalScored += OnGoal;
            previousCaptureTime = Time.time;
        }

        private void CreatePlayer(Team team, Vector2 position, float angle)
        {
            int slot = Players.Count;
            PlayerController player = PhysicsActorFactory.CreatePlayer(Root, team, position, angle);
            player.name = team + " (authoritative)";
            player.gameObject.AddComponent<SimulationStride>();
            player.gameObject.AddComponent<Dribbler>().Init(Ball, Possession, null);
            PlayerKicker kicker = player.gameObject.AddComponent<PlayerKicker>();
            kicker.Init(Ball, Possession);
            kicker.Kicked += type => { kickSerials[slot]++; lastKicks[slot] = type; };
            Players.Add(player);
        }

        private void OnGoal(Team team)
        {
            PlayerController scorer = Ball.GoalScorer(team);
            celebratingTeam = scorer != null && scorer.Team == team ? team : (Team?)null;
            goalAt = Time.time;
        }

        public void SetConnection(int slot, ulong clientId)
        {
            if (slot < 0 || slot >= clientIds.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            clientIds[slot] = clientId;
        }

        public NetworkMatchState Capture(uint sequence)
        {
            float elapsed = Mathf.Max(0, Time.time - previousCaptureTime);
            previousCaptureTime = Time.time;
            if (resetVersion != Simulation.ResetVersion)
            {
                resetVersion = Simulation.ResetVersion;
                ballAngle = 0f;
                celebratingTeam = null;
            }
            Vector2 velocity = Ball.Body.linearVelocity;
            float projectedRoll = velocity.x + velocity.y * 0.35f;
            if (Mathf.Abs(projectedRoll) > 0.05f) rollDirection = Mathf.Sign(projectedRoll);
            if (Ball.Body.simulated) ballAngle = Mathf.Repeat(ballAngle - velocity.magnitude * rollDirection * 140f * elapsed, 360f);
            if (Simulation.IsPlaying) celebratingTeam = null;
            return new NetworkMatchState
            {
                Sequence = sequence, ServerTime = Time.unscaledTimeAsDouble,
                ResetVersion = Simulation.ResetVersion,
                BlueScore = Simulation.BlueScore, RedScore = Simulation.RedScore,
                Half = Simulation.Half, Clock = Simulation.Clock, Overtime = Simulation.IsOvertime,
                Playing = Simulation.IsPlaying, FullTime = Simulation.IsFullTime, Message = Simulation.Message,
                OwnerSlot = Possession.Owner == Players[0] ? 0 : Possession.Owner == Players[1] ? 1 : -1,
                BallPosition = Ball.Body.position, BallVelocity = velocity,
                BallAngle = ballAngle, BallCurve = Ball.Curve,
                Blue = CapturePlayer(0), Red = CapturePlayer(1)
            };
        }

        private NetworkPlayerState CapturePlayer(int slot)
        {
            PlayerController player = Players[slot];
            return new NetworkPlayerState
            {
                ClientId = clientIds[slot], Position = player.Body.position,
                Velocity = player.Body.simulated ? player.Body.linearVelocity : Vector2.zero,
                Angle = player.Body.rotation, Charge = player.KickCharge, Charging = player.ChargingKick,
                Curve = player.ShotCurve, KickSerial = kickSerials[slot], LastKick = lastKicks[slot],
                Celebrating = celebratingTeam == player.Team && Time.time - goalAt < 2.6f
            };
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Simulation.GoalScored -= OnGoal;
            foreach (PlayerController player in Players)
            {
                player.SetInputEnabled(false);
                player.SetActive(false);
            }
            Ball.Body.simulated = false;
            UnityEngine.Object.Destroy(Ball.Collider.sharedMaterial);
            UnityEngine.Object.Destroy(Root.gameObject);
        }
    }
}
