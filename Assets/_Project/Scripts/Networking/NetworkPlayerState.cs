using PointPass.Players;
using Unity.Netcode;
using UnityEngine;

namespace PointPass.Networking
{
    public struct NetworkPlayerState : INetworkSerializeByMemcpy
    {
        public ulong ClientId;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Angle;
        public float Charge;
        public float Curve;
        public PlayerKicker.KickType Charging;
        public uint KickSerial;
        public PlayerKicker.KickType LastKick;
        public bool Celebrating;

        public static NetworkPlayerState Blend(NetworkPlayerState a, NetworkPlayerState b, float t)
        {
            b.Position = Vector2.Lerp(a.Position, b.Position, t);
            b.Velocity = Vector2.Lerp(a.Velocity, b.Velocity, t);
            b.Angle = Mathf.LerpAngle(a.Angle, b.Angle, t);
            return b;
        }
    }
}
