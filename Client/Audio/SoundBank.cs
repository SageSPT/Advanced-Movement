using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace AdvancedMovement.Audio
{
    internal sealed class SoundBank
    {
        private const string BundleFile = "sounds.bundle";
        private const string AssetRoot = "assets/sounds/";

        private sealed class Entry
        {
            [JsonProperty("file")] public string File;
            [JsonProperty("volume")] public float Volume;
            [JsonIgnore] public AudioClip Clip;
            [JsonIgnore] public bool Failed;
        }

        private sealed class Index
        {
            [JsonProperty("cues")] public Dictionary<string, Dictionary<string, List<Entry>>> Cues;
        }

        private readonly AssetBundle _bundle;
        private readonly Index _index;
        private readonly System.Random _rng = new System.Random();
        private readonly Dictionary<string, int> _lastPick = new Dictionary<string, int>();

        private SoundBank(AssetBundle bundle, Index index)
        {
            _bundle = bundle;
            _index = index;
        }

        public static SoundBank Load(string dir)
        {
            var index = JsonConvert.DeserializeObject<Index>(File.ReadAllText(Path.Combine(dir, "index.json")));
            if (index?.Cues == null)
            {
                throw new InvalidDataException("bad sound index in " + dir);
            }
            string path = Path.Combine(dir, BundleFile);
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                throw new FileNotFoundException("could not open the sound bundle", path);
            }
            return new SoundBank(bundle, index);
        }

        public bool Has(string cue, string group)
        {
            return _index.Cues.TryGetValue(cue, out var groups) && groups.TryGetValue(group, out var list) && list.Count > 0;
        }

        public AudioClip Pick(string cue, string group, out float volume)
        {
            volume = 1f;
            if (!_index.Cues.TryGetValue(cue, out var groups) || !groups.TryGetValue(group, out var list) || list.Count == 0)
            {
                return null;
            }
            string key = cue + "/" + group;
            _lastPick.TryGetValue(key, out int last);
            int i = list.Count == 1 ? 0 : _rng.Next(list.Count - 1);
            if (list.Count > 1 && i >= last)
            {
                i++;
            }
            _lastPick[key] = i;
            Entry e = list[i];
            if (e.Clip == null && !e.Failed)
            {
                e.Clip = _bundle.LoadAsset<AudioClip>(AssetRoot + e.File.Replace('\\', '/').ToLowerInvariant());
                if (e.Clip == null)
                {
                    e.Failed = true;
                    Plugin.Log.LogWarning("Sound not found in " + BundleFile + ": " + e.File);
                }
            }
            volume = e.Volume > 0f ? e.Volume : 1f;
            return e.Clip;
        }
    }
}
