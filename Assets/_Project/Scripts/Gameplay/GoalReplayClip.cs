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
        }
        public Team scoringTeam;
        public Team[] teams;
        public float goalTime;
        public Frame[] frames;
        public float Duration => frames != null && frames.Length > 1 ? frames[frames.Length-1].time-frames[0].time : 0;
    }
}
