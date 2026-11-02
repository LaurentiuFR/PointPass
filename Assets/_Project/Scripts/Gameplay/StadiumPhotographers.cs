using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Photographer flashes and brief local surface reflections; stadium lights remain constant.</summary>
    public class StadiumPhotographers : MonoBehaviour
    {
        public bool AffectGlobalLighting { get; set; } = true;
        private readonly SpriteRenderer[] flashes=new SpriteRenderer[12];
        private readonly SpriteRenderer[] reflections=new SpriteRenderer[12];
        private readonly Vector2[] positions=new Vector2[12];
        private float age=10;
        public bool IsFlashing => age<1.6f;
        public float PeakIntensity { get; private set; }

        public void Build(Material material)
        {
            PresentationMesh people=new PresentationMesh();
            for (int i=0;i<positions.Length;i++)
            {
                Vector2 position=i<8 ? new Vector2(-7.2f+(i%4)*4.8f,i<4 ? 6.35f:-6.35f)
                    : new Vector2(i<10 ? -10.9f:10.9f,i%2==0 ? -4.2f:4.2f);
                positions[i]=position;
                people.Origin=position;
                Vector2 aim=-position.normalized;
                people.Rotation=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg-90;
                people.Ellipse(Vector2.zero,new Vector2(0.09f,0.12f),new Color(0.4f,0.42f,0.33f));
                people.Ellipse(new Vector2(0,0.08f),new Vector2(0.053f,0.055f),new Color(0.57f,0.42f,0.3f));
                people.Quad(new Vector2(0,0.14f),new Vector2(0.14f,0.08f),MatchPalette.Ink);
                flashes[i]=Disc("PhotographerFlash"+i,position,Vector2.one*0.65f,15);
                reflections[i]=Disc("FlashReflection"+i,position+aim*0.4f,new Vector2(2.5f,1.8f),-7);
            }
            people.Build(transform,"Photographers",material,0);
        }

        private SpriteRenderer Disc(string name,Vector2 position,Vector2 size,int order)
        {
            GameObject go=new GameObject(name);
            go.transform.SetParent(transform,false); go.transform.localPosition=position;
            go.transform.localScale=new Vector3(size.x,size.y,1);
            SpriteRenderer sprite=go.AddComponent<SpriteRenderer>();
            sprite.sprite=SpriteLibrary.SoftDisc(); sprite.sortingOrder=order; sprite.color=Color.clear;
            return sprite;
        }

        public void Celebrate() { age=0; }
        private void Update() => Advance(Time.deltaTime);
        public void Advance(float seconds)
        {
            if (seconds<=0) return;
            age+=seconds; PeakIntensity=0;
            Vector2 peakPoint=Vector2.zero;
            for (int i=0;i<flashes.Length;i++)
            {
                if (flashes[i]==null) continue;
                float delay=0.16f+(i%4)*0.27f+(i/4)*0.065f;
                float pulse=Mathf.Clamp01(1-Mathf.Abs(age-delay)/0.045f);
                flashes[i].color=new Color(0.88f,0.94f,1,pulse*0.75f);
                reflections[i].color=new Color(0.8f,0.89f,1,pulse*0.13f);
                if (pulse>PeakIntensity) { PeakIntensity=pulse; peakPoint=positions[i]; }
            }
            if (AffectGlobalLighting) Shader.SetGlobalVector("_PointPassFlash",new Vector4(peakPoint.x,peakPoint.y,PeakIntensity,0));
        }

        public void ResetFlashes()
        {
            age=10; PeakIntensity=0;
            foreach (SpriteRenderer sprite in flashes) if (sprite!=null) sprite.color=Color.clear;
            foreach (SpriteRenderer sprite in reflections) if (sprite!=null) sprite.color=Color.clear;
            if (AffectGlobalLighting) Shader.SetGlobalVector("_PointPassFlash",Vector4.zero);
        }
        private void OnDestroy() { ResetFlashes(); PresentationMesh.ReleaseMeshes(transform); }
    }
}
