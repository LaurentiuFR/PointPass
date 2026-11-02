using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Feathered contact and floodlight shadows, kept in world space as the athlete turns.</summary>
    public class StadiumShadow : MonoBehaviour
    {
        private readonly Transform[] shadows = new Transform[4];
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
                renderer.color = new Color(0.015f, 0.025f, 0.02f, i == 0 ? 0.22f : 0.08f);
                renderer.sortingOrder = order;
                shadows[i] = child.transform;
            }
            LateUpdate();
        }

        private void LateUpdate()
        {
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] == null) continue;
                Vector2 direction = ((Vector2)transform.position - StadiumFixtures.LightPosition(i)).normalized;
                shadows[i].position = transform.position + (Vector3)(direction * height * (i == 0 ? 0.35f : 0.6f));
                shadows[i].rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                // Counteract parent scale so ball/player/referee shadows stay consistent.
                Vector3 parentScale = transform.lossyScale;
                shadows[i].localScale = new Vector3(size * (i == 0 ? 1.4f : 1.8f) / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                    size * 0.85f / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1);
            }
        }
    }
}
