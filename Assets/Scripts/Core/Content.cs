using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GameHeart
{
    /// <summary>Story/chart data from Resources and painted stills streamed from StreamingAssets/Art.</summary>
    public static class Content
    {
        public static Story Story;
        static readonly Dictionary<string, Chart> charts = new Dictionary<string, Chart>();
        static readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        static readonly LinkedList<string> artLru = new LinkedList<string>();
        static readonly HashSet<string> pinned = new HashSet<string>();
        static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();
        const int ArtCache = 14;

        public static void Init()
        {
            Story = JsonUtility.FromJson<Story>(Resources.Load<TextAsset>("Data/story").text);
        }

        public static Chart Chart(string song)
        {
            if (charts.TryGetValue(song, out var c)) return c;
            var ta = Resources.Load<TextAsset>("Charts/" + song);
            if (ta == null) { Debug.LogError("Missing chart " + song); return null; }
            c = JsonUtility.FromJson<Chart>(ta.text);
            charts[song] = c;
            return c;
        }

        public static Texture2D Art(string key) => key != null && art.TryGetValue(key, out var t) ? t : null;

        public static void Pin(string key) { if (key != null) pinned.Add(key); }

        public static IEnumerator LoadArt(string key)
        {
            if (string.IsNullOrEmpty(key) || art.ContainsKey(key)) { Touch(key); yield break; }
            using (var req = UnityWebRequestTexture.GetTexture(AudioDirector.StreamingUrl("Art/" + key + ".jpg"), true))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning($"Art missing {key}: {req.error}"); yield break; }
                var tex = DownloadHandlerTexture.GetContent(req);
                tex.name = key;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Trilinear;
                tex.anisoLevel = 2;
                art[key] = tex;
                Touch(key);
            }
        }

        public static IEnumerator LoadAll(IEnumerable<string> keys)
        {
            foreach (var k in keys) yield return LoadArt(k);
        }

        static void Touch(string key)
        {
            if (key == null) return;
            artLru.Remove(key);
            artLru.AddFirst(key);
            while (artLru.Count > ArtCache)
            {
                var node = artLru.Last;
                while (node != null && pinned.Contains(node.Value)) node = node.Previous;
                if (node == null) break;
                artLru.Remove(node);
                if (art.TryGetValue(node.Value, out var t) && t != null) UnityEngine.Object.Destroy(t);
                art.Remove(node.Value);
            }
        }

        public static Sprite Portrait(string mood)
        {
            if (string.IsNullOrEmpty(mood)) mood = "neutral";
            if (portraits.TryGetValue(mood, out var s)) return s;
            s = Resources.Load<Sprite>("Portraits/captain_" + mood) ?? Resources.Load<Sprite>("Portraits/captain_neutral");
            portraits[mood] = s;
            return s;
        }

        public static IEnumerable<string> ArtFor(Shot[] shots)
        {
            if (shots == null) yield break;
            foreach (var s in shots) yield return s.art;
        }
    }
}
