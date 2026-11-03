using PointPass.Core;
using UnityEngine;

namespace PointPass.Players
{
    /// <summary>One reused effect per player, including replicated results on remote footballers.</summary>
    public sealed class TackleFeedback : MonoBehaviour
    {
        private PlayerTackler tackler;
        private SpriteRenderer pulse;
        private float age = 1;
        private Color tint;
        private void Awake()
        {
            tackler=GetComponent<PlayerTackler>();
            var root=new GameObject("TackleContact",typeof(SpriteRenderer)); root.transform.SetParent(transform,false);
            pulse=root.GetComponent<SpriteRenderer>(); pulse.sprite=SpriteLibrary.SoftDisc();
            pulse.sortingOrder=3; pulse.enabled=false;
            tackler.Resolved+=OnResult;
        }
        private void OnResult(TackleResult result)
        {
            age=0; tint=result==TackleResult.WonBall?new Color(.25f,.9f,.57f):new Color(1f,.38f,.29f);
            GetComponent<PlayerAnimator>()?.PlayKick(PlayerKicker.KickType.Pass);
        }
        private void Update()
        {
            if(age>=.4f) { pulse.enabled=false; return; }
            age+=Time.unscaledDeltaTime; float t=age/.4f;
            pulse.enabled=true; pulse.transform.localScale=Vector3.one*Mathf.Lerp(.65f,1.45f,t);
            Color shade=tint; shade.a=(1-t)*.32f; pulse.color=shade;
        }
        private void OnDestroy() { if(tackler!=null) tackler.Resolved-=OnResult; }
    }
}
