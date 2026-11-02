using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Keeps the whole pitch (and goals) in view on any screen shape.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            float halfWidth = MatchConfig.PitchWidth * 0.5f + MatchConfig.GoalDepth + 0.6f;
            float halfHeight = MatchConfig.PitchHeight * 0.5f + 0.8f;
            cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / cam.aspect);
        }
    }
}
