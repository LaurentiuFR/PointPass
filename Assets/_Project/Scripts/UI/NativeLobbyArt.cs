using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using UnityEngine;

namespace PointPass.UI
{
    /// <summary>Menu views of the actual match and actor factory. No separate menu character art.</summary>
    public class NativeLobbyArt : MonoBehaviour
    {
        private const int PreviewLayer = 29;
        private UnityEngine.UI.RawImage background;
        private readonly System.Collections.Generic.List<UnityEngine.UI.RawImage> portraits = new System.Collections.Generic.List<UnityEngine.UI.RawImage>();
        private readonly System.Collections.Generic.List<UnityEngine.UI.RawImage> pitches = new System.Collections.Generic.List<UnityEngine.UI.RawImage>();
        private Transform previewPlayer, previewBall, ballShadow, boot;
        private Vector3 playerScale, bootRest, ballRest, shadowScale;
        private Color shadowColor;
        private SpriteRenderer shadowRenderer;
        private RenderTexture matchTexture, softenedTexture, actorTexture;
        private Camera captureCamera, actorCamera;
        private GameObject previewWorld;
        private Material soften;
        private bool refresh = true;
        public RenderTexture PlayerTexture => actorTexture;
        public bool UsesMatchAssets => actorTexture != null && background != null && background.texture != null;

