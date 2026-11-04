using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameHeart
{
    public enum Judgment { Perfect, Great, Good, Miss }

    public class SongResult
    {
        public string song, diff, grade;
        public int score, perfect, great, good, miss, maxCombo, units;
        public float acc, gauge;
        public bool cleared, fc, ap, isNewBest;
    }

    class NoteRt
    {
        public ChartNote src;
        public NoteType type;
        public int lane;
        public float t, end;
        public bool headDone, tailDone, holding, missed;
        public Judgment headJ;
        public NoteView view;
        public float missedAt;
    }

    /// <summary>
    /// Plays one chart: song clock, input, judging, scoring and the 3D highway.
    /// Timing: T = song time − user offset. Notes are judged and drawn against T.
    /// </summary>
    public class RhythmGame : MonoBehaviour
    {
        public static RhythmGame I;
        public const int Lanes = 4;
        public const float LaneW = 1.7f;
        public const float HighwayLength = 72f;
        public const float PerfectW = 0.050f, GreatW = 0.095f, GoodW = 0.140f;
        const float HoldReleaseGrace = 0.12f;

        public event Action<int, Judgment, float, NoteType> Judged;   // lane (-1 = pulse), judgment, signed delta, type
        public event Action<int, bool> LaneInput;                      // lane, down
        public event Action<int, bool> Beat;                           // beat index, downbeat
        public event Action<int> ComboMilestone;
        public event Action<SongResult> Finished;

        public Chart Chart { get; private set; }
        public ChartDiff Diff { get; private set; }
        public string DiffName { get; private set; }
        public float SongTime { get; private set; }
        public float T => SongTime - Save.S.offsetMs / 1000f;
        public bool Playing { get; private set; }
        public bool Paused { get; private set; }
        public bool Autoplay;

        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Score => units == 0 ? 0 : Mathf.RoundToInt(1_000_000f * weighted / units);
        public float Accuracy => judgedUnits == 0 ? 1f : weighted / judgedUnits;
        public float Gauge { get; private set; } = 0.5f;
        public const float ClearLine = 0.7f;
        public float Progress => Chart == null ? 0 : Mathf.Clamp01(SongTime / Mathf.Max(1, Chart.length));
        public int[] Counts = new int[4];
        public bool Surge => Combo >= 30 && Gauge >= 0.85f;

        AudioSource src;
        bool audioStarted;
        float speedUnits;
        readonly List<NoteRt> notes = new List<NoteRt>();
        readonly List<NoteRt> live = new List<NoteRt>();
        int spawnIdx, beatIdx, units, judgedUnits;
        float weighted, gaugeScale;
        readonly bool[] laneDown = new bool[Lanes];
        readonly bool[] laneHeldPrev = new bool[Lanes];
        readonly Dictionary<int, int> touchLane = new Dictionary<int, int>();
        float endTime;
        HighwayView view;
        Camera cam;

        static readonly KeyCode[][] Keys =
        {
            new[] { KeyCode.D, KeyCode.LeftArrow, KeyCode.Z },
            new[] { KeyCode.F, KeyCode.DownArrow, KeyCode.X },
            new[] { KeyCode.J, KeyCode.UpArrow, KeyCode.N },
            new[] { KeyCode.K, KeyCode.RightArrow, KeyCode.M },
        };

        void Awake() { I = this; }

        public void Begin(Chart chart, string diffName, AudioClip clip, Chapter chapter, Camera camera, float seek = 0f)
        {
            Chart = chart;
            DiffName = diffName;
            Diff = chart.Diff(diffName);
            cam = camera;
            src = AudioDirector.I.Song;
            src.Stop();
            src.clip = clip;
            src.loop = false;
            AudioDirector.I.SetSongGain(chart.gain <= 0 ? 1 : chart.gain);
            speedUnits = 12f + Save.S.noteSpeed * 4.6f;
            notes.Clear(); live.Clear();
            units = 0;
            foreach (var n in Diff.notes)
            {
                if (n.t < seek + 0.5f && seek > 0) continue;
                var rt = new NoteRt { src = n, type = (NoteType)n.type, lane = n.lane, t = n.t, end = n.t + Mathf.Max(0, n.d) };
                notes.Add(rt);
                units += rt.type == NoteType.Hold ? 2 : 1;
            }
            notes.Sort((a, b) => a.t.CompareTo(b.t));
            gaugeScale = Mathf.Clamp(260f / Mathf.Max(60, units), 0.45f, 2.2f);
            Gauge = 0.5f;
            Combo = MaxCombo = 0;
            weighted = 0; judgedUnits = 0;
            Counts = new int[4];
            spawnIdx = 0;
            beatIdx = 0;
            float lastNote = 0;
            foreach (var n in notes) lastNote = Mathf.Max(lastNote, n.end);
            endTime = Mathf.Max(lastNote + 2.5f, 10f);
            if (chart.length > 0) endTime = Mathf.Min(endTime, chart.length + 0.3f);
            if (clip.length > 1f) endTime = Mathf.Min(endTime, clip.length + 0.2f);
            SongTime = seek - 2.2f;
            audioStarted = false;
            Playing = true;
            Paused = false;
            if (view == null) view = new GameObject("Highway").AddComponent<HighwayView>();
            view.transform.SetParent(transform, false);
            view.Build(chapter);
            if (chart.beats != null) while (beatIdx < chart.beats.Length && chart.beats[beatIdx] < SongTime) beatIdx++;
            Input.simulateMouseWithTouches = false;
            GH.Log("song_begin", $"{chart.song} {diffName} notes={notes.Count} units={units} end={endTime:F1}");
        }

        public void Stop()
        {
            Playing = false;
            if (src != null) src.Stop();
            foreach (var n in live) if (n.view != null) view.Release(n.view);
            live.Clear();
        }

        public void SetPaused(bool p)
        {
            if (!Playing || p == Paused) return;
            Paused = p;
            if (audioStarted) { if (p) src.Pause(); else src.UnPause(); }
        }

        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            if (!Playing || Paused) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Clock(dt);
            ReadInput();
            if (Autoplay) RunBot();
            Spawn();
            Judge();
            Beats();
            view.Tick(T, speedUnits, laneDown);
            DrawNotes();
            if (SongTime >= endTime || (audioStarted && src.clip.length > 1f && !src.isPlaying && SongTime > src.clip.length - 0.05f)) Finish();
        }

        void Clock(float dt)
        {
            SongTime += dt;
            if (!audioStarted)
            {
                if (SongTime >= 0)
                {
                    src.Play();
                    if (SongTime > 0.02f && src.clip.length > 1f) src.time = Mathf.Min(SongTime, src.clip.length - 0.1f);
                    audioStarted = true;
                }
                return;
            }
            if (src.isPlaying)
            {
                float a = src.time;
                float diff = a - SongTime;
                if (Mathf.Abs(diff) > 0.12f) SongTime = a;
                else SongTime += diff * Mathf.Min(1f, dt * 6f);
            }
        }

        // ------------------------------------------------------------------ input
        void ReadInput()
        {
            for (int l = 0; l < Lanes; l++)
            {
                bool down = false, held = false;
                foreach (var k in Keys[l])
                {
                    if (Input.GetKeyDown(k)) down = true;
                    if (Input.GetKey(k)) held = true;
                }
                if (down) Press(l);
                laneDown[l] = held;
            }
            if (Input.GetKeyDown(KeyCode.Space)) PressPulseOnly();

            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began)
                {
                    if (t.position.y > UnityEngine.Screen.height * 0.86f && t.position.x > UnityEngine.Screen.width * 0.8f) continue; // pause corner
                    int lane = LaneFromScreen(t.position.x);
                    touchLane[t.fingerId] = lane;
                    Press(lane);
                }
                if (touchLane.TryGetValue(t.fingerId, out var tl))
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) touchLane.Remove(t.fingerId);
                    else laneDown[tl] = true;
                }
            }
            if (Input.touchCount == 0)
            {
                touchLane.Clear();
                if (Input.GetMouseButtonDown(0) && !(Input.mousePosition.y > UnityEngine.Screen.height * 0.86f && Input.mousePosition.x > UnityEngine.Screen.width * 0.8f))
                {
                    mouseLane = LaneFromScreen(Input.mousePosition.x);
                    Press(mouseLane);
                }
                if (Input.GetMouseButton(0) && mouseLane >= 0) laneDown[mouseLane] = true;
                if (!Input.GetMouseButton(0)) mouseLane = -1;
            }
            for (int l = 0; l < Lanes; l++)
            {
                if (laneHeldPrev[l] && !laneDown[l]) LaneInput?.Invoke(l, false);
                laneHeldPrev[l] = laneDown[l];
            }
        }

        int mouseLane = -1;

        public int LaneFromScreen(float x)
        {
            if (cam == null) return Mathf.Clamp((int)(x / UnityEngine.Screen.width * Lanes), 0, Lanes - 1);
            for (int l = 0; l < Lanes - 1; l++)
            {
                float bx = (l + 1 - Lanes / 2f) * LaneW;
                float sx = cam.WorldToScreenPoint(new Vector3(bx, 0, 0.6f)).x;
                if (x < sx) return l;
            }
            return Lanes - 1;
        }

        void Press(int lane)
        {
            LaneInput?.Invoke(lane, true);
            AudioDirector.I.Sfx("hit_tap", 0.9f, 1f, true);
            view.Press(lane);
            NoteRt best = null;
            float bestAbs = 999;
            foreach (var n in live)
            {
                if (n.headDone || n.missed) continue;
                if (n.type != NoteType.Pulse && n.lane != lane) continue;
                float d = Mathf.Abs(T - n.t);
                if (d <= GoodW + 0.02f && d < bestAbs) { best = n; bestAbs = d; }
            }
            if (best != null) Hit(best, lane);
        }

        void PressPulseOnly()
        {
            NoteRt best = null;
            float bestAbs = 999;
            foreach (var n in live)
            {
                if (n.headDone || n.missed || n.type != NoteType.Pulse) continue;
                float d = Mathf.Abs(T - n.t);
                if (d <= GoodW + 0.02f && d < bestAbs) { best = n; bestAbs = d; }
            }
            AudioDirector.I.Sfx("hit_tap", 0.9f, 1f, true);
            if (best != null) Hit(best, -1);
        }

        void Hit(NoteRt n, int lane)
        {
            float delta = T - n.t;
            float a = Mathf.Abs(delta);
            var j = a <= PerfectW ? Judgment.Perfect : a <= GreatW ? Judgment.Great : Judgment.Good;
            n.headDone = true;
            n.headJ = j;
            Apply(j);
            int fxLane = n.type == NoteType.Pulse ? -1 : n.lane;
            if (n.type == NoteType.Hold) { n.holding = true; view.HoldStart(n.lane); }
            else { view.Release(n.view); n.view = null; }
            if (n.type == NoteType.Pulse) AudioDirector.I.Sfx("hit_pulse", 1f, 1f, true);
            else if (j == Judgment.Perfect) AudioDirector.I.Sfx("hit_perfect", 0.55f, 1f, true);
            view.HitFx(fxLane, j);
            Judged?.Invoke(fxLane, j, delta, n.type);
        }

        void Apply(Judgment j)
        {
            judgedUnits++;
            Counts[(int)j]++;
            float w = j == Judgment.Perfect ? 1f : j == Judgment.Great ? 0.75f : j == Judgment.Good ? 0.4f : 0f;
            weighted += w;
            float g = j == Judgment.Perfect ? 0.011f : j == Judgment.Great ? 0.008f : j == Judgment.Good ? 0.002f : -0.045f;
            Gauge = Mathf.Clamp01(Gauge + g * (j == Judgment.Miss ? Mathf.Sqrt(gaugeScale) : gaugeScale));
            if (j == Judgment.Miss) Combo = 0;
            else
            {
                Combo++;
                if (Combo > MaxCombo) MaxCombo = Combo;
                if (Combo % 50 == 0) ComboMilestone?.Invoke(Combo);
            }
        }

        // ------------------------------------------------------------------ autoplay (QA)
        void RunBot()
        {
            foreach (var n in live)
            {
                if (!n.headDone && !n.missed && T >= n.t - 0.004f)
                {
                    if (n.type != NoteType.Pulse) view.Press(n.lane);
                    LaneInput?.Invoke(n.type == NoteType.Pulse ? 0 : n.lane, true);
                    AudioDirector.I.Sfx("hit_tap", 0.9f, 1f, true);
                    Hit(n, n.lane);
                }
                if (n.holding) laneDown[n.lane] = true;
            }
        }

        // ------------------------------------------------------------------ simulation
        void Spawn()
        {
            float horizon = HighwayLength / speedUnits + 0.2f;
            while (spawnIdx < notes.Count && notes[spawnIdx].t - T < horizon)
            {
                var n = notes[spawnIdx++];
                n.view = view.Get(n.type, n.lane);
                live.Add(n);
            }
        }

        void Judge()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var n = live[i];
                if (!n.headDone && !n.missed && T - n.t > GoodW)
                {
                    n.missed = true;
                    n.missedAt = T;
                    Apply(Judgment.Miss);
                    if (n.type == NoteType.Hold) Apply(Judgment.Miss);
                    Judged?.Invoke(n.type == NoteType.Pulse ? -1 : n.lane, Judgment.Miss, T - n.t, n.type);
                    view.MissFx(n.type == NoteType.Pulse ? -1 : n.lane);
                }
                if (n.holding)
                {
                    if (T >= n.end)
                    {
                        n.holding = false; n.tailDone = true;
                        Apply(Judgment.Perfect);
                        view.HoldEnd(n.lane, true);
                        Judged?.Invoke(n.lane, Judgment.Perfect, 0, NoteType.Hold);
                        view.Release(n.view); n.view = null;
                    }
                    else if (!laneDown[n.lane] && n.end - T > HoldReleaseGrace)
                    {
                        n.holding = false; n.tailDone = true; n.missed = true; n.missedAt = T;
                        Apply(Judgment.Miss);
                        view.HoldEnd(n.lane, false);
                        Judged?.Invoke(n.lane, Judgment.Miss, 0, NoteType.Hold);
                    }
                    else if (!laneDown[n.lane])
                    {
                        n.holding = false; n.tailDone = true;
                        Apply(Judgment.Perfect);
                        view.HoldEnd(n.lane, true);
                        Judged?.Invoke(n.lane, Judgment.Perfect, 0, NoteType.Hold);
                        view.Release(n.view); n.view = null;
                    }
                }
                bool gone = n.view == null && !n.holding;
                if (n.missed && n.view != null && T - Mathf.Max(n.missedAt, n.type == NoteType.Hold ? n.end : n.t) > 0.45f)
                {
                    view.Release(n.view); n.view = null; gone = true;
                }
                if (gone && (n.headDone || n.missed)) live.RemoveAt(i);
            }
        }

        void Beats()
        {
            if (Chart.beats == null) return;
            while (beatIdx < Chart.beats.Length && Chart.beats[beatIdx] <= SongTime)
            {
                bool down = Chart.downbeats != null && Array.BinarySearch(Chart.downbeats, Chart.beats[beatIdx]) >= 0;
                Beat?.Invoke(beatIdx, down);
                beatIdx++;
            }
        }

        void DrawNotes()
        {
            foreach (var n in live)
            {
                if (n.view == null) continue;
                float zHead = (n.t - T) * speedUnits;
                float zTail = (n.end - T) * speedUnits;
                if (n.holding) zHead = 0;
                float fade = n.missed ? Mathf.Clamp01(1 - (T - n.missedAt) / 0.45f) : 1f;
                view.Place(n.view, n.type, n.lane, zHead, zTail, n.holding, n.missed, fade);
            }
        }

        void Finish()
        {
            if (!Playing) return;
            // judge any leftovers as misses
            foreach (var n in notes)
            {
                if (!n.headDone && !n.missed) { Apply(Judgment.Miss); if (n.type == NoteType.Hold) Apply(Judgment.Miss); n.missed = true; }
                else if (n.holding) { Apply(Judgment.Perfect); n.holding = false; }
            }
            Playing = false;
            var r = new SongResult
            {
                song = Chart.song, diff = DiffName, score = Score, acc = Accuracy, gauge = Gauge,
                perfect = Counts[0], great = Counts[1], good = Counts[2], miss = Counts[3], maxCombo = MaxCombo, units = units,
            };
            r.fc = r.miss == 0 && units > 0;
            r.ap = r.fc && r.great == 0 && r.good == 0;
            r.cleared = Gauge >= ClearLine;
            r.grade = Grade(r.acc);
            GH.Log("song_end", $"{r.song} {r.diff} score={r.score} acc={r.acc:F4} gauge={r.gauge:F2} cleared={r.cleared} P={r.perfect} G={r.great} g={r.good} M={r.miss} combo={r.maxCombo}");
            foreach (var n in live) if (n.view != null) view.Release(n.view);
            live.Clear();
            Finished?.Invoke(r);
        }

        public static string Grade(float acc) =>
            acc >= 0.985f ? "S+" : acc >= 0.95f ? "S" : acc >= 0.90f ? "A" : acc >= 0.80f ? "B" : acc >= 0.70f ? "C" : "D";

        public Vector3 LaneWorld(int lane, float z = 0) => new Vector3(lane < 0 ? 0 : (lane - (Lanes - 1) / 2f) * LaneW, 0.15f, z);
    }
}
