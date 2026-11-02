using System;
using PointPass.Core;
using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Validation
{
    public static class GoalPresentationValidation
    {
        public static void Run(Action<bool, string> check)
        {
            GameObject root = new GameObject("Goal presentation validation");
            try
            {
                GoalNet net = GoalNet.Create(root.transform, 1);
                Mesh mesh = net.transform.Find("OverheadNet").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] rest = mesh.vertices;
                check(net.transform.Find("OverheadNet").GetComponent<MeshRenderer>().sortingOrder > 6,
                    "Overhead goal ropes draw above both the player and ball");
                Vector2 hit = new Vector2(MatchConfig.FieldHalfWidth + MatchConfig.GoalDepth, 0.6f);
                net.Impact(hit, 5); float soft = net.ImpactStrength;
                net.Advance(0.08f);
                float softTravel = Travel(rest, mesh.vertices);
                net.ResetNet();
                net.Impact(hit, 20);
                net.Advance(0.08f);
                check(net.ImpactStrength > soft && Travel(rest, mesh.vertices) > softTravel * 2,
                    "A hard shot deforms the net more than a soft pass");
                bool anchored = true;
                float near = 0, far = 0;
                Vector3[] bent = mesh.vertices;
                for (int i = 0; i < rest.Length; i++)
                {
                    if (rest[i].x <= MatchConfig.FieldHalfWidth) anchored &= Vector3.Distance(rest[i], bent[i]) < 0.0001f;
                    if (Vector2.Distance(rest[i], hit) < 0.25f) near = Mathf.Max(near, Vector3.Distance(rest[i], bent[i]));
                    if (rest[i].y < -1) far = Mathf.Max(far, Vector3.Distance(rest[i], bent[i]));
                }
                check(anchored && near > far * 2, "Net impact stays local and the front crossbar anchors remain fixed");
                net.Advance(2);
                check(Travel(rest, mesh.vertices) < 0.0001f, "Net settles back to rest after the damped impact");
                net.Impact(hit, 20); net.Advance(0.08f); net.ResetNet();
                check(Travel(rest, mesh.vertices) < 0.0001f && net.ImpactStrength == 0,
                    "Kickoff reset clears all net deformation");
                GameObject ballObject = new GameObject("Net ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
                ballObject.transform.SetParent(root.transform);
                Ball ball = ballObject.GetComponent<Ball>();
                typeof(Ball).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(ball, null);
                ball.Body.linearDamping = 1.2f;
                ball.SettleInNet(); ball.SettleInNet();
                check(ball.Body.linearDamping > 1.2f, "Net contact adds drag to catch the ball inside the recess");
                ball.ResetTo(Vector2.zero);
                check(Mathf.Abs(ball.Body.linearDamping - 1.2f) < 0.0001f,
                    "Kickoff restores original pitch drag even after repeated net contacts");

                StadiumBuilder.Build(root.transform);
                StadiumCrowd crowd = root.GetComponentInChildren<StadiumCrowd>();
                Mesh crowdMesh = crowd.transform.Find("Spectators").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] seated = crowdMesh.vertices;
                check(crowd.SpectatorCount > 400 && crowdMesh.vertexCount < 65535,
                    "Hundreds of individual spectators fit in one mobile-safe mesh");
                crowd.Celebrate(Team.Blue); crowd.Advance(0.7f);
                check(crowd.IsCelebrating && Travel(seated, crowdMesh.vertices) > 0.08f,
                    "Scoring animates raised arms and jumps in the crowd");
                float upward=0;
                Vector3[] celebrating=crowdMesh.vertices;
                for (int i=0;i<seated.Length;i++) upward=Mathf.Max(upward,celebrating[i].y-seated[i].y);
                check(upward>0.15f,"Celebrating fans raise hands above their seated positions");
                crowd.ResetCelebration();
                check(!crowd.IsCelebrating && Travel(seated, crowdMesh.vertices) < 0.0001f,
                    "Restart clears crowd celebrations immediately");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static float Travel(Vector3[] a, Vector3[] b)
        {
            float maximum = 0;
            for (int i = 0; i < a.Length; i++) maximum = Mathf.Max(maximum, Vector3.Distance(a[i], b[i]));
            return maximum;
        }
    }
}
