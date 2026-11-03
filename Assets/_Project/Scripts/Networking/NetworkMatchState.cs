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
            // Discrete ownership/goal/reset events belong to the displayed instant too.
            // Do not show the next snapshot's owner while the ball is still interpolating to it.
            NetworkMatchState result = t < 1f ? a : b;
            result.BallPosition = Vector2.Lerp(a.BallPosition, b.BallPosition, t);
            result.BallVelocity = Vector2.Lerp(a.BallVelocity, b.BallVelocity, t);
            result.BallAngle = Mathf.LerpAngle(a.BallAngle, b.BallAngle, t);
            result.Blue = NetworkPlayerState.Blend(a.Blue, b.Blue, t);
            result.Red = NetworkPlayerState.Blend(a.Red, b.Red, t);
            return result;
        }
    }
}
