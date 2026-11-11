using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using AdvancedMovement.Audio;
using AdvancedMovement.Body;
using AdvancedMovement.Motion;
using AdvancedMovement.Moves;
using AdvancedMovement.Viewmodel;

[assembly: InternalsVisibleTo("AdvancedMovement.Fika")]

namespace AdvancedMovement
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("com.SPT.core", "4.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.sage.advancedmovement";
        public const string Name = "Advanced Movement";
        public const string Author = "Sage";
        public const string Version = "1.0.1";

        internal static ManualLogSource Log;

        internal static MotionProfile SlideProfile;
        internal static MotionProfile DiveTakeOffProfile;
        internal static MotionProfile DiveLandingProfile;
        internal static GestureLibrary Gestures;
        internal static BodyClipLibrary BodyClips;
        internal static SoundBank Sounds;

        private static int _failedPatches;

        private void Awake()
        {
            Log = Logger;

            Settings.Bind(Config);

            string dataDir = Path.Combine(Path.GetDirectoryName(Info.Location), "data");
            try
            {
                SlideProfile = MotionProfile.Load(Path.Combine(dataDir, "profiles", "slide.json"));
                Gestures = GestureLibrary.Load(Path.Combine(dataDir, "vm", "weapon_motion.json"));
                BodyClips = BodyClipLibrary.Load(Path.Combine(dataDir, "body", "clips.json"));
                DiveTakeOffProfile = LoadOptional(Path.Combine(dataDir, "profiles", "dive_in.json"));
                DiveLandingProfile = LoadOptional(Path.Combine(dataDir, "profiles", "dive_out.json"));
            }
            catch (Exception ex)
            {
                Log.LogError($"Data missing or invalid in {dataDir}: {ex.Message}. Advanced Movement is disabled.");
                SlideProfile = null;
            }
            try
            {
                Sounds = SoundBank.Load(Path.Combine(dataDir, "sounds"));
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Slide sounds missing or invalid: {ex.Message}. Slide audio is off.");
            }

            var harmony = new Harmony(Guid);
            Patches.Apply(harmony);

            Log.LogInfo($"{Name} {Version} by {Author} loaded" + (_failedPatches > 0 ? $" ({_failedPatches} patch(es) failed, see above)" : string.Empty));
        }

        internal static void TryPatch(Harmony harmony, string name, Action<Harmony> patch)
        {
            try
            {
                patch(harmony);
            }
            catch (Exception ex)
            {
                _failedPatches++;
                Log.LogError($"{name} could not be patched, that feature is off: {ex.Message}");
            }
        }

        private static MotionProfile LoadOptional(string path)
        {
            try
            {
                return MotionProfile.Load(path);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"{Path.GetFileName(path)} missing or invalid: {ex.Message}. That move is off.");
                return null;
            }
        }

        private void Update()
        {
            GesturePlayer.RestoreWeaponRoot();
            WeaponPin.Restore();
            AirSlide.Update();
            MoveController.Update();
        }
    }
}
