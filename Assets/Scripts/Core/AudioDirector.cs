using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GameHeart
{
    /// <summary>
    /// Owns every AudioSource: the gameplay song, two crossfading BGM decks, Captain B's voice,
    /// and a pool for SFX. Songs and BGM stream from StreamingAssets/Music as MP3 on demand.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I;

        public AudioSource Song { get; private set; }
        AudioSource bgmA, bgmB, voice;
        AudioSource[] sfx;
        int sfxIndex;
        bool bgmOnA = true;
        string bgmKey;
        float bgmTarget = 1f, duck = 1f, duckTarget = 1f;
        Coroutine bgmFade;

        readonly Dictionary<string, AudioClip> music = new Dictionary<string, AudioClip>();
        readonly LinkedList<string> musicLru = new LinkedList<string>();
        readonly Dictionary<string, AudioClip> sfxClips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, AudioClip> voiceClips = new Dictionary<string, AudioClip>();
        public readonly Dictionary<string, VoiceInfo> VoiceMeta = new Dictionary<string, VoiceInfo>();
        const int MusicCacheSize = 3;

        public float VoiceEndsAt { get; private set; }
        public string CurrentVoice { get; private set; }
        public float VoiceTime => voice != null && voice.isPlaying ? voice.time : -1f;

        void Awake()
        {
            I = this;
            Song = Make("Song");
            bgmA = Make("BGM A", true);
            bgmB = Make("BGM B", true);
            voice = Make("Voice");
            sfx = new AudioSource[12];
            for (int i = 0; i < sfx.Length; i++) sfx[i] = Make("SFX " + i);
            var table = Resources.Load<TextAsset>("Data/voice");
            if (table != null)
                foreach (var v in JsonUtility.FromJson<VoiceTable>(table.text).lines) VoiceMeta[v.id] = v;
            Save.SettingsChanged += ApplyVolumes;
        }

        void OnDestroy() { Save.SettingsChanged -= ApplyVolumes; }

        AudioSource Make(string n, bool loop = false)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0;
            return s;
        }

        void Update()
        {
            duckTarget = voice.isPlaying ? 0.38f : 1f;
            duck = GH.Damp(duck, duckTarget, voice.isPlaying ? 10f : 3f, Time.unscaledDeltaTime);
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            var s = Save.S;
            float bgm = s.music * bgmTarget * duck;
            if (bgmFade == null)
            {
                (bgmOnA ? bgmA : bgmB).volume = bgm;
                (bgmOnA ? bgmB : bgmA).volume = 0;
            }
            Song.volume = s.music * songGain;
            voice.volume = s.voice;
        }

        float songGain = 1f;
        public void SetSongGain(float g) { songGain = g; ApplyVolumes(); }

        // ------------------------------------------------------------------ music loading
        public static string StreamingUrl(string rel)
        {
            string root = Application.streamingAssetsPath;
            if (!root.Contains("://")) root = "file://" + root;
            return root + "/" + rel + (root.StartsWith("http") ? "?v=" + Application.version : "");
        }

        public bool HasMusic(string key) => music.ContainsKey(key);

        public IEnumerator LoadMusic(string key, Action<float> progress = null)
        {
            if (music.TryGetValue(key, out var have) && have != null) { Touch(key); yield break; }
            using (var req = UnityWebRequestMultimedia.GetAudioClip(StreamingUrl("Music/" + key + ".mp3"), AudioType.MPEG))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
                var op = req.SendWebRequest();
                while (!op.isDone) { progress?.Invoke(req.downloadProgress * 0.9f); yield return null; }
                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (key.EndsWith("_inst")) Debug.LogWarning($"Optional music missing {key}: {req.error}");
                    else Debug.LogError($"Music load failed {key}: {req.error}");
                    yield break;
                }
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = key;
                while (clip.loadState == AudioDataLoadState.Loading) yield return null;
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                // WebAudio decodes asynchronously: length stays 0 until decodeAudioData finishes.
                float waited = 0;
                while (clip.length <= 0.01f && waited < 20f) { waited += Time.unscaledDeltaTime; yield return null; }
                music[key] = clip;
                Touch(key);
                progress?.Invoke(1f);
                GH.Log("music_loaded", $"{key} len={clip.length:F2}");
            }
        }

        void Touch(string key)
        {
            musicLru.Remove(key);
            musicLru.AddFirst(key);
            while (musicLru.Count > MusicCacheSize)
            {
                string old = musicLru.Last.Value;
                musicLru.RemoveLast();
                if (old == bgmKey || (Song.clip != null && Song.clip.name == old)) { musicLru.AddFirst(old); break; }
                if (music.TryGetValue(old, out var c) && c != null) Destroy(c);
                music.Remove(old);
            }
        }

        public AudioClip Music(string key) => music.TryGetValue(key, out var c) ? c : null;

        // ------------------------------------------------------------------ BGM
        public void PlayBgm(string key, float fade = 1.2f, float volume = 1f, float startAt = 0f)
        {
            if (key == bgmKey && (bgmOnA ? bgmA : bgmB).isPlaying) { bgmTarget = volume; return; }
            StartCoroutine(PlayBgmRoutine(key, fade, volume, startAt));
        }

        IEnumerator PlayBgmRoutine(string key, float fade, float volume, float startAt)
        {
            bgmKey = key;
            if (!HasMusic(key)) yield return LoadMusic(key);
            if (bgmKey != key) yield break;
            var clip = Music(key);
            if (clip == null) yield break;
            var from = bgmOnA ? bgmA : bgmB;
            var to = bgmOnA ? bgmB : bgmA;
            bgmOnA = !bgmOnA;
            to.clip = clip;
            to.volume = 0;
            to.Play();
            if (startAt > 0 && startAt < clip.length) to.time = startAt;
            bgmTarget = volume;
            if (bgmFade != null) StopCoroutine(bgmFade);
            bgmFade = StartCoroutine(Crossfade(from, to, fade));
        }

        IEnumerator Crossfade(AudioSource from, AudioSource to, float dur)
        {
            float fromV = from.volume;
            yield return GH.Tween(dur, k =>
            {
                float target = Save.S.music * bgmTarget * duck;
                to.volume = target * k;
                from.volume = fromV * (1 - k);
            }, GH.Smooth01);
            from.Stop();
            bgmFade = null;
        }

        public void StopBgm(float fade = 1f)
        {
            bgmKey = null;
            var cur = bgmOnA ? bgmA : bgmB;
            if (bgmFade != null) StopCoroutine(bgmFade);
            bgmFade = StartCoroutine(FadeOut(cur, fade));
        }

        IEnumerator FadeOut(AudioSource s, float dur)
        {
            float v = s.volume;
            yield return GH.Tween(dur, k => s.volume = v * (1 - k), GH.Smooth01);
            s.Stop();
            bgmFade = null;
        }

        public void SetBgmLevel(float v) => bgmTarget = v;

        // ------------------------------------------------------------------ voice
        public float PlayVoice(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            if (!voiceClips.TryGetValue(id, out var clip))
            {
                clip = Resources.Load<AudioClip>("Voice/" + id);
                voiceClips[id] = clip;
            }
            if (clip == null) return 0;
            voice.Stop();
            voice.clip = clip;
            voice.Play();
            CurrentVoice = id;
            VoiceEndsAt = Time.unscaledTime + clip.length;
            return clip.length;
        }

        public void StopVoice() { voice.Stop(); CurrentVoice = null; }
        public bool VoicePlaying => voice.isPlaying;

        /// <summary>Loudness of the current voice line (0..1) from the baked envelope; WebGL cannot read output samples.</summary>
        public float VoiceLevel()
        {
            if (!voice.isPlaying || CurrentVoice == null || !VoiceMeta.TryGetValue(CurrentVoice, out var m) || m.env == null || m.env.Length == 0) return 0;
            float f = voice.time * 30f;
            int i = Mathf.Clamp((int)f, 0, m.env.Length - 1);
            return m.env[i];
        }

        public void ReleaseVoices()
        {
            foreach (var kv in voiceClips) if (kv.Value != null) Resources.UnloadAsset(kv.Value);
            voiceClips.Clear();
        }

        // ------------------------------------------------------------------ SFX
        public void Sfx(string name, float vol = 1f, float pitch = 1f, bool isHit = false)
        {
            if (!sfxClips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                sfxClips[name] = clip;
            }
            if (clip == null) return;
            var s = sfx[sfxIndex = (sfxIndex + 1) % sfx.Length];
            s.pitch = pitch;
            s.PlayOneShot(clip, vol * (isHit ? Save.S.hitsound : Save.S.sfx));
        }
    }
}
