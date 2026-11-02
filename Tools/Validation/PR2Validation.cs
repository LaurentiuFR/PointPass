using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PointPass.Validation
{
public static class PR2Validation
{
    static readonly List<string> results = new List<string>();
    static void Check(bool condition, string name)
    {
        results.Add((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new Exception(name);
    }
    static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    static bool Near(float a, float b) => Mathf.Abs(a-b) < 0.01f;

    public static void Run()
    {
        int exit = 0;
        try { Kicks(); Movement(); Buttons(); Joystick(); PhysicalPass(); DribbleFeel(); InputRouting(); InputFeelValidation.Run(Check); TurningValidation.Run(Check); PresentationValidation.Run(Check); MenuValidation.Run(Check); FlowAnimationValidation.Run(Check); CurveValidation.Run(Check); FlickValidation.Run(Check); StadiumValidation.Run(Check); }
        catch (Exception ex) { results.Add(ex.ToString()); exit = 1; }
        File.WriteAllLines("../PR2Verification/engine-tests.txt", results);
        Debug.Log(string.Join("\n", results));
        EditorApplication.Exit(exit);
    }

    static void Kicks()
    {
        var ballObject = new GameObject("Test Ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
        var ball = ballObject.GetComponent<Ball>(); Call(ball, "Awake");
        ball.Body.gravityScale = 0; ball.Body.position = Vector2.up * 0.64f;
        ballObject.GetComponent<CircleCollider2D>().radius = 0.25f;
        var p = new GameObject("Test Player", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker));
        var c = p.GetComponent<PlayerController>(); var d = p.GetComponent<Dribbler>(); var k = p.GetComponent<PlayerKicker>();
        p.GetComponent<CircleCollider2D>().radius = 0.4f;
        var possession = new PossessionSystem();
        Call(d, "Awake"); Call(k, "Awake"); d.Init(ball, possession, null); k.Init(ball, possession);
        c.Init(Team.Blue, new Vector2(-3,0), -90);
        c.BeginKick(PlayerKicker.KickType.Pass);
        Check(c.ChargingKick == PlayerKicker.KickType.None, "No charge without possession");
        possession.TryAcquire(c);
        c.BeginKick(PlayerKicker.KickType.Pass);
        c.BeginKick(PlayerKicker.KickType.Shot);
        Check(c.ChargingKick == PlayerKicker.KickType.Pass, "First action wins");
        c.ReleaseKick(PlayerKicker.KickType.Shot);
        Check(c.ChargingKick == PlayerKicker.KickType.Pass, "Wrong release does not fire");
        var originalPosition = ball.Body.position;
        c.ReleaseKick(PlayerKicker.KickType.Pass);
        Check(possession.IsOwner(c), "Release queues until physics tick");
        Call(k,"FixedUpdate");
        Check(!possession.HasOwner, "Kick releases ownership");
        Check(Near(ball.Body.linearVelocity.y,8), "Tap pass minimum speed");
        Check(ball.Body.position == originalPosition, "Kick does not teleport");
        Check(Physics2D.GetIgnoreCollision(p.GetComponent<Collider2D>(),ball.Collider), "Kicker collision temporarily ignored");
        Call(d,"FixedUpdate");
        Check(!possession.HasOwner && Near(ball.Body.linearVelocity.y,8), "Dribbler cannot overwrite kick or immediately reacquire");
        Call(k,"FixedUpdate");
        Check(Near(ball.Body.linearVelocity.y,8), "Queued kick consumed once");
        c.SetActive(false);
        Check(!Physics2D.GetIgnoreCollision(p.GetComponent<Collider2D>(),ball.Collider), "Freeze restores collision");
        c.SetActive(true);
        foreach (var type in new[] { PlayerKicker.KickType.Pass, PlayerKicker.KickType.Shot })
        {
            foreach (float charge in new[] { 0f,0.5f,1f,2f })
            {
                possession.TryAcquire(c); c.Body.rotation = -90;
                c.BeginKick(type); Set(k,"chargeStarted",Time.time-charge*(type == PlayerKicker.KickType.Pass ? 0.65f : 1f));
                float expected = Mathf.Lerp(type == PlayerKicker.KickType.Pass ? 8:10, type == PlayerKicker.KickType.Pass ? 14:20, Mathf.Clamp01(charge));
                c.ReleaseKick(type); Call(k,"FixedUpdate");
                Check(Near(ball.Body.linearVelocity.x, expected) && Near(ball.Body.linearVelocity.y,0), type + " charge " + charge + " and facing");
            }
        }
        possession.TryAcquire(c); c.Body.linearVelocity=Vector2.up*6.48f;
        c.SetMove(Vector2.down); c.BeginKick(PlayerKicker.KickType.Pass);
        originalPosition=ball.Body.position;
        c.ReleaseKick(PlayerKicker.KickType.Pass); Call(k,"FixedUpdate");
        Check(Near(ball.Body.linearVelocity.y,-8) && Near(ball.Body.linearVelocity.x,0),
            "Immediate backward pass ignores turn animation and opposite momentum");
        Check(ball.Body.position==originalPosition,"Sharp-angle pass never repositions ball");
        possession.TryAcquire(c); c.SetMove(Vector2.right); c.Body.linearVelocity=Vector2.right*6.48f;
        c.BeginKick(PlayerKicker.KickType.Pass); c.ReleaseKick(PlayerKicker.KickType.Pass); Call(k,"FixedUpdate");
        Check(Near(ball.Body.linearVelocity.x,9.62f),"Moving pass has controlled forward follow-through");
        c.Body.linearVelocity=Vector2.zero;
        possession.TryAcquire(c);
        var guide=p.AddComponent<PlayerAimGuide>(); Call(guide,"Awake"); guide.Init(ball);
        c.BeginKick(PlayerKicker.KickType.Pass); Call(guide,"LateUpdate");
        var aimLine=p.GetComponentInChildren<LineRenderer>();
        Check(aimLine.enabled && Vector2.Distance(aimLine.GetPosition(0),ball.Body.position+Vector2.right*0.35f)<0.001f,
            "Charge aim guide starts at actual ball position");
        c.CancelKick(); Call(guide,"LateUpdate");
        Check(!aimLine.enabled,"Aim guide disappears on cancellation");
        possession.TryAcquire(c); c.BeginKick(PlayerKicker.KickType.Shot); possession.Clear();
        Check(c.ChargingKick == PlayerKicker.KickType.None, "Possession loss cancels charge");
        possession.TryAcquire(c); c.BeginKick(PlayerKicker.KickType.Shot); c.ReleaseKick(PlayerKicker.KickType.Shot);
        ball.Body.linearVelocity = Vector2.zero; c.SetActive(false); c.SetActive(true); Call(k,"FixedUpdate");
        Check(ball.Body.linearVelocity == Vector2.zero, "Freeze cancels queued kick");
        possession.TryAcquire(c); c.BeginKick(PlayerKicker.KickType.Shot); c.SetSprint(true); c.ResetToKickoff();
        Check(!possession.HasOwner && c.ChargingKick == PlayerKicker.KickType.None, "Kickoff clears charge and possession");
        Check(c.Body.position == new Vector2(-3,0) && Near(c.Body.rotation,-90), "Kickoff position and facing preserved");
        UnityEngine.Object.DestroyImmediate(p); UnityEngine.Object.DestroyImmediate(ballObject);
    }

    static void Movement()
    {
        var go = new GameObject("Movement",typeof(Rigidbody2D),typeof(PlayerMovement));
        var m = go.GetComponent<PlayerMovement>(); m.SetMoveInput(Vector2.right);
        for(int i=0;i<40;i++) Call(m,"FixedUpdate");
        Check(Near(m.Body.linearVelocity.x,4.8f),"Base run speed");
        m.SetSprint(true); for(int i=0;i<40;i++) Call(m,"FixedUpdate");
        Check(Near(m.Body.linearVelocity.x,6.48f),"Sprint speed");
        m.SpeedMultiplier=0.92f; for(int i=0;i<40;i++) Call(m,"FixedUpdate");
        Check(Near(m.Body.linearVelocity.x,5.9616f),"Sprint combines with dribbling slowdown");
        m.SetSprint(false); for(int i=0;i<40;i++) Call(m,"FixedUpdate");
        Check(Near(m.Body.linearVelocity.x,4.416f),"Releasing sprint preserves dribble slowdown");
        m.SetMoveInput(Vector2.zero); for(int i=0;i<40;i++) Call(m,"FixedUpdate");
        Check(m.Body.linearVelocity == Vector2.zero,"Deceleration stops movement");
        m.SetMoveInput(Vector2.right * 0.09f); Call(m,"FixedUpdate");
        Check(m.Body.linearVelocity == Vector2.zero,"Small stick drift does not move player");
        m.SetMoveInput(Vector2.left);
        Check(m.AimDirection == Vector2.left,"Aim follows stick before visual turn");
        m.SetMoveInput(Vector2.zero);
        Check(m.AimDirection == Vector2.left,"Releasing stick retains exact last aim");
        m.ResetTo(Vector2.zero,-90);
        Check(Vector2.Distance(m.AimDirection,Vector2.right)<0.001f,"Stationary aim retains facing");
        m.SpeedMultiplier=1; m.SetMoveInput(Vector2.right);
        for(int i=0;i<40;i++) Call(m,"FixedUpdate");
        m.SetMoveInput(Vector2.left);
        for(int i=0;i<8;i++) Call(m,"FixedUpdate");
        Check(m.Body.linearVelocity.x<0,"Direction reversal brakes within 160ms");
        m.SetSprint(true); m.ResetTo(Vector2.zero,0); m.SetMoveInput(Vector2.right);
        for(int i=0;i<40;i++) Call(m,"FixedUpdate");
        Check(Near(m.Body.linearVelocity.x,4.8f),"Movement reset clears sprint");
        UnityEngine.Object.DestroyImmediate(go);
    }

    static void Buttons()
    {
        var events = new GameObject("Events",typeof(EventSystem));
        var go = new GameObject("Button",typeof(HoldActionButton)); var button=go.GetComponent<HoldActionButton>();
        int pressed=0,released=0,cancelled=0;
        button.Pressed+=()=>pressed++; button.Released+=()=>released++; button.Cancelled+=()=>cancelled++;
        var first = new PointerEventData(events.GetComponent<EventSystem>()){pointerId=1};
        var second = new PointerEventData(events.GetComponent<EventSystem>()){pointerId=2};
        button.OnPointerDown(first); button.OnPointerDown(second); button.OnPointerUp(second);
        Check(button.IsHeld && pressed==1 && released==0,"Second finger cannot steal/release action");
        button.OnPointerUp(first);
        Check(!button.IsHeld && released==1,"Owning finger releases once");
        button.OnPointerDown(first); button.Cancel(); button.OnPointerUp(first);
        Check(!button.IsHeld && cancelled==1 && released==1,"Cancellation never fires a kick");
        UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(events);
    }

    static void Joystick()
    {
        var events = new GameObject("Events",typeof(EventSystem));
        var go = new GameObject("Joystick",typeof(RectTransform),typeof(VirtualJoystick));
        var handle = new GameObject("Handle",typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(200,200);
        var j = go.GetComponent<VirtualJoystick>(); j.Init(rect,handle.GetComponent<RectTransform>());
        var first=new PointerEventData(events.GetComponent<EventSystem>()){pointerId=1,position=new Vector2(80,0)};
        var second=new PointerEventData(events.GetComponent<EventSystem>()){pointerId=2,position=new Vector2(-80,0)};
        j.OnPointerDown(first); var value=j.Value;
        j.OnPointerDown(second); j.OnDrag(second); j.OnPointerUp(second);
        Check(j.Value==value && value.x>0,"Joystick retains original finger");
        j.OnPointerUp(first); Check(j.Value==Vector2.zero,"Joystick owning release resets");
        j.OnPointerDown(first); j.ResetInput(); Check(j.Value==Vector2.zero,"Interrupted joystick resets");
        UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(handle); UnityEngine.Object.DestroyImmediate(events);
    }

    static void PhysicalPass()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        var ballObject = new GameObject("Physical Ball",typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(Ball));
        var ball = ballObject.GetComponent<Ball>(); Call(ball,"Awake");
        ball.Body.gravityScale=0; ball.Collider.GetComponent<CircleCollider2D>().radius=0.25f;
        ball.Body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        ball.Body.linearDamping=1.2f;
        ball.Body.linearVelocity=Vector2.up*8;
        Physics2D.Simulate(0.02f);
        Check(ball.Body.position.y>0.09f,"Released ball travels through physics");
        var receiver = new GameObject("Receiver",typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(PlayerMovement),typeof(PlayerController),typeof(Dribbler));
        var controller=receiver.GetComponent<PlayerController>(); var dribbler=receiver.GetComponent<Dribbler>();
        controller.Body.gravityScale=0; controller.Body.position=Vector2.up*4; controller.Body.rotation=180;
        receiver.transform.position=Vector2.up*4; receiver.transform.rotation=Quaternion.Euler(0,0,180);
        receiver.GetComponent<CircleCollider2D>().radius=0.4f;
        Call(dribbler,"Awake"); var possession=new PossessionSystem(); dribbler.Init(ball,possession,null);
        for(int i=0;i<45 && !possession.HasOwner;i++) { Physics2D.Simulate(0.02f); Call(dribbler,"FixedUpdate"); }
        Check(possession.IsOwner(controller),"Another player receives a physical low-power pass");
        Check(receiver.GetComponent<PlayerMovement>().SpeedMultiplier<1,"Receiver enters dribbling state");
        controller.SetActive(false);
        Check(!possession.HasOwner,"Freeze releases receiver possession");
        controller.SetActive(true); controller.Body.position=Vector2.up*6; receiver.transform.position=Vector2.up*6;
        Set(dribbler,"noPickupUntil",Time.time-1);
        ball.ResetTo(Vector2.zero); ball.Body.linearVelocity=Vector2.up*14;
        for(int i=0;i<90 && !possession.HasOwner;i++) { Physics2D.Simulate(0.02f); Call(dribbler,"FixedUpdate"); }
        Check(possession.IsOwner(controller),"Charged physical pass can be received after natural slowdown");
        controller.SetActive(false);
        UnityEngine.Object.DestroyImmediate(receiver);

        var wall = new GameObject("Wall",typeof(BoxCollider2D)); wall.transform.position=Vector2.up*2;
        wall.GetComponent<BoxCollider2D>().size=new Vector2(10,0.3f);
        var material=new PhysicsMaterial2D("Test bounce"){bounciness=0.6f,friction=0}; ball.Collider.sharedMaterial=material;
        ball.ResetTo(Vector2.zero); ball.Body.linearVelocity=Vector2.up*20;
        Physics2D.SyncTransforms();
        bool bounced=false; for(int i=0;i<20;i++) { Physics2D.Simulate(0.02f); if(ball.Body.linearVelocity.y<0) {bounced=true;break;} }
        Check(bounced,"Fast physical shot collides and rebounds from wall");
        UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(ballObject); UnityEngine.Object.DestroyImmediate(material);
    }

    static void DribbleFeel()
    {
        var p=new GameObject("Dribble player",typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(PlayerMovement),typeof(PlayerController),typeof(Dribbler));
        var b=new GameObject("Dribble ball",typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(Ball));
        var ball=b.GetComponent<Ball>(); Call(ball,"Awake"); ball.Body.gravityScale=0; ball.Body.linearDamping=1.2f;
        ball.Body.position=Vector2.up*0.64f; b.GetComponent<CircleCollider2D>().radius=0.25f;
        var controller=p.GetComponent<PlayerController>(); controller.Body.gravityScale=0;
        p.GetComponent<CircleCollider2D>().radius=0.4f;
        var movement=p.GetComponent<PlayerMovement>(); var dribbler=p.GetComponent<Dribbler>();
        Call(dribbler,"Awake"); var possession=new PossessionSystem(); dribbler.Init(ball,possession,null);
        Physics2D.SyncTransforms(); Call(dribbler,"FixedUpdate"); controller.SetSprint(true);
        foreach(var direction in new[]{Vector2.up,Vector2.left,Vector2.right})
        {
            controller.SetMove(direction); bool retained=true;
            for(int i=0;i<50;i++)
            {
                Call(movement,"FixedUpdate"); Call(dribbler,"FixedUpdate"); Call(ball,"FixedUpdate"); Physics2D.Simulate(0.02f);
                if(!possession.IsOwner(controller)) retained=false;
            }
            Check(retained,"Sprint dribble retains possession through direction "+direction);
        }
        UnityEngine.Object.DestroyImmediate(p); UnityEngine.Object.DestroyImmediate(b);
    }

    static void InputRouting()
    {
        var hud=MatchHud.Create();
        var p = new GameObject("Input player",typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(PlayerMovement),typeof(PlayerController),typeof(Dribbler),typeof(PlayerKicker),typeof(HumanPlayerInput));
        var b = new GameObject("Input ball",typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(Ball));
        var ball=b.GetComponent<Ball>(); Call(ball,"Awake");
        var c=p.GetComponent<PlayerController>(); var d=p.GetComponent<Dribbler>(); var k=p.GetComponent<PlayerKicker>(); var input=p.GetComponent<HumanPlayerInput>();
        Call(d,"Awake"); Call(k,"Awake"); Call(input,"Awake");
        var possession=new PossessionSystem(); d.Init(ball,possession,null); k.Init(ball,possession); input.Init(hud);
        possession.TryAcquire(c);
        var finger=new PointerEventData(UnityEngine.Object.FindFirstObjectByType<EventSystem>()){pointerId=7};
        hud.PassButton.OnPointerDown(finger);
        Check(c.ChargingKick==PlayerKicker.KickType.Pass,"Mobile press routes through controller");
        hud.ShootButton.OnPointerDown(new PointerEventData(UnityEngine.Object.FindFirstObjectByType<EventSystem>()){pointerId=8});
        Check(c.ChargingKick==PlayerKicker.KickType.Pass,"Second mobile action cannot replace charge");
        hud.PassButton.OnPointerUp(finger); Call(k,"FixedUpdate");
        Check(!possession.HasOwner,"Mobile release executes physical kick");
        hud.ShootButton.Cancel(); possession.TryAcquire(c);
        hud.PassButton.OnPointerDown(finger); Call(input,"OnApplicationFocus",false);
        Check(c.ChargingKick==PlayerKicker.KickType.None && !hud.PassButton.IsHeld,"Focus loss cancels mobile charge");
        Call(input,"OnApplicationPause",false); hud.PassButton.OnPointerDown(finger);
        Check(c.ChargingKick==PlayerKicker.KickType.None,"Pause resume does not override lost focus");
        hud.PassButton.Cancel(); Call(input,"OnApplicationFocus",true); hud.PassButton.OnPointerDown(finger);
        Check(c.ChargingKick==PlayerKicker.KickType.Pass,"Fresh mobile press works after focus restoration");
        hud.PassButton.Cancel(); Call(k,"FixedUpdate");
        Check(possession.IsOwner(c),"Cancelled mobile charge retains possession");
        Check(hud.transform.Find("SafeArea/PassButton")!=null,"Action controls are under safe-area layout");
        hud.SetCharge(PlayerKicker.KickType.Pass,2);
        var passRing=hud.PassButton.transform.Find("PowerRing").GetComponent<UnityEngine.UI.Image>();
        var shotRing=hud.ShootButton.transform.Find("PowerRing").GetComponent<UnityEngine.UI.Image>();
        Check(Near(passRing.fillAmount,1) && Near(shotRing.fillAmount,0),"Pass indicator clamps maximum power");
        hud.SetCharge(PlayerKicker.KickType.Shot,0.5f);
        Check(Near(passRing.fillAmount,0) && Near(shotRing.fillAmount,0.5f),"Shot indicator clears pass indicator");
        hud.SetCharge(PlayerKicker.KickType.None,0);
        Check(!hud.transform.Find("SafeArea/Charge").gameObject.activeSelf && Near(shotRing.fillAmount,0),"Charge UI clears when action ends");
        bool decorativeHit=false;
        foreach(var graphic in hud.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            if(graphic.raycastTarget && graphic.name!="PassButton" && graphic.name!="ShootButton"
                && graphic.name!="SprintButton" && graphic.name!="JoystickBase" && graphic.name!="PauseButton") decorativeHit=true;
        Check(!decorativeHit,"Decorative HUD graphics never intercept touch input");
        hud.ShowMessage("FULL TIME\n"+MatchHud.ScoreLine(2,1)+"\nTap to play again");
        var banner=hud.transform.Find("SafeArea/Banner");
        Check(!banner.GetComponent<CanvasGroup>().blocksRaycasts,"Match announcement never blocks controls");
        Check(banner.Find("Message").GetComponent<UnityEngine.UI.Text>().text=="FULL TIME",
            "Multiline announcement has distinct title");
        Check(banner.Find("Detail").GetComponent<UnityEngine.UI.Text>().text.Contains("Tap to play again"),
            "Full-time result retains restart instruction");
        UnityEngine.Object.DestroyImmediate(p); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(hud.gameObject);
    }
}
}
