using BepInEx.Configuration;

namespace AdvancedMovement
{
    internal static class Settings
    {
        private const string Slide = "1. Slide";
        private const string Dive = "2. Dive";
        private const string AirAim = "3. Air Aiming";
        private const string FirstPerson = "4. First Person";
        private const string Body = "5. Body";
        private const string Audio = "6. Audio";
        private const string Debug = "7. Debug";

        public static ConfigEntry<bool> SlideEnabled;
        public static ConfigEntry<float> MinEntrySpeed;
        public static ConfigEntry<float> DistanceScale;
        public static ConfigEntry<float> SpeedScale;
        public static ConfigEntry<bool> SlideToSprint;
        public static ConfigEntry<bool> UseSlowDownGlide;
        public static ConfigEntry<bool> EndWhenBlocked;
        public static ConfigEntry<bool> SlideCancel;
        public static ConfigEntry<float> SlideCancelTime;
        public static ConfigEntry<bool> SlideJumpEnabled;
        public static ConfigEntry<bool> SlideFromJump;
        public static ConfigEntry<float> SlideJumpSpeedCarry;

        public static ConfigEntry<bool> DiveEnabled;
        public static ConfigEntry<bool> StayStillAfterDive;
        public static ConfigEntry<float> DiveDistanceScale;
        public static ConfigEntry<float> ProneHandoverTime;

        public static ConfigEntry<bool> AirAimEnabled;
        public static ConfigEntry<bool> AirAimInFalls;

        public static ConfigEntry<bool> GestureEnabled;
        public static ConfigEntry<bool> PerClassGestures;
        public static ConfigEntry<bool> DiveWeaponLeft;
        public static ConfigEntry<bool> SteadyWeaponInMoves;
        public static ConfigEntry<bool> PinWeaponOnProne;
        public static ConfigEntry<float> PinFadeTime;
        public static ConfigEntry<float> SteadyWeaponFadeTime;
        public static ConfigEntry<float> GestureStrength;

        public static ConfigEntry<bool> BodyEnabled;
        public static ConfigEntry<float> BodySpineWeight;
        public static ConfigEntry<float> BodyHeadWeight;
        public static ConfigEntry<float> BodyFaceForward;
        public static ConfigEntry<float> BodyPelvisWeight;
        public static ConfigEntry<bool> BodyCentreHips;
        public static ConfigEntry<float> BodyDropTime;
        public static ConfigEntry<float> BodyRiseTime;

        public static ConfigEntry<bool> AudioEnabled;
        public static ConfigEntry<float> SlideVolume;
        public static ConfigEntry<float> RattleVolume;
        public static ConfigEntry<float> EaseOutSpeed;
        public static ConfigEntry<float> LoopFadeOut;

        public static ConfigEntry<bool> LogSlides;

