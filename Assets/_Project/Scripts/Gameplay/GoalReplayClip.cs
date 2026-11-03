using System;
using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>A compact, playable match-state highlight, not an exported video.</summary>
    [Serializable]
    public class GoalReplayClip
    {
        [Serializable]
        public struct Frame
        {
            public float time;
            public Vector2 ball;
            public Vector2[] players;
            public float[] angles;
            public float ballAngle;
            public ActorPose[] poses;
        }
        [Serializable] public struct BonePose
        {
            public Vector2 position, scale;
            public float angle;
            public int order;
        }
        [Serializable] public struct ActorPose { public BonePose[] bones; }
        [Serializable] public struct ActorRig { public string[] paths; }
        [Serializable] public struct Shot
        {
            public float time;
            public int player;
            public Vector2 origin, direction;
        }
        [Serializable] public struct NetImpact
        {
            public float time, side, speed;
            public Vector2 point;
        }
        public int version = 3;
        public Team scoringTeam;
        public Team[] teams;
        public bool[] goalkeepers;
        public ActorRig[] rigs;
        public Shot[] shots;
        public NetImpact[] impacts;
        public int scorer = -1;
        public float goalTime;
        public Frame[] frames;
        public float Duration => frames != null && frames.Length > 1 ? frames[frames.Length-1].time-frames[0].time : 0;
    }
}
