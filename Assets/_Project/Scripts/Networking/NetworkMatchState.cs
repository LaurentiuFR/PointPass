using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PointPass.Networking
{
    public struct NetworkMatchState : INetworkSerializeByMemcpy
    {
        public uint Sequence;
        public double ServerTime;
        public int ResetVersion;
        public int BlueScore;
        public int RedScore;
        public int Half;
        public float Clock;
        public bool Overtime;
        public bool Playing;
        public bool FullTime;
        public FixedString128Bytes Message;
        public int OwnerSlot;
        public Vector2 BallPosition;
        public Vector2 BallVelocity;
        public float BallAngle;
        public float BallCurve;
        public NetworkPlayerState Blue;
        public NetworkPlayerState Red;

        public static NetworkMatchState Blend(NetworkMatchState a, NetworkMatchState b, float t)
        {
            if (a.ResetVersion != b.ResetVersion) return b;
            b.BallPosition = Vector2.Lerp(a.BallPosition, b.BallPosition, t);
            b.BallVelocity = Vector2.Lerp(a.BallVelocity, b.BallVelocity, t);
            b.BallAngle = Mathf.LerpAngle(a.BallAngle, b.BallAngle, t);
            b.Blue = NetworkPlayerState.Blend(a.Blue, b.Blue, t);
            b.Red = NetworkPlayerState.Blend(a.Red, b.Red, t);
            return b;
        }
    }
}
