using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Retains the original grass detail while remapping its overly bright palette.</summary>
    public class TurfSurface : MonoBehaviour
    {
        private Material material;

        public void Init(SpriteRenderer surface)
        {
            Shader shader = Resources.Load<Shader>("Art/NaturalTurf");
            if (shader == null) return;
            material = new Material(shader) { name = "Natural muted turf" };
            surface.sharedMaterial = material;
            surface.color = Color.white;
        }

        private void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
