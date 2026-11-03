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
        public float Stamina;
        public bool Exhausted;
        public bool Sprinting;
        public uint TackleSerial;
        public TackleResult TackleResult;
        public float TackleRecovery;
        public bool Tackling;

        public static NetworkPlayerState Blend(NetworkPlayerState a, NetworkPlayerState b, float t)
        {
            NetworkPlayerState result = t < 1f ? a : b;
            result.Position = Vector2.Lerp(a.Position, b.Position, t);
            result.Velocity = Vector2.Lerp(a.Velocity, b.Velocity, t);
            result.Angle = Mathf.LerpAngle(a.Angle, b.Angle, t);
            result.Stamina = Mathf.Lerp(a.Stamina,b.Stamina,t);
            return result;
        }
    }
}
