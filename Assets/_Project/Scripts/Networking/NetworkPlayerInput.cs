using PointPass.Players;
using UnityEngine;

namespace PointPass.Networking
{
    /// <summary>Local intent only. Charge, possession and kick execution remain on the server.</summary>
    public sealed class NetworkPlayerInput : IPlayerCommandSink
    {
        private readonly System.Action<NetworkInputCommand> send;
        private readonly PlayerController controller;
        private NetworkInputCommand command;
        private NetworkPlayerState state;
        private bool cancellationSent;
        public bool IsActive { get; set; }
        public float KickCharge => state.Charge;
        public PlayerKicker.KickType ChargingKick => state.Charging;
        public float ShotCurve => state.Curve;

        public NetworkPlayerInput(NetworkSession session, PlayerController controller)
            : this(session.SendInput, controller) { }
        public NetworkPlayerInput(System.Action<NetworkInputCommand> send, PlayerController controller)
        { this.send = send; this.controller = controller; }
        public void SetState(NetworkPlayerState value) { state = value; }
        public void SetMove(Vector2 direction)
        {
            command.Move = Vector2.ClampMagnitude(direction, 1f);
            command.Aim = controller.AimDirection;
        }
        public void SetSprint(bool held) { command.Sprint = held; }
        public void SetShotCurve(float curve) { command.Curve = Mathf.Clamp(curve, -1f, 1f); }
        public bool BeginKick(PlayerKicker.KickType type)
        {
            if (type == PlayerKicker.KickType.None || !IsActive || !controller.Dribbler.HasBall) return false;
            cancellationSent = false;
            Send(type == PlayerKicker.KickType.Pass ? NetworkKickAction.BeginPass : NetworkKickAction.BeginShot);
            return true;
        }
        public void ReleaseKick(PlayerKicker.KickType type)
        {
            if (type == PlayerKicker.KickType.None) return;
            Send(type == PlayerKicker.KickType.Pass ? NetworkKickAction.ReleasePass : NetworkKickAction.ReleaseShot);
        }
        public void CancelKick()
        {
            // Focus loss/inactive input calls this every frame. One cancellation is
            // sufficient until another kick starts; heartbeats still send stopped input.
            if (cancellationSent) return;
            cancellationSent = true;
            Send(NetworkKickAction.Cancel);
        }
        public void SendState() => Send(NetworkKickAction.None);

        private void Send(NetworkKickAction action)
        {
            command.Sequence++;
            command.Action = action;
            send(command);
        }
    }
}
