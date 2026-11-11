using EFT;
using UnityEngine;

namespace AdvancedMovement.Moves
{
    internal static class AirAim
    {
        public static bool AllowAimCheck;

        private const float LandingFadeTime = 0.4f;
        private static float _lastAirTime = -100f;

        public static bool Enabled => Settings.AirAimEnabled.Value;

        public static bool IsAirState(EPlayerState state)
        {
            return state == EPlayerState.Jump || (state == EPlayerState.FallDown && Settings.AirAimInFalls.Value);
        }

        public static bool InAir(Player player)
        {
            if (MoveController.Diving)
            {
                return true;
            }
            BaseMovementState state = player.MovementContext.CurrentState;
            return state != null && IsAirState(state.Name);
        }

        public static float SteadyWeight(Player player)
        {
            BaseMovementState state = player.MovementContext.CurrentState;
            if (InAir(player) || (state != null && state.Name == EPlayerState.JumpLanding))
            {
                _lastAirTime = Time.time;
                return 1f;
            }
            float t = (Time.time - _lastAirTime) / LandingFadeTime;
            return t >= 1f ? 0f : 1f - Mathf.SmoothStep(0f, 1f, t);
        }

        public static bool IsLocalFirearm(Player.FirearmController firearm, out Player player)
        {
            player = Patches.LocalPlayer();
            return Enabled && player != null && player.HandsController == firearm;
        }

        public static bool IsAiming(Player player)
        {
            return player.HandsController is Player.FirearmController firearm && firearm.IsAiming;
        }
    }
}
