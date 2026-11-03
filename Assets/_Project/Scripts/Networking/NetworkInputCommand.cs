using PointPass.Players;
using Unity.Netcode;
using UnityEngine;

namespace PointPass.Networking
{
    public enum NetworkKickAction : byte { None, BeginPass, BeginShot, ReleasePass, ReleaseShot, Cancel }

    /// <summary>No positions, power or ownership claims are accepted from a client.</summary>
    public struct NetworkInputCommand : INetworkSerializeByMemcpy
    {
        public uint Sequence;
        public Vector2 Move;
        public Vector2 Aim;
        public float Curve;
        public bool Sprint;
        public NetworkKickAction Action;

        public bool IsValid => Finite(Move.x) && Finite(Move.y) && Finite(Aim.x) && Finite(Aim.y) && Finite(Curve)
            && Action <= NetworkKickAction.Cancel;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Apply(PlayerController player)
        {
            if (Action == NetworkKickAction.Cancel) player.CancelActions();
            if (!player.IsActive) return;
            // Retain taps shorter than the movement heartbeat. Both commands execute
            // before physics; the second restores the actual movement (including zero).
            player.SetMove(Vector2.ClampMagnitude(Aim, 1f));
            player.SetMove(Vector2.ClampMagnitude(Move, 1f));
            player.SetSprint(Sprint);
            if (Action == NetworkKickAction.BeginPass) player.BeginKick(PlayerKicker.KickType.Pass);
            if (Action == NetworkKickAction.BeginShot) player.BeginKick(PlayerKicker.KickType.Shot);
            player.SetShotCurve(Mathf.Clamp(Curve, -1f, 1f));
            if (Action == NetworkKickAction.ReleasePass) player.ReleaseKick(PlayerKicker.KickType.Pass);
            if (Action == NetworkKickAction.ReleaseShot) player.ReleaseKick(PlayerKicker.KickType.Shot);
        }
    }
}
