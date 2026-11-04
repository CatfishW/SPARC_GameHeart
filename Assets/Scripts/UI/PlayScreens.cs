using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHeart
{
    /// <summary>Pre-song card over the live stage: song info, difficulty, note speed, START.</summary>
    public class SongCard : GHScreen
    {
        public static readonly string[] Diffs = { "easy", "normal", "hard" };
        string result;
        int diff;
        readonly List<GHButton> diffButtons = new List<GHButton>();
        TextMeshProUGUI best, speed;
        Chapter ch;
        Chart chart;

        public static SongCard Make(Chapter ch, Chart chart)
        {
            var s = Create<SongCard>(GameRoot.I.ScreenLayer, "SongCard");
            s.ch = ch; s.chart = chart;
            s.Build();
            return s;
        }

        void Build()
        {
            diff = Mathf.Clamp(Save.S.difficulty, 0, 2);
            var qd = GH.Query("diff");
            if (qd != null && System.Array.IndexOf(Diffs, qd) >= 0) diff = System.Array.IndexOf(Diffs, qd);
            var shade = UI.Img(Root, "Shade", Sprites.GradientH, new Color(0, 0, 0, 0.85f));
            UI.Fill(shade.rectTransform);
            shade.rectTransform.localScale = new Vector3(-1, 1, 1);
            var panel = UI.Panel(Root, "Card", ch.Color);
            UI.At(panel, new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(760, 920), new Vector2(1, 0.5f));
            var chap = UI.Text(panel, "Chapter", (ch.index >= 6 ? "FINALE" : $"CHAPTER {GH.Pad2(ch.index)}") + "  ·  " + ch.title.ToUpperInvariant(), UI.Label, 28, ch.Accent, TextAlignmentOptions.Left);
            UI.At(chap.rectTransform, new Vector2(0, 1), new Vector2(50, -50), new Vector2(660, 40), new Vector2(0, 1));
            chap.characterSpacing = 8;
            var title = UI.Text(panel, "Title", ch.songTitle.ToUpperInvariant(), UI.Display, 64, Color.white, TextAlignmentOptions.Left);
            UI.At(title.rectTransform, new Vector2(0, 1), new Vector2(50, -100), new Vector2(660, 170), new Vector2(0, 1));
            title.enableAutoSizing = true; title.fontSizeMin = 40; title.fontSizeMax = 66;
            UI.Glowing(title, ch.Color, 0.3f);
            float bpm = chart != null ? chart.bpm : ch.bpmHint;
            float len = chart != null ? chart.length : 0;
            var meta = UI.Text(panel, "Meta", $"{ch.artist}\n<color=#{ColorUtility.ToHtmlStringRGB(ch.Accent)}>♥ {bpm:0} BPM</color>   ·   {(int)(len / 60)}:{(int)(len % 60):00}", UI.Body, 30, UI.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(meta.rectTransform, new Vector2(0, 1), new Vector2(50, -280), new Vector2(660, 100), new Vector2(0, 1));

            var dl = UI.Text(panel, "DiffLabel", "DIFFICULTY", UI.Label, 27, UI.TextDim, TextAlignmentOptions.Left);
            UI.At(dl.rectTransform, new Vector2(0, 1), new Vector2(50, -400), new Vector2(400, 34), new Vector2(0, 1));
            dl.characterSpacing = 8;
            var colors = new[] { UI.Good, UI.Gold, UI.Bad };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                int level = chart != null && chart.diffs != null && i < chart.diffs.Length ? chart.diffs[i].level : (i + 1) * 3;
                var b = UI.Button(panel, Diffs[i].ToUpperInvariant() + "  " + level, colors[i], () => { diff = k; Refresh(); }, 28);
                UI.At((RectTransform)b.transform, new Vector2(0, 1), new Vector2(50 + i * 222, -446), new Vector2(206, 80), new Vector2(0, 1));
                diffButtons.Add(b);
            }
            best = UI.Text(panel, "Best", "", UI.Body, 28, UI.Fg, TextAlignmentOptions.TopLeft);
            UI.At(best.rectTransform, new Vector2(0, 1), new Vector2(50, -550), new Vector2(660, 80), new Vector2(0, 1));

            var sl = UI.Text(panel, "SpeedLabel", "NOTE SPEED", UI.Label, 27, UI.TextDim, TextAlignmentOptions.Left);
            UI.At(sl.rectTransform, new Vector2(0, 1), new Vector2(50, -640), new Vector2(300, 34), new Vector2(0, 1));
            sl.characterSpacing = 8;
            var minus = UI.Button(panel, "–", ch.Color, () => { Save.S.noteSpeed = Mathf.Max(1, Save.S.noteSpeed - 0.5f); Refresh(); }, 40);
            UI.At((RectTransform)minus.transform, new Vector2(0, 1), new Vector2(330, -628), new Vector2(70, 60), new Vector2(0, 1));
            speed = UI.Text(panel, "Speed", "", UI.DisplayBold, 34, Color.white, TextAlignmentOptions.Center);
            UI.At(speed.rectTransform, new Vector2(0, 1), new Vector2(410, -628), new Vector2(110, 60), new Vector2(0, 1));
            var plus = UI.Button(panel, "+", ch.Color, () => { Save.S.noteSpeed = Mathf.Min(10, Save.S.noteSpeed + 0.5f); Refresh(); }, 40);
            UI.At((RectTransform)plus.transform, new Vector2(0, 1), new Vector2(530, -628), new Vector2(70, 60), new Vector2(0, 1));

            var hint = UI.Text(panel, "Keys", "Keys  <b>D F J K</b>  ·  Pulses  <b>SPACE</b> or any lane\nTouch: tap the lanes at the bottom", UI.BodyLight, 26, UI.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(hint.rectTransform, new Vector2(0, 1), new Vector2(50, -712), new Vector2(660, 70), new Vector2(0, 1));

            var back = UI.Button(panel, "BACK", UI.TextDim, () => result = "back", 30);
            UI.At((RectTransform)back.transform, new Vector2(0, 0), new Vector2(50, 40), new Vector2(200, 84), new Vector2(0, 0));
            var start = UI.Button(panel, "START  ▶", ch.Color, () => result = "start", 40, UI.DisplayBold);
            UI.At((RectTransform)start.transform, new Vector2(1, 0), new Vector2(-50, 40), new Vector2(420, 84), new Vector2(1, 0));
            start.HoverSfx = "ui_hover";
            Refresh();
            Focus(start);
        }

        void Refresh()
        {
            for (int i = 0; i < diffButtons.Count; i++) diffButtons[i].Highlighted = i == diff;
            var b = Save.Best(ch.song, Diffs[diff]);
            best.text = b == null ? "<color=#B8A9B0>No record yet — be the first beat.</color>"
                : $"BEST  <b>{GH.FormatScore(b.score)}</b>   ·   {b.grade}   ·   {b.acc * 100:0.00}%{(b.ap ? "   ·   <color=#FFD36E>ALL PERFECT</color>" : b.fc ? "   ·   <color=#6BFF9E>FULL COMBO</color>" : "")}";
            speed.text = Save.S.noteSpeed.ToString("0.0");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) result = "back";
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { diff = Mathf.Max(0, diff - 1); Refresh(); }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { diff = Mathf.Min(2, diff + 1); Refresh(); }
        }

        public IEnumerator Ask(System.Action<string, string> done)
        {
            result = null;
            yield return FadeIn(0.35f);
            string qa = GH.Query("diff");
            float t = 0;
            while (result == null)
            {
                t += Time.unscaledDeltaTime;
                if (GH.QA && GH.Flag("auto") && t > 1f) { if (qa != null) diff = System.Array.IndexOf(Diffs, qa); result = "start"; }
                yield return null;
            }
            Save.S.difficulty = diff;
            Save.Write();
            done(result, Diffs[Mathf.Max(0, diff)]);
        }
    }

    /// <summary>In-song HUD.</summary>
    public class Hud : MonoBehaviour
    {
        RectTransform root;
        CanvasGroup group;
        TextMeshProUGUI score, acc, combo, comboLabel, judge, bpmText, lyric, songTitle, surgeText, hintText;
        RectTransform judgeRt, comboRt;
        Image gaugeFill, gaugeGlow, progressFill, lowWarn, heartIcon;
        EcgLine ecg;
        readonly float[] ecgBuf = new float[180];
        int ecgHead;
        float ecgAcc, ecgSpike, judgeT = 9, comboPunch, displayScore, lowPulse;
        Chapter ch;
        RhythmGame game;
        int lyricIdx = -1;
        readonly TextMeshProUGUI[] laneHints = new TextMeshProUGUI[4];
        CanvasGroup hintsGroup;
        bool tutorial;
        CanvasGroup lyricGroup;
        public GHButton PauseButton;
        public System.Action OnPause;

        public static Hud Make(Chapter ch, RhythmGame game)
        {
            var rt = UI.Node("HUD", GameRoot.I.HudLayer);
            UI.Fill(rt);
            var h = rt.gameObject.AddComponent<Hud>();
            h.root = rt; h.ch = ch; h.game = game;
            h.group = UI.Group(rt, 0);
            h.Build();
            return h;
        }

        void Build()
        {
            tutorial = ch.id == "tutorial";
            lowWarn = UI.Img(root, "LowWarn", Sprites.Vignette, new Color(1, 0.05f, 0.1f, 0));
            lowWarn.material = UI.Add1;
            UI.Fill(lowWarn.rectTransform);

            // ECG monitor (top-left)
            var mon = UI.Panel(root, "Monitor", ch.Color, 16, 0.4f);
            UI.At(mon, new Vector2(0, 1), new Vector2(36, -30), new Vector2(470, 150), new Vector2(0, 1));
            heartIcon = UI.Glow(mon, "Heart", Sprites.Heart, ch.Color, UI.Add2);
            UI.At(heartIcon.rectTransform, new Vector2(0, 1), new Vector2(42, -40), new Vector2(40, 40));
            bpmText = UI.Text(mon, "BPM", "", UI.DisplayBold, 34, Color.white, TextAlignmentOptions.Left);
            UI.At(bpmText.rectTransform, new Vector2(0, 1), new Vector2(76, -18), new Vector2(380, 44), new Vector2(0, 1));
            var ecgRt = UI.Node("ECG", mon);
            UI.Stretch(ecgRt, Vector2.zero, new Vector2(1, 0), new Vector2(20, 14), new Vector2(-20, 92));
            ecg = ecgRt.gameObject.AddComponent<EcgLine>();
            ecg.material = UI.Add2;
            ecg.color = ch.Color;
            ecg.Thickness = 4;
            ecg.AlphaAlong = u => Mathf.Clamp01(u * 4) * Mathf.Clamp01((1 - u) * 30 + 0.2f);

            // stability gauge (left)
            var gp = UI.Panel(root, "Gauge", ch.Color, 12, 0.35f);
            UI.At(gp, new Vector2(0, 0.5f), new Vector2(40, -40), new Vector2(46, 520), new Vector2(0, 0.5f));
            var track = UI.Img(gp, "Track", Sprites.Round(8), new Color(1, 1, 1, 0.07f));
            UI.Fill(track.rectTransform, 10, 10, 10, 10);
            gaugeGlow = UI.Glow(track.rectTransform, "Glow", Sprites.Round(8), ch.Color.WithA(0.6f), UI.Add2);
            gaugeFill = UI.Img(track.rectTransform, "Fill", Sprites.Round(8), ch.Color);
            UI.Stretch(gaugeFill.rectTransform, Vector2.zero, new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);
            UI.Stretch(gaugeGlow.rectTransform, Vector2.zero, new Vector2(1, 0.5f), new Vector2(-8, -8), new Vector2(8, 8));
            var line = UI.Img(gp, "ClearLine", Sprites.White, Color.white.WithA(0.9f));
            UI.Stretch(line.rectTransform, new Vector2(0, RhythmGame.ClearLine), new Vector2(1, RhythmGame.ClearLine), new Vector2(-6, -1.5f), new Vector2(6, 1.5f));
            var gl = UI.Text(gp, "Label", "HEART\nSTABILITY", UI.Label, 21, UI.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(gl.rectTransform, new Vector2(0, 0), new Vector2(-2, -12), new Vector2(200, 50), new Vector2(0, 1));
            gl.characterSpacing = 4;
            gl.rectTransform.localRotation = Quaternion.identity;

            // score (top-right)
            score = UI.Text(root, "Score", "0 000 000", UI.Display, 54, Color.white, TextAlignmentOptions.Right);
            UI.At(score.rectTransform, new Vector2(1, 1), new Vector2(-150, -28), new Vector2(560, 70), new Vector2(1, 1));
            UI.Glowing(score, ch.Color, 0.2f);
            acc = UI.Text(root, "Acc", "100.00%", UI.Label, 32, UI.TextDim, TextAlignmentOptions.Right);
            UI.At(acc.rectTransform, new Vector2(1, 1), new Vector2(-150, -96), new Vector2(400, 40), new Vector2(1, 1));
            acc.characterSpacing = 4;
            PauseButton = UI.Button(root, "II", ch.Color, () => OnPause?.Invoke(), 34, UI.DisplayBold);
            UI.At((RectTransform)PauseButton.transform, new Vector2(1, 1), new Vector2(-36, -32), new Vector2(84, 84), new Vector2(1, 1));

            // progress + title (top-centre)
            var pr = UI.Img(root, "Progress", Sprites.Round(3), new Color(1, 1, 1, 0.1f));
            UI.At(pr.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(640, 6), new Vector2(0.5f, 1));
            progressFill = UI.Img(pr.rectTransform, "Fill", Sprites.Round(3), ch.Color);
            progressFill.material = UI.Add2;
            UI.Stretch(progressFill.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            songTitle = UI.Text(root, "Song", ch.songTitle.ToUpperInvariant() + "  ·  " + game.DiffName.ToUpperInvariant(), UI.Label, 27, UI.TextDim, TextAlignmentOptions.Center);
            UI.At(songTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(800, 34), new Vector2(0.5f, 1));
            songTitle.characterSpacing = 8;
            lyric = UI.Text(root, "Lyric", "", UI.BodyLight, 30, Color.white.WithA(0.85f), TextAlignmentOptions.Center);
            UI.At(lyric.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(1000, 44), new Vector2(0.5f, 1));
            lyric.fontStyle = FontStyles.Italic;
            lyricGroup = UI.Group(lyric, 0);

            // judgement + combo
            comboRt = UI.Node("Combo", root);
            UI.At(comboRt, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(500, 160));
            combo = UI.Text(comboRt, "Num", "", UI.Display, 96, Color.white, TextAlignmentOptions.Center);
            UI.Stretch(combo.rectTransform, new Vector2(0, 0.3f), Vector2.one, Vector2.zero, Vector2.zero);
            UI.Glowing(combo, ch.Color, 0.35f);
            comboLabel = UI.Text(comboRt, "Label", "COMBO", UI.Label, 28, ch.Accent, TextAlignmentOptions.Center);
            UI.Stretch(comboLabel.rectTransform, Vector2.zero, new Vector2(1, 0.3f), Vector2.zero, Vector2.zero);
            comboLabel.characterSpacing = 16;
            judgeRt = UI.Node("Judge", root);
            UI.At(judgeRt, new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(600, 90));
            judge = UI.Text(judgeRt, "Text", "", UI.Display, 56, Color.white, TextAlignmentOptions.Center);
            UI.Fill(judge.rectTransform);
            UI.Glowing(judge, Color.white, 0.4f);
            surgeText = UI.Text(root, "Surge", "HEART SURGE", UI.Display, 34, UI.Gold, TextAlignmentOptions.Center);
            UI.At(surgeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 285), new Vector2(600, 50));
            surgeText.characterSpacing = 18;
            UI.Glowing(surgeText, UI.Gold, 0.4f);
            surgeText.alpha = 0;

            // lane hints for tutorial / first plays
            var hints = UI.Node("LaneHints", root);
            UI.Fill(hints);
            hintsGroup = UI.Group(hints, 0);
            string[] keys = { "D", "F", "J", "K" };
            for (int i = 0; i < 4; i++)
            {
                var t = UI.Text(hints, "Key" + i, keys[i], UI.DisplayBold, 40, Color.white, TextAlignmentOptions.Center);
                UI.At(t.rectTransform, new Vector2(0, 0), Vector2.zero, new Vector2(80, 60), new Vector2(0.5f, 0.5f));
                UI.Glowing(t, ch.Color, 0.4f);
                laneHints[i] = t;
            }
            hintText = UI.Text(hints, "Hint", "", UI.Body, 34, Color.white, TextAlignmentOptions.Center);
            UI.At(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1400, 60));

            game.Judged += OnJudged;
            game.ComboMilestone += OnMilestone;
            game.Beat += (i, down) => { ecgSpike = Mathf.Max(ecgSpike, down ? 0.7f : 0.45f); };
            StartCoroutine(GH.Tween(0.5f, k => group.alpha = k));
        }

        void OnDestroy()
        {
            if (game != null) { game.Judged -= OnJudged; game.ComboMilestone -= OnMilestone; }
        }

        void OnJudged(int lane, Judgment j, float delta, NoteType type)
        {
            string txt = j == Judgment.Perfect ? "PERFECT" : j == Judgment.Great ? "GREAT" : j == Judgment.Good ? "GOOD" : "MISS";
            judge.text = txt;
            var c = HighwayView.JudgeColor(j);
            judge.color = c;
            judge.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, c);
            judgeT = 0;
            if (j != Judgment.Miss) { comboPunch = 1; ecgSpike = 1; }
            if (lane >= 0)
            {
                var p = StageWorld.I.WorldToCanvas(game.LaneWorld(lane, 0) + new Vector3(0, 0.5f, 0), GameRoot.I.CanvasRect);
                judgeRt.anchorMin = judgeRt.anchorMax = new Vector2(0.5f, 0.5f);
                judgeRt.anchoredPosition = new Vector2(p.x * 0.35f, p.y + 150);
            }
            else
            {
                judgeRt.anchorMin = judgeRt.anchorMax = new Vector2(0.5f, 0.5f);
                var p = StageWorld.I.WorldToCanvas(game.LaneWorld(-1, 0), GameRoot.I.CanvasRect);
                judgeRt.anchoredPosition = new Vector2(0, p.y + 170);
            }
        }

        void OnMilestone(int n)
        {
            AudioDirector.I.Sfx("combo", 0.6f);
            StageWorld.I.ComboBurst();
        }

        public void ShowHint(string text) { if (hintText != null) hintText.text = text; }

        void Update()
        {
            if (game == null) return;
            float dt = Time.unscaledDeltaTime;
            // score / acc
            displayScore = Mathf.Lerp(displayScore, game.Score, 1 - Mathf.Exp(-14 * dt));
            score.text = GH.FormatScore(Mathf.RoundToInt(displayScore));
            acc.text = (game.Accuracy * 100).ToString("0.00") + "%";
            // gauge
            float g = game.Gauge;
            var am = gaugeFill.rectTransform.anchorMax; am.y = GH.Damp(am.y, g, 10, dt); gaugeFill.rectTransform.anchorMax = am;
            gaugeGlow.rectTransform.anchorMax = am;
            Color gc = g < 0.4f ? UI.Bad : g < RhythmGame.ClearLine ? UI.Gold : g >= 0.92f ? Color.Lerp(UI.Gold, Color.white, 0.3f) : UI.Good;
            gaugeFill.color = Color.Lerp(gaugeFill.color, gc, dt * 6);
            gaugeGlow.color = gaugeFill.color.WithA(0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 6));
            lowPulse = g < 0.35f && game.Playing ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7)) * (0.35f - g) * 1.6f : 0;
            lowWarn.color = lowWarn.color.WithA(GH.Damp(lowWarn.color.a, lowPulse * (Save.S.reduceFlash ? 0.4f : 1f), 10, dt));
            // progress
            var pm = progressFill.rectTransform.anchorMax; pm.x = game.Progress; progressFill.rectTransform.anchorMax = pm;
            // combo
            comboPunch = Mathf.Max(0, comboPunch - dt * 6);
            bool showCombo = game.Combo >= 5;
            combo.text = game.Combo.ToString();
            float ca = showCombo ? 1 : 0;
            combo.alpha = GH.Damp(combo.alpha, ca * 0.95f, 12, dt);
            comboLabel.alpha = combo.alpha * 0.9f;
            comboRt.localScale = Vector3.one * (1 + comboPunch * 0.12f);
            // judgement pop
            judgeT += dt;
            float jk = Mathf.Clamp01(judgeT / 0.5f);
            judge.alpha = judgeT < 0.5f ? 1 - Mathf.Pow(jk, 3) : 0;
            judgeRt.localScale = Vector3.one * (judgeT < 0.08f ? 1.25f - judgeT * 3 : 1f);
            // surge
            surgeText.alpha = GH.Damp(surgeText.alpha, game.Surge ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5) : 0, 6, dt);
            // ECG
            float bpmNow = game.Chart.bpm;
            bpmText.text = $"{bpmNow:0} <size=60%>BPM</size>";
            heartIcon.rectTransform.localScale = Vector3.one * (1 + ecgSpike * 0.3f);
            ecgAcc += dt;
            while (ecgAcc > 1f / 90f)
            {
                ecgAcc -= 1f / 90f;
                ecgSpike = Mathf.Max(0, ecgSpike - 0.06f);
                float spikeShape = ecgSpike > 0.85f ? 1f : ecgSpike > 0.7f ? -0.3f : ecgSpike > 0.55f ? 0.25f : 0;
                ecgBuf[ecgHead] = spikeShape + Random.Range(-0.02f, 0.02f);
                ecgHead = (ecgHead + 1) % ecgBuf.Length;
            }
            ecg.Points.Clear();
            for (int i = 0; i < ecgBuf.Length; i++)
            {
                float v = ecgBuf[(ecgHead + i) % ecgBuf.Length];
                ecg.Points.Add(new Vector2(i / (float)(ecgBuf.Length - 1), 0.35f + v * 0.55f));
            }
            ecg.SetDirty();
            // lyrics
            if (Save.S.lyrics && game.Chart.lyrics != null && game.Chart.lyrics.Length > 0)
            {
                int idx = -1;
                for (int i = 0; i < game.Chart.lyrics.Length; i++)
                    if (game.SongTime >= game.Chart.lyrics[i].t - 0.15f && game.SongTime < game.Chart.lyrics[i].e + 0.4f) idx = i;
                if (idx != lyricIdx) { lyricIdx = idx; if (idx >= 0) lyric.text = game.Chart.lyrics[idx].text; }
                lyricGroup.alpha = GH.Damp(lyricGroup.alpha, idx >= 0 ? 1 : 0, 8, dt);
            }
            // lane hints
            bool showHints = (tutorial || !Save.Cleared("tutorial")) && game.SongTime < 22f;
            hintsGroup.alpha = GH.Damp(hintsGroup.alpha, showHints ? 1 : 0, 4, dt);
            if (hintsGroup.alpha > 0.01f)
            {
                for (int i = 0; i < 4; i++)
                {
                    var p = StageWorld.I.WorldToCanvas(game.LaneWorld(i, -0.6f), GameRoot.I.CanvasRect);
                    laneHints[i].rectTransform.anchorMin = laneHints[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    laneHints[i].rectTransform.anchoredPosition = p + new Vector2(0, -40);
                }
                if (tutorial)
                {
                    float t = game.SongTime;
                    hintText.text = t < 6 ? "Tap when a signal touches the glowing line" : t < 12 ? "Hold long notes until they finish" : t < 22 ? "Wide golden pulse? Hit SPACE or any lane!" : "";
                }
                else hintText.text = "";
            }
        }

        public void SetDim(bool dim)
        {
            if (group != null) group.alpha = dim ? 0.12f : 1f;
        }

        public IEnumerator Countdown()
        {
            var t = UI.Text(root, "Count", "", UI.Display, 120, Color.white, TextAlignmentOptions.Center);
            UI.At(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(600, 200));
            UI.Glowing(t, ch.Color, 0.5f);
            foreach (var s in new[] { "READY", "GO!" })
            {
                t.text = s;
                AudioDirector.I.Sfx(s == "GO!" ? "go" : "ready", 0.7f);
                yield return GH.Tween(0.55f, k => { t.alpha = 1 - k * k; t.rectTransform.localScale = Vector3.one * (1.3f - 0.3f * GH.EaseOutCubic(k)); });
            }
            Destroy(t.gameObject);
        }

        public IEnumerator Hide()
        {
            yield return GH.Tween(0.4f, k => group.alpha = 1 - k);
            Destroy(gameObject);
        }
    }

    /// <summary>Pause menu.</summary>
    public class PauseMenu : GHScreen
    {
        public string Result;

        public static PauseMenu Make(Chapter ch)
        {
            var s = Create<PauseMenu>(GameRoot.I.ModalLayer, "Pause");
            var dim = UI.Img(s.Root, "Dim", Sprites.White, new Color(0.01f, 0, 0.01f, 0.84f), true);
            UI.Fill(dim.rectTransform);
            var t = UI.Text(s.Root, "Title", "PAUSED", UI.Display, 90, Color.white, TextAlignmentOptions.Center);
            UI.At(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(800, 120));
            UI.Glowing(t, ch.Color, 0.4f);
            string[] items = { "RESUME", "RESTART", "SETTINGS", "QUIT SONG" };
            string[] keys = { "resume", "restart", "settings", "quit" };
            GHButton first = null;
            for (int i = 0; i < items.Length; i++)
            {
                string k = keys[i];
                var b = UI.Button(s.Root, items[i], ch.Color, () => s.Result = k, 34);
                UI.At((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 100 - i * 104), new Vector2(460, 84));
                first ??= b;
            }
            s.StartCoroutine(s.FadeIn(0.2f));
            Focus(first);
            return s;
        }

        void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Result = "resume"; }
    }

    /// <summary>Results: grade, score count-up, judgement breakdown, stability verdict.</summary>
    public class ResultsScreen : GHScreen
    {
        string choice;

        public static ResultsScreen Make() => Create<ResultsScreen>(GameRoot.I.ScreenLayer, "Results");

        public IEnumerator Show(Chapter ch, SongResult r, bool storyMode, System.Action<string> done)
        {
            var dim = UI.Img(Root, "Dim", Sprites.GradientH, new Color(0, 0, 0, 0.88f), true);
            UI.Fill(dim.rectTransform);
            var tint = UI.Glow(Root, "Tint", Sprites.Glow, ch.Color.WithA(0.25f), UI.Add1);
            UI.At(tint.rectTransform, new Vector2(0.3f, 0.5f), Vector2.zero, new Vector2(1400, 1200));
            yield return FadeIn(0.4f);

            // grade
            var gradeRt = UI.Node("Grade", Root);
            UI.At(gradeRt, new Vector2(0.3f, 0.55f), Vector2.zero, new Vector2(700, 420));
            var ring = UI.Glow(gradeRt, "Ring", Sprites.SoftRing, ch.Color.WithA(0.8f), UI.Add2);
            UI.At(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 520));
            var grade = UI.Text(gradeRt, "Letter", r.grade, UI.Display, 260, Color.white, TextAlignmentOptions.Center);
            UI.Fill(grade.rectTransform);
            Color gcol = r.grade.StartsWith("S") ? UI.Gold : r.grade == "A" ? UI.Teal : r.grade == "B" ? UI.Good : UI.Coral;
            UI.Glowing(grade, gcol, 0.55f);
            grade.alpha = 0;
            var verdict = UI.Text(Root, "Verdict", r.cleared ? "VITALS STABLE" : "VITALS UNSTABLE", UI.Display, 44, r.cleared ? UI.Good : UI.Coral, TextAlignmentOptions.Center);
            UI.At(verdict.rectTransform, new Vector2(0.3f, 0.5f), new Vector2(0, -250), new Vector2(800, 60));
            verdict.characterSpacing = 10;
            UI.Glowing(verdict, r.cleared ? UI.Good : UI.Coral, 0.3f);
            verdict.alpha = 0;
            var badge = UI.Text(Root, "Badge", r.ap ? "ALL PERFECT" : r.fc ? "FULL COMBO" : "", UI.Label, 36, r.ap ? UI.Gold : UI.Teal, TextAlignmentOptions.Center);
            UI.At(badge.rectTransform, new Vector2(0.3f, 0.5f), new Vector2(0, -310), new Vector2(600, 50));
            badge.characterSpacing = 14;
            badge.alpha = 0;

            // stats panel
            var panel = UI.Panel(Root, "Stats", ch.Color);
            UI.At(panel, new Vector2(1, 0.5f), new Vector2(-100, 30), new Vector2(760, 760), new Vector2(1, 0.5f));
            var song = UI.Text(panel, "Song", ch.songTitle.ToUpperInvariant(), UI.Display, 46, Color.white, TextAlignmentOptions.Left);
            UI.At(song.rectTransform, new Vector2(0, 1), new Vector2(50, -40), new Vector2(660, 60), new Vector2(0, 1));
            song.enableAutoSizing = true; song.fontSizeMin = 28; song.fontSizeMax = 46;
            var diffT = UI.Text(panel, "Diff", r.diff.ToUpperInvariant() + (r.isNewBest ? "   ·   <color=#FFD36E>NEW BEST!</color>" : ""), UI.Label, 28, ch.Accent, TextAlignmentOptions.Left);
            UI.At(diffT.rectTransform, new Vector2(0, 1), new Vector2(50, -104), new Vector2(660, 40), new Vector2(0, 1));
            diffT.characterSpacing = 8;
            var scoreT = UI.Text(panel, "Score", "0 000 000", UI.Display, 76, Color.white, TextAlignmentOptions.Left);
            UI.At(scoreT.rectTransform, new Vector2(0, 1), new Vector2(50, -160), new Vector2(660, 90), new Vector2(0, 1));
            UI.Glowing(scoreT, ch.Color, 0.25f);
            var accT = UI.Text(panel, "Acc", "", UI.Body, 32, UI.TextDim, TextAlignmentOptions.Left);
            UI.At(accT.rectTransform, new Vector2(0, 1), new Vector2(50, -256), new Vector2(660, 44), new Vector2(0, 1));
            accT.text = $"Accuracy <b><color=#FFFFFF>{r.acc * 100:0.00}%</color></b>   ·   Max combo <b><color=#FFFFFF>{r.maxCombo}</color></b>   ·   Stability <b><color=#FFFFFF>{r.gauge * 100:0}%</color></b>";
            accT.enableAutoSizing = true; accT.fontSizeMin = 20; accT.fontSizeMax = 30;
            string[] names = { "PERFECT", "GREAT", "GOOD", "MISS" };
            int[] vals = { r.perfect, r.great, r.good, r.miss };
            int total = Mathf.Max(1, r.units);
            for (int i = 0; i < 4; i++)
            {
                var c = HighwayView.JudgeColor((Judgment)i);
                var n = UI.Text(panel, "N" + i, names[i], UI.Label, 30, c, TextAlignmentOptions.Left);
                UI.At(n.rectTransform, new Vector2(0, 1), new Vector2(50, -330 - i * 66), new Vector2(200, 44), new Vector2(0, 1));
                n.characterSpacing = 6;
                var bar = UI.Img(panel, "Bar" + i, Sprites.Round(5), new Color(1, 1, 1, 0.07f));
                UI.At(bar.rectTransform, new Vector2(0, 1), new Vector2(240, -344 - i * 66), new Vector2(330, 14), new Vector2(0, 1));
                var fill = UI.Img(bar.rectTransform, "Fill", Sprites.Round(5), c);
                fill.material = UI.Add2;
                UI.Stretch(fill.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
                var v = UI.Text(panel, "V" + i, vals[i].ToString(), UI.DisplayBold, 32, Color.white, TextAlignmentOptions.Right);
                UI.At(v.rectTransform, new Vector2(1, 1), new Vector2(-50, -330 - i * 66), new Vector2(120, 44), new Vector2(1, 1));
                float target = vals[i] / (float)total;
                StartCoroutine(GH.Tween(1.1f, k => fill.rectTransform.anchorMax = new Vector2(target * k, 1)));
            }
            var btnRetry = UI.Button(panel, "RETRY", UI.TextDim, () => choice = "retry", 32);
            UI.At((RectTransform)btnRetry.transform, new Vector2(0, 0), new Vector2(50, 46), new Vector2(260, 86), new Vector2(0, 0));
            var btnNext = UI.Button(panel, storyMode ? "CONTINUE  ▶" : "DONE  ▶", ch.Color, () => choice = "continue", 34, UI.DisplayBold);
            UI.At((RectTransform)btnNext.transform, new Vector2(1, 0), new Vector2(-50, 46), new Vector2(360, 86), new Vector2(1, 0));

            // captain reaction
            var face = UI.Img(Root, "Captain", Content.Portrait(r.grade.StartsWith("S") ? "excited" : r.cleared ? "happy" : "worried"), Color.white);
            UI.At(face.rectTransform, new Vector2(0, 0), new Vector2(-30, -60), new Vector2(380, 570), new Vector2(0, 0));
            face.preserveAspect = true;
            var fg = UI.Group(face, 0);

            // animate
            AudioDirector.I.Sfx("results_roll", 0.6f);
            yield return GH.Tween(1.2f, k => scoreT.text = GH.FormatScore(Mathf.RoundToInt(r.score * k)), GH.EaseOutCubic);
            AudioDirector.I.Sfx(r.grade.StartsWith("S") ? "grade_s" : "grade", 0.9f);
            GameRoot.I.Overlay.Flash(gcol, 0.35f);
            yield return GH.Tween(0.5f, k => { grade.alpha = k; gradeRt.localScale = Vector3.one * (1.8f - 0.8f * GH.EaseOutBack(k)); });
            StartCoroutine(GH.Tween(0.4f, k => { verdict.alpha = k; badge.alpha = k; fg.alpha = k; }));
            var bark = Content.Story.BarkLine(r.fc ? "bark_fullcombo" : r.cleared ? "bark_clear" : "bark_fail");
            if (bark != null) AudioDirector.I.PlayVoice(bark.voice);
            Focus(btnNext);
            choice = null;
            float t = 0;
            while (choice == null)
            {
                t += Time.unscaledDeltaTime;
                ring.rectTransform.localRotation = Quaternion.Euler(0, 0, t * 20);
                ring.rectTransform.localScale = Vector3.one * (1 + Mathf.Max(0, EcgLine.Pqrst(t)) * 0.08f);
                if (GH.QA && GH.Flag("auto") && t > 2.5f) choice = "continue";
                yield return null;
            }
            done(choice);
        }
    }
}
