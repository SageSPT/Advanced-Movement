using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace AdvancedMovement.Body
{
    internal sealed class BodyClip
    {
        [JsonProperty("fps")] public float Fps;
        [JsonProperty("frames")] public int Frames;
        [JsonProperty("length")] public float Length;
        [JsonProperty("bones")] public Dictionary<string, List<float[]>> Bones;
        [JsonProperty("pelvis")] public List<float[]> Pelvis;

        public bool Rotation(string bone, float t, out Quaternion rotation)
        {
            if (Bones == null || !Bones.TryGetValue(bone, out List<float[]> track) || track.Count == 0)
            {
                rotation = Quaternion.identity;
                return false;
            }
            Frame(t, out int i, out int j, out float u);
            float[] a = track[i], b = track[j];
            rotation = Quaternion.Slerp(new Quaternion(a[0], a[1], a[2], a[3]), new Quaternion(b[0], b[1], b[2], b[3]), u);
            return true;
        }

        public Vector3 PelvisPosition(float t)
        {
            Frame(t, out int i, out int j, out float u);
            float[] a = Pelvis[i], b = Pelvis[j];
            return Vector3.Lerp(new Vector3(a[0], a[1], a[2]), new Vector3(b[0], b[1], b[2]), u);
        }

        private void Frame(float t, out int i, out int j, out float u)
        {
            float f = Mathf.Clamp(t * Fps, 0f, Frames - 1);
            i = Mathf.Min((int)f, Mathf.Max(0, Frames - 2));
            j = Mathf.Min(i + 1, Frames - 1);
            u = f - i;
        }
    }

    internal sealed class BodyClipLibrary
    {
        [JsonProperty("legLength")] public float LegLength;
        [JsonProperty("clips")] public Dictionary<string, BodyClip> Clips;

        public BodyClip Get(string name)
        {
            return name != null && Clips != null && Clips.TryGetValue(name, out BodyClip clip) ? clip : null;
        }

        public static BodyClipLibrary Load(string path)
        {
            var library = JsonConvert.DeserializeObject<BodyClipLibrary>(File.ReadAllText(path));
            if (library?.Clips == null || library.Clips.Count == 0)
            {
                throw new InvalidDataException("no body clips in " + path);
            }
            return library;
        }
    }
}
