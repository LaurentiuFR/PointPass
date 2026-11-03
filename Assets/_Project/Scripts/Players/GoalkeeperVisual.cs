using UnityEngine;

namespace PointPass.Players
{
    /// <summary>Visual-only dive, gather and recovery poses; recorded as normal replay bones.</summary>
    public class GoalkeeperVisual : MonoBehaviour
    {
        private GoalkeeperBrain brain;
        private Transform torso, left, right;
        private Vector3 torsoRest, leftRest, rightRest;
        private Material kit;

        public void Init(Material ownedKit)
        {
            brain = GetComponent<GoalkeeperBrain>(); kit = ownedKit;
            torso = transform.Find("Sprite"); left = transform.Find("LeftArm"); right = transform.Find("RightArm");
            torsoRest = torso.localPosition; leftRest = left.localPosition; rightRest = right.localPosition;
        }
        private void LateUpdate()
        {
            if (brain == null || Time.timeScale <= 0) return;
            Pose();
        }
        public void Pose()
        {
            if (torso == null) return;
            bool diving = brain.State == GoalkeeperBrain.KeeperState.Diving;
            bool recovering = brain.State == GoalkeeperBrain.KeeperState.Recovering;
            bool holding = brain.State == GoalkeeperBrain.KeeperState.Holding || brain.State == GoalkeeperBrain.KeeperState.Distributing;
            if (!diving && !recovering && !holding) return;
            float dive = diving ? Mathf.Clamp01(brain.StateAge / 0.08f) : recovering ? 1 - Mathf.Clamp01(brain.StateAge / 0.55f) : 0;
            torso.localPosition = torsoRest;
            torso.localRotation = Quaternion.Euler(0, 0, -brain.DiveSide * 65f * dive);
            left.localPosition = leftRest + new Vector3(-0.08f * dive, holding ? 0.04f : dive * 0.08f, 0);
            right.localPosition = rightRest + new Vector3(0.08f * dive, holding ? 0.04f : dive * 0.08f, 0);
            left.localRotation = Quaternion.Euler(0, 0, holding ? -155 : -120 * dive);
            right.localRotation = Quaternion.Euler(0, 0, holding ? 155 : 120 * dive);
        }
        private void OnDestroy()
        {
            if (kit == null) return;
            if (Application.isPlaying) Destroy(kit); else DestroyImmediate(kit);
        }
    }
}
