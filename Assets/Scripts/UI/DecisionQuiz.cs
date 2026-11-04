using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHeart
{
    /// <summary>"How should the heart respond?" — vitals readout and three rhythm cards with live ECG traces.</summary>
    public class DecisionScreen : GHScreen
    {
        string choice;
        readonly List<(EcgLine line, float rate, GHButton btn)> cards = new List<(EcgLine, float, GHButton)>();
        Image portrait;
        TextMeshProUGUI prompt;
        float phase;

        public static DecisionScreen Make(Chapter ch, Texture2D bgTex)
        {
            var s = Create<DecisionScreen>(GameRoot.I.ScreenLayer, "Decision");
            s.Build(ch, bgTex);
            return s;
        }

        void Build(Chapter ch, Texture2D bgTex)
        {
            var d = ch.decision;
            var black = UI.Img(Root, "Black", Sprites.White, Color.black, true);
            UI.Fill(black.rectTransform);
            var bg = UI.Raw(Root, "Bg", bgTex, new Color(0.45f, 0.42f, 0.45f, 1));
            UI.Fill(bg.rectTransform);
            var kb = bg.gameObject.AddComponent<KenBurns>(); kb.Target = bg; kb.Restart("drift", 30);
            if (bgTex != null) kb.ImageAspect = bgTex.width / (float)bgTex.height;
            var tint = UI.Img(Root, "Tint", Sprites.GradientV, ch.Color.Mul(0.25f).WithA(0.85f));
            UI.Fill(tint.rectTransform);
            var vig = UI.Img(Root, "Vig", Sprites.Vignette, new Color(0, 0, 0, 0.85f));
            UI.Fill(vig.rectTransform);

            var head = UI.Text(Root, "Header", "HEART RHYTHM CONTROL", UI.Label, 30, ch.Accent, TextAlignmentOptions.Center);
            UI.At(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(1200, 44));
            head.characterSpacing = 16;

            // prompt with captain
            var promptPanel = UI.Panel(Root, "PromptPanel", ch.Color);
            UI.At(promptPanel, new Vector2(0.5f, 1), new Vector2(0, -108), new Vector2(1750, 150), new Vector2(0.5f, 1));
            prompt = UI.Text(promptPanel, "Prompt", d.prompt, UI.Body, 42, Color.white, TextAlignmentOptions.MidlineLeft);
            UI.Fill(prompt.rectTransform, 230, 10, 50, 10);
            prompt.enableAutoSizing = true; prompt.fontSizeMin = 30; prompt.fontSizeMax = 44;
            var face = UI.Node("Face", promptPanel);
            UI.At(face, new Vector2(0, 0.5f), new Vector2(20, -10), new Vector2(200, 230), new Vector2(0, 0.5f));
            var mask = face.gameObject.AddComponent<RectMask2D>();
            portrait = UI.Img(face, "Captain", Content.Portrait("thinking"), Color.white);
            UI.At(portrait.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 10), new Vector2(240, 360), new Vector2(0.5f, 1));
            portrait.preserveAspect = true;

            // vitals
            var vit = UI.Panel(Root, "Vitals", ch.Color);
            UI.At(vit, new Vector2(0.5f, 0.5f), new Vector2(-875, -80), new Vector2(430, 120 + d.vitals.Length * 96), new Vector2(0, 0.5f));
            var vt = UI.Text(vit, "Title", "BODY STATUS", UI.Label, 28, ch.Accent, TextAlignmentOptions.Left);
            UI.At(vt.rectTransform, new Vector2(0, 1), new Vector2(30, -26), new Vector2(380, 40), new Vector2(0, 1));
            vt.characterSpacing = 10;
            var live = UI.Glow(vit, "Live", Sprites.Circle, UI.Bad, UI.Add2);
            UI.At(live.rectTransform, new Vector2(1, 1), new Vector2(-34, -44), new Vector2(16, 16));
            live.gameObject.AddComponent<Blink>();
            for (int i = 0; i < d.vitals.Length; i++)
            {
                var v = d.vitals[i];
                var row = UI.Node("Row" + i, vit);
                UI.At(row, new Vector2(0, 1), new Vector2(30, -86 - i * 96), new Vector2(370, 84), new Vector2(0, 1));
                var sep = UI.Img(row, "Sep", Sprites.White, new Color(1, 1, 1, 0.08f));
                UI.Stretch(sep.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero);
                var lab = UI.Text(row, "Label", v.label.ToUpperInvariant(), UI.Label, 22, UI.TextDim, TextAlignmentOptions.TopLeft);
                UI.Stretch(lab.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-60, -10));
                lab.characterSpacing = 4;
                var val = UI.Text(row, "Value", v.value, UI.DisplayBold, 30, Color.white, TextAlignmentOptions.BottomLeft);
                UI.Stretch(val.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 8), new Vector2(-60, 0));
                Color tc = v.trend == "up" ? UI.Gold : v.trend == "down" ? UI.Teal : v.trend == "warn" ? UI.Bad : UI.Good;
                string icon = v.trend == "up" ? "▲" : v.trend == "down" ? "▼" : v.trend == "warn" ? "!" : "✓";
                var ic = UI.Text(row, "Trend", icon, UI.BodyBold, 40, tc, TextAlignmentOptions.Center);
                UI.At(ic.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(50, 60));
            }

            // option cards
            var row2 = UI.Node("Options", Root);
            UI.At(row2, new Vector2(0.5f, 0.5f), new Vector2(-405, -80), new Vector2(1280, 450), new Vector2(0, 0.5f));
            var defs = new[] { ("faster", "BEAT FASTER", "Pump harder · ~120 BPM", 2.0f), ("same", "KEEP THE PACE", "Steady · ~75 BPM", 1.25f), ("slower", "BEAT SLOWER", "Rest easy · ~55 BPM", 0.9f) };
            for (int i = 0; i < defs.Length; i++)
            {
                var (key, title, sub, rate) = defs[i];
                var b = UI.Button(row2, title, ch.Color, () => choice = key, 40, UI.DisplayBold);
                var rt = (RectTransform)b.transform;
                UI.At(rt, new Vector2(0, 0), new Vector2(i * 435, 0), new Vector2(410, 450), new Vector2(0, 0));
                b.LabelText.rectTransform.offsetMin = new Vector2(10, 92);
                b.LabelText.rectTransform.offsetMax = new Vector2(-10, -(450 - 150));
                b.LabelText.fontSize = 34;
                b.LabelText.enableAutoSizing = true; b.LabelText.fontSizeMin = 24; b.LabelText.fontSizeMax = 34;
                b.LabelText.textWrappingMode = TextWrappingModes.NoWrap;
                b.LabelText.characterSpacing = 2;
                var s2 = UI.Text(b.Content, "Sub", sub, UI.Label, 26, UI.TextDim, TextAlignmentOptions.Center);
                UI.Stretch(s2.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 40), new Vector2(-10, 84));
                var ecgRt = UI.Node("ECG", b.Content);
                UI.Stretch(ecgRt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(26, 176), new Vector2(-26, 392));
                var screen = UI.Img(b.Content, "Screen", Sprites.Round(12), new Color(0, 0, 0, 0.35f));
                UI.Stretch(screen.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 168), new Vector2(-18, 400));
                screen.transform.SetSiblingIndex(0);
                var grid = UI.Img(screen.rectTransform, "Grid", Sprites.Outline(12, 1.5f), ch.Color.WithA(0.18f));
                UI.Fill(grid.rectTransform);
                var ecg = ecgRt.gameObject.AddComponent<EcgLine>();
                ecg.material = UI.Add2;
                ecg.color = ch.Color;
                ecg.Thickness = 5;
                var key2 = UI.Text(b.Content, "Key", (i + 1).ToString(), UI.Label, 22, UI.TextDim.WithA(0.8f), TextAlignmentOptions.Center);
                UI.At(key2.rectTransform, new Vector2(1, 1), new Vector2(-34, -30), new Vector2(40, 40));
                cards.Add((ecg, rate, b));
            }
            var hint = UI.Text(Root, "Hint", "Pick the rhythm the body needs   ·   click a card or press 1  2  3", UI.Label, 26, UI.TextDim, TextAlignmentOptions.Center);
            UI.At(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -350), new Vector2(1400, 40));
            hint.characterSpacing = 4;
        }

        void Update()
        {
            phase += Time.unscaledDeltaTime;
            foreach (var (line, rate, _) in cards)
            {
                line.Points.Clear();
                int n = 120;
                for (int i = 0; i < n; i++)
                {
                    float x = i / (float)(n - 1);
                    line.Points.Add(new Vector2(x, 0.3f + 0.62f * EcgLine.Pqrst(x * rate * 1.6f - phase * rate * 0.8f)));
                }
                line.AlphaAlong = u => Mathf.Clamp01(u * 5) * Mathf.Clamp01((1 - u) * 5);
                line.SetDirty();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) cards[0].btn.Fire();
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) cards[1].btn.Fire();
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) cards[2].btn.Fire();
        }

        public IEnumerator Ask(System.Action<string> result, bool speak)
        {
            choice = null;
            yield return FadeIn(0.45f);
            Focus(null);
            if (speak)
            {
                var bark = Content.Story.BarkLine("bark_choose");
                if (bark != null) AudioDirector.I.PlayVoice(bark.voice);
            }
            string qa = GH.Query("choose");
            float t = 0;
            while (choice == null)
            {
                t += Time.unscaledDeltaTime;
                if (qa != null && t > 1.2f) choice = qa;
                yield return null;
            }
            GH.Log("decision", choice);
            foreach (var c in cards) c.btn.interactable = false;
            result(choice);
        }

        public IEnumerator Respond(StoryLine line, bool correct, Chapter ch)
        {
            portrait.sprite = Content.Portrait(line.mood);
            prompt.text = line.text;
            prompt.color = correct ? Color.white : UI.Coral;
            AudioDirector.I.PlayVoice(line.voice);
            GameRoot.I.Overlay.Flash(correct ? UI.Good : UI.Bad, 0.25f);
            float t = 0;
            yield return null;
            while (AudioDirector.I.VoicePlaying && t < 12f && !(GH.QA && GH.Flag("fast") && t > 0.5f)) { t += Time.unscaledDeltaTime; yield return null; }
            yield return GH.Wait(GH.QA && GH.Flag("fast") ? 0.1f : 0.5f);
        }
    }

    public class Blink : MonoBehaviour
    {
        Graphic g;
        void Awake() { g = GetComponent<Graphic>(); }
        void Update() { if (g != null) g.color = g.color.WithA(0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f))); }
    }

    /// <summary>Pulse Check: multiple-choice questions with instant explanations.</summary>
    public class QuizScreen : GHScreen
    {
        int picked = -1;
        bool next;

        public static QuizScreen Make() => Create<QuizScreen>(GameRoot.I.ScreenLayer, "Quiz");

        public IEnumerator Run(Chapter ch, Texture2D bg)
        {
            var black = UI.Img(Root, "Black", Sprites.White, Color.black, true);
            UI.Fill(black.rectTransform);
            var img = UI.Raw(Root, "Bg", bg, new Color(0.35f, 0.33f, 0.36f));
            UI.Fill(img.rectTransform);
            var kb = img.gameObject.AddComponent<KenBurns>(); kb.Target = img; kb.Restart("drift", 40);
            if (bg != null) kb.ImageAspect = bg.width / (float)bg.height;
            var vig = UI.Img(Root, "Vig", Sprites.Vignette, new Color(0, 0, 0, 0.9f));
            UI.Fill(vig.rectTransform);
            var heart = UI.Glow(Root, "Heart", Sprites.Heart, ch.Color, UI.Add2);
            UI.At(heart.rectTransform, new Vector2(0.5f, 1), new Vector2(-250, -84), new Vector2(54, 54));
            heart.gameObject.AddComponent<HeartBeatPulse>();
            var title = UI.Text(Root, "Title", "PULSE CHECK", UI.Display, 54, Color.white, TextAlignmentOptions.Center);
            UI.At(title.rectTransform, new Vector2(0.5f, 1), new Vector2(30, -84), new Vector2(560, 70));
            UI.Glowing(title, ch.Color, 0.3f);
            var counter = UI.Text(Root, "Counter", "", UI.Label, 28, UI.TextDim, TextAlignmentOptions.Center);
            UI.At(counter.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -158), new Vector2(400, 40));
            counter.characterSpacing = 8;
            yield return FadeIn(0.4f);
            var bark = Content.Story.BarkLine("bark_quiz");
            if (bark != null) AudioDirector.I.PlayVoice(bark.voice);

            int correct = 0;
            for (int qi = 0; qi < ch.quiz.Length; qi++)
            {
                var q = ch.quiz[qi];
                counter.text = $"QUESTION {qi + 1} / {ch.quiz.Length}";
                var card = UI.Node("Q" + qi, Root);
                UI.Fill(card);
                var cg = UI.Group(card, 0);
                var qp = UI.Panel(card, "QPanel", ch.Color);
                UI.At(qp, new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(1400, 170), new Vector2(0.5f, 1));
                var qt = UI.Text(qp, "Text", q.q, UI.Body, 46, Color.white, TextAlignmentOptions.Center);
                UI.Fill(qt.rectTransform, 40, 10, 40, 10);
                // shuffled options
                var order = new List<int>();
                for (int i = 0; i < q.options.Length; i++) order.Add(i);
                for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
                var buttons = new GHButton[q.options.Length];
                picked = -1;
                for (int i = 0; i < order.Count; i++)
                {
                    int oi = order[i];
                    var b = UI.Button(card, q.options[oi], ch.Color, () => picked = oi, 36, UI.Body);
                    b.LabelText.characterSpacing = 0;
                    var rt = (RectTransform)b.transform;
                    int col = i % 2, row = i / 2;
                    UI.At(rt, new Vector2(0.5f, 1), new Vector2(col == 0 ? -350 : 350, -420 - row * 150), new Vector2(660, 120), new Vector2(0.5f, 0.5f));
                    var letter = UI.Text(b.Content, "Letter", ((char)('A' + i)).ToString(), UI.Display, 34, ch.Accent, TextAlignmentOptions.Center);
                    UI.At(letter.rectTransform, new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(60, 60));
                    b.LabelText.rectTransform.offsetMin = new Vector2(104, 4);
                    b.LabelText.rectTransform.offsetMax = new Vector2(-28, -4);
                    b.LabelText.alignment = TextAlignmentOptions.MidlineLeft;
                    b.LabelText.enableAutoSizing = true; b.LabelText.fontSizeMin = 22; b.LabelText.fontSizeMax = 34;
                    buttons[oi] = b;
                }
                yield return GH.Tween(0.35f, k => cg.alpha = k);
                Focus(null);
                string qa = GH.Query("quiz");
                float t = 0;
                while (picked < 0)
                {
                    t += Time.unscaledDeltaTime;
                    if (qa != null && t > 1f) picked = qa == "wrong" ? (q.answer + 1) % q.options.Length : q.answer;
                    for (int i = 0; i < order.Count && i < 4; i++)
                        if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.A + i)) picked = order[i];
                    yield return null;
                }
                bool ok = picked == q.answer;
                if (ok) correct++;
                GH.Log("quiz", $"{qi} picked={picked} ok={ok}");
                foreach (var b in buttons) b.interactable = false;
                var right = buttons[q.answer];
                right.SetAccent(UI.Good);
                right.Highlighted = true;
                right.Bg.color = UI.Good.WithA(0.35f);
                if (!ok)
                {
                    buttons[picked].SetAccent(UI.Bad);
                    StartCoroutine(Shake((RectTransform)buttons[picked].transform));
                }
                AudioDirector.I.Sfx(ok ? "quiz_right" : "quiz_wrong", 0.9f);
                var line = Content.Story.BarkLine(ok ? "bark_correct" : "bark_wrong");
                if (line != null) AudioDirector.I.PlayVoice(line.voice);
                GameRoot.I.Overlay.Flash(ok ? UI.Good : UI.Bad, 0.2f);
                // explanation
                var ep = UI.Panel(card, "Explain", ok ? UI.Good : UI.Coral);
                UI.At(ep, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1400, 130), new Vector2(0.5f, 0));
                var et = UI.Text(ep, "Text", (ok ? "<color=#6BFF9E>CORRECT!</color>  " : "<color=#FF8A80>NOT QUITE.</color>  ") + q.explain, UI.Body, 34, Color.white, TextAlignmentOptions.MidlineLeft);
                UI.Fill(et.rectTransform, 40, 6, 300, 6);
                next = false;
                var nb = UI.Button(ep, "NEXT ▶", ch.Color, () => next = true, 30);
                UI.At((RectTransform)nb.transform, new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(220, 74), new Vector2(1, 0.5f));
                var eg = UI.Group(ep, 0);
                yield return GH.Tween(0.3f, k => eg.alpha = k);
                Focus(nb);
                t = 0;
                while (!next) { t += Time.unscaledDeltaTime; if (GH.QA && GH.Query("quiz") != null && t > 1.2f) next = true; yield return null; }
                yield return GH.Tween(0.25f, k => cg.alpha = 1 - k);
                Destroy(card.gameObject);
            }
            counter.text = $"{correct} / {ch.quiz.Length} CORRECT";
            yield return GH.Wait(0.6f);
        }

        IEnumerator Shake(RectTransform rt)
        {
            var p = rt.anchoredPosition;
            yield return GH.Tween(0.4f, k => rt.anchoredPosition = p + new Vector2(Mathf.Sin(k * 40) * 14 * (1 - k), 0), x => x);
            rt.anchoredPosition = p;
        }
    }

    public class HeartBeatPulse : MonoBehaviour
    {
        public float Rate = 1.1f;
        void Update()
        {
            float b = Mathf.Max(0, EcgLine.Pqrst(Time.unscaledTime * Rate));
            transform.localScale = Vector3.one * (1 + b * 0.22f);
        }
    }
}