        public void BuildBackground()
        {
            background = LobbyUi.Rect(transform, "MatchBackground", Vector2.zero, new Vector2(1824, 1000))
                .gameObject.AddComponent<UnityEngine.UI.RawImage>();
            background.color = Color.white; background.raycastTarget = false;
            var fit = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 16f / 9;
        }
        public RectTransform PlayerPicture(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var image = LobbyUi.Rect(parent, name, position, size).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.color = Color.white; image.raycastTarget = false; image.texture = actorTexture;
            portraits.Add(image); return image.rectTransform;
        }
        public RectTransform PitchPicture(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var image = LobbyUi.Rect(parent, name, position, size).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.color = new Color(0.79f, 0.88f, 0.86f); image.raycastTarget = false;
            image.texture = matchTexture; pitches.Add(image); return image.rectTransform;
        }
        public void SetHomeVisible(bool home)
        {
            // The new menu has a quiet navy canvas; only the central scene shows the pitch.
            if (background != null) background.gameObject.SetActive(false);
        }
        private static RenderTexture Texture(string name, int width, int height, bool transparent = false, bool cameraTarget = false)
        {
            // URP Render Graph requires a depth attachment for camera output textures.
            var texture = new RenderTexture(width, height, transparent || cameraTarget ? 24 : 0, RenderTextureFormat.ARGB32)
            { name = name, antiAliasing = transparent ? 4 : 1, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.Create(); return texture;
        }
        private void BuildRenderer()
        {
            if (captureCamera != null) return;
            matchTexture = Texture("PointPass match backdrop", 1280, 720, cameraTarget: true);
            softenedTexture = Texture("PointPass softened match backdrop", 1280, 720);
            actorTexture = Texture("PointPass real player preview", 512, 512, true);
            Shader shader = Resources.Load<Shader>("Menu/NativeBackdropBlur");
            if (shader != null) soften = new Material(shader) { name = "Menu backdrop softening" };
            captureCamera = new GameObject("PointPass Menu Backdrop Camera", typeof(Camera)).GetComponent<Camera>();
            captureCamera.enabled = false;

            previewWorld = new GameObject("PointPass Native Menu Player");
            Vector3 origin = new Vector3(1000, 1000, 0); previewWorld.transform.position = origin;
            Ball ball = MatchBootstrap.CreateBall(previewWorld.transform);
            var possession = new PossessionSystem();
            PlayerController player = MatchBootstrap.CreatePlayer(previewWorld.transform, Team.Blue, ball, possession, origin, 0);
            player.Body.rotation = 180; player.transform.rotation = Quaternion.Euler(0, 0, 180);
            ball.Body.position = (Vector2)origin + new Vector2(0.32f, -0.34f);
            ball.transform.position = ball.Body.position;
            foreach (Transform child in previewWorld.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            foreach (MonoBehaviour script in previewWorld.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
            foreach (Rigidbody2D body in previewWorld.GetComponentsInChildren<Rigidbody2D>()) body.simulated = false;
            foreach (Collider2D collider in previewWorld.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            foreach (TrailRenderer trail in previewWorld.GetComponentsInChildren<TrailRenderer>()) trail.emitting = false;
            previewPlayer = player.transform; previewBall = ball.transform;
            playerScale = previewPlayer.localScale; ballRest = previewBall.localPosition;
            boot = previewPlayer.Find("BootRight"); if (boot != null) bootRest = boot.localPosition;
            ballShadow = previewBall.Find("FloodlightShadow0");
            if (ballShadow != null)
            {
                shadowScale = ballShadow.localScale; shadowRenderer = ballShadow.GetComponent<SpriteRenderer>();
                if (shadowRenderer != null) shadowColor = shadowRenderer.color;
            }
            actorCamera = new GameObject("Native Player Preview Camera", typeof(Camera)).GetComponent<Camera>();
            actorCamera.transform.SetParent(previewWorld.transform, false);
            actorCamera.transform.position = origin + new Vector3(0, 0.15f, -10);
            actorCamera.orthographic = true; actorCamera.orthographicSize = 1.1f;
            actorCamera.clearFlags = CameraClearFlags.SolidColor; actorCamera.backgroundColor = Color.clear;
            actorCamera.cullingMask = 1 << PreviewLayer; actorCamera.targetTexture = actorTexture;
            actorCamera.allowHDR = false; actorCamera.allowMSAA = true; actorCamera.enabled = false;
            foreach (var image in portraits) if (image != null) image.texture = actorTexture;
            foreach (var image in pitches) if (image != null) image.texture = matchTexture;
            background.texture = softenedTexture;
        }
        private void OnEnable() { refresh = true; if (previewWorld != null) previewWorld.SetActive(true); }
        private void OnDisable() { if (previewWorld != null) previewWorld.SetActive(false); }
        private void LateUpdate()
        {
            if (!Application.isPlaying || Camera.main == null) return;
            BuildRenderer();
            AnimateIdle(Time.unscaledTime);
            actorCamera.Render();
            if (!refresh) return;
            captureCamera.CopyFrom(Camera.main); captureCamera.enabled = false;
            captureCamera.cullingMask &= ~((1 << PreviewLayer) | (1 << 30));
            captureCamera.targetTexture = matchTexture; captureCamera.aspect = 16f / 9; captureCamera.allowHDR = false;
            // Capture the stadium without duplicating the live match's footballers or ball.
            var hidden = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (renderer.enabled && renderer.gameObject.layer != PreviewLayer
                    && (renderer.GetComponentInParent<PlayerController>() != null || renderer.GetComponentInParent<Ball>() != null))
                { hidden.Add(renderer); renderer.enabled = false; }
            try { captureCamera.Render(); }
            finally { foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true; }
            if (soften != null) Graphics.Blit(matchTexture, softenedTexture, soften); else Graphics.Blit(matchTexture, softenedTexture);
            actorCamera.Render(); refresh = false;
        }
        private void AnimateIdle(float time)
        {
            if (previewPlayer == null) return;
            float breath = Mathf.Sin(time * 2.1f) * 0.012f;
            previewPlayer.localScale = Vector3.Scale(playerScale, new Vector3(1f + breath * 0.3f, 1f + breath, 1));
            float phase = time % 5.2f;
            float flight = Mathf.Clamp01((phase - 3.8f) / 0.8f);
            float height = phase >= 3.8f && phase <= 4.6f ? 4f * flight * (1f - flight) : 0f;
            previewBall.localPosition = ballRest + Vector3.up * (height * 0.32f);
            previewBall.localScale = Vector3.one * (0.6f + height * 0.04f);
            if (boot != null) boot.localPosition = bootRest + Vector3.up * (Mathf.Max(0, 1f - Mathf.Abs(phase - 3.8f) / 0.13f) * 0.09f);
            if (ballShadow != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    Transform shadow = previewBall.Find("FloodlightShadow" + i);
                    if (shadow == null) continue;
                    shadow.localPosition = Vector3.down * (height * 0.32f);
                    shadow.localScale = shadowScale * (1f + height * 0.28f);
                    var renderer = shadow.GetComponent<SpriteRenderer>();
                    if (renderer != null) { Color shade = shadowColor; shade.a *= 1f - height * 0.45f; renderer.color = shade; }
                }
            }
        }
        private void OnDestroy()
        {
            if (captureCamera != null) DestroyOwnedResource(captureCamera.gameObject);
            if (previewWorld != null) DestroyOwnedResource(previewWorld);
            if (soften != null) DestroyOwnedResource(soften);
            foreach (var texture in new[] { matchTexture, softenedTexture, actorTexture })
                if (texture != null) { texture.Release(); DestroyOwnedResource(texture); }
        }
        private static void DestroyOwnedResource(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
