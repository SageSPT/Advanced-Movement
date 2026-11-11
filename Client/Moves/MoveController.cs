using System;
using EFT;
using AdvancedMovement.Audio;
using AdvancedMovement.Body;
using AdvancedMovement.States;
using AdvancedMovement.Viewmodel;
using UnityEngine;

namespace AdvancedMovement.Moves
{
    internal static class MoveController
    {
        private const float SpeedLimitMargin = 0.5f;
        private const float JumpBodyFadeIn = 0.1f;
        private const float JumpBodyHold = 0.35f;

        private static ProfileMoveState _move;
        private static DiveState _dive;
        private static DivePhase _divePhase;
        private static Player _player;
        private static float _entrySpeed;
        private static bool _bodyReleased;
        private static string _diveKey = "8";

        public static bool Busy => _move != null || _dive != null;

        public static bool Diving => _dive != null && _dive.Phase != DivePhase.Land;

        public static bool InDive => _dive != null;

        public static bool HoldStill;

        private const float ProneSettleTime = 2.5f;
        private static float _settleUntil = -100f;
        private static Vector3 _settleStart;
        private static EPlayerState _settleState;
        private static bool _settleLogged = true;

        public static bool ProneSettling(MovementContext ctx)
        {
            return Time.time < _settleUntil && _player != null && ctx == _player.MovementContext && ctx.IsInPronePose;
        }

        public static void EndProneSettle()
        {
            _settleUntil = -100f;
        }

        private static void UpdateProneSettle()
        {
            if (_settleLogged || _player == null)
            {
                return;
            }
            MovementContext ctx = _player.MovementContext;
            EPlayerState state = ctx.CurrentState != null ? ctx.CurrentState.Name : EPlayerState.None;
            if (state != _settleState)
            {
                Log($"After dive: state {_settleState} -> {state} at {Vector3.Distance(_settleStart, ctx.TransformPosition):F2} m, aiming {(_player.HandsController is Player.FirearmController f && f.IsAiming)}");
                _settleState = state;
            }
            if (!ProneSettling(ctx))
            {
                Log($"After dive: settle ended, moved {Vector3.Distance(_settleStart, ctx.TransformPosition):F2} m since landing");
                _settleLogged = true;
            }
        }

        public static bool TryStartSlide(Player player, float poseDelta)
        {
            if (!Settings.SlideEnabled.Value || poseDelta >= 0f || Busy || Plugin.SlideProfile == null)
            {
                return false;
            }
            if (!CanStartFromSprint(player, out float speed, out Vector3 direction, out BaseMovementState idle))
            {
                return false;
            }
            StartSlide(player, speed, direction, idle, "Slide start");
            return true;
        }

        public static bool TryStartLandingSlide(Player player, Vector3 airVelocity)
        {
            if (!Settings.SlideEnabled.Value || !Settings.SlideFromJump.Value || Busy || Plugin.SlideProfile == null)
            {
                Log($"Landing slide skipped: disabled or busy (busy {Busy})");
                return false;
            }
            MovementContext ctx = player.MovementContext;
            if (ctx.OverridenState != null || !ctx.IsGrounded || ctx.IsInPronePose || ctx.CurrentState is JumpPlayerState)
            {
                Log($"Landing slide skipped: state {ctx.CurrentState?.GetType().Name}, override {ctx.OverridenState != null}, grounded {ctx.IsGrounded}, prone {ctx.IsInPronePose}");
                return false;
            }
            Vector3 velocity = ctx.Velocity;
            velocity.y = 0f;
            airVelocity.y = 0f;
            float speed = Mathf.Max(velocity.magnitude, airVelocity.magnitude);
            if (speed < Settings.MinEntrySpeed.Value)
            {
                Log($"Landing slide skipped: {speed:F2} m/s is below MinEntrySpeed");
                return false;
            }
            if (!ctx.States.TryGetValue(EPlayerState.Idle, out BaseMovementState idle) || idle == null)
            {
                return false;
            }
            Vector3 direction = airVelocity.sqrMagnitude > 1f ? airVelocity : velocity.sqrMagnitude > 1f ? velocity : ctx.TransformForwardVector;
            StartSlide(player, speed, direction, idle, "Landing slide start");
            return true;
        }

