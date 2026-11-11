using EFT;
using EFT.InventoryLogic;
using AdvancedMovement.Audio;
using AdvancedMovement.Motion;
using UnityEngine;

namespace AdvancedMovement.Viewmodel
{
    internal static class GesturePlayer
    {
        private enum Phase { None, In, Loop, Out }

        private const float CrossFade = 0.1f;
        private const string RifleSet = "slide_rifle";
        private const string AdsSet = "slide_aim";
        private const string DiveSet = "dive";

        private static GestureSet _set;
        private static string _hipSetName;
        private static string _adsSetName;
        private static Player _player;
        private static bool _setIsAds;
        private static Phase _phase;
        private static float _time;
        private static bool _ending;

        private static Quaternion _fromRotation = Quaternion.identity;
        private static Vector3 _fromPosition;
        private static float _fade;
        private static Quaternion _lastRotation = Quaternion.identity;
        private static Vector3 _lastPosition;

        private static Transform _restoreTarget;
        private static Vector3 _restoreLocalPosition;
        private static Quaternion _restoreLocalRotation;

        public static bool Active => _phase != Phase.None;

        public static void StartSlide(Player player)
        {
            StartSet(player, HipSetFor(player), AdsSet);
        }

        public static void StartDive(Player player)
        {
            StartSet(player, HasFirearm(player) ? DiveSet : null, DiveSet);
        }

        private static void StartSet(Player player, string hipSetName, string adsSetName)
        {
            bool wasActive = Active;
            if (!Settings.GestureEnabled.Value || Plugin.Gestures == null || hipSetName == null)
            {
                if (wasActive)
                {
                    EndSlide();
                }
                return;
            }
            bool aiming = IsAiming(player);
            GestureSet set = Plugin.Gestures.Get(aiming ? adsSetName : hipSetName) ?? Plugin.Gestures.Get(hipSetName);
            if (set == null)
            {
                return;
            }
            if (!wasActive)
            {
                Stop();
            }
            _hipSetName = hipSetName;
            _adsSetName = adsSetName;
            _setIsAds = aiming;
            _player = player;
            _set = set;
            _ending = false;
            SetPhase(Phase.In);
        }

        public static void EndSlide()
        {
            if (_phase == Phase.In || _phase == Phase.Loop)
            {
                _ending = true;
                if (_phase == Phase.Loop)
                {
                    SetPhase(Phase.Out);
                }
            }
        }

        public static void Stop()
        {
            _phase = Phase.None;
            _set = null;
            _player = null;
            _ending = false;
        }

        private static void SetPhase(Phase phase)
        {
            _fromRotation = _lastRotation;
            _fromPosition = _lastPosition;
            _fade = _phase == Phase.None ? 1f : 0f;
            _phase = phase;
            _time = 0f;
        }

        private static bool HasFirearm(Player player)
        {
            return player.HandsController is Player.FirearmController fc && fc.Item is Weapon;
        }

        private static bool IsAiming(Player player)
        {
            return player.HandsController is Player.FirearmController fc && fc.IsAiming;
        }

        private static string HipSetFor(Player player)
        {
            if (!(player.HandsController is Player.FirearmController fc) || !(fc.Item is Weapon weapon))
            {
                return null;
            }
            if (!Settings.PerClassGestures.Value)
            {
                return RifleSet;
            }
            switch (weapon.WeapClass)
            {
                case "pistol": return "slide_pistol";
                case "smg": return "slide_smg";
                case "machinegun": return "slide_machinegun";
                case "sniperRifle": return "slide_sniper";
                case "grenadeLauncher": return "slide_launcher";
                default: return RifleSet;
            }
        }

        private static GesturePhase Current =>
            _phase == Phase.In ? _set.In : _phase == Phase.Loop ? _set.Loop : _phase == Phase.Out ? _set.Out : null;

        public static void Apply(Transform weaponRoot, Transform view, float dt)
        {
            if (_phase == Phase.None || weaponRoot == null || view == null)
            {
                return;
            }
            _time += dt;
            SwitchSetIfAimChanged();

            GesturePhase phase = Current;
            if (_time >= phase.Length)
            {
                if (_phase == Phase.In)
                {
                    SetPhase(_ending ? Phase.Out : Phase.Loop);
                }
                else if (_phase == Phase.Loop)
                {
                    _time %= Mathf.Max(0.01f, phase.Length);
                }
                else
                {
                    Stop();
                    _lastRotation = Quaternion.identity;
                    _lastPosition = Vector3.zero;
                    return;
                }
                phase = Current;
            }

            if (_phase == Phase.Out)
            {
                PlayRattleMarkers(phase, dt);
            }

            phase.SampleWeapon(_time, out Quaternion rotation, out Vector3 position);
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + dt / CrossFade);
                rotation = Quaternion.Slerp(_fromRotation, rotation, _fade);
                position = Vector3.Lerp(_fromPosition, position, _fade);
            }
            _lastRotation = rotation;
            _lastPosition = position;

            bool mirror = !(_hipSetName == DiveSet && Settings.DiveWeaponLeft.Value);
            if (mirror)
            {
                rotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
                position.x = -position.x;
            }

            float strength = Settings.GestureStrength.Value;
            rotation = Quaternion.Slerp(Quaternion.identity, rotation, strength);
            position *= strength;

            _restoreTarget = weaponRoot;
            _restoreLocalPosition = weaponRoot.localPosition;
            _restoreLocalRotation = weaponRoot.localRotation;

            Quaternion camera = view.rotation;
            Quaternion worldDelta = camera * rotation * Quaternion.Inverse(camera);
            weaponRoot.SetPositionAndRotation(weaponRoot.position + camera * position, worldDelta * weaponRoot.rotation);
        }

        private static void SwitchSetIfAimChanged()
        {
            bool aiming = _player != null && IsAiming(_player);
            if (aiming == _setIsAds)
            {
                return;
            }
            GestureSet other = Plugin.Gestures.Get(aiming ? _adsSetName : _hipSetName);
            if (other != null)
            {
                _set = other;
                _setIsAds = aiming;
                _fromRotation = _lastRotation;
                _fromPosition = _lastPosition;
                _fade = 0f;
            }
        }

        private static void PlayRattleMarkers(GesturePhase phase, float dt)
        {
            if (phase.Notes == null)
            {
                return;
            }
            float previous = _time - dt;
            void Marker(string name, float scale)
            {
                if (phase.Notes.TryGetValue(name, out float at) && previous < at && _time >= at)
                {
                    SlideAudio.Rattle(scale);
                }
            }
            Marker("rattle_small", 0.6f);
            Marker("rattle_medium", 1f);
            Marker("rattle_large", 1.3f);
        }

        public static void RestoreWeaponRoot()
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
