using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Retains the original grass detail while remapping its overly bright palette.</summary>
    public class TurfSurface : MonoBehaviour
    {
        private Material material;
        public float Wetness { get; private set; }

        public void Init(SpriteRenderer surface)
        {
            Shader shader = Resources.Load<Shader>("Art/NaturalTurf");
            if (shader == null) return;
            material = new Material(shader) { name = "Natural muted turf" };
            surface.sharedMaterial = material;
            surface.color = Color.white;
            material.SetVector("_StadiumOrigin", surface.transform.parent != null
                ? surface.transform.parent.position : Vector3.zero);
            SetWetness(0f);
        }

        /// <summary>Presentation parameter only; dry is the default and does not change ball physics.</summary>
        public void SetWetness(float wetness)
        {
            Wetness = float.IsNaN(wetness) || float.IsInfinity(wetness) ? 0f : Mathf.Clamp01(wetness);
            if (material != null) material.SetFloat("_Wetness", Wetness);
        }

        private void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
