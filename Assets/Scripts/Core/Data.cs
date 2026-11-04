using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameHeart
{
    // ------------------------------------------------------------------ story
    [Serializable] public class StoryLine { public string speaker, mood, text, voice; }
    [Serializable] public class Shot { public string art, move, fx, tint; public StoryLine[] lines; }
    [Serializable] public class Vital { public string label, value, trend; }
    [Serializable] public class Feedback { public string choice; public Shot[] shots; }
    [Serializable] public class Decision { public string prompt, correct; public Vital[] vitals; public Feedback[] feedback; public StoryLine[] right; }
    [Serializable] public class QuizItem { public string q; public string[] options; public int answer; public string explain; }
    [Serializable] public class Bark { public string key; public StoryLine line; }
    [Serializable] public class Sequence { public string id; public Shot[] shots; }

    [Serializable]
    public class Chapter
    {
        public string id; public int index; public string title, subtitle, song, songTitle, artist;
        public int bpmHint; public string color, accent, stage, theme;
        public Shot[] intro; public Decision decision; public Shot[] win, struggle; public QuizItem[] quiz;

        public bool HasDecision => decision != null && !string.IsNullOrEmpty(decision.prompt);
        public Color Color => GH.Hex(color);
        public Color Accent => GH.Hex(accent);
    }

    [Serializable]
    public class Story
    {
        public Sequence prologue; public Chapter[] chapters; public Bark[] barks;

        public Chapter Get(string id) => Array.Find(chapters, c => c.id == id);
        public StoryLine BarkLine(string key) => Array.Find(barks, b => b.key == key)?.line;
    }

    [Serializable] public class VoiceInfo { public string id; public float duration; public float[] env; }
    [Serializable] public class VoiceTable { public VoiceInfo[] lines; }

    // ------------------------------------------------------------------ charts
    public enum NoteType { Tap = 0, Hold = 1, Pulse = 2 }

    [Serializable] public class ChartNote { public float t; public int lane; public int type; public float d; }
    [Serializable] public class ChartDiff { public string name; public int level; public ChartNote[] notes; }
    [Serializable] public class LyricLine { public float t, e; public string text; }
    [Serializable] public class Section { public float t; public string kind; public float energy; }

    [Serializable]
    public class Chart
    {
        public string song, title;
        public float bpm, offset, length, previewStart, gain = 1f;
        public float[] beats;
        public float[] downbeats;
        public ChartDiff[] diffs;
        public LyricLine[] lyrics;
        public Section[] sections;
        public float[] energy; // 10 Hz loudness envelope, 0..1

        public ChartDiff Diff(string name)
        {
            var d = Array.Find(diffs, x => x.name == name);
            return d ?? diffs[Mathf.Min(1, diffs.Length - 1)];
        }

        public float EnergyAt(float t)
        {
            if (energy == null || energy.Length == 0) return 0.5f;
            float f = Mathf.Clamp(t * 10f, 0, energy.Length - 1.001f);
            int i = (int)f;
            return Mathf.Lerp(energy[i], energy[Mathf.Min(i + 1, energy.Length - 1)], f - i);
        }
    }

    // ------------------------------------------------------------------ save
    [Serializable]
    public class SongBest
    {
        public string key; public int score; public float acc; public string grade; public bool fc, ap; public int combo;
    }

    [Serializable]
    public class Progress
    {
        public int version = 2;
        public bool prologueSeen;
        public List<string> cleared = new List<string>();
        public List<string> wins = new List<string>();
        public List<SongBest> bests = new List<SongBest>();
        public bool finaleSeen;
    }

    [Serializable]
    public class Settings
    {
        public float music = 0.85f, sfx = 0.7f, voice = 1f, hitsound = 0.55f;
        public float noteSpeed = 6f;
        public int offsetMs = 0;
        public int difficulty = 1;
        public bool autoAdvance = true;
        public bool reduceFlash = false;
        public bool lyrics = true;
        public bool highQuality = true;
        public bool subtitles = true;
    }

    public static class Save
    {
        const string ProgressKey = "gh.progress.v2", SettingsKey = "gh.settings.v2";
        public static Progress P = new Progress();
        public static Settings S = new Settings();
        public static event Action SettingsChanged;

        public static void Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(ProgressKey)) P = JsonUtility.FromJson<Progress>(PlayerPrefs.GetString(ProgressKey)) ?? new Progress();
                if (PlayerPrefs.HasKey(SettingsKey)) S = JsonUtility.FromJson<Settings>(PlayerPrefs.GetString(SettingsKey)) ?? new Settings();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save load failed, starting fresh: " + e.Message);
                P = new Progress(); S = new Settings();
            }
            if (GH.Flag("fresh")) { P = new Progress(); }
            if (GH.Flag("unlock"))
            {
                foreach (var id in new[] { "tutorial", "cold", "exercise", "sickness", "sleep", "finale" })
                    if (!P.cleared.Contains(id)) P.cleared.Add(id);
                P.prologueSeen = true;
            }
        }

        public static void Write()
        {
            if (GH.Flag("nosave")) return;
            PlayerPrefs.SetString(ProgressKey, JsonUtility.ToJson(P));
            PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(S));
            PlayerPrefs.Save();
        }

        public static void NotifySettings() { Write(); SettingsChanged?.Invoke(); }

        public static bool Cleared(string chapterId) => P.cleared.Contains(chapterId);

        public static void MarkCleared(string chapterId, bool win)
        {
            if (!P.cleared.Contains(chapterId)) P.cleared.Add(chapterId);
            if (win && !P.wins.Contains(chapterId)) P.wins.Add(chapterId);
            Write();
        }

        public static SongBest Best(string song, string diff) => P.bests.Find(b => b.key == song + ":" + diff);

        public static bool Submit(string song, string diff, int score, float acc, string grade, bool fc, bool ap, int combo)
        {
            var b = Best(song, diff);
            bool isNew = b == null || score > b.score;
            if (b == null) { b = new SongBest { key = song + ":" + diff }; P.bests.Add(b); }
            if (isNew) { b.score = score; b.acc = acc; b.grade = grade; }
            b.fc |= fc; b.ap |= ap; b.combo = Mathf.Max(b.combo, combo);
            Write();
            return isNew;
        }
    }
}
