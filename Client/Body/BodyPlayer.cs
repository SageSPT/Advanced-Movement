using System.Collections.Generic;
using EFT;
using AdvancedMovement.Sync;
using UnityEngine;

namespace AdvancedMovement.Body
{
    internal static class BodyPlayer
    {
        private static BodyAnimator _local;
        private static readonly Dictionary<Player, BodyAnimator> Remote = new Dictionary<Player, BodyAnimator>();

        public static bool Active => _local != null && _local.Active;
        public static string ClipName => _local?.ClipName;

        public static bool Play(Player player, string clipName, bool loop = false, float fadeInTime = -1f)
        {
            if (_local == null || _local.Player != player)
            {
                _local?.Stop();
                _local = new BodyAnimator(player);
                Remote.Clear();
            }
            bool started = _local.Play(clipName, loop, fadeInTime);
            if (started)
            {
                BodySync.Broadcast(BodyOpType.Play, clipName, loop, fadeInTime);
            }
            return started;
        }

        public static void Then(string clipName, bool loop = false)
        {
            if (_local == null || !_local.Active)
            {
                return;
            }
            _local.Then(clipName, loop);
            BodySync.Broadcast(BodyOpType.Then, clipName, loop);
        }

        public static void Release(float holdSeconds = 0f)
        {
            if (_local == null || !_local.Active)
            {
                return;
            }
            _local.Release(holdSeconds);
            BodySync.Broadcast(BodyOpType.Release, value: holdSeconds);
        }

        public static void Reset()
        {
            if (_local != null && _local.Active)
            {
                BodySync.Broadcast(BodyOpType.Stop);
            }
            _local?.Stop();
            _local = null;
            foreach (BodyAnimator animator in Remote.Values)
            {
                animator.Stop();
            }
            Remote.Clear();
        }

        public static void Apply(Player player)
        {
            if (_local != null && _local.Player == player)
            {
                _local.Apply();
            }
        }

        public static void ApplyRemote(Player player)
        {
            if (player != null && Remote.TryGetValue(player, out BodyAnimator animator))
            {
                animator.Apply();
                if (!animator.Active)
                {
                    Remote.Remove(player);
                }
            }
        }

        public static void Receive(Player player, BodyOp op)
        {
            if (player == null)
            {
                return;
            }
            Remote.TryGetValue(player, out BodyAnimator animator);
            switch (op.Type)
            {
                case BodyOpType.Play:
                    if (animator == null)
                    {
                        animator = new BodyAnimator(player);
                        Remote[player] = animator;
                    }
                    animator.Play(op.Clip, op.Loop, op.Value);
                    break;
                case BodyOpType.Then:
                    animator?.Then(op.Clip, op.Loop);
                    break;
                case BodyOpType.Release:
                    animator?.Release(op.Value);
                    break;
                default:
                    animator?.Stop();
                    Remote.Remove(player);
                    break;
            }
        }

        public static string DiveClip(Player player, Vector3 direction, string prefix, bool hasDiagonals)
        {
            Vector3 forward = player.MovementContext.TransformForwardVector;
            forward.y = 0f;
            direction.y = 0f;
            float angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            float abs = Mathf.Abs(angle);
            bool right = angle > 0f;
            if (abs > 135f)
            {
                return prefix + "2";
            }
            if (abs > 67.5f)
            {
                return prefix + (right ? "6" : "4");
            }
            if (abs > 22.5f)
            {
                return prefix + (hasDiagonals ? (right ? "9" : "7") : "8");
            }
            return prefix + "8";
        }

        public static string DirectionalClip(Player player, Vector3 direction, string prefix)
        {
            Vector3 forward = player.MovementContext.TransformForwardVector;
            forward.y = 0f;
            direction.y = 0f;
            float angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            string suffix = angle < -20f ? "7" : angle > 20f ? "9" : "8";
            return prefix + suffix;
        }
    }
}
