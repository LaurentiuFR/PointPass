using System;
using PointPass.Core;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Two nonphysical passing gates. Only an executed pass can score a gate hit.</summary>
    public class PassingPractice : MonoBehaviour
    {
        public const float GateX = 4f;
        public const float GateHalfHeight = 1.35f;
        public const float PostRadius = 0.11f;
        private Ball ball;
        private PossessionSystem possession;
        private PlayerController player;
        private PlayerKicker kicker;
        private Transform gates;
        private readonly SpriteRenderer[][] gateSprites = new SpriteRenderer[2][];
        private bool armed;
        private Vector2 previousBall;
        private float attemptAge;

        public bool IsRunning { get; private set; }
        public bool IsFreePlay { get; private set; }
        public int Attempts { get; private set; }
        public int Hits { get; private set; }
        public int ActiveGate { get; private set; }
        public Vector2 ActiveGateCenter => new Vector2(ActiveGate == 0 ? GateX : -GateX, 0f);
        public string Feedback { get; private set; } = "HOLD PASS, THEN RELEASE THROUGH THE LIT GATE";
        public event Action Changed;

        public void Init(Ball matchBall, PossessionSystem ownership, PlayerController footballer)
        {
            Unsubscribe();
            ball = matchBall; possession = ownership; player = footballer;
            kicker = player != null ? player.GetComponent<PlayerKicker>() : null;
            if (kicker != null) kicker.Kicked += OnKicked;
            if (possession != null) possession.OwnerChanged += OnOwnerChanged;
            MatchPalette.AccentChanged += OnAccentChanged;
            if (gates == null) BuildGates();
            gates.gameObject.SetActive(false);
        }

        public void BeginSession()
        {
            if (ball == null || player == null) return;
            IsFreePlay = false;
            IsRunning = true; Hits = Attempts = ActiveGate = 0; armed = false;
            player.GetComponent<PlayerMovement>().ResetTo(Vector2.zero, -90f);
            ResetBall();
            gates.gameObject.SetActive(true);
            Feedback = "HOLD PASS, THEN RELEASE THROUGH THE LIT GATE";
            Refresh();
        }

        public void EndSession()
        {
            IsRunning = armed = IsFreePlay = false;
            if (gates != null) gates.gameObject.SetActive(false);
        }

        public void BeginFreePlay()
        {
            BeginSession(); IsFreePlay = true; gates.gameObject.SetActive(false);
            Feedback = "FREE PLAY  /  MOVE, PASS AND SHOOT AT YOUR OWN PACE"; Refresh();
        }

        /// <summary>Explicit practice reset only; passing itself always uses physical ball travel.</summary>
        public void ResetBall()
        {
            if (!IsRunning || player == null || !player.IsActive || !ball.Body.simulated || Time.timeScale <= 0f) return;
            armed = false;
            player.CancelActions();
            player.GetComponent<HumanPlayerInput>()?.SuppressUntilRelease();
            player.Dribbler?.Release();
            possession.Clear();
            Vector2 position = player.Body.position + player.AimDirection * 0.68f;
            float margin = MatchConfig.BallDiameter * 0.5f + 0.04f;
            position.x = Mathf.Clamp(position.x, -MatchConfig.FieldHalfWidth + margin, MatchConfig.FieldHalfWidth - margin);
            position.y = Mathf.Clamp(position.y, -MatchConfig.FieldHalfHeight + margin, MatchConfig.FieldHalfHeight - margin);
            ball.ResetTo(position);
            previousBall = position;
            Feedback = IsFreePlay ? "FREE PLAY  /  BALL RESET" : "BALL RESET  /  COLLECT IT AND AIM FOR GATE " + (ActiveGate + 1);
            Refresh();
        }

        private void OnKicked(PlayerKicker.KickType type)
        {
            if (!IsRunning || !player.IsActive) return;
            if (IsFreePlay) { armed = false; return; }
            armed = type == PlayerKicker.KickType.Pass;
            previousBall = ball.Body.position;
            attemptAge = 0;
            if (armed) Attempts++;
            Feedback = armed ? "PASS AWAY  /  TARGET GATE " + (ActiveGate + 1)
                : "USE PASS TO HIT THE GATES";
            Refresh();
        }

        private void OnOwnerChanged(PlayerController owner)
        {
            if (owner == null || !armed || !IsRunning) return;
            armed = false;
            Feedback = "BALL COLLECTED  /  TRY THE NEXT PASS";
            Refresh();
        }

        private void FixedUpdate() => Advance(Time.fixedDeltaTime);

        /// <summary>Observes the physical segment; never changes ball position or velocity.</summary>
        public void Advance(float seconds)
        {
            if (!IsRunning || IsFreePlay || ball == null || player == null || !player.IsActive
                || !ball.Body.simulated || Time.timeScale <= 0f || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            Vector2 current = ball.Body.position;
            if (armed)
            {
                attemptAge += seconds;
                if (CrossesGate(previousBall, current, ActiveGate))
                {
                    armed = false; Hits++; ActiveGate = 1 - ActiveGate;
                    Feedback = "GATE HIT!  /  NEXT: GATE " + (ActiveGate + 1);
                    Refresh();
                }
                else if (attemptAge > 8f || (attemptAge > 0.2f && ball.Body.linearVelocity.sqrMagnitude < 0.04f))
                {
                    armed = false;
                    Feedback = "PASS FINISHED  /  RESET BALL OR COLLECT IT";
                    Refresh();
                }
            }
            previousBall = current;
        }

        /// <summary>Outward crossing, whole ball inside the posts; segment test prevents tunnelling.</summary>
        public static bool CrossesGate(Vector2 from, Vector2 to, int gate)
        {
            float sign = gate == 0 ? 1f : -1f;
            float plane = GateX + MatchConfig.BallDiameter * 0.5f;
            float before = from.x * sign, after = to.x * sign;
            if (before >= plane || after < plane || after <= before) return false;
            float t = (plane - before) / (after - before);
            float y = Mathf.Lerp(from.y, to.y, t);
            return Mathf.Abs(y) <= GateHalfHeight - PostRadius - MatchConfig.BallDiameter * 0.5f;
        }

        private void BuildGates()
        {
            gates = new GameObject("PracticeGates").transform;
            gates.SetParent(transform, false);
            for (int gate = 0; gate < 2; gate++)
            {
                Transform root = new GameObject("Gate" + (gate + 1)).transform;
                root.SetParent(gates, false);
                root.localPosition = new Vector3(gate == 0 ? GateX : -GateX, 0, 0);
                for (int post = 0; post < 2; post++)
                {
                    float y = post == 0 ? -GateHalfHeight : GateHalfHeight;
                    Marker(root, "Cone" + post, new Vector2(0.22f, 0.22f), new Vector2(0, y), 3, true);
                }
                for (int dash = 0; dash < 7; dash++)
                    Marker(root, "GateMark" + dash, new Vector2(0.04f, 0.14f),
                        new Vector2(0, Mathf.Lerp(-1.05f, 1.05f, dash / 6f)), 2, false);
                gateSprites[gate] = root.GetComponentsInChildren<SpriteRenderer>();
            }
            UpdateGateColours();
        }

        private static void Marker(Transform parent, string name, Vector2 size, Vector2 position, int order, bool circle)
        {
            SpriteRenderer marker = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            marker.transform.SetParent(parent, false); marker.transform.localPosition = position;
            marker.sprite = circle ? SpriteLibrary.WhiteCircle() : SpriteLibrary.WhiteSquare();
            Vector2 native = marker.sprite.bounds.size;
            marker.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1);
            marker.sortingOrder = order;
        }

        private void Refresh() { UpdateGateColours(); Changed?.Invoke(); }
        private void UpdateGateColours()
        {
            for (int gate = 0; gate < gateSprites.Length; gate++)
            {
                if (gateSprites[gate] == null) continue;
                foreach (SpriteRenderer sprite in gateSprites[gate])
                {
                    Color color = gate == ActiveGate ? MatchPalette.Accent : MatchPalette.Muted;
                    color.a = gate == ActiveGate ? 0.95f : 0.35f;
                    sprite.color = color;
                }
            }
        }
        private void OnAccentChanged(Color previous, Color accent)
        {
            if (this == null) { MatchPalette.AccentChanged -= OnAccentChanged; return; }
            UpdateGateColours();
        }
        private void Unsubscribe()
        {
            if (kicker != null) kicker.Kicked -= OnKicked;
            if (possession != null) possession.OwnerChanged -= OnOwnerChanged;
            MatchPalette.AccentChanged -= OnAccentChanged;
        }
        private void OnDestroy() => Unsubscribe();
    }
}
