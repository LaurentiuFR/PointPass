using System;
using System.Reflection;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PointPass.Validation
{
    /// <summary>Intentional touch cancellation must not become a release-to-kick.</summary>
    public static class MobileCancellationValidation
    {
        private static void Call(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        public static void Run(Action<bool, string> check)
        {
            MatchHud hud = MatchHud.Create();
            GameObject playerObject = new GameObject("Mobile cancel player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker), typeof(HumanPlayerInput));
            GameObject ballObject = new GameObject("Mobile cancel ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
            try
            {
                Ball ball = ballObject.GetComponent<Ball>(); Call(ball, "Awake"); ball.Body.gravityScale = 0;
                PlayerController player = playerObject.GetComponent<PlayerController>();
                Dribbler dribbler = playerObject.GetComponent<Dribbler>(); Call(dribbler, "Awake");
                PlayerKicker kicker = playerObject.GetComponent<PlayerKicker>(); Call(kicker, "Awake");
                HumanPlayerInput input = playerObject.GetComponent<HumanPlayerInput>(); Call(input, "Awake");
                PossessionSystem possession = new PossessionSystem();
                dribbler.Init(ball, possession, null); kicker.Init(ball, possession); input.Init(hud); Call(input, "OnEnable");
                hud.GetComponent<CanvasScaler>().enabled = false; hud.GetComponent<Canvas>().scaleFactor = 1;
                Call(hud.ShootButton, "Awake"); Call(hud.PassButton, "Awake"); Call(hud.SprintButton, "Awake");
                hud.SetPlayerState(true, true, false);
                Call(input, "Update"); // Arm sprint after the required released state.
                EventSystem system = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
                PointerEventData shotFinger = new PointerEventData(system) { pointerId = 11, position = new Vector2(200, 200) };
                PointerEventData sprintFinger = new PointerEventData(system) { pointerId = 12, position = new Vector2(400, 200) };
                Text bend = hud.ShootButton.transform.Find("BendStrength").GetComponent<Text>();
                check(!hud.CancelChargeButton.gameObject.activeSelf && !bend.gameObject.activeSelf,
                    "Mobile Cancel and bend feedback stay hidden when no kick is charging");
                possession.TryAcquire(player); player.SetMove(Vector2.right);
                hud.SprintButton.OnPointerDown(sprintFinger);
                hud.ShootButton.OnPointerDown(shotFinger);
                shotFinger.position += Vector2.up * 90; hud.ShootButton.OnDrag(shotFinger);
                Call(input, "Update");
                check(hud.CancelChargeButton.gameObject.activeSelf && bend.text == "LEFT 100%" && bend.gameObject.activeSelf,
                    "Charged touch shot exposes a separate Cancel button and signed bend strength near Shoot");
                hud.CancelChargeButton.onClick.Invoke();
                check(player.ChargingKick == PlayerKicker.KickType.None && possession.IsOwner(player) && !hud.ShootButton.IsHeld,
                    "Touch Cancel ends the owned hold while retaining ball possession");
                hud.ShootButton.OnPointerUp(shotFinger); Call(kicker, "FixedUpdate");
                check(ball.Body.linearVelocity == Vector2.zero && ball.Curve == 0 && possession.IsOwner(player),
                    "Releasing a cancelled shot finger cannot fire a delayed kick or curve");
                Call(input, "Update");
                check(hud.SprintButton.IsHeld && (bool)typeof(PlayerMovement).GetField("sprinting", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(player.GetComponent<PlayerMovement>()), "Cancelling a charge preserves an independently held sprint finger");
                check(!hud.CancelChargeButton.gameObject.activeSelf && !bend.gameObject.activeSelf,
                    "Cancellation clears the Cancel control and bend indicator immediately");
                hud.PassButton.OnPointerDown(shotFinger); Call(input, "Update");
                check(hud.CancelChargeButton.gameObject.activeSelf && !bend.gameObject.activeSelf,
                    "Pass charges can be cancelled without showing a shot bend indicator");
                hud.CancelChargeButton.onClick.Invoke(); hud.PassButton.OnPointerUp(shotFinger); Call(kicker, "FixedUpdate");
                check(possession.IsOwner(player) && ball.Body.linearVelocity == Vector2.zero,
                    "Cancelling a mobile pass retains possession and suppresses its stale release");
                hud.ShootButton.OnPointerDown(shotFinger); player.SetShotCurve(-0.5f); Call(input, "Update");
                check(bend.text == "RIGHT 50%", "Bend feedback displays the signed proportional strength rather than only a direction");
                Call(input, "OnApplicationFocus", false);
                check(player.ChargingKick == PlayerKicker.KickType.None && !hud.CancelChargeButton.gameObject.activeSelf && !bend.gameObject.activeSelf,
                    "Focus loss cancels the charge and clears all mobile charge feedback");
                hud.SetCharge(PlayerKicker.KickType.Shot, 0.5f, float.NaN);
                check(bend.text == "STRAIGHT", "Invalid bend strength falls back to straight feedback");
                hud.SetPracticeMode(true);
                check(!hud.transform.Find("SafeArea/Scoreboard").gameObject.activeSelf
                    && !hud.transform.Find("SafeArea/ClockPill").gameObject.activeSelf && hud.ShootButton.gameObject.activeSelf,
                    "Practice hides match score and timer while retaining action controls");
                hud.SetPracticeMode(false);
                check(hud.transform.Find("SafeArea/Scoreboard").gameObject.activeSelf
                    && hud.transform.Find("SafeArea/ClockPill").gameObject.activeSelf,
                    "Leaving practice restores the normal scoreboard and timer");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject); UnityEngine.Object.DestroyImmediate(ballObject);
                UnityEngine.Object.DestroyImmediate(hud.gameObject);
            }
        }
    }
}
