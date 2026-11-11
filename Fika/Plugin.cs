using System;
using BepInEx;
using HarmonyLib;

namespace AdvancedMovement.Coop
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(AdvancedMovement.Plugin.Guid)]
    [BepInDependency(FikaGuid)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.sage.advancedmovement.fika";
        public const string Name = "Advanced Movement Fika Sync";
        public const string Author = "Sage";
        public const string Version = AdvancedMovement.Plugin.Version;

        private const string FikaGuid = "com.fika.core";

        private void Awake()
        {
            try
            {
                FikaSync.Init(new Harmony(Guid));
                Logger.LogInfo($"{Name} {Version} by {Author} loaded");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Fika sync could not start, other players won't see slides or dives: {ex.Message}");
            }
        }
    }
}
