using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Feathered contact and floodlight shadows, kept in world space as the athlete turns.</summary>
    public class StadiumShadow : MonoBehaviour
    {
        private readonly Transform[] shadows = new Transform[4];
        private readonly SpriteRenderer[] renderers = new SpriteRenderer[4];
        private float size, height;

        public void Init(float diameter, float projectedHeight, int order)
        {
            size = diameter; height = projectedHeight;
            for (int i = 0; i < shadows.Length; i++)
            {
                GameObject child = new GameObject("FloodlightShadow" + i);
                child.transform.SetParent(transform, false);
                SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = SpriteLibrary.SoftDisc();
                renderer.color = new Color(0.015f, 0.025f, 0.055f, 0.08f);
                renderer.sortingOrder = order;
                shadows[i] = child.transform;
                renderers[i] = renderer;
            }
            LateUpdate();
        }

        private void LateUpdate()
        {
            StadiumActorLighting lighting = GetComponent<StadiumActorLighting>();
            Vector3 origin = lighting != null && lighting.PitchOrigin != null ? lighting.PitchOrigin.position
                : transform.parent != null ? transform.parent.position : Vector3.zero;
            Vector2 pitchPosition = transform.position - origin;
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] == null) continue;
                Vector2 fromLight = pitchPosition - StadiumFixtures.LightPosition(i);
                Vector2 direction = fromLight.normalized;
                float projection = Mathf.Clamp(fromLight.magnitude / 9f, 0.35f, 2f);
                shadows[i].position = transform.position + (Vector3)(direction * height * projection);
                shadows[i].rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                renderers[i].color = new Color(0.015f, 0.025f, 0.055f,
                    0.035f + StadiumFixtures.PoolAt(pitchPosition, i) * 0.075f);
                // Counteract parent scale so ball/player/referee shadows stay consistent.
                Vector3 parentScale = transform.lossyScale;
                shadows[i].localScale = new Vector3(size * (1.15f + projection * 0.35f) / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                    size * 0.85f / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1);
            }
        }
    }
}
