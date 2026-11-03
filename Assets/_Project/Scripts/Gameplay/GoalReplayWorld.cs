using PointPass.Core;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Full world presentation on an isolated layer, with no running gameplay or colliders.</summary>
    public class GoalReplayWorld : MonoBehaviour
    {
        private const int ReplayLayer = 30;
        private static readonly Vector3 Origin = new Vector3(1000, 1000, 0);
        private GoalReplayClip clip;
        private Transform ball, ballVisual;
        private Transform[] actors;
        private Transform[][] bones;
        private SpriteRenderer[][] boneRenderers;
        private GoalNet[] nets;
        private StadiumCrowd crowd;
        private GoalConfetti confetti;
        private SidelineReferee referee;
        private StadiumPhotographers photographers;
        private Camera replayCamera;
        private LineRenderer shotArrow;
        private Material arrowMaterial;
        private float presentedTime;
        private bool celebrated;
        public Camera Camera => replayCamera;
        public Vector2 BallPosition => ball != null ? (Vector2)(ball.position - Origin) : Vector2.zero;
        public bool ShotArrowVisible => shotArrow != null && shotArrow.enabled;

        public void Build(GoalReplayClip replay, RenderTexture target)
        {
            clip = replay;
            transform.position = Origin;
            PitchBuilder.Build(transform, null);
            Ball replayBall = MatchBootstrap.CreateBall(transform);
            ball = replayBall.transform; ballVisual = ball.Find("Sprite");
            var possession = new PossessionSystem();
            actors = new Transform[clip.teams.Length]; bones = new Transform[actors.Length][];
            boneRenderers = new SpriteRenderer[actors.Length][];
            for (int i = 0; i < actors.Length; i++)
            {
                bool goalkeeper = clip.goalkeepers != null && i < clip.goalkeepers.Length && clip.goalkeepers[i];
                PlayerController actor = goalkeeper
                    ? MatchBootstrap.CreateGoalkeeper(transform, clip.teams[i], replayBall, possession, Vector2.zero, 0)
                    : MatchBootstrap.CreatePlayer(transform, clip.teams[i], replayBall, possession, Vector2.zero, 0);
                actors[i] = actor.transform;
                int count = clip.rigs != null && i < clip.rigs.Length && clip.rigs[i].paths != null ? clip.rigs[i].paths.Length : 0;
                bones[i] = new Transform[count]; boneRenderers[i] = new SpriteRenderer[count];
                for (int b = 0; b < count; b++)
                {
                    bones[i][b] = actors[i].Find(clip.rigs[i].paths[b]);
                    if (bones[i][b] != null) boneRenderers[i][b] = bones[i][b].GetComponent<SpriteRenderer>();
                }
            }
            nets = GetComponentsInChildren<GoalNet>(); crowd = GetComponentInChildren<StadiumCrowd>();
            confetti = GetComponentInChildren<GoalConfetti>(); referee = GetComponentInChildren<SidelineReferee>();
            photographers = GetComponentInChildren<StadiumPhotographers>();
            if (photographers != null) photographers.AffectGlobalLighting = false;
            // Disable every gameplay and automatic animation clock. Recorded poses and local
            // presentation events alone drive this view, even while the live match is paused.
            foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>())
                if (component != this && !(component is StadiumShadow) && component.GetType().Name != "StadiumActorLighting") component.enabled = false;
            foreach (Collider2D collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            foreach (Rigidbody2D body in GetComponentsInChildren<Rigidbody2D>()) body.simulated = false;
            foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>()) { trail.emitting = false; trail.Clear(); }
            replayCamera = new GameObject("ReplayCamera", typeof(Camera)).GetComponent<Camera>();
            replayCamera.transform.SetParent(transform, false); replayCamera.orthographic = true;
            replayCamera.clearFlags = CameraClearFlags.SolidColor; replayCamera.backgroundColor = MatchPalette.Ink;
            replayCamera.targetTexture = target; replayCamera.allowMSAA = true; replayCamera.allowHDR = false;
            replayCamera.allowDynamicResolution = false; replayCamera.cullingMask = 1 << ReplayLayer;
            replayCamera.nearClipPlane = 0.1f; replayCamera.farClipPlane = 50;
            GameObject arrow = new GameObject("RecordedShotArrow", typeof(LineRenderer)); arrow.transform.SetParent(transform, false);
            shotArrow = arrow.GetComponent<LineRenderer>(); arrowMaterial = new Material(Shader.Find("Sprites/Default"));
            shotArrow.sharedMaterial = arrowMaterial; shotArrow.useWorldSpace = true; shotArrow.positionCount = 5;
            shotArrow.widthMultiplier = 0.05f; shotArrow.sortingOrder = 15; shotArrow.numCapVertices = 3;
            shotArrow.startColor = shotArrow.endColor = MatchPalette.Accent; shotArrow.enabled = false;
            foreach (Transform child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = ReplayLayer;
            FrameCamera();
            ResetPresentation(); Present(clip.frames[0].time, false);
        }

        private void ResetPresentation()
        {
            celebrated = false; presentedTime = clip.frames[0].time;
            foreach (GoalNet net in nets) net.ResetNet();
            crowd?.ResetCelebration(); confetti?.ResetMatch(); referee?.ResetSignal(); photographers?.ResetFlashes();
        }
        public void Present(float time, bool showArrow)
        {
            if (clip == null) return;
            if (time < presentedTime) ResetPresentation();
            // Integrate local event clocks in short replay-time steps. Changing speed changes
            // only the playhead, never physics or the global game time scale.
            while (presentedTime + 0.0001f < time)
            {
                float next = Mathf.Min(time, presentedTime + 0.025f);
                if (!celebrated && next >= clip.goalTime)
                {
                    celebrated = true; crowd?.Celebrate(clip.scoringTeam); confetti?.Celebrate(clip.scoringTeam);
                    referee?.SignalGoal(clip.scoringTeam); photographers?.Celebrate();
                }
                if (clip.impacts != null) foreach (GoalReplayClip.NetImpact impact in clip.impacts)
                    if (impact.time >= presentedTime && impact.time < next)
                        foreach (GoalNet net in nets) if (Mathf.Sign(net.Side) == Mathf.Sign(impact.side)) net.Impact(impact.point, impact.speed);
                float delta = next - presentedTime;
                foreach (GoalNet net in nets) net.Advance(delta);
                crowd?.Advance(delta); confetti?.Advance(delta); referee?.Advance(delta); photographers?.Advance(delta);
                presentedTime = next;
            }
            int nextFrame = 1;
            while (nextFrame < clip.frames.Length - 1 && clip.frames[nextFrame].time < time) nextFrame++;
            GoalReplayClip.Frame a = clip.frames[nextFrame - 1], b = clip.frames[nextFrame];
            float t = Mathf.InverseLerp(a.time, b.time, time);
            ball.position = Origin + (Vector3)Vector2.Lerp(a.ball, b.ball, t);
            if (ballVisual != null) ballVisual.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(a.ballAngle, b.ballAngle, t));
            for (int p = 0; p < actors.Length; p++)
            {
                actors[p].position = Origin + (Vector3)Vector2.Lerp(a.players[p], b.players[p], t);
                actors[p].rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(a.angles[p], b.angles[p], t));
                if (a.poses == null || b.poses == null) continue;
                for (int bone = 0; bone < bones[p].Length; bone++)
                {
                    if (bones[p][bone] == null) continue;
                    GoalReplayClip.BonePose from = a.poses[p].bones[bone], to = b.poses[p].bones[bone];
                    bones[p][bone].localPosition = Vector2.Lerp(from.position, to.position, t);
                    Vector2 scale = Vector2.Lerp(from.scale, to.scale, t);
                    bones[p][bone].localScale = new Vector3(scale.x, scale.y, 1);
                    bones[p][bone].localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(from.angle, to.angle, t));
                    if (boneRenderers[p][bone] != null) boneRenderers[p][bone].sortingOrder = t < 0.5f ? from.order : to.order;
                }
            }
            DrawArrow(time, showArrow);
        }

        private void FrameCamera()
        {
            // A single fixed shot envelope keeps the entire recorded action and scoring net
            // inside frame. No chasing, cuts or zoom jumps can lose a fast curved ball.
            Bounds bounds = new Bounds(clip.frames[0].ball, Vector3.zero);
            foreach (GoalReplayClip.Frame frame in clip.frames)
            {
                bounds.Encapsulate(frame.ball);
                if (clip.scorer >= 0 && clip.scorer < frame.players.Length) bounds.Encapsulate(frame.players[clip.scorer]);
                else foreach (Vector2 player in frame.players) bounds.Encapsulate(player);
            }
            float goalSide = clip.scoringTeam == Team.Blue ? 1 : -1;
            bounds.Encapsulate(new Vector3(goalSide * (MatchConfig.FieldHalfWidth + MatchConfig.GoalDepth), MatchConfig.GoalWidth * 0.5f, 0));
            bounds.Encapsulate(new Vector3(goalSide * (MatchConfig.FieldHalfWidth + MatchConfig.GoalDepth), -MatchConfig.GoalWidth * 0.5f, 0));
            bounds.Expand(new Vector3(3.6f, 5f, 0));
            float aspect = replayCamera.targetTexture != null ? (float)replayCamera.targetTexture.width / replayCamera.targetTexture.height : 16f / 9f;
            replayCamera.orthographicSize = Mathf.Max(4.8f, bounds.extents.y + 0.2f, (bounds.extents.x + 0.2f) / aspect);
            replayCamera.transform.position = Origin + new Vector3(bounds.center.x, bounds.center.y, -10);
        }
        private void DrawArrow(float time, bool visible)
        {
            shotArrow.enabled = false;
            if (!visible || clip.shots == null) return;
            foreach (GoalReplayClip.Shot shot in clip.shots)
            {
                if (time < shot.time || time > shot.time + 0.7f) continue;
                Vector2 direction = shot.direction.normalized, side = Vector2.Perpendicular(direction);
                Vector3 start = Origin + (Vector3)shot.origin;
                Vector3 end = start + (Vector3)(direction * 1.65f);
                shotArrow.SetPosition(0, start); shotArrow.SetPosition(1, end);
                shotArrow.SetPosition(2, end - (Vector3)(direction * 0.28f - side * 0.17f));
                shotArrow.SetPosition(3, end); shotArrow.SetPosition(4, end - (Vector3)(direction * 0.28f + side * 0.17f));
                shotArrow.enabled = true;
            }
        }
        public void RefreshAccent()
        {
            if (shotArrow != null) shotArrow.startColor = shotArrow.endColor = MatchPalette.Accent;
        }
        private void OnEnable() { if (replayCamera != null) replayCamera.enabled = true; }
        private void OnDisable() { if (replayCamera != null) replayCamera.enabled = false; }
        private void OnDestroy()
        {
            if (arrowMaterial != null) { if (Application.isPlaying) Destroy(arrowMaterial); else DestroyImmediate(arrowMaterial); }
        }
    }
}
