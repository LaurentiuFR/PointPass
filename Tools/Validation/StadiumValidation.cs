using System;
using PointPass.Core;
using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Presentation geometry must remain bounded, batched and independent of pitch physics.</summary>
    public static class StadiumValidation
    {
        public static void Run(Action<bool, string> check)
        {
            GameObject root = new GameObject("Stadium validation");
            try
            {
                StadiumBuilder.Build(root.transform);
                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>();
                check(filters.Length == 2 && renderers.Length == 2,
                    "Stadium dressing and markings use two static mesh renderers");
                check(root.GetComponentsInChildren<Collider2D>().Length == 0,
                    "Stadium presentation adds no physical obstacles");
                check(renderers[0].sharedMaterial == renderers[1].sharedMaterial,
                    "Stadium meshes share one runtime material");

                bool finite = true;
                bool safeIndices = true;
                bool safeCounts = true;
                Mesh surround = null;
                Mesh markings = null;
                foreach (MeshFilter filter in filters)
                {
                    Mesh mesh = filter.sharedMesh;
                    safeCounts &= mesh.vertexCount > 0 && mesh.vertexCount < 65535;
                    foreach (Vector3 point in mesh.vertices)
                        finite &= IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z);
                    foreach (int index in mesh.triangles)
                        safeIndices &= index >= 0 && index < mesh.vertexCount;
                    if (filter.name == "StadiumSurrounds") surround = mesh;
                    if (filter.name == "PitchMarkings") markings = mesh;
                }
                check(finite && safeIndices && safeCounts,
                    "Stadium mesh vertices and indices are finite and safe for sixteen-bit buffers");
                check(surround != null && surround.bounds.extents.x > MatchConfig.PitchWidth * 0.5f + MatchConfig.GoalDepth
                    && surround.bounds.extents.y > MatchConfig.PitchHeight * 0.5f,
                    "Stadium surrounds extend beyond the field and goal recesses");
                bool markingsBounded = markings != null;
                if (markings != null)
                    foreach (Vector3 point in markings.vertices)
                        markingsBounded &= Mathf.Abs(point.x) <= MatchConfig.FieldHalfWidth + 0.04f
                            && Mathf.Abs(point.y) <= MatchConfig.FieldHalfHeight + 0.04f;
                check(markingsBounded, "Painted pitch markings stay within the configured field boundary");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            GameObject pitchRoot = new GameObject("Pitch physics validation");
            try
            {
                PitchBuilder.Build(pitchRoot.transform, team => { });
                check(pitchRoot.GetComponentsInChildren<Collider2D>().Length == 10
                    && pitchRoot.GetComponentsInChildren<GoalZone>().Length == 2,
                    "Stadium rendering preserves eight pitch walls and two goal triggers");
                Transform pitch = pitchRoot.transform.Find("Pitch");
                BoxCollider2D top = pitch.Find("WallTop").GetComponent<BoxCollider2D>();
                BoxCollider2D side = pitch.Find("WallRightUpper").GetComponent<BoxCollider2D>();
                check(Mathf.Abs(top.transform.localPosition.y - top.size.y * 0.5f - MatchConfig.FieldHalfHeight) < 0.0001f
                    && Mathf.Abs(side.transform.localPosition.x - side.size.x * 0.5f - MatchConfig.FieldHalfWidth) < 0.0001f,
                    "Physical pitch wall faces remain aligned with the painted boundary");
                BoxCollider2D goal = pitch.Find("GoalZoneRight").GetComponent<BoxCollider2D>();
                float expectedX = MatchConfig.FieldHalfWidth + MatchConfig.BallDiameter
                    + (MatchConfig.GoalDepth - MatchConfig.BallDiameter) * 0.5f;
                check(goal.isTrigger && Mathf.Abs(goal.transform.localPosition.x - expectedX) < 0.0001f,
                    "Goal trigger geometry is unchanged by stadium dressing");
            }
            finally { UnityEngine.Object.DestroyImmediate(pitchRoot); }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
