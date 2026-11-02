using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Brief, pooled team-colour flakes emitted only in the scoring club's supporter sections.</summary>
    public class GoalConfetti : MonoBehaviour
    {
        private const int PieceCount = 72;
        private Mesh mesh;
        private MeshRenderer confettiRenderer;
        private readonly Vector3[] vertices = new Vector3[PieceCount*4];
        private readonly Color[] colors = new Color[PieceCount*4];
        private float age = 10;
        private Team team;
        private int goals;
        public bool IsActive => confettiRenderer != null && confettiRenderer.enabled;
        public int GoalCount => goals;
        public Team CelebratingTeam => team;

        public void Build(Material material)
        {
            mesh = new Mesh { name="Pooled goal confetti" }; mesh.MarkDynamic();
            int[] triangles = new int[PieceCount*6];
            for (int i=0;i<PieceCount;i++)
            {
                int v=i*4,t=i*6;
                triangles[t]=v; triangles[t+1]=v+1; triangles[t+2]=v+2;
                triangles[t+3]=v; triangles[t+4]=v+2; triangles[t+5]=v+3;
            }
            mesh.vertices=vertices; mesh.colors=colors; mesh.triangles=triangles;
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(28,18,1));
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            confettiRenderer=gameObject.AddComponent<MeshRenderer>();
            confettiRenderer.sharedMaterial=material; confettiRenderer.sortingOrder=18; confettiRenderer.enabled=false;
        }

        private static float Hash(int i,float salt) => Mathf.Repeat(Mathf.Sin(i*23.71f+salt)*1275.13f,1);

        public void Celebrate(Team scoringTeam)
        {
            goals++;
            team=scoringTeam; age=0;
            if (confettiRenderer != null) confettiRenderer.enabled=true;
            Advance(0.001f);
        }

        private void Update() => Advance(Time.deltaTime);

        public void Advance(float seconds)
        {
            if (!IsActive || seconds<=0) return;
            age+=seconds;
            if (age>=2.9f) { Clear(); return; }
            for (int i=0;i<PieceCount;i++)
            {
                float t=Mathf.Max(0,age-Hash(i,1)*0.25f);
                float side=i%2==0 ? 1:-1;
                Vector2 origin=new Vector2((team==Team.Blue ? -1:1)*(i%4<2 ? 3.2f:7.8f),side*6.65f);
                float height=Mathf.Max(0,1.1f+(0.6f+Hash(i,2))*t-1.9f*t*t);
                Vector2 point=origin+new Vector2((Hash(i,3)-0.5f)*t*1.25f,
                    -side*t*0.32f+height*0.4f+Mathf.Sin(t*5+ i)*0.035f);
                float angle=t*(5+Hash(i,4)*6)+i;
                Vector2 a=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*0.026f;
                Vector2 b=Vector2.Perpendicular(a)*(0.35f+0.55f*Mathf.Abs(Mathf.Sin(t*9+i)));
                int v=i*4;
                vertices[v]=point-a-b; vertices[v+1]=point+a-b; vertices[v+2]=point+a+b; vertices[v+3]=point-a+b;
                Color color=i%4==0 ? MatchPalette.Paper : Color.Lerp(MatchPalette.TeamColor(team),Color.white,Hash(i,6)*0.25f);
                color.a=age<Hash(i,1)*0.25f ? 0:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.5f,2.8f,t));
                for (int j=0;j<4;j++) colors[v+j]=color;
            }
            mesh.vertices=vertices; mesh.colors=colors;
        }

        public void Clear() { age=10; if (confettiRenderer != null) confettiRenderer.enabled=false; }
        public void ResetMatch() { goals=0; Clear(); }
        private void OnDestroy()
        {
            if (mesh==null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
