using System;
using PointPass.Core;
using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Validation
{
    public static class MatchDayValidation
    {
        public static void Run(Action<bool,string> check)
        {
            GameObject root = new GameObject("Match day validation");
            try
            {
                StadiumBuilder.Build(root.transform);
                SidelineReferee referee=root.GetComponentInChildren<SidelineReferee>();
                check(referee != null && Mathf.Abs(referee.transform.localPosition.x)<1
                    && referee.transform.localPosition.y < -MatchConfig.FieldHalfHeight,
                    "Referee stands outside the touchline near halfway");
                check(referee.GetComponent<Rigidbody2D>()==null && referee.GetComponent<Collider2D>()==null,
                    "Referee cannot interfere with ball physics or introduce fouls/offsides");
                referee.SignalGoal(Team.Blue); referee.Advance(0.3f);
                check(referee.SignalAmount>0.95f && referee.LastScoringTeam==Team.Blue,
                    "Referee acknowledges the scoring event with a centre-spot signal");
                check(referee.transform.Find("SignalArm").GetComponentInChildren<MeshRenderer>().sortingOrder>5
                    && referee.transform.Find("SignalArm").localScale.y>1.3f,
                    "Referee's pointing arm is visible above the torso and extends toward midfield");
                Quaternion raised=referee.transform.Find("SignalArm").localRotation;
                referee.Advance(0);
                check(referee.transform.Find("SignalArm").localRotation==raised,
                    "Referee goal signal freezes when game time stops");
                referee.ResetSignal();
                check(referee.SignalAmount==0 && referee.transform.Find("SignalArm").localRotation==Quaternion.identity,
                    "Kickoff restores the referee's neutral pose");
                referee.SignalGoal(Team.Red); referee.Advance(2.1f);
                check(referee.SignalAmount==0,"Referee goal acknowledgement fades back to rest");

                GoalConfetti confetti=root.GetComponentInChildren<GoalConfetti>();
                confetti.Celebrate(Team.Blue); confetti.Advance(0.5f);
                check(confetti.IsActive && confetti.GetComponent<MeshFilter>().sharedMesh.vertexCount==288,
                    "First goal emits a bounded reusable confetti burst");
                bool visible=false,bounded=true;
                foreach (Color color in confetti.GetComponent<MeshFilter>().sharedMesh.colors)
                { visible|=color.a>0.5f; bounded&=color.a>=0 && color.a<=1; }
                check(visible && bounded,"Confetti flakes have visible, valid opacity during the celebration");
                Vector3[] paused=confetti.GetComponent<MeshFilter>().sharedMesh.vertices;
                confetti.Advance(0);
                check(Same(paused,confetti.GetComponent<MeshFilter>().sharedMesh.vertices),
                    "Confetti remains stationary while game time is paused");
                confetti.Advance(3);
                check(!confetti.IsActive,"Confetti expires instead of accumulating across goals");
                confetti.Celebrate(Team.Red); bool second=confetti.IsActive;
                confetti.Celebrate(Team.Blue); bool third=confetti.IsActive;
                confetti.Celebrate(Team.Red);
                check(!second && !third && confetti.IsActive,"Confetti appears occasionally rather than on every goal");
                confetti.ResetMatch();
                check(!confetti.IsActive && confetti.GoalCount==0,"New match clears confetti and resets its goal cadence");

                StadiumFixtures fixtures=root.GetComponentInChildren<StadiumFixtures>();
                int lights=0,cameras=0;
                foreach (Transform child in fixtures.transform)
                {
                    if (child.name.StartsWith("FloodlightTower")) lights++;
                    if (child.name.StartsWith("BroadcastCamera")) cameras++;
                }
                check(lights==4 && cameras==6,"Four floodlight towers and six distinct broadcast camera positions surround the pitch");
                check(fixtures.transform.Find("PitchIllumination").GetComponent<MeshRenderer>().sortingOrder==-9,
                    "Soft pitch illumination stays above turf and below the white markings");
                check(fixtures.GetComponentsInChildren<Camera>().Length==0 && fixtures.GetComponentsInChildren<Light>().Length==0,
                    "Broadcast props and projected lighting add no extra scene renders or per-frame light passes");
                Shader shader=Resources.Load<Shader>("Art/NaturalTurf");
                check(shader!=null,"Natural turf shader is included as a build-reachable resource");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static bool Same(Vector3[] a,Vector3[] b)
        {
            for (int i=0;i<a.Length;i++) if (a[i]!=b[i]) return false;
            return true;
        }
    }
}
