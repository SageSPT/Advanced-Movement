using EFT;
using UnityEngine;

namespace AdvancedMovement.Moves
{
    internal static class SlideJump
    {
        private const float ArmedTime = 0.5f;

        private static MovementContext _context;
        private static Vector3 _velocity;
        private static float _armedAt = -1f;

        public static void Arm(MovementContext context, Vector3 velocity)
        {
            _context = context;
            _velocity = velocity;
            _velocity.y = 0f;
            _armedAt = Time.time;
        }

        public static void OnJumpEnter(JumpPlayerState jump)
        {
            if (_armedAt < 0f || jump.MovementContext != _context)
            {
                return;
            }
            bool fresh = Time.time - _armedAt <= ArmedTime;
            _armedAt = -1f;
            _context = null;
            float speed = _velocity.magnitude;
            if (!fresh || speed < 0.1f || speed <= jump._initialMagnitude)
            {
                return;
            }
            jump._initialNormalizedDirection = _velocity / speed;
            jump._initialMagnitude = speed;
            jump._currentMagnitude = speed;
        }
    }
}
