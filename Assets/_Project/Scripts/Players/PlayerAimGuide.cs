using UnityEngine;
using PointPass.Gameplay;
using PointPass.Core;

namespace PointPass.Players
{
    /// <summary>Local presentation only: shows the commanded kick direction while charging.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAimGuide : MonoBehaviour
    {
        private PlayerController controller;
        private LineRenderer line;
        private LineRenderer selectionRing;
        private Material material;
        private Ball ball;

        public void Init(Ball matchBall) => ball = matchBall;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            GameObject guide = new GameObject("KickAimGuide");
            guide.transform.SetParent(transform, false);
            line = guide.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) material = new Material(shader);
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 21;
            line.widthMultiplier = 0.045f;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.sortingOrder = 3;
            line.enabled = false;

            GameObject ring = new GameObject("LocalPlayerRing");
            ring.transform.SetParent(transform, false);
            selectionRing = ring.AddComponent<LineRenderer>();
            selectionRing.sharedMaterial = material;
            selectionRing.useWorldSpace = true;
            selectionRing.loop = true;
            selectionRing.positionCount = 40;
            selectionRing.widthMultiplier = 0.025f;
            selectionRing.sortingOrder = 2;
            selectionRing.enabled = false;
        }

        private void LateUpdate()
        {
            selectionRing.enabled = material != null && controller.IsActive;
            if (selectionRing.enabled)
            {
                bool carrying = controller.Dribbler != null && controller.Dribbler.HasBall;
                Color ringColor = MatchPalette.Accent;
                ringColor.a = carrying ? 0.48f : 0.22f;
                selectionRing.startColor = selectionRing.endColor = ringColor;
                for (int i = 0; i < selectionRing.positionCount; i++)
                {
                    float angle = i * (Mathf.PI * 2f / selectionRing.positionCount);
                    selectionRing.SetPosition(i, controller.Body.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.55f);
                }
            }
            bool visible = material != null && ball != null && controller.IsActive && controller.Dribbler != null && controller.Dribbler.HasBall
                && controller.ChargingKick != PlayerKicker.KickType.None;
            line.enabled = visible;
            if (!visible) return;

            Vector2 direction = controller.AimDirection;
            Vector2 origin = ball.Body.position + direction * 0.35f;
            bool shooting = controller.ChargingKick == PlayerKicker.KickType.Shot;
            float charge = controller.KickCharge;
            float length = shooting ? Mathf.Lerp(2.3f, 4.5f, charge) : Mathf.Lerp(1.1f, 2.5f, charge);
            // This is a short direction cue, not a landing-point guarantee: collisions and
            // drag still determine the actual physical shot after release.
            float speed = shooting ? Mathf.Lerp(10f, 20f, charge) : Mathf.Lerp(8f, 14f, charge);
            float travelTime = length / speed;
            float curve = shooting ? controller.ShotCurve : 0f;
            const int segments = 16;
            Vector2 tip = origin;
            line.SetPosition(0, origin);
            for (int i = 1; i <= segments; i++)
            {
                float angle = ball.GetCurveAngle(curve, travelTime * (i - 0.5f) / segments) * Mathf.Deg2Rad;
                Vector2 tangent = new Vector2(direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle),
                    direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle));
                tip += tangent * (length / segments);
                line.SetPosition(i, tip);
            }
            float endAngle = ball.GetCurveAngle(curve, travelTime) * Mathf.Deg2Rad;
            Vector2 endDirection = new Vector2(direction.x * Mathf.Cos(endAngle) - direction.y * Mathf.Sin(endAngle),
                direction.x * Mathf.Sin(endAngle) + direction.y * Mathf.Cos(endAngle));
            Vector2 side = new Vector2(-endDirection.y, endDirection.x);
            line.SetPosition(17, tip - endDirection * 0.25f + side * 0.17f);
            line.SetPosition(18, tip);
            line.SetPosition(19, tip - endDirection * 0.25f - side * 0.17f);
            line.SetPosition(20, tip);
            Color color = shooting ? MatchPalette.Gold : MatchPalette.Accent;
            color.a = 0.75f;
            line.startColor = line.endColor = color;
        }

        private void OnDisable()
        {
            if (line != null) line.enabled = false;
            if (selectionRing != null) selectionRing.enabled = false;
        }
        private void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