        private static void StartSlide(Player player, float speed, Vector3 direction, BaseMovementState idle, string label)
        {
            MovementContext ctx = player.MovementContext;
            var settings = new MoveSettings
            {
                Profile = Plugin.SlideProfile,
                DistanceScale = Settings.DistanceScale.Value,
                DurationScale = 1f / Mathf.Max(0.1f, Settings.SpeedScale.Value),
                UseSlowDownGlide = Settings.UseSlowDownGlide.Value,
                EndWhenBlocked = Settings.EndWhenBlocked.Value,
            };
            var slide = new SlideState(ctx, settings, ctx.CurrentState, direction);
            EnterOverride(ctx, slide, idle, settings.Profile.PeakSpeed() * settings.DistanceScale / settings.DurationScale, out slide.SavedSpeedLimit);

            _move = slide;
            _player = player;
            _entrySpeed = speed;
            _bodyReleased = false;
            SlideAudio.Start(player);
            GesturePlayer.StartSlide(player);
            BodyPlayer.Play(player, BodyPlayer.DirectionalClip(player, direction, "slide_"));

            Log($"{label}: {speed:F2} m/s, {settings.Profile.TotalDistance * settings.DistanceScale:F2} m over {slide.EndTime:F2} s, surface {player.CurrentSurface}");
        }

        public static bool TryStartDive(Player player)
        {
            if (!Settings.DiveEnabled.Value || Busy || Plugin.DiveTakeOffProfile == null || Plugin.DiveLandingProfile == null)
            {
                return false;
            }
            if (!CanStartFromSprint(player, out float speed, out Vector3 direction, out BaseMovementState idle))
            {
                return false;
            }
            MovementContext ctx = player.MovementContext;
            var dive = new DiveState(ctx, Plugin.DiveTakeOffProfile, Plugin.DiveLandingProfile, Settings.DiveDistanceScale.Value, ctx.CurrentState, direction);
            EnterOverride(ctx, dive, idle, dive.PeakSpeed, out dive.SavedSpeedLimit);

            _dive = dive;
            _divePhase = DivePhase.TakeOff;
            _player = player;
            _entrySpeed = speed;
            SlideAudio.PlayDiveCue(player, "dive_jump");
            GesturePlayer.StartDive(player);
            _diveKey = BodyPlayer.DiveClip(player, direction, string.Empty, false);
            if (BodyPlayer.Play(player, "dive_in_" + _diveKey, fadeInTime: JumpBodyFadeIn))
            {
                BodyPlayer.Then("dive_loop_" + _diveKey, loop: true);
            }

            Log($"Dive start: {speed:F2} m/s, direction {_diveKey}");
            return true;
        }

        private static bool CanStartFromSprint(Player player, out float speed, out Vector3 direction, out BaseMovementState idle)
        {
            MovementContext ctx = player.MovementContext;
            speed = 0f;
            direction = Vector3.zero;
            idle = null;
            if (ctx.OverridenState != null || !ctx.IsGrounded || ctx.IsInPronePose || !(ctx.CurrentState is SprintPlayerState))
            {
                return false;
            }
            Vector3 velocity = ctx.Velocity;
            velocity.y = 0f;
            speed = velocity.magnitude;
            if (speed < Settings.MinEntrySpeed.Value)
            {
                return false;
            }
            if (!ctx.States.TryGetValue(EPlayerState.Idle, out idle) || idle == null)
            {
                return false;
            }
            direction = speed > 1f ? velocity : ctx.TransformForwardVector;
            return true;
        }

        private static void EnterOverride(MovementContext ctx, BaseMovementState state, BaseMovementState idle, float peakSpeed, out float savedSpeedLimit)
        {
            ctx.OverrideState(state);
            ctx.PreviousState = idle;
            ctx.SetPoseLevel(0f, force: true);
            ctx.PlayerAnimator.Animator.Play(idle.AnimatorStateHash, 0, 0f);
            savedSpeedLimit = ctx.CharacterController.SpeedLimit;
            ctx.CharacterController.SpeedLimit = Mathf.Max(peakSpeed, savedSpeedLimit) + SpeedLimitMargin;
        }

        public static void Update()
        {
            SlideAudio.Update(_move != null ? _move.CurrentSpeed : 0f, _move != null && !_move.Finished);
            UpdateProneSettle();
            if (!Busy)
            {
                return;
            }
            if (_player == null || _player.HealthController == null || !_player.HealthController.IsAlive)
            {
                _move = null;
                _dive = null;
                GesturePlayer.Stop();
                BodyPlayer.Reset();
                return;
            }
            if (_move != null)
            {
                UpdateMove();
            }
            else
            {
                UpdateDive();
            }
        }

        private static void UpdateMove()
        {
            if (_move is SlideState && !_move.Finished && !_bodyReleased && (_move.InSlowDownGlide || _move.Cancelling))
            {
                _bodyReleased = true;
                BodyPlayer.Release();
            }
            if (_move.Finished)
            {
                ExitMove();
            }
        }

        private static void UpdateDive()
        {
            if (_dive.Phase != _divePhase)
            {
                _divePhase = _dive.Phase;
                if (_divePhase == DivePhase.Land)
                {
                    SlideAudio.PlayDiveCue(_player, "dive_land");
                    GesturePlayer.EndSlide();
                    BodyPlayer.Play(_player, "dive_out_" + _diveKey, fadeInTime: JumpBodyFadeIn);
                }
            }
            if (_dive.Finished)
            {
                ExitDive();
            }
        }

