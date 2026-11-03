using PointPass.Players;
using UnityEngine;

namespace PointPass.UI
{
    public partial class MatchHud
    {
        private UnityEngine.UI.Image staminaRing, tackleRing;
        private UnityEngine.UI.Text sprintState, athleticFeedback;
        private CanvasGroup tackleAvailability;
        private float staminaTarget=1, staminaDisplay=1, tackleCooldown, feedbackUntil;
        private bool sprintExhausted;
        private bool controlLayoutPreview;
        private uint tackleSerial;
        private static Sprite staminaProgressSprite;
        public HoldActionButton TackleButton { get; private set; }
        public MobileControlLayout ControlLayout { get; private set; }

        private void BuildAthleticsAndLayout()
        {
            TackleButton=BuildAction("Tackle",new Vector2(-520,178),150,Mint,1,out tackleRing);
            tackleAvailability=TackleButton.gameObject.AddComponent<CanvasGroup>();
            sprintState=SprintButton.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();
            staminaRing.sprite=StaminaProgressSprite();
            staminaRing.fillAmount=1;
            athleticFeedback=Label(safeAreaRoot,"TackleFeedback","",29,new Vector2(0,112),new Vector2(570,55),Paper,true);
            athleticFeedback.rectTransform.anchorMin=athleticFeedback.rectTransform.anchorMax=new Vector2(.5f,0);
            var edit=Panel(safeAreaRoot,"EditControls",Ink,Vector2.zero,new Vector2(78,64));
            Place(edit.rectTransform,new Vector2(1,1),new Vector2(-157,-58),new Vector2(78,64));
            edit.raycastTarget=true;
            var button=edit.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=edit;
            button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            LobbyUi.Icon(edit.transform,"Gear",LobbyIcon.Symbol.Gear,Vector2.zero,32,Mint);
            button.onClick.AddListener(MobileControlEditor.Open);
            ControlLayout=gameObject.AddComponent<MobileControlLayout>();
            Canvas.ForceUpdateCanvases();
            ControlLayout.Initialize(safeAreaRoot,Joystick);
            ControlLayout.Register("Joystick",Joystick.GetComponent<RectTransform>());
            ControlLayout.Register("Pass",PassButton.GetComponent<RectTransform>());
            ControlLayout.Register("Shoot",ShootButton.GetComponent<RectTransform>());
            ControlLayout.Register("Sprint",SprintButton.GetComponent<RectTransform>());
            ControlLayout.Register("Tackle",TackleButton.GetComponent<RectTransform>());
            ControlLayout.Register("Cancel",CancelChargeButton.GetComponent<RectTransform>());
            ControlLayout.LoadSaved();
        }
        public void SetAthleticState(PlayerStamina stamina, PlayerTackler tackler)
        {
            staminaTarget=stamina!=null?stamina.Remaining:1;
            sprintExhausted=stamina!=null&&stamina.Exhausted;
            tackleCooldown=tackler!=null?tackler.RecoveryRemaining:0;
            if(tackler!=null&&tackler.ResultSerial!=tackleSerial)
            {
                tackleSerial=tackler.ResultSerial; feedbackUntil=Time.unscaledTime+.8f;
                athleticFeedback.text=tackler.LastResult==TackleResult.WonBall?"TACKLE WON · BALL LOOSE":"MISSED · RECOVER";
                athleticFeedback.color=tackler.LastResult==TackleResult.WonBall?new Color(.3f,.92f,.61f):new Color(1,.43f,.34f);
            }
        }
        private void UpdateAthletics()
        {
            if(staminaRing==null) return;
            staminaDisplay=Mathf.MoveTowards(staminaDisplay,staminaTarget,Time.unscaledDeltaTime*1.8f);
            staminaRing.fillAmount=staminaDisplay;
            Color low=new Color(.98f,.28f,.26f), mid=new Color(1,.8f,.25f), high=new Color(.27f,.88f,.54f);
            staminaRing.color=staminaDisplay<.5f?Color.Lerp(low,mid,staminaDisplay*2):Color.Lerp(mid,high,(staminaDisplay-.5f)*2);
            sprintState.text=sprintExhausted?"RECOVER":"SPRINT";
            sprintState.fontSize=sprintExhausted?19:23;
            if(sprintExhausted) sprintAvailabilityTarget=playerActive ? .4f : .25f;
            else sprintAvailabilityTarget=playerActive ? 1 : .25f;
            tackleRing.fillAmount=Mathf.Clamp01(tackleCooldown/.76f);
            tackleRing.color=new Color(1,.6f,.3f,.75f);
            float available=controlLayoutPreview ? 1 : !playerActive ? .25f : playerHasBall||tackleCooldown>0 ? .4f : 1;
            tackleAvailability.alpha=Mathf.MoveTowards(tackleAvailability.alpha,available,Time.unscaledDeltaTime*7);
            if(Time.unscaledTime>=feedbackUntil) athleticFeedback.text="";
        }
        public void PrepareLayoutPreview()
        {
            controlLayoutPreview=true;
            scoreRoot.gameObject.SetActive(false);
            phaseText.transform.parent.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            replayCue.gameObject.SetActive(false);
            safeAreaRoot.Find("Charge").gameObject.SetActive(false);
            safeAreaRoot.Find("PauseButton").gameObject.SetActive(false);
            safeAreaRoot.Find("EditControls").gameObject.SetActive(false);
            SetPlayerState(true,true,false);
            safeAreaRoot.Find("ControlHint").gameObject.SetActive(false);
            CancelChargeButton.gameObject.SetActive(true);
        }
        private static Sprite StaminaProgressSprite()
        {
            if(staminaProgressSprite!=null)return staminaProgressSprite;
            const int size=128;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Stamina progress ring",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*64);
                byte alpha=(byte)(255*Mathf.Clamp01(63-radius)*Mathf.Clamp01(radius-55));
                pixels[y*size+x]=new Color32(255,255,255,alpha);
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            staminaProgressSprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100);
            return staminaProgressSprite;
        }
    }
}
