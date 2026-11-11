using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace AdvancedMovement.Motion
{
    internal sealed class MotionProfile
    {
        [JsonProperty("fps")] public float Fps;
        [JsonProperty("distance")] public List<float> Distance;
        [JsonProperty("mainEndTime")] public float MainEndTime;
        [JsonProperty("height")] public List<float> Height;

        public float Length => (Distance.Count - 1) / Fps;
        public float TotalDistance => Distance[Distance.Count - 1];

        public float DistanceAt(float t)
        {
            float f = Mathf.Clamp(t * Fps, 0f, Distance.Count - 1);
            int i = Mathf.Min((int)f, Distance.Count - 2);
            return Mathf.Lerp(Distance[i], Distance[i + 1], f - i);
        }

        public float HeightAt(float t)
        {
            if (Height == null || Height.Count < 2)
            {
                return 0f;
            }
            float f = Mathf.Clamp(t * Fps, 0f, Height.Count - 1);
            int i = Mathf.Min((int)f, Height.Count - 2);
            return Mathf.Lerp(Height[i], Height[i + 1], f - i);
        }

        public float SpeedAt(float t)
        {
            float dt = 1f / Fps;
            return (DistanceAt(t + dt * 0.5f) - DistanceAt(t - dt * 0.5f)) / dt;
        }

        public float PeakSpeed()
        {
            float max = 0f;
            for (int i = 1; i < Distance.Count; i++)
            {
                max = Mathf.Max(max, (Distance[i] - Distance[i - 1]) * Fps);
            }
            return max;
        }

        public static MotionProfile Load(string path)
        {
            var profile = JsonConvert.DeserializeObject<MotionProfile>(File.ReadAllText(path));
            if (profile?.Distance == null || profile.Distance.Count < 2 || profile.Fps <= 0f)
            {
                throw new InvalidDataException("bad motion profile " + path);
            }
            return profile;
        }
    }
}
