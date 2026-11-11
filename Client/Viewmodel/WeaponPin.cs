using EFT;
using UnityEngine;

namespace AdvancedMovement.Viewmodel
{
    internal static class WeaponPin
    {
        private const float AimFadeTime = 0.2f;

        private static bool _hasPose;
        private static Vector3 _relPosition;
        private static Quaternion _relRotation = Quaternion.identity;
        private static bool _pinned;
        private static bool _releasing;
        private static float _releaseStart;
        private static float _releaseTime;
        private static Player.FirearmController _firearm;

        private static Transform _restoreTarget;
        private static Vector3 _restoreLocalPosition;
        private static Quaternion _restoreLocalRotation;

        public static void Record(Transform weaponRoot, Transform view)
        {
            if (_pinned || weaponRoot == null || view == null)
            {
                return;
            }
            Quaternion inverse = Quaternion.Inverse(view.rotation);
            _relPosition = inverse * (weaponRoot.position - view.position);
            _relRotation = inverse * weaponRoot.rotation;
            _hasPose = true;
        }

        public static void Pin(Player player)
        {
            if (!_hasPose || !Settings.PinWeaponOnProne.Value || !(player.HandsController is Player.FirearmController firearm))
            {
                return;
            }
            _firearm = firearm;
            _pinned = true;
            _releasing = false;
        }

        public static void Clear()
        {
            _pinned = false;
            _releasing = false;
            _hasPose = false;
            _firearm = null;
        }

        public static void Apply(Player player, Transform weaponRoot, Transform view)
        {
            if (!_pinned || weaponRoot == null || view == null)
            {
                return;
            }
            if (player.HandsController != _firearm)
            {
                Clear();
                return;
            }
            if (!_releasing)
            {
                bool aiming = _firearm.IsAiming;
                if (aiming || ShouldRelease(player))
                {
                    _releasing = true;
                    _releaseStart = Time.time;
                    _releaseTime = aiming ? Mathf.Min(AimFadeTime, Settings.PinFadeTime.Value) : Settings.PinFadeTime.Value;
                }
            }
            float weight = 1f;
            if (_releasing)
            {
                float t = (Time.time - _releaseStart) / _releaseTime;
                if (t >= 1f)
                {
                    Clear();
                    return;
                }
                weight = 1f - Mathf.SmoothStep(0f, 1f, t);
            }

            _restoreTarget = weaponRoot;
            _restoreLocalPosition = weaponRoot.localPosition;
            _restoreLocalRotation = weaponRoot.localRotation;

            Vector3 position = view.position + view.rotation * _relPosition;
            Quaternion rotation = view.rotation * _relRotation;
            weaponRoot.SetPositionAndRotation(
                Vector3.Lerp(weaponRoot.position, position, weight),
                Quaternion.Slerp(weaponRoot.rotation, rotation, weight));
        }

        private static bool ShouldRelease(Player player)
        {
            MovementContext ctx = player.MovementContext;
            return !ctx.IsInPronePose
                || ctx.MovementDirection.sqrMagnitude > 0.01f
                || _firearm.IsTriggerPressed
                || _firearm.IsInReloadOperation();
        }

        public static void Restore()
        {
            if (_restoreTarget != null)
            {
                _restoreTarget.localPosition = _restoreLocalPosition;
                _restoreTarget.localRotation = _restoreLocalRotation;
                _restoreTarget = null;
            }
        }
    }
}
