using EFT;
using AdvancedMovement.Motion;
using UnityEngine;

namespace AdvancedMovement.States
{
    internal enum MoveEnd
    {
        Complete,
        Blocked,
        Airborne,
        Jump,
        Stand,
    }

    internal sealed class MoveSettings
    {
        public MotionProfile Profile;
        public float DistanceScale = 1f;
        public float DurationScale = 1f;
        public bool UseSlowDownGlide = true;
        public bool EndWhenBlocked = true;
    }

    internal abstract class ProfileMoveState : MovementState
    {
        private const int BlockedFramesToEnd = 3;
        private const float BlockedMoveFraction = 0.3f;
        private const float MaxAirTime = 0.25f;

        public readonly MoveSettings Settings;
        public readonly Vector3 SlideDirection;
        public readonly float EndTime;
        public Vector3 StartPosition { get; private set; }
        public float Elapsed { get; private set; }
        public bool Finished { get; private set; }
        public MoveEnd EndReason { get; private set; }
        public float SavedSpeedLimit = float.NaN;
        public bool SprintHeld { get; private set; } = true;
        public Vector2 LastMove { get; private set; }

        private int _lastFrame = -1;
        private Vector3 _lastPosition;
        private float _lastRequested;
        private int _blockedFrames;
        private float _airTime;
        private float _cancelDuration = -1f;
        private float _cancelElapsed;
        private float _cancelStartSpeed;
        private float _cancelSpeed;

        protected ProfileMoveState(MovementContext ctx, MoveSettings settings, BaseMovementState template, Vector3 direction) : base(ctx)
        {
            Settings = settings;
            Name = EPlayerState.Sidestep;
            Type = template.Type;
            AnimatorStateName = template.AnimatorStateName;
            AnimatorStateHash = template.AnimatorStateHash;
            AnimationAuthority = template.AnimationAuthority;
            AuthoritySpeed = template.AuthoritySpeed;
            StateSensitivity = template.StateSensitivity;
            RotationSpeedClamp = template.RotationSpeedClamp;
            CanInteract = false;
            StickToGround = true;

            direction.y = 0f;
            SlideDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            float profileEnd = settings.UseSlowDownGlide ? settings.Profile.Length : settings.Profile.MainEndTime;
            EndTime = profileEnd * settings.DurationScale;
        }

        public bool InSlowDownGlide => ProfileTime(Elapsed) >= Settings.Profile.MainEndTime;

        public bool Cancelling => _cancelDuration > 0f;

        public float CurrentSpeed => Cancelling ? _cancelSpeed : Settings.Profile.SpeedAt(ProfileTime(Elapsed)) * Settings.DistanceScale / Settings.DurationScale;

        public void BeginCancel(float duration, float endSpeed)
        {
            if (Finished || Cancelling)
            {
                return;
            }
            if (duration <= 0f)
            {
                Finish(MoveEnd.Stand);
                return;
            }
            _cancelStartSpeed = CurrentSpeed;
            _cancelSpeed = _cancelStartSpeed;
            _cancelEndSpeed = Mathf.Min(endSpeed, _cancelStartSpeed);
            _cancelDuration = duration;
            _cancelElapsed = 0f;
        }

        private float _cancelEndSpeed;

        public Vector3 CurrentVelocity => SlideDirection * CurrentSpeed;

        private float ProfileTime(float t) => t / Settings.DurationScale;

        public override void Enter(bool isFromSameState)
        {
            base.Enter(isFromSameState);
            StartPosition = MovementContext.TransformPosition;
            _lastPosition = StartPosition;
        }

        public override void Exit(bool toSameState)
        {
            if (!float.IsNaN(SavedSpeedLimit))
            {
                MovementContext.CharacterController.SpeedLimit = SavedSpeedLimit;
            }
            base.Exit(toSameState);
        }

        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            if (Finished || Time.frameCount == _lastFrame)
            {
                return;
            }
            _lastFrame = Time.frameCount;

            ProcessRotation(deltaTime);

            if (UpdateBlocked() && Settings.EndWhenBlocked)
            {
                Finish(MoveEnd.Blocked);
                return;
            }
            _airTime = MovementContext.IsGrounded ? 0f : _airTime + deltaTime;
            if (_airTime > MaxAirTime)
            {
                Finish(MoveEnd.Airborne);
                return;
            }

            if (Cancelling)
            {
                _cancelElapsed += deltaTime;
                float k = Mathf.Clamp01(_cancelElapsed / _cancelDuration);
                _cancelSpeed = Mathf.Lerp(_cancelStartSpeed, _cancelEndSpeed, Mathf.SmoothStep(0f, 1f, k));
                Vector3 glide = SlideDirection * (_cancelSpeed * deltaTime);
                _lastRequested = _cancelSpeed * deltaTime;
                ApplyMotion(ref glide, deltaTime);
                if (k >= 1f)
                {
                    Finish(MoveEnd.Stand);
                }
                return;
            }

            float t0 = Elapsed;
            float t1 = Mathf.Min(Elapsed + deltaTime, EndTime);
            float distance = (Settings.Profile.DistanceAt(ProfileTime(t1)) - Settings.Profile.DistanceAt(ProfileTime(t0))) * Settings.DistanceScale;
            Vector3 motion = SlideDirection * distance;
            _lastRequested = distance;
            Elapsed = t1;

            ApplyMotion(ref motion, deltaTime);

            if (Elapsed >= EndTime)
            {
                Finish(MoveEnd.Complete);
            }
        }

        private bool UpdateBlocked()
        {
            Vector3 position = MovementContext.TransformPosition;
            Vector3 moved = position - _lastPosition;
            moved.y = 0f;
            _lastPosition = position;

            bool blockedThisFrame = _lastRequested > 0.002f && moved.magnitude < _lastRequested * BlockedMoveFraction;
            _blockedFrames = blockedThisFrame ? _blockedFrames + 1 : 0;
            return _blockedFrames >= BlockedFramesToEnd;
        }

        public void Finish(MoveEnd reason)
        {
            if (!Finished)
            {
                Finished = true;
                EndReason = reason;
            }
        }

        public override void Move(Vector2 direction)
        {
            LastMove = direction;
        }

        public override void EnableSprint(bool enable, bool isToggle = false)
        {
            SprintHeld = isToggle ? !SprintHeld : enable;
        }

        public override void Prone() { }
        public override void Vaulting() { }
    }

    internal sealed class SlideState : ProfileMoveState
    {
        public SlideState(MovementContext ctx, MoveSettings settings, BaseMovementState template, Vector3 direction)
            : base(ctx, settings, template, direction)
        {
        }

        private const float CancelEndSpeed = 2.5f;

        public override void Jump()
        {
            Finish(MoveEnd.Jump);
        }

        public override void ChangePose(float poseDelta)
        {
            if (poseDelta == 0f)
            {
                return;
            }
            if (AdvancedMovement.Settings.SlideCancel.Value)
            {
                BeginCancel(AdvancedMovement.Settings.SlideCancelTime.Value, CancelEndSpeed);
            }
        }
    }
}
