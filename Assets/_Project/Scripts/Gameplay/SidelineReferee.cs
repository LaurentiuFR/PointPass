using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>A presentation-only official acknowledging goals by pointing toward the centre spot.</summary>
    public class SidelineReferee : MonoBehaviour
    {
        private Transform leftArm, rightArm;
        private float signalAge = 10;
        public float SignalAmount { get; private set; }
        public Team LastScoringTeam { get; private set; }

        public void Build(Material material)
        {
            PresentationMesh body = new PresentationMesh();
            Color kit = new Color(0.88f,0.77f,0.22f), black = new Color(0.07f,0.09f,0.1f);
            body.Ellipse(new Vector2(-0.075f,0.2f),new Vector2(0.057f,0.1f),black);
            body.Ellipse(new Vector2(0.075f,0.2f),new Vector2(0.057f,0.1f),black);
            body.Ellipse(new Vector2(0,-0.065f),new Vector2(0.18f,0.15f),black);
            body.Ellipse(new Vector2(0,0.025f),new Vector2(0.21f,0.17f),kit);
            body.Line(new Vector2(-0.1f,0.075f),new Vector2(0.1f,0.075f),0.017f,new Color(0.72f,0.61f,0.16f));
            body.Quad(new Vector2(0.09f,0.025f),new Vector2(0.04f,0.045f),black);
            body.Ellipse(new Vector2(0,0.095f),new Vector2(0.095f,0.1f),new Color(0.67f,0.45f,0.3f));
            body.Ellipse(new Vector2(0,0.075f),new Vector2(0.09f,0.085f),new Color(0.13f,0.09f,0.065f));
            body.Build(transform,"RefereeBody",material,5);
            leftArm=Arm(material,-1); rightArm=Arm(material,1);
            gameObject.AddComponent<StadiumShadow>().Init(0.6f,0.25f,1);
        }

        private Transform Arm(Material material, float side)
        {
            Transform pivot = new GameObject(side<0 ? "WhistleArm" : "SignalArm").transform;
            pivot.SetParent(transform,false); pivot.localPosition=new Vector2(side*0.16f,0.025f);
            PresentationMesh limb = new PresentationMesh();
            Color skin = new Color(0.67f,0.45f,0.3f);
            limb.Line(Vector2.zero,new Vector2(side*0.015f,-0.1f),0.075f,new Color(0.88f,0.77f,0.22f));
            limb.Line(new Vector2(side*0.015f,-0.1f),new Vector2(side*0.015f,-0.21f),0.052f,skin);
            limb.Ellipse(new Vector2(side*0.015f,-0.21f),new Vector2(0.035f,0.04f),skin);
            // The pointing arm crosses in front of the shirt/head in this overhead projection.
            limb.Build(pivot,"RefereeLimb",material,6);
            return pivot;
        }

        public void SignalGoal(Team team) { LastScoringTeam=team; signalAge=0; }
        private void Update() => Advance(Time.deltaTime);

        public void Advance(float seconds)
        {
            if (seconds <= 0 || rightArm == null) return;
            signalAge += seconds;
            SignalAmount = Mathf.SmoothStep(0,1,signalAge/0.22f)
                * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.35f,1.85f,signalAge)));
            rightArm.localRotation=Quaternion.Euler(0,0,-165f*SignalAmount);
            rightArm.localScale=new Vector3(1,1+0.4f*SignalAmount,1);
            leftArm.localRotation=Quaternion.Euler(0,0,135f*SignalAmount);
        }

        public void ResetSignal()
        {
            signalAge=10; SignalAmount=0;
            if (leftArm != null) leftArm.localRotation=Quaternion.identity;
            if (rightArm != null) rightArm.localRotation=Quaternion.identity;
            if (rightArm != null) rightArm.localScale=Vector3.one;
        }

        private void OnDestroy() => PresentationMesh.ReleaseMeshes(transform);
    }
}