        private static void ExitMove()
        {
            ProfileMoveState move = _move;
            _move = null;
            MovementContext ctx = _player.MovementContext;
            if (ctx.OverridenState == move)
            {
                ctx.ExitOverridenState();
            }
            LogEnd("Slide", move.EndReason, move.Elapsed, move.StartPosition, ctx);

            try
            {
                ExitSlide(move, ctx);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Action after the slide failed: " + ex.Message);
                EndEffects();
            }
        }

        private static void ExitSlide(ProfileMoveState slide, MovementContext ctx)
        {
            if (slide.EndReason == MoveEnd.Jump)
            {
                DoSlideJump(slide, ctx);
                return;
            }

            EndEffects();
            if (!(ctx.CurrentState is MovementState next))
            {
                return;
            }
            bool endedNormally = slide.EndReason == MoveEnd.Complete || slide.EndReason == MoveEnd.Blocked;
            if (Settings.SlideToSprint.Value && endedNormally && slide.SprintHeld && slide.LastMove.y > 0.1f)
            {
                next.ChangePose(_player.Physical.MaxPoseLevel - ctx.PoseLevel);
                _player.Move(slide.LastMove);
                _player.EnableSprint(true);
            }
            else if (slide.EndReason == MoveEnd.Stand)
            {
                next.ChangePose(_player.Physical.MaxPoseLevel - ctx.PoseLevel);
                if (slide.LastMove.sqrMagnitude > 0.01f)
                {
                    _player.Move(slide.LastMove);
                    if (slide.SprintHeld && slide.LastMove.y > 0.1f)
                    {
                        _player.EnableSprint(true);
                    }
                }
            }
        }

        private static void DoSlideJump(ProfileMoveState slide, MovementContext ctx)
        {
            GesturePlayer.EndSlide();
            SlideAudio.End();
            if (!Settings.SlideJumpEnabled.Value)
            {
                BodyPlayer.Release();
                ctx.SetPoseLevel(_player.Physical.MaxPoseLevel, force: true);
                return;
            }
            string clip = BodyPlayer.DirectionalClip(_player, slide.SlideDirection, "slide_jump_");
            if (BodyPlayer.Play(_player, clip, fadeInTime: JumpBodyFadeIn))
            {
                BodyPlayer.Release(JumpBodyHold);
            }
            ctx.SetPoseLevel(_player.Physical.MaxPoseLevel, force: true);
            SlideJump.Arm(ctx, slide.CurrentVelocity * Settings.SlideJumpSpeedCarry.Value);
            ctx.TryJump();
        }

        private static void ExitDive()
        {
            DiveState dive = _dive;
            _dive = null;
            MovementContext ctx = _player.MovementContext;
            if (ctx.OverridenState == dive)
            {
                ctx.ExitOverridenState();
            }
            LogEnd("Dive", dive.EndReason, 0f, dive.StartPosition, ctx);
            try
            {
                if (dive.EndReason == MoveEnd.Airborne)
                {
                    EndEffects();
                }
                else
                {
                    GoProne(ctx);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Going prone after the dive failed: " + ex.Message);
                EndEffects();
            }
        }

        private static void GoProne(MovementContext ctx)
        {
            GesturePlayer.EndSlide();
            SlideAudio.End();
            if (ctx.CurrentState is MovementState next)
            {
                next.Prone();
            }
            if (ctx.IsInPronePose)
            {
                HoldStill = Settings.StayStillAfterDive.Value;
                if (Settings.StayStillAfterDive.Value)
                {
                    _settleUntil = Time.time + ProneSettleTime;
                }
                _settleStart = ctx.TransformPosition;
                _settleState = ctx.CurrentState != null ? ctx.CurrentState.Name : EPlayerState.None;
                _settleLogged = false;
                WeaponPin.Pin(_player);
            }
            else
            {
                WeaponPin.Clear();
            }
            BodyPlayer.Release(ctx.IsInPronePose ? Settings.ProneHandoverTime.Value : 0f);
        }

        private static void EndEffects()
        {
            GesturePlayer.EndSlide();
            BodyPlayer.Release();
            SlideAudio.End();
        }

        internal static void Log(string message)
        {
            if (Settings.LogSlides.Value)
            {
                Plugin.Log.LogInfo(message);
            }
        }

        private static void LogEnd(string move, MoveEnd reason, float elapsed, Vector3 start, MovementContext ctx)
        {
            if (!Settings.LogSlides.Value)
            {
                return;
            }
            Vector3 travelled = ctx.TransformPosition - start;
            travelled.y = 0f;
            Plugin.Log.LogInfo($"{move} end: {reason}{(elapsed > 0f ? $" after {elapsed:F2} s" : "")}, travelled {travelled.magnitude:F2} m (entry {_entrySpeed:F2} m/s)");
        }
    }
}
