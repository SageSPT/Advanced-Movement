using EFT;
using UnityEngine;

namespace AdvancedMovement.Body
{
    internal sealed class BodyAnimator
    {
        private const float CrossFadeTime = 0.15f;

        private readonly Player _player;
        private BodyRetarget _retarget;
        private bool _retargetFailed;

        private BodyClip _clip;
        private float _time;
        private bool _loop;
        private bool _faceForward;
        private BodyClip _fromClip;
        private float _fromTime;
        private float _blend = 1f;
        private BodyClip _next;
        private bool _nextLoop;

        private float _weight;
        private float _targetWeight;
        private float _fadeInTime;
        private float _releaseDelay = -1f;
        private bool _grounderOff;
        private float _savedGrounderWeight = -1f;

        public BodyAnimator(Player player)
        {
            _player = player;
        }

        public Player Player => _player;
        public bool Active => _clip != null;
        public string ClipName { get; private set; }

        public bool Play(string clipName, bool loop, float fadeInTime)
        {
            if (!Settings.BodyEnabled.Value || Plugin.BodyClips == null || _player == null)
            {
                return false;
            }
            BodyClip clip = Plugin.BodyClips.Get(clipName);
            if (clip == null || Retarget() == null)
            {
                return false;
            }
            if (_clip != null)
            {
                _fromClip = _clip;
                _fromTime = _time;
                _blend = 0f;
            }
            else
            {
                _fromClip = null;
                _blend = 1f;
                _weight = 0f;
            }
            _clip = clip;
            ClipName = clipName;
            _time = 0f;
            _loop = loop;
            _faceForward = clipName.Contains("strafe_slide_") && !clipName.Contains("jump");
            _next = null;
            _targetWeight = 1f;
            _releaseDelay = -1f;
            _fadeInTime = fadeInTime > 0f ? fadeInTime : Settings.BodyDropTime.Value;

            if (!_grounderOff)
            {
                _grounderOff = true;
                _player.MovementContext?.GrounderSetActive(grounderIsOn: false);
                if (_player.Grounder != null)
                {
                    _savedGrounderWeight = _player.Grounder.weight;
                    _player.Grounder.weight = 0f;
                }
            }
            return true;
        }

        public void Then(string clipName, bool loop)
        {
            if (_clip == null || Plugin.BodyClips == null)
            {
                return;
            }
            _next = Plugin.BodyClips.Get(clipName);
            _nextLoop = loop;
        }

        public void Release(float holdSeconds)
        {
            if (holdSeconds > 0f)
            {
                _releaseDelay = holdSeconds;
            }
            else
            {
                _releaseDelay = -1f;
                _targetWeight = 0f;
            }
        }

        public void Stop()
        {
            if (_grounderOff && _player != null && _player.MovementContext != null && !_player.MovementContext.IsInPronePose)
            {
                _player.MovementContext.GrounderSetActive(grounderIsOn: true);
            }
            if (_savedGrounderWeight >= 0f && _player != null && _player.Grounder != null)
            {
                _player.Grounder.weight = _savedGrounderWeight;
            }
            _savedGrounderWeight = -1f;
            _grounderOff = false;
            _clip = null;
            _fromClip = null;
            _next = null;
            ClipName = null;
            _weight = 0f;
            _releaseDelay = -1f;
        }

        private BodyRetarget Retarget()
        {
            if (_retarget != null && _retarget.Valid)
            {
                return _retarget;
            }
            if (_retargetFailed)
            {
                return null;
            }
            Animator animator = (_player.BodyAnimatorCommon as AnimationSystem.UnityAnimatorWrapper)?.Animator;
            if (animator == null)
            {
                _retargetFailed = true;
                Plugin.Log.LogWarning("A player's body animator is not a Unity Animator; the slide body animation is off for that player");
                return null;
            }
            _retarget = BodyRetarget.Build(animator.transform, Plugin.BodyClips.LegLength,
                Settings.BodySpineWeight.Value, Settings.BodyHeadWeight.Value);
            if (!_retarget.Valid)
            {
                _retargetFailed = true;
                return null;
            }
            return _retarget;
        }

        public void Apply()
        {
            if (_clip == null)
            {
                return;
            }
            if (_player == null || _player.HealthController == null || !_player.HealthController.IsAlive)
            {
                Stop();
                return;
            }
            float dt = Time.deltaTime;
            if (_releaseDelay > 0f)
            {
                _releaseDelay -= dt;
                if (_releaseDelay <= 0f)
                {
                    _releaseDelay = -1f;
                    _targetWeight = 0f;
                }
            }
            float fadeTime = _targetWeight > _weight ? _fadeInTime : Settings.BodyRiseTime.Value;
            _weight = Mathf.MoveTowards(_weight, _targetWeight, fadeTime > 0f ? dt / fadeTime : 1f);
            if (_weight <= 0f && _targetWeight <= 0f)
            {
                Stop();
                return;
            }

            AdvanceTime(dt);

            MovementContext ctx = _player.MovementContext;
            Vector3 forward = ctx.TransformForwardVector;
            forward.y = 0f;
            Quaternion characterRotation = Quaternion.LookRotation(forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward, Vector3.up);
            float eased = Mathf.SmoothStep(0f, 1f, _weight);

            _retarget.Apply(_clip, _time, _fromClip, _fromTime, Mathf.SmoothStep(0f, 1f, _blend), eased,
                characterRotation, ctx.TransformPosition, new BodyRetarget.Options
                {
                    PelvisWeight = Settings.BodyPelvisWeight.Value,
                    CentreHips = Settings.BodyCentreHips.Value && _faceForward,
                    FaceForward = _faceForward ? Settings.BodyFaceForward.Value : 0f,
                });

            if (_savedGrounderWeight >= 0f && _player.Grounder != null)
            {
                _player.Grounder.weight = _savedGrounderWeight * (1f - eased);
            }
        }

        private void AdvanceTime(float dt)
        {
            if (_fromClip != null)
            {
                _fromTime = Mathf.Min(_fromTime + dt, _fromClip.Length);
                _blend = Mathf.Min(1f, _blend + dt / CrossFadeTime);
                if (_blend >= 1f)
                {
                    _fromClip = null;
                }
            }
            _time += dt;
            if (_time < _clip.Length)
            {
                return;
            }
            if (_next != null)
            {
                _fromClip = _clip;
                _fromTime = _clip.Length;
                _blend = 0f;
                _clip = _next;
                _loop = _nextLoop;
                _next = null;
                _time = 0f;
            }
            else if (_loop && _clip.Length > 0f)
            {
                _time %= _clip.Length;
            }
            else
            {
                _time = _clip.Length;
            }
        }
    }
}
