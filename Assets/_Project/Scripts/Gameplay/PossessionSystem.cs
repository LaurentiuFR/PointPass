using System;
using PointPass.Players;

namespace PointPass.Gameplay
{
    /// <summary>
    /// The one place that knows who owns the ball. Nothing else keeps its own "has ball" flag.
    /// A player can only take the ball if nobody else owns it. (Tackles and passes will build
    /// on this later by releasing or transferring ownership through this class.)
    /// </summary>
    public class PossessionSystem
    {
        public PlayerController Owner { get; private set; }

        /// <summary>Raised with the new owner, or null when the ball becomes free.</summary>
        public event Action<PlayerController> OwnerChanged;

        public bool HasOwner => Owner != null;

        public bool IsOwner(PlayerController player)
        {
            return player != null && Owner == player;
        }

        /// <summary>Returns true if the player now owns the ball (or already did).</summary>
        public bool TryAcquire(PlayerController player)
        {
            if (player == null) return false;
            if (Owner != null && Owner != player) return false;

            SetOwner(player);
            return true;
        }

        /// <summary>Only the current owner can release. Anyone else is ignored.</summary>
        public void Release(PlayerController player)
        {
            if (player == null || Owner != player) return;
            SetOwner(null);
        }

        /// <summary>Makes the ball free regardless of owner (kick-off, goals, resets).</summary>
        public void Clear()
        {
            SetOwner(null);
        }

        private void SetOwner(PlayerController newOwner)
        {
            if (Owner == newOwner) return;
            Owner = newOwner;
            OwnerChanged?.Invoke(newOwner);
        }
    }
}
