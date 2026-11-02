using PointPass.Core;
using PointPass.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Replays recorded positions on an isolated pitch; never moves live match objects.</summary>
    public class GoalReplayView : MonoBehaviour
    {
        private GoalReplayClip clip;
        private RectTransform field,ball;
        private RectTransform[] actors;
        private float playhead;
        private Material turfMaterial;
        public float PlaybackTime => playhead;

        public void Build()
        {
            field=Rect("ReplayPitch",transform,new Vector2(0,0),new Vector2(1060,580));
            Image ground=field.gameObject.AddComponent<Image>();
            ground.sprite=SpriteLibrary.Get("grass_stadium"); ground.color=Color.white;
            Shader shader=Resources.Load<Shader>("Art/NaturalTurf");
            if (shader!=null)
            {
                turfMaterial=new Material(shader); turfMaterial.SetFloat("_UIReplay",1);
                ground.material=turfMaterial;
            }
            RectTransform markings=Rect("Markings",field,Vector2.zero,new Vector2(580,1060));
            markings.localRotation=Quaternion.Euler(0,0,90);
            FootballGraphic pitchGraphic=markings.gameObject.AddComponent<FootballGraphic>();
            pitchGraphic.raycastTarget=false; pitchGraphic.ShowRoute=false;
            ball=Image("ReplayBall",field,SpriteLibrary.Get("ball_polished"),Vector2.one*27).rectTransform;
            SharpText label=Rect("ReplayLabel",transform,new Vector2(0,340),new Vector2(1000,52)).gameObject.AddComponent<SharpText>();
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize=32; label.fontStyle=FontStyle.Bold;
            label.color=MatchPalette.Paper; label.alignment=TextAnchor.MiddleCenter; label.text="THE BUILD-UP. THE FINISH. YOUR MOMENT.";
            label.raycastTarget=false;
        }

        public void Play(GoalReplayClip replay)
        {
            clip=replay; playhead=0;
            if (actors!=null) foreach (RectTransform actor in actors) Destroy(actor.gameObject);
            actors=new RectTransform[clip.teams.Length];
            for (int i=0;i<actors.Length;i++)
            {
                string name=clip.teams[i]==Team.Blue ? "player_blue_rigged":"player_red_rigged";
                Image actor=Image("ReplayPlayer"+i,field,SpriteLibrary.Get(name),Vector2.one*50);
                actor.material=SpriteLibrary.KitMaterial(clip.teams[i]); actors[i]=actor.rectTransform;
            }
            ball.SetAsLastSibling(); Advance(0);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);
        public void Advance(float seconds)
        {
            if (clip?.frames==null || clip.frames.Length<2) return;
            playhead=Mathf.Repeat(playhead+seconds,clip.Duration+0.65f);
            float time=clip.frames[0].time+Mathf.Min(playhead,clip.Duration);
            int next=1;
            while (next<clip.frames.Length-1 && clip.frames[next].time<time) next++;
            GoalReplayClip.Frame a=clip.frames[next-1], b=clip.frames[next];
            float t=Mathf.InverseLerp(a.time,b.time,time);
            ball.anchoredPosition=Map(Vector2.Lerp(a.ball,b.ball,t));
            for (int i=0;i<actors.Length;i++)
            {
                actors[i].anchoredPosition=Map(Vector2.Lerp(a.players[i],b.players[i],t));
                actors[i].localRotation=Quaternion.Euler(0,0,Mathf.LerpAngle(a.angles[i],b.angles[i],t));
            }
        }

        private static Vector2 Map(Vector2 world) => new Vector2(world.x*46f,world.y*46f);
        private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            RectTransform rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false); rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        private static Image Image(string name,Transform parent,Sprite sprite,Vector2 size)
        {
            Image image=Rect(name,parent,Vector2.zero,size).gameObject.AddComponent<Image>();
            image.sprite=sprite; image.raycastTarget=false; return image;
        }
        private void OnDestroy()
        {
            if (turfMaterial==null) return;
            if (Application.isPlaying) Destroy(turfMaterial); else DestroyImmediate(turfMaterial);
        }
    }
}
