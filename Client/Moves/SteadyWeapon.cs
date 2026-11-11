using AdvancedMovement.Body;
using EFT;
using UnityEngine;

namespace AdvancedMovement.Moves
{
    internal static class SteadyWeapon
    {
        private static float _lastMoveTime = -100f;

        public static float Weight(Player player)
        {
            float weight = 0f;
            if (AirAim.Enabled && AirAim.IsAiming(player))
            {
                weight = AirAim.SteadyWeight(player);
            }
            if (Settings.SteadyWeaponInMoves.Value)
            {
                if (MoveController.Busy || BodyPlayer.Active)
                {
                    _lastMoveTime = Time.time;
                    return 1f;
                }
                float t = (Time.time - _lastMoveTime) / Settings.SteadyWeaponFadeTime.Value;
                if (t < 1f)
                {
                    weight = Mathf.Max(weight, 1f - Mathf.SmoothStep(0f, 1f, t));
                }
            }
            return weight;
        }
    }
}
