using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace AdvancedMovement.Audio
{
    internal static class SlideAudio
    {
        private static GameObject _go;
        private static AudioSource _loop;
        private static AudioSource _shots;
        private static Player _player;
        private static bool _active;
        private static bool _easing;
        private static float _loopBase;
        private static string _surface;
        private static string _weaponGroup;

        private const float FullSpeed = 5.5f;

        public static void Start(Player player)
        {
            SoundBank bank = Plugin.Sounds;
            if (!Settings.AudioEnabled.Value || bank == null || !EnsureSources(player))
            {
                return;
            }
            _surface = MapSurface(player.CurrentSurface);
            _weaponGroup = WeaponGroup(player);
            _easing = false;
            _active = true;

            PlayOnce("slide_start", _surface, Settings.SlideVolume.Value);
            Rattle(0.8f);

            AudioClip loop = bank.Pick("slide_loop", bank.Has("slide_loop", _surface) ? _surface : Fallback("slide_loop"), out float listedVolume);
            if (loop != null)
            {
                _loopBase = listedVolume * Settings.SlideVolume.Value;
                _loop.clip = loop;
                _loop.loop = true;
                _loop.volume = 0f;
                _loop.pitch = 1f;
                _loop.time = Random.Range(0f, Mathf.Max(0f, loop.length - 0.1f));
                _loop.Play();
            }
        }

        public static void Update(float speed, bool sliding)
        {
            if (!_active || _loop == null)
            {
                return;
            }
            if (sliding && !_easing && speed < Settings.EaseOutSpeed.Value)
            {
                Ease();
            }
            float target = _easing ? 0f : _loopBase * Mathf.Sqrt(Mathf.Clamp01(speed / FullSpeed));
            float rate = _easing ? 1f / Mathf.Max(0.05f, Settings.LoopFadeOut.Value) : 8f;
            _loop.volume = Mathf.MoveTowards(_loop.volume, target, rate * Time.deltaTime * Mathf.Max(_loopBase, 0.01f));
            _loop.pitch = 0.92f + 0.16f * Mathf.Clamp01(speed / FullSpeed);
            if (_easing && _loop.volume <= 0.001f)
            {
                _loop.Stop();
                _active = false;
            }
        }

        public static void End()
        {
            if (_active && !_easing)
            {
                Ease();
            }
        }

        private static void Ease()
        {
            _easing = true;
            PlayOnce("slide_ease_out", _surface, Settings.SlideVolume.Value);
        }

        public static void PlayDiveCue(Player player, string cue)
        {
            if (!Settings.AudioEnabled.Value || Plugin.Sounds == null || !EnsureSources(player))
            {
                return;
            }
            _weaponGroup = WeaponGroup(player);
            PlayOnce(cue, "default", Settings.SlideVolume.Value);
            Rattle(cue == "dive_land" ? 1.3f : 0.8f);
        }

        public static void Rattle(float scale)
        {
            if (_weaponGroup != null && _shots != null && Settings.AudioEnabled.Value)
            {
                PlayOnce("weapon_rattle", _weaponGroup, Settings.RattleVolume.Value * scale);
            }
        }

        private static void PlayOnce(string cue, string group, float volume)
        {
            SoundBank bank = Plugin.Sounds;
            if (bank == null || _shots == null || group == null)
            {
                return;
            }
            if (!bank.Has(cue, group))
            {
                group = Fallback(cue);
            }
            AudioClip clip = bank.Pick(cue, group, out float listedVolume);
            if (clip != null)
            {
                _shots.PlayOneShot(clip, listedVolume * volume);
            }
        }

        private static string Fallback(string cue) => cue == "weapon_rattle" ? "medium" : "concrete";

        private static bool EnsureSources(Player player)
        {
            if (_go != null && _player == player)
            {
                return true;
            }
            if (_go != null)
            {
                Object.Destroy(_go);
            }
            Transform parent = player.Transform?.Original;
            if (parent == null)
            {
                return false;
            }
            _go = new GameObject("AdvancedMovement.SlideAudio");
            _go.transform.SetParent(parent, false);
            _go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            _loop = MakeSource();
            _shots = MakeSource();
            _player = player;
            return true;
        }

        private static AudioSource MakeSource()
        {
            AudioSource s = _go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.priority = 32;
            if (Singleton<BetterAudio>.Instantiated)
            {
                s.outputAudioMixerGroup = Singleton<BetterAudio>.Instance.ClientPlayerMovementMixer;
            }
            return s;
        }

        public static string MapSurface(BaseBallistic.ESurfaceSound s)
        {
            switch (s)
            {
                case BaseBallistic.ESurfaceSound.Metal: return "metal_thick";
                case BaseBallistic.ESurfaceSound.MetalThin: return "metal_car";
                case BaseBallistic.ESurfaceSound.GarbageMetal: return "metal_grate";
                case BaseBallistic.ESurfaceSound.Wood: return "wood_floor";
                case BaseBallistic.ESurfaceSound.WoodThin: return "wood_floor";
                case BaseBallistic.ESurfaceSound.WoodThick: return "wood_solid";
                case BaseBallistic.ESurfaceSound.Soil: return "dirt";
                case BaseBallistic.ESurfaceSound.Grass: return "grass";
                case BaseBallistic.ESurfaceSound.Asphalt: return "concrete_grit";
                case BaseBallistic.ESurfaceSound.Glass:
                case BaseBallistic.ESurfaceSound.WholeGlass: return "glass";
                case BaseBallistic.ESurfaceSound.Gravel: return "gravel";
                case BaseBallistic.ESurfaceSound.Puddle:
                case BaseBallistic.ESurfaceSound.Swamp: return "mud";
                case BaseBallistic.ESurfaceSound.Slate:
                case BaseBallistic.ESurfaceSound.Tile: return "tile";
                case BaseBallistic.ESurfaceSound.Garbage:
                case BaseBallistic.ESurfaceSound.Plastic: return "plastic";
                case BaseBallistic.ESurfaceSound.Snow: return "snow";
                default: return "concrete";
            }
        }

        private static string WeaponGroup(Player player)
        {
            if (!(player.HandsController is Player.FirearmController fc) || !(fc.Item is Weapon w))
            {
                return null;
            }
            switch (w.WeapClass)
            {
                case "pistol":
                case "smg": return "small";
                case "marksmanRifle":
                case "sniperRifle": return "large";
                case "machinegun": return "lmg";
                case "grenadeLauncher": return "launcher";
                default: return "medium";
            }
        }
    }
}
