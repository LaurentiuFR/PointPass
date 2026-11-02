using PointPass.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Resolution-independent pitch graphics with feathered line edges, no low-res texture.</summary>
    public class FootballGraphic : Image
    {
        public bool ShowRoute { get; set; } = true;
        protected override void OnEnable()
        {
            base.OnEnable();
            sprite=SpriteLibrary.WhiteSquare();
            color=Color.white;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            Vector2 half = r.size * 0.43f;
            Color paint = new Color(0.81f, 0.9f, 0.7f, 0.23f);
            Stroke(mesh, new Vector2(-half.x,-half.y),new Vector2(half.x,-half.y),1.5f,paint);
            Stroke(mesh, new Vector2(half.x,-half.y),half,1.5f,paint);
            Stroke(mesh, half,new Vector2(-half.x,half.y),1.5f,paint);
            Stroke(mesh, new Vector2(-half.x,half.y),-half,1.5f,paint);
            Stroke(mesh, new Vector2(-half.x,0),new Vector2(half.x,0),1.5f,paint);
            float radius = Mathf.Min(half.x,half.y)*0.42f;
            for (int i=0;i<96;i++)
            {
                float a=i*Mathf.PI*2/96, b=(i+1)*Mathf.PI*2/96;
                Stroke(mesh,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,
                    new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,1.5f,paint);
            }
            foreach (float side in new[]{-1f,1f})
            {
                float y=half.y*side, inner=y-side*half.y*0.22f, width=half.x*0.5f;
                Stroke(mesh,new Vector2(-width,y),new Vector2(-width,inner),1.5f,paint);
                Stroke(mesh,new Vector2(-width,inner),new Vector2(width,inner),1.5f,paint);
                Stroke(mesh,new Vector2(width,inner),new Vector2(width,y),1.5f,paint);
                float goalWidth=half.x*0.25f, back=y+side*half.y*0.075f;
                Stroke(mesh,new Vector2(-goalWidth,y),new Vector2(-goalWidth,back),1.5f,paint);
                Stroke(mesh,new Vector2(-goalWidth,back),new Vector2(goalWidth,back),1.5f,paint);
                Stroke(mesh,new Vector2(goalWidth,back),new Vector2(goalWidth,y),1.5f,paint);
            }
            if (!ShowRoute) return;
            Vector2 from=new Vector2(-half.x*0.65f,-half.y*0.6f), middle=new Vector2(half.x*0.5f,0);
            Vector2 to=new Vector2(0,half.y*0.7f);
            Stroke(mesh,from,middle,2.2f,MatchPalette.Accent*new Color(1,1,1,0.5f));
            Stroke(mesh,middle,to,2.2f,MatchPalette.Accent*new Color(1,1,1,0.5f));
        }

        private static void Stroke(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 side=Vector2.Perpendicular((b-a).normalized);
            int start=mesh.currentVertCount;
            for (int edge=0;edge<4;edge++)
            {
                float offset=edge==0 ? -width*0.5f-0.75f : edge==1 ? -width*0.5f : edge==2 ? width*0.5f : width*0.5f+0.75f;
                Color shade=color; if (edge==0 || edge==3) shade.a=0;
                mesh.AddVert(a+side*offset,shade,Vector2.zero); mesh.AddVert(b+side*offset,shade,Vector2.zero);
            }
            for (int edge=0;edge<3;edge++)
            {
                int i=start+edge*2;
                mesh.AddTriangle(i,i+1,i+3); mesh.AddTriangle(i,i+3,i+2);
            }
        }
    }
}
