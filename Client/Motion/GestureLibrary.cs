using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace AdvancedMovement.Motion
{
    internal sealed class GesturePhase
    {
        [JsonProperty("fps")] public float Fps;
        [JsonProperty("length")] public float Length;
        [JsonProperty("weapon")] public List<float[]> Weapon;
        [JsonProperty("notes")] public Dictionary<string, float> Notes;

        public void SampleWeapon(float t, out Quaternion rotation, out Vector3 position)
        {
            float f = Mathf.Clamp(t * Fps, 0f, Weapon.Count - 1);
            int i = Mathf.Min((int)f, Mathf.Max(0, Weapon.Count - 2));
            int j = Mathf.Min(i + 1, Weapon.Count - 1);
            float u = f - i;
            float[] a = Weapon[i], b = Weapon[j];
            rotation = Quaternion.Slerp(new Quaternion(a[0], a[1], a[2], a[3]), new Quaternion(b[0], b[1], b[2], b[3]), u);
            position = Vector3.Lerp(new Vector3(a[4], a[5], a[6]), new Vector3(b[4], b[5], b[6]), u);
        }
    }

    internal sealed class GestureSet
    {
        [JsonProperty("in")] public GesturePhase In;
        [JsonProperty("loop")] public GesturePhase Loop;
        [JsonProperty("out")] public GesturePhase Out;
    }

    internal sealed class GestureLibrary
    {
        [JsonProperty("sets")] public Dictionary<string, GestureSet> Sets;

        public GestureSet Get(string name)
        {
            return name != null && Sets != null && Sets.TryGetValue(name, out GestureSet set) && set.In != null && set.Loop != null && set.Out != null ? set : null;
        }

        public static GestureLibrary Load(string path)
        {
            var library = JsonConvert.DeserializeObject<GestureLibrary>(File.ReadAllText(path));
            if (library?.Sets == null || library.Sets.Count == 0)
            {
                throw new InvalidDataException("no weapon motion sets in " + path);
            }
            return library;
        }
    }
}
