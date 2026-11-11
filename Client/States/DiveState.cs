using EFT;
using AdvancedMovement.Motion;
using UnityEngine;

namespace AdvancedMovement.States
{
    internal enum DivePhase
    {
        TakeOff,
        Air,
        Land,
    }

    internal sealed class DiveState : MovementState
    {
        private const float MinTakeOffTimeBeforeLanding = 0.2f;
        private const float MaxAirTime = 3f;
        private const int BlockedFramesToLand = 3;

        public readonly Vector3 DiveDirection;
        public DivePhase Phase { get; private set; } = DivePhase.TakeOff;
        public Vector3 StartPosition { get; private set; }
        public bool Finished { get; private set; }
        public MoveEnd EndReason { get; private set; }
        public float SavedSpeedLimit = float.NaN;
        public float CurrentSpeed { get; private set; }

        private readonly MotionProfile _takeOff;
        private readonly MotionProfile _landing;
        private readonly float _distanceScale;
        private float _phaseTime;
        private float _verticalSpeed;
        private int _lastFrame = -1;
        private Vector3 _lastPosition;
        private float _lastRequested;
        private int _blockedFrames;

        public DiveState(MovementContext ctx, MotionProfile takeOff, MotionProfile landing, float distanceScale, BaseMovementState template, Vector3 direction) : base(ctx)
        {
            _takeOff = takeOff;
            _landing = landing;
            _distanceScale = distanceScale;
            Name = EPlayerState.Sidestep;
            Type = template.Type;
            AnimatorStateName = template.AnimatorStateName;
            AnimatorStateHash = template.AnimatorStateHash;
            AnimationAuthority = template.AnimationAuthority;
            AuthoritySpeed = template.AuthoritySpeed;
            StateSensitivity = template.StateSensitivity;
            RotationSpeedClamp = template.RotationSpeedClamp;
            CanInteract = false;
            StickToGround = false;

            direction.y = 0f;
            DiveDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }

        public float PeakSpeed => Mathf.Max(_takeOff.PeakSpeed(), _landing.PeakSpeed()) * _distanceScale;

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

        public override void ApplyGravity(ref Vector3 motion, float deltaTime)
        {
            if (Phase == DivePhase.Land)
            {
                base.ApplyGravity(ref motion, deltaTime);
            }
        }

        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            if (Finished || Time.frameCount == _lastFrame)
            {
                return;
            }
            _lastFrame = Time.frameCount;
            ProcessRotation(deltaTime);

            bool blocked = UpdateBlocked();
            switch (Phase)
            {
                case DivePhase.TakeOff:
                    UpdateTakeOff(deltaTime, blocked);
                    break;
                case DivePhase.Air:
                    UpdateAir(deltaTime, blocked);
                    break;
                default:
                    UpdateLanding(deltaTime, blocked);
                    break;
            }
        }

        private void UpdateTakeOff(float deltaTime, bool blocked)
        {
            if (blocked || (_phaseTime > MinTakeOffTimeBeforeLanding && MovementContext.IsGrounded && _verticalSpeed < 0f))
            {
                SetPhase(DivePhase.Land);
                return;
            }
            float t0 = _phaseTime;
            float t1 = Mathf.Min(t0 + deltaTime, _takeOff.Length);
            float forward = (_takeOff.DistanceAt(t1) - _takeOff.DistanceAt(t0)) * _distanceScale;
            float up = _takeOff.HeightAt(t1) - _takeOff.HeightAt(t0);
            _verticalSpeed = up / deltaTime;
            CurrentSpeed = forward / deltaTime;
            _phaseTime = t1;
            Move(DiveDirection * forward + Vector3.up * up, deltaTime, forward);

            if (_phaseTime >= _takeOff.Length)
            {
                if (MovementContext.IsGrounded)
                {
                    SetPhase(DivePhase.Land);
                }
                else
                {
                    CurrentSpeed = _takeOff.SpeedAt(_takeOff.Length) * _distanceScale;
                    SetPhase(DivePhase.Air);
                }
            }
        }

        private void UpdateAir(float deltaTime, bool blocked)
        {
            if (MovementContext.IsGrounded && _phaseTime > 0f)
            {
                SetPhase(DivePhase.Land);
                return;
            }
            if (_phaseTime > MaxAirTime)
            {
                Finish(MoveEnd.Airborne);
                return;
            }
            if (blocked)
            {
                CurrentSpeed = 0f;
            }
            _verticalSpeed += Physics.gravity.y * deltaTime;
            _phaseTime += deltaTime;
            float forward = CurrentSpeed * deltaTime;
            Move(DiveDirection * forward + Vector3.up * (_verticalSpeed * deltaTime), deltaTime, forward);
        }

        private void UpdateLanding(float deltaTime, bool blocked)
        {
            if (blocked)
            {
                Finish(MoveEnd.Complete);
                return;
            }
            float t0 = _phaseTime;
            float t1 = Mathf.Min(t0 + deltaTime, _landing.Length);
            float forward = (_landing.DistanceAt(t1) - _landing.DistanceAt(t0)) * _distanceScale;
            CurrentSpeed = forward / deltaTime;
            _phaseTime = t1;
            Vector3 motion = DiveDirection * forward;
            _lastRequested = forward;
            ApplyMotion(ref motion, deltaTime);
            if (_phaseTime >= _landing.Length)
            {
                Finish(MoveEnd.Complete);
            }
        }

        private void Move(Vector3 motion, float deltaTime, float requested)
        {
            _lastRequested = requested;
            MovementContext.ApplyMotion(motion, deltaTime);
        }

        private void SetPhase(DivePhase phase)
        {
            Phase = phase;
            _phaseTime = 0f;
            _blockedFrames = 0;
            StickToGround = phase == DivePhase.Land;
        }

        private bool UpdateBlocked()
        {
            Vector3 position = MovementContext.TransformPosition;
            Vector3 moved = position - _lastPosition;
            moved.y = 0f;
            _lastPosition = position;
            bool blockedThisFrame = _lastRequested > 0.002f && moved.magnitude < _lastRequested * 0.3f;
            _blockedFrames = blockedThisFrame ? _blockedFrames + 1 : 0;
            return _blockedFrames >= BlockedFramesToLand;
        }

        public void Finish(MoveEnd reason)
        {
            if (!Finished)
            {
                Finished = true;
                EndReason = reason;
            }
        }

        public override void Jump() { }
        public override void ChangePose(float poseDelta) { }
        public override void Prone() { }
        public override void Vaulting() { }
    }
}
