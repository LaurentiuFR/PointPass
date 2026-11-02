using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Applies the stadium's projected 2D light model to sprite artwork, retaining kit materials.</summary>
    public class StadiumActorLighting : MonoBehaviour
    {
        private SpriteRenderer[] sprites;
        private Material spriteMaterial;
        private MaterialPropertyBlock properties;
        private Vector3 lastOrigin = new Vector3(float.PositiveInfinity, 0, 0);
        public Transform PitchOrigin { get; private set; }

        public void Init(Transform pitchOrigin)
        {
            PitchOrigin = pitchOrigin;
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            Shader shader = Resources.Load<Shader>("Art/StadiumSprite");
            if (shader != null && spriteMaterial == null)
                spriteMaterial = new Material(shader) { name = "Stadium sprite illumination" };
            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite.name.StartsWith("FloodlightShadow")) continue;
                if (sprite.sharedMaterial != null && sprite.sharedMaterial.shader.name == "PointPass/Club Kit") continue;
                if (spriteMaterial != null) sprite.sharedMaterial = spriteMaterial;
            }
            lastOrigin = new Vector3(float.PositiveInfinity, 0, 0);
            LateUpdate();
        }

        private void LateUpdate()
        {
            Vector3 origin = PitchOrigin != null ? PitchOrigin.position : Vector3.zero;
            if (sprites == null || origin == lastOrigin) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite == null || sprite.name.StartsWith("FloodlightShadow")) continue;
                sprite.GetPropertyBlock(properties);
                properties.SetVector("_StadiumOrigin", origin);
                properties.SetFloat("_WorldLighting", 1f);
                sprite.SetPropertyBlock(properties);
            }
            lastOrigin = origin;
        }

        private void OnDestroy()
        {
            if (spriteMaterial == null) return;
            if (Application.isPlaying) Destroy(spriteMaterial); else DestroyImmediate(spriteMaterial);
        }
    }
}
