using System;
using Comfort.Common;
using EFT;
using HarmonyLib;
using AdvancedMovement.Body;
using AdvancedMovement.Moves;
using AdvancedMovement.Viewmodel;
using UnityEngine;

namespace AdvancedMovement
{
    internal static class Patches
    {
        public static void Apply(Harmony harmony)
        {
            Plugin.TryPatch(harmony, "slide from sprint", h => h.Patch(AccessTools.Method(typeof(SprintPlayerState), nameof(SprintPlayerState.ChangePose)),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(SprintChangePosePrefix))));
            Plugin.TryPatch(harmony, "first-person weapon pull", h => h.Patch(AccessTools.Method(typeof(PlayerBones), nameof(PlayerBones.ShiftWeaponRoot),
                        new[] { typeof(float), typeof(EPointOfView), typeof(float), typeof(bool), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(ShiftWeaponRootPrefix)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(ShiftWeaponRootPostfix))));
            Plugin.TryPatch(harmony, "aim cancel on state change", h => h.Patch(AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.StateChangedHandler)),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(AimStateChangedPrefix))));
            Plugin.TryPatch(harmony, "aim in the air", h => h.Patch(AccessTools.DeclaredMethod(typeof(Player.FirearmController), nameof(Player.FirearmController.ManualUpdate)),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(FirearmUpdatePrefix)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(FirearmUpdatePostfix))));
            Plugin.TryPatch(harmony, "aiming while jumping", h => h.Patch(AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.SetAim), new[] { typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(SetAimPrefix)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(SetAimPostfix))));
            Plugin.TryPatch(harmony, "aim state check", h => h.Patch(AccessTools.Method(typeof(EFTHardSettings), nameof(EFTHardSettings.CanAimInState)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(CanAimInStatePostfix))));
            Plugin.TryPatch(harmony, "sprint after an aimed jump", h => h.Patch(AccessTools.Method(typeof(JumpPlayerState), nameof(JumpPlayerState.ManualAnimatorMoveUpdate)),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(JumpUpdatePrefix))));
            Plugin.TryPatch(harmony, "still after a dive", h => h.Patch(AccessTools.PropertyGetter(typeof(MovementContext), nameof(MovementContext.PlayerAnimatorDeltaPosition)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(AnimatorDeltaPostfix))));
            Plugin.TryPatch(harmony, "movement keys after a dive", h => h.Patch(AccessTools.Method(typeof(Player), nameof(Player.Move), new[] { typeof(Vector2) }),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(PlayerMovePrefix))));
            Plugin.TryPatch(harmony, "dive from sprint", h => h.Patch(AccessTools.Method(typeof(MovementState), nameof(MovementState.Prone)),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(PronePrefix))));
            Plugin.TryPatch(harmony, "slide on landing", h => h.Patch(AccessTools.Method(typeof(MovementState), nameof(MovementState.ChangePose)),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(AirChangePosePrefix))));
            Plugin.TryPatch(harmony, "slide jump", h => h.Patch(AccessTools.Method(typeof(JumpPlayerState), nameof(JumpPlayerState.Enter)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(JumpEnterPostfix))));
            Plugin.TryPatch(harmony, "pose restore during moves", h => h.Patch(AccessTools.Method(typeof(Player), nameof(Player.RestorePose)),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(RestorePosePrefix))));
            Plugin.TryPatch(harmony, "body animation", h => h.Patch(AccessTools.Method(typeof(Player), nameof(Player.VisualPass)),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(VisualPassPrefix))));
        }

        public static Player LocalPlayer()
        {
            return Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.MainPlayer : null;
        }

        private static void AirChangePosePrefix(MovementState __instance, float poseDelta)
        {
            if (poseDelta != 0f)
            {
                try
                {
                    AirSlide.OnAirPose(__instance, poseDelta);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError(ex);
                }
            }
        }

        private static bool SprintChangePosePrefix(SprintPlayerState __instance, float poseDelta)
        {
            try
            {
                Player player = LocalPlayer();
                if (player == null || __instance.MovementContext != player.MovementContext)
                {
                    return true;
                }
                return !MoveController.TryStartSlide(player, poseDelta);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Slide failed to start, using EFT's crouch instead: " + ex);
                return true;
            }
        }

        private static bool AimStateChangedPrefix(Player.FirearmController __instance, EPlayerState nextstate)
        {
            if (nextstate == EPlayerState.ProneMove && __instance.IsAiming)
            {
                Player local = LocalPlayer();
                if (local != null && local.HandsController == __instance && MoveController.ProneSettling(local.MovementContext))
                {
                    return false;
                }
            }
            if (!AirAim.IsLocalFirearm(__instance, out Player _) || !__instance.IsAiming)
            {
                return true;
            }
            return !AirAim.IsAirState(nextstate);
        }

        private static void FirearmUpdatePrefix(Player.FirearmController __instance, out float __state)
        {
            __state = -1f;
            if (!AirAim.IsLocalFirearm(__instance, out Player player) || !__instance.IsAiming || !AirAim.InAir(player))
            {
                return;
            }
            __state = player.MovementContext.FreefallTime;
            player.MovementContext.FreefallTime = 0f;
        }

        private static void FirearmUpdatePostfix(float __state)
        {
            if (__state >= 0f)
            {
                Player player = LocalPlayer();
                if (player != null)
                {
                    player.MovementContext.FreefallTime = __state;
                }
            }
        }

        private static void SetAimPrefix(Player.FirearmController __instance, bool value)
        {
            if (value && !__instance.IsAiming && AirAim.IsLocalFirearm(__instance, out Player player) && AirAim.InAir(player))
            {
                AirAim.AllowAimCheck = true;
            }
        }

        private static void SetAimPostfix()
        {
            AirAim.AllowAimCheck = false;
        }

        private static void CanAimInStatePostfix(EPlayerState currentStateName, ref bool __result)
        {
            if (!__result && AirAim.AllowAimCheck && AirAim.IsAirState(currentStateName))
            {
                __result = true;
            }
        }

        private static void ShiftWeaponRootPrefix(PlayerBones __instance, EPointOfView pv, ref float thirdPersonAuthority, ref float positionCacheValue)
        {
            if (pv != EPointOfView.FirstPerson)
            {
                return;
            }
            Player player = LocalPlayer();
            if (player == null || __instance.Player != player)
            {
                return;
            }
            float weight = SteadyWeapon.Weight(player);
            if (weight <= 0f)
            {
                return;
            }
            thirdPersonAuthority *= 1f - weight;
            positionCacheValue *= 1f - weight;
        }

        private static void AnimatorDeltaPostfix(MovementContext __instance, ref Vector3 __result)
        {
            if (MoveController.ProneSettling(__instance))
            {
                __result.x = 0f;
                __result.z = 0f;
            }
        }

        private static void PlayerMovePrefix(Player __instance, ref Vector2 direction)
        {
            if (__instance != LocalPlayer())
            {
                return;
            }
            if (!MoveController.HoldStill)
            {
                if (direction.sqrMagnitude >= 0.01f)
                {
                    MoveController.EndProneSettle();
                }
                return;
            }
            if (direction.sqrMagnitude < 0.01f || !__instance.MovementContext.IsInPronePose)
            {
                MoveController.HoldStill = false;
                return;
            }
            direction = Vector2.zero;
        }

        private static void JumpUpdatePrefix(JumpPlayerState __instance)
        {
            if (!AirAim.Enabled || !__instance._isSprintWasPreviousState)
            {
                return;
            }
            Player player = LocalPlayer();
            if (player != null && __instance.MovementContext == player.MovementContext && AirAim.IsAiming(player))
            {
                __instance._isSprintWasPreviousState = false;
            }
        }

        private static bool PronePrefix(MovementState __instance)
        {
            if (!(__instance is SprintPlayerState))
            {
                return true;
            }
            try
            {
                Player player = LocalPlayer();
                if (player == null || __instance.MovementContext != player.MovementContext)
                {
                    return true;
                }
                return !MoveController.TryStartDive(player);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Dive failed to start, using EFT's prone instead: " + ex);
                return true;
            }
        }

        private static void JumpEnterPostfix(JumpPlayerState __instance)
        {
            try
            {
                SlideJump.OnJumpEnter(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Slide jump speed could not be applied: " + ex);
            }
        }

        private static bool RestorePosePrefix(Player __instance)
        {
            return !(MoveController.Busy && __instance == LocalPlayer());
        }

        private static void VisualPassPrefix(Player __instance)
        {
            if (!BodyPlayer.Active)
            {
                return;
            }
            try
            {
                BodyPlayer.Apply(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Body animation failed and was stopped: " + ex);
                BodyPlayer.Reset();
            }
        }

        private static void ShiftWeaponRootPostfix(PlayerBones __instance, EPointOfView pv)
        {
            if (pv != EPointOfView.FirstPerson)
            {
                return;
            }
            Player player = LocalPlayer();
            if (player == null || __instance.Player != player || player.CameraContainer == null)
            {
                return;
            }
            try
            {
                if (MoveController.InDive)
                {
                    WeaponPin.Record(__instance.Weapon_Root_Anim, player.CameraContainer.transform);
                }
                WeaponPin.Apply(player, __instance.Weapon_Root_Anim, player.CameraContainer.transform);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Weapon pin failed and was stopped: " + ex);
                WeaponPin.Clear();
            }
            if (!GesturePlayer.Active)
            {
                return;
            }
            try
            {
                GesturePlayer.Apply(__instance.Weapon_Root_Anim, player.CameraContainer.transform, Time.deltaTime);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Weapon motion failed and was stopped: " + ex);
                GesturePlayer.Stop();
            }
        }
    }
}