        public static void Bind(ConfigFile config)
        {
            SlideEnabled = config.Bind(Slide, "Enabled", true, "Crouch while sprinting starts a slide");
            MinEntrySpeed = config.Bind(Slide, "MinEntrySpeed", 3.0f, "Minimum running speed (m/s) needed to slide; below it, crouch works as normal");
            DistanceScale = config.Bind(Slide, "Distance", 1f, new ConfigDescription("Multiplies how far the slide goes (1 = 5.8 m)", Range(0.25f, 3f)));
            SpeedScale = config.Bind(Slide, "Speed", 1.3f, new ConfigDescription("Multiplies slide speed: same distance in less time (1.3 = about 1.2 s)", Range(0.5f, 2f)));
            SlideToSprint = config.Bind(Slide, "SlideToSprint", true, "If sprint and forward are still held when the slide ends, stand up into a sprint. Off = always end crouched");
            UseSlowDownGlide = config.Bind(Slide, "SlowDownGlide", true, "Include the slow glide at the end of the slide. Off = the slide stops after its fast part");
            EndWhenBlocked = config.Bind(Slide, "EndWhenBlocked", true, "End the slide when a wall or obstacle stops it");
            SlideCancel = config.Bind(Slide, "SlideCancel", true, "Pressing crouch again during a slide stands you up and ends it");
            SlideCancelTime = config.Bind(Slide, "SlideCancelTime", 0.3f, new ConfigDescription("Seconds the slide takes to ease out and stand up after a cancel. 0 = stop at once", Range(0f, 1f)));
            SlideJumpEnabled = config.Bind(Slide, "SlideJump", true, "Jumping during a slide jumps out of it and keeps the slide's speed. Off = jump only stands you up");
            SlideFromJump = config.Bind(Slide, "SlideOnLanding", true, "Press or hold crouch while in the air to slide as soon as you land, if you are still moving faster than MinEntrySpeed. Release or press crouch again before landing to cancel");
            SlideJumpSpeedCarry = config.Bind(Slide, "SlideJumpSpeedCarry", 1f, new ConfigDescription("How much of the slide's speed the jump keeps (1 = all of it)", Range(0f, 1.5f)));

            DiveEnabled = config.Bind(Dive, "Enabled", true, "Press prone while sprinting to dive forward and land prone");
            StayStillAfterDive = config.Bind(Dive, "StayStillAfterDive", true, "After a dive, ignore the movement keys you were already holding until you let go of them, so you lie still instead of crawling (EFT cancels aiming while crawling)");
            DiveDistanceScale = config.Bind(Dive, "Distance", 1f, new ConfigDescription("Multiplies how far the dive goes (1 = 5.3 m in the air plus 1.7 m on landing)", Range(0.25f, 2f)));
            ProneHandoverTime = config.Bind(Dive, "ProneHandoverTime", 0.8f, new ConfigDescription("Seconds the landing pose is held while EFT moves into prone, before blending to EFT's prone pose", Range(0f, 2f)));

            AirAimEnabled = config.Bind(AirAim, "Enabled", true, "Aim down sights while jumping and diving: keep aiming when you jump, and start aiming in the air");
            AirAimInFalls = config.Bind(AirAim, "WhileFalling", true, "Also allow aiming while falling after walking or sliding off a ledge");

            GestureEnabled = config.Bind(FirstPerson, "WeaponMotion", true, "Move the weapon in first person while sliding");
            PerClassGestures = config.Bind(FirstPerson, "PerWeaponClass", true, "Use separate motion for pistols, SMGs, machine guns, snipers and launchers. Off = rifle motion for all");
            DiveWeaponLeft = config.Bind(FirstPerson, "DiveWeaponLeft", true, "During the dive, swing the weapon to the left side. Off = right side");
            SteadyWeaponInMoves = config.Bind(FirstPerson, "SteadyWeapon", true, "Stop EFT from pulling the weapon toward the third-person body pose during slides, dives and the hand-over to prone, so the weapon stays in front of the camera. Off = EFT's normal weapon pull");
            PinWeaponOnProne = config.Bind(FirstPerson, "KeepWeaponUpAfterDive", true, "After a dive, keep the weapon up in front of the camera while you lie prone. It moves into EFT's prone position when you aim, shoot, crawl, reload or stand up. Off = EFT's own weapon swing into prone");
            PinFadeTime = config.Bind(FirstPerson, "KeepWeaponUpFade", 0.5f, new ConfigDescription("Seconds to ease the weapon into EFT's prone position when you crawl, shoot, reload or stand up (aiming always takes 0.2 s)", Range(0.05f, 2f)));
            SteadyWeaponFadeTime = config.Bind(FirstPerson, "SteadyWeaponFadeTime", 0.5f, new ConfigDescription("Seconds to ease EFT's weapon pull back in after a move ends", Range(0.05f, 2f)));
            GestureStrength = config.Bind(FirstPerson, "Strength", 1f, new ConfigDescription("Scales the first-person weapon motion", Range(0f, 1.5f)));

            BodyEnabled = config.Bind(Body, "BodyAnimation", true, "Pose the body with the slide animation. Off = EFT's crouch pose");
            BodySpineWeight = config.Bind(Body, "SpineWeight", 1f, new ConfigDescription("How much of the animation's spine lean is used (the first-person view moves with it)", Range(0f, 1f)));
            BodyHeadWeight = config.Bind(Body, "NeckHeadWeight", 1f, new ConfigDescription("How much of the animation's neck and head motion is used", Range(0f, 1f)));
            BodyFaceForward = config.Bind(Body, "UpperBodyFaceForward", 1f, new ConfigDescription("Turns the chest and head toward where you look while the hips and legs stay sideways. 0 = the chest stays turned about 50 degrees to the left", Range(0f, 1f)));
            BodyPelvisWeight = config.Bind(Body, "HipDrop", 1f, new ConfigDescription("How far the hips drop toward the ground (1 = full slide height)", Range(0f, 1f)));
            BodyCentreHips = config.Bind(Body, "KeepHipsCentred", true, "Remove the animation's sideways hip drift so the body stays under the first-person view");
            BodyDropTime = config.Bind(Body, "DropTime", 0.3f, new ConfigDescription("Seconds to ease down into the slide pose. The first-person view follows the upper body, so this is also how fast the camera drops", Range(0.05f, 0.8f)));
            BodyRiseTime = config.Bind(Body, "RiseTime", 0.45f, new ConfigDescription("Seconds to rise from the slide pose back to EFT's animation. Starts when the fast part of the slide ends", Range(0.05f, 1.5f)));

            AudioEnabled = config.Bind(Audio, "Enabled", true, "Slide sounds: surface scrape (start, loop, ease-out) and weapon rattle");
            SlideVolume = config.Bind(Audio, "SlideVolume", 1f, new ConfigDescription("Slide scrape volume", Range(0f, 2f)));
            RattleVolume = config.Bind(Audio, "RattleVolume", 1f, new ConfigDescription("Weapon rattle volume", Range(0f, 2f)));
            EaseOutSpeed = config.Bind(Audio, "EaseOutSpeed", 2.9f, "Slide speed (m/s) below which the ease-out scrape plays and the loop fades");
            LoopFadeOut = config.Bind(Audio, "LoopFadeOut", 0.35f, "Seconds for the scrape loop to fade out after the ease-out starts");

            LogSlides = config.Bind(Debug, "LogSlides", false, "Write one line per slide start and end to the BepInEx log");
        }

        private static AcceptableValueRange<float> Range(float min, float max)
        {
            return new AcceptableValueRange<float>(min, max);
        }
    }
}
