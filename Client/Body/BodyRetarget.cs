using System.Collections.Generic;
using UnityEngine;

namespace AdvancedMovement.Body
{
    internal sealed class BodyRetarget
    {
        public struct Options
        {
            public float PelvisWeight;
            public bool CentreHips;
            public float FaceForward;
        }

        private sealed class Bone
        {
            public string Name;
            public Transform Transform;
            public Quaternion BindRotation;
            public float Weight;
            public float FaceForwardShare;
        }

        private static readonly string[] BoneOrder =
        {
            "Base HumanPelvis", "Base HumanSpine1", "Base HumanSpine2", "Base HumanSpine3", "Base HumanNeck", "Base HumanHead",
            "Base HumanLThigh1", "Base HumanLCalf", "Base HumanLFoot", "Base HumanLToe",
            "Base HumanRThigh1", "Base HumanRCalf", "Base HumanRFoot", "Base HumanRToe",
        };

        private readonly List<Bone> _bones = new List<Bone>();
        private Transform _root;
        private Bone _pelvis;
        private float _hipScale = 1f;

        public bool Valid => _root != null && _pelvis != null && _pelvis.Transform != null;

        public static BodyRetarget Build(Transform root, float clipLegLength, float spineWeight, float headWeight)
        {
            var retarget = new BodyRetarget { _root = root };

            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!byName.ContainsKey(t.name))
                {
                    byName[t.name] = t;
                }
            }
            Dictionary<Transform, Matrix4x4> bind = ReadBindPoses(root);

            foreach (string name in BoneOrder)
            {
                if (!byName.TryGetValue(name, out Transform t))
                {
                    Plugin.Log.LogWarning($"Body bone {name} not found; it will not be animated");
                    continue;
                }
                Quaternion bindRotation = bind.TryGetValue(t, out Matrix4x4 m) ? m.rotation : Quaternion.Inverse(root.rotation) * t.rotation;
                bool isSpine = name.Contains("Spine");
                bool isHead = name.Contains("Neck") || name.Contains("Head");
                var bone = new Bone
                {
                    Name = name,
                    Transform = t,
                    BindRotation = bindRotation,
                    Weight = isSpine ? spineWeight : isHead ? headWeight : 1f,
                    FaceForwardShare = FaceForwardShareFor(name),
                };
                retarget._bones.Add(bone);
                if (name == "Base HumanPelvis")
                {
                    retarget._pelvis = bone;
                }
            }

            Vector3 BindPosition(string n) => byName.TryGetValue(n, out Transform bt) && bind.TryGetValue(bt, out Matrix4x4 bm) ? (Vector3)bm.GetColumn(3) : Vector3.zero;
            Vector3 up = BindPosition("Base HumanNeck") - BindPosition("Base HumanPelvis");
            Vector3 right = BindPosition("Base HumanRThigh1") - BindPosition("Base HumanLThigh1");
            if (up.sqrMagnitude > 1e-6f && right.sqrMagnitude > 1e-6f)
            {
                up.Normalize();
                right = (right - Vector3.Dot(right, up) * up).normalized;
                Vector3 forward = Vector3.Cross(right, up);
                Quaternion toUpright = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
                foreach (Bone b in retarget._bones)
                {
                    b.BindRotation = toUpright * b.BindRotation;
                }
            }

            float eftLegLength = LegLength(root, byName, bind);
            retarget._hipScale = clipLegLength > 0.1f && eftLegLength > 0.1f ? eftLegLength / clipLegLength : 1f;
            return retarget;
        }

        private static Dictionary<Transform, Matrix4x4> ReadBindPoses(Transform root)
        {
            var bind = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] bones = smr.bones;
                Matrix4x4[] bindposes = smr.sharedMesh != null ? smr.sharedMesh.bindposes : null;
                if (bones == null || bindposes == null || bones.Length != bindposes.Length)
                {
                    continue;
                }
                Matrix4x4 meshToRoot = root.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null && !bind.ContainsKey(bones[i]))
                    {
                        bind[bones[i]] = meshToRoot * bindposes[i].inverse;
                    }
                }
            }
            return bind;
        }

        private static float LegLength(Transform root, Dictionary<string, Transform> byName, Dictionary<Transform, Matrix4x4> bind)
        {
            if (!byName.TryGetValue("Base HumanLThigh1", out Transform thigh) || !byName.TryGetValue("Base HumanLCalf", out Transform calf) ||
                !byName.TryGetValue("Base HumanLFoot", out Transform foot) || !bind.TryGetValue(thigh, out Matrix4x4 mt) ||
                !bind.TryGetValue(calf, out Matrix4x4 mc) || !bind.TryGetValue(foot, out Matrix4x4 mf))
            {
                return 0f;
            }
            Vector3 pt = root.TransformPoint(mt.GetColumn(3)), pc = root.TransformPoint(mc.GetColumn(3)), pf = root.TransformPoint(mf.GetColumn(3));
            return Vector3.Distance(pt, pc) + Vector3.Distance(pc, pf);
        }

        private static float FaceForwardShareFor(string name)
        {
            switch (name)
            {
                case "Base HumanSpine1": return 0.4f;
                case "Base HumanSpine2": return 0.75f;
                case "Base HumanSpine3":
                case "Base HumanNeck":
                case "Base HumanHead": return 1f;
                default: return 0f;
            }
        }

        public void Apply(BodyClip clip, float t, BodyClip fromClip, float fromTime, float blend, float weight,
            Quaternion characterRotation, Vector3 characterPosition, Options options)
        {
            if (!Valid || clip == null || weight <= 0f)
            {
                return;
            }
            bool crossfade = fromClip != null && blend < 1f;
            foreach (Bone b in _bones)
            {
                if (b.Transform == null || b.Weight <= 0f || !clip.Rotation(b.Name, t, out Quaternion delta))
                {
                    continue;
                }
                if (crossfade && fromClip.Rotation(b.Name, fromTime, out Quaternion fromDelta))
                {
                    delta = Quaternion.Slerp(fromDelta, delta, blend);
                }
                float share = options.FaceForward * b.FaceForwardShare;
                if (share > 0f)
                {
                    Vector3 f = delta * Vector3.forward;
                    if (f.x * f.x + f.z * f.z > 1e-4f)
                    {
                        float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                        delta = Quaternion.AngleAxis(-yaw * share, Vector3.up) * delta;
                    }
                }

                Quaternion target = characterRotation * (delta * b.BindRotation);
                float w = weight * b.Weight;
                b.Transform.rotation = w >= 1f ? target : Quaternion.Slerp(b.Transform.rotation, target, w);

                if (b == _pelvis && options.PelvisWeight > 0f)
                {
                    Vector3 local = clip.PelvisPosition(t);
                    if (crossfade)
                    {
                        local = Vector3.Lerp(fromClip.PelvisPosition(fromTime), local, blend);
                    }
                    local *= _hipScale;
                    if (options.CentreHips)
                    {
                        local.x = 0f;
                    }
                    Vector3 position = characterPosition + characterRotation * local;
                    b.Transform.position = Vector3.Lerp(b.Transform.position, position, weight * options.PelvisWeight);
                }
            }
        }
    }
}
