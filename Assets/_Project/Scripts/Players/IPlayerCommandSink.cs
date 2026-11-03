using UnityEngine;

namespace PointPass.Players
{
    /// <summary>Optional command destination for a remotely simulated footballer.</summary>
    public interface IPlayerCommandSink
    {
        bool IsActive { get; }
        float KickCharge { get; }
        PlayerKicker.KickType ChargingKick { get; }
        float ShotCurve { get; }
        void SetMove(Vector2 direction);
        void SetSprint(bool held);
        bool BeginKick(PlayerKicker.KickType type);
        void ReleaseKick(PlayerKicker.KickType type);
        void CancelKick();
        void SetShotCurve(float curve);
        bool RequestTackle();
    }
}
