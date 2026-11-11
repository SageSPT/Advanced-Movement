using EFT;
using UnityEngine;

namespace AdvancedMovement.Moves
{
    internal static class AirSlide
    {
        private const float MaxQueueTime = 4f;

        private static Player _player;
        private static bool _queued;
        private static float _queuedAt;
        private static bool _wasInAir;
        private static Vector3 _airVelocity;

        public static void OnAirPose(MovementState state, float poseDelta)
        {
            Player player = Patches.LocalPlayer();
            if (player == null || state.MovementContext != player.MovementContext)
            {
                return;
            }
            if (!(state is JumpPlayerState))
            {
                if (!state.MovementContext.IsGrounded)
                {
                    MoveController.Log($"Crouch in the air ignored: state is {state.GetType().Name}, not a jump");
                }
                return;
            }
            _player = player;
            if (poseDelta < 0f)
            {
                _queued = Settings.SlideFromJump.Value;
                _queuedAt = Time.time;
                MoveController.Log($"Landing slide queued (pose {state.MovementContext.PoseLevel:F2})");
            }
            else if (poseDelta > 0f)
            {
                if (_queued)
                {
                    MoveController.Log("Landing slide cancelled: crouch released in the air");
                }
                _queued = false;
            }
        }

        public static void Update()
        {
            Player player = Patches.LocalPlayer();
            if (player == null || player != _player)
            {
                _queued = false;
                _wasInAir = false;
                _player = player;
                return;
            }
            MovementContext ctx = player.MovementContext;
            if (ctx == null)
            {
                return;
            }
            bool inAir = ctx.CurrentState is JumpPlayerState;
            if (inAir)
            {
                Vector3 v = ctx.Velocity;
                v.y = 0f;
                if (v.sqrMagnitude > 0.01f)
                {
                    _airVelocity = v;
                }
                _wasInAir = true;
                if (_queued && Time.time - _queuedAt > MaxQueueTime)
                {
                    MoveController.Log("Landing slide cancelled: in the air too long");
                    _queued = false;
                }
                return;
            }
            if (!_wasInAir)
            {
                return;
            }
            _wasInAir = false;
            bool queued = _queued;
            _queued = false;
            if (queued)
            {
                MoveController.Log($"Landed into {ctx.CurrentState?.GetType().Name}, grounded {ctx.IsGrounded}, air speed {_airVelocity.magnitude:F2} m/s");
            }
            if (queued && ctx.IsGrounded)
            {
                MoveController.TryStartLandingSlide(player, _airVelocity);
            }
        }
    }
}
