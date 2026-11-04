using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHeart
{
    public class TitleScreen : GHScreen
    {
        string choice;
        RectTransform menu, logo;
        CanvasGroup menuGroup, pressGroup;
        EcgLine ecg, ecgGlow;
        float t;
        bool pressed;

        public static TitleScreen Make()
        {
            var s = Create<TitleScreen>(GameRoot.I.ScreenLayer, "Title");
            s.Build();
            return s;
        }

        void Build()
        {
            var black = UI.Img(Root, "Black", Sprites.White, Color.black, true);
            UI.Fill(black.rectTransform);
            var art = Content.Art("key_art");
            var bg = UI.Raw(Root, "KeyArt", art, Color.white);
            UI.Fill(bg.rectTransform);
            var kb = bg.gameObject.AddComponent<KenBurns>(); kb.Target = bg; kb.Restart("push", 60);
            if (art != null) kb.ImageAspect = art.width / (float)art.height;
            UIParticles.Create(Root, "sparks", 0.6f);
            var shade = UI.Img(Root, "Shade", Sprites.GradientV, new Color(0, 0, 0, 0.9f));
            UI.Stretch(shade.rectTransform, Vector2.zero, new Vector2(1, 0.62f), Vector2.zero, Vector2.zero);
            shade.rectTransform.localScale = new Vector3(1, -1, 1);
            var vig = UI.Img(Root, "Vig", Sprites.Vignette, new Color(0, 0, 0, 0.7f));
            UI.Fill(vig.rectTransform);

            logo = UI.Node("Logo", Root);
            UI.At(logo, new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(1500, 300));
            var halo = UI.Glow(logo, "Halo", Sprites.Glow, UI.Crimson.WithA(0.35f), UI.Add2);
            UI.At(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1500, 420));
            var glowRt = UI.Node("ECGGlow", logo);
            UI.At(glowRt, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1700, 220));
            ecgGlow = glowRt.gameObject.AddComponent<EcgLine>();
            ecgGlow.material = UI.Add1;
            ecgGlow.color = UI.Crimson.WithA(0.35f);
            ecgGlow.Thickness = 22;
            var ecgRt = UI.Node("ECG", logo);
            UI.At(ecgRt, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1700, 220));
            ecg = ecgRt.gameObject.AddComponent<EcgLine>();
            ecg.material = UI.Add2;
            ecg.color = UI.Crimson;
            ecg.Thickness = 5;
            var title = UI.Text(logo, "Title", "GAME HEART", UI.Display, 150, Color.white, TextAlignmentOptions.Center);
            UI.At(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1500, 190));
            title.characterSpacing = 10;
            UI.Glowing(title, UI.Crimson, 0.5f);
            var sub = UI.Text(logo, "Sub", "RHYTHM  OF  LIFE", UI.Label, 50, UI.Coral, TextAlignmentOptions.Center);
            UI.At(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(1200, 64));
            sub.characterSpacing = 30;
            UI.Glowing(sub, UI.Crimson, 0.3f);

            var press = UI.Text(Root, "Press", "PRESS ANY KEY  ·  TAP TO BEGIN", UI.Label, 34, UI.Fg, TextAlignmentOptions.Center);
            UI.At(press.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(1200, 50));
            press.characterSpacing = 12;
            pressGroup = UI.Group(press, 1);

            menu = UI.Node("Menu", Root);
            UI.At(menu, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(1500, 110));
            menuGroup = UI.Group(menu, 0);
            menuGroup.blocksRaycasts = false;
            var items = new List<(string, string)>();
            bool hasProgress = Save.P.prologueSeen || Save.P.cleared.Count > 0;
            items.Add((hasProgress ? "CONTINUE" : "START STORY", "story"));
            if (hasProgress) items.Add(("NEW STORY", "new"));
            items.Add(("SETTINGS", "settings"));
            items.Add(("CREDITS", "credits"));
            float w = 330, gap = 24;
            float x0 = -(items.Count * w + (items.Count - 1) * gap) / 2 + w / 2;
            for (int i = 0; i < items.Count; i++)
            {
                var (label, key) = items[i];
                var b = UI.Button(menu, label, i == 0 ? UI.Crimson : UI.Coral, () => choice = key, 32);
                UI.At((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (w + gap), 0), new Vector2(w, 86));
            }
            var foot = UI.Text(Root, "Foot", "A SPARC body-science adventure  ·  remastered edition", UI.BodyLight, 22, UI.TextDim.WithA(0.7f), TextAlignmentOptions.Center);
            UI.At(foot.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1400, 34));
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            ecg.Points.Clear();
            int n = 200;
            float head = (t * 0.45f) % 1.4f;
            for (int i = 0; i < n; i++)
            {
                float x = i / (float)(n - 1);
                ecg.Points.Add(new Vector2(x, 0.42f + 0.5f * EcgLine.Pqrst(x * 3f + 0.1f)));
            }
            ecg.AlphaAlong = u => { float d = head - u; float edge = Mathf.Clamp01(u * 8f) * Mathf.Clamp01((1 - u) * 8f); return edge * (d < 0 ? 0.22f : Mathf.Clamp01(1 - d * 1.4f) * 0.85f + 0.22f); };
            ecg.SetDirty();
            ecgGlow.Points.Clear();
            ecgGlow.Points.AddRange(ecg.Points);
            ecgGlow.AlphaAlong = u => { float d = head - u; float edge = Mathf.Clamp01(u * 8f) * Mathf.Clamp01((1 - u) * 8f); return edge * (d < 0 ? 0f : Mathf.Clamp01(1 - d * 2.5f)); };
            ecgGlow.SetDirty();
            float beat = Mathf.Max(0, EcgLine.Pqrst(t * 1.1f));
            logo.localScale = Vector3.one * (1 + beat * 0.02f);
            if (!pressed) pressGroup.alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 2.2f));
        }

        public IEnumerator Run(System.Action<string> done, bool skipPress = false)
        {
            AudioDirector.I.PlayBgm("title_theme", 2f);
            yield return FadeIn(skipPress ? 0.4f : 1.2f);
            choice = null;
            if (!(GH.QA && GH.Query("title") != null) && !skipPress)
            {
                while (!(Input.anyKeyDown || Input.touchCount > 0)) yield return null;
            }
            pressed = true;
            AudioDirector.I.Sfx("start", 0.9f);
            GameRoot.I.Overlay.Flash(UI.Crimson, 0.3f);
            StartCoroutine(GH.Tween(0.4f, k => pressGroup.alpha = 1 - k));
            yield return GH.Tween(0.5f, k => { menuGroup.alpha = k; logo.anchoredPosition = new Vector2(0, -70 + 150 * GH.EaseOutCubic(k)); });
            menuGroup.blocksRaycasts = true;
            Focus(menu.GetComponentInChildren<GHButton>());
            float w = 0;
            while (choice == null)
            {
                w += Time.unscaledDeltaTime;
                if (GH.QA && GH.Query("title") != null && w > 1f) choice = GH.Query("title");
                yield return null;
            }
            done(choice);
        }
    }

    /// <summary>Heartbeat Control Center: chapter map with unlocks, grades and story/free-play entry.</summary>
    public class HubScreen : GHScreen
    {
        string chapterId, mode;
        RectTransform detail;
        TextMeshProUGUI dTitle, dSong, dBest, bubble;
        GHButton playStory, playSong;
        Chapter selected;
        readonly List<(GHButton b, Chapter c)> cards = new List<(GHButton, Chapter)>();

        public static HubScreen Make()
        {
            var s = Create<HubScreen>(GameRoot.I.ScreenLayer, "Hub");
            s.Build();
            return s;
        }

        public static bool Unlocked(Chapter c)
        {
            var all = Content.Story.chapters;
            int i = System.Array.IndexOf(all, c);
            if (i <= 0) return true;
            if (c.id == "finale") { for (int k = 0; k < i; k++) if (!Save.Cleared(all[k].id)) return false; return true; }
            return Save.Cleared(all[i - 1].id);
        }

        void Build()
        {
            var black = UI.Img(Root, "Black", Sprites.White, Color.black, true);
            UI.Fill(black.rectTransform);
            var art = Content.Art("hub_bodymap");
            var bg = UI.Raw(Root, "Bg", art, new Color(0.7f, 0.7f, 0.75f));
            UI.Fill(bg.rectTransform);
            var kb = bg.gameObject.AddComponent<KenBurns>(); kb.Target = bg; kb.Restart("drift", 40);
            if (art != null) kb.ImageAspect = art.width / (float)art.height;
            UIParticles.Create(Root, "sparks", 0.5f);
            var vig = UI.Img(Root, "Vig", Sprites.Vignette, new Color(0, 0, 0, 0.85f));
            UI.Fill(vig.rectTransform);
            var shade = UI.Img(Root, "Shade", Sprites.GradientV, new Color(0, 0, 0, 0.8f));
            UI.Stretch(shade.rectTransform, Vector2.zero, new Vector2(1, 0.55f), Vector2.zero, Vector2.zero);
            shade.rectTransform.localScale = new Vector3(1, -1, 1);

            var h = UI.Text(Root, "Header", "HEARTBEAT CONTROL CENTER", UI.Display, 56, Color.white, TextAlignmentOptions.Left);
            UI.At(h.rectTransform, new Vector2(0, 1), new Vector2(80, -50), new Vector2(1300, 70), new Vector2(0, 1));
            UI.Glowing(h, UI.Crimson, 0.35f);
            int cleared = 0;
            foreach (var c in Content.Story.chapters) if (Save.Cleared(c.id)) cleared++;
            var sub = UI.Text(Root, "Sub", $"Where does the body need you?   ·   {cleared} / {Content.Story.chapters.Length} rhythms restored", UI.Label, 30, UI.TextDim, TextAlignmentOptions.Left);
            UI.At(sub.rectTransform, new Vector2(0, 1), new Vector2(84, -126), new Vector2(1300, 44), new Vector2(0, 1));
            sub.characterSpacing = 4;

            var settings = UI.Button(Root, "SETTINGS", UI.Coral, () => { chapterId = null; mode = "settings"; }, 26);
            UI.At((RectTransform)settings.transform, new Vector2(1, 1), new Vector2(-60, -50), new Vector2(220, 70), new Vector2(1, 1));
            var title = UI.Button(Root, "TITLE", UI.TextDim, () => { chapterId = null; mode = "title"; }, 26);
            UI.At((RectTransform)title.transform, new Vector2(1, 1), new Vector2(-300, -50), new Vector2(170, 70), new Vector2(1, 1));

            // chapter cards
            var row = UI.Node("Cards", Root);
            UI.At(row, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1780, 440));
            var chapters = Content.Story.chapters;
            float cw = 278, gap = 22;
            float x0 = -(chapters.Length * cw + (chapters.Length - 1) * gap) / 2 + cw / 2;
            Chapter next = null;
            for (int i = 0; i < chapters.Length; i++)
            {
                var c = chapters[i];
                bool open = Unlocked(c);
                if (next == null && open && !Save.Cleared(c.id)) next = c;
                var b = UI.Button(row, "", c.Color, () => Select(c), 28);
                var rt = (RectTransform)b.transform;
                UI.At(rt, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (cw + gap), 0), new Vector2(cw, 420));
                b.interactable = true;
                var thumbRt = UI.Node("Thumb", b.Content);
                UI.Stretch(thumbRt, new Vector2(0, 0.42f), Vector2.one, new Vector2(8, 0), new Vector2(-8, -8));
                thumbRt.gameObject.AddComponent<RectMask2D>();
                var tex = Content.Art(ThumbArt(c));
                var thumb = UI.Raw(thumbRt, "Img", tex, open ? Color.white : new Color(0.25f, 0.25f, 0.28f));
                UI.Fill(thumb.rectTransform);
                if (tex != null)
                {
                    float aspect = tex.width / (float)tex.height, boxAspect = (cw - 16) / (420 * 0.58f - 8);
                    float w = boxAspect / aspect;
                    thumb.uvRect = new Rect(0.5f - w / 2, 0, w, 1);
                }
                var grad = UI.Img(thumbRt, "Grad", Sprites.GradientV, new Color(0.06f, 0.03f, 0.06f, 1));
                UI.Stretch(grad.rectTransform, Vector2.zero, new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);
                grad.rectTransform.localScale = new Vector3(1, -1, 1);
                var num = UI.Text(b.Content, "Num", c.index >= 6 ? "FINALE" : "CH." + GH.Pad2(c.index), UI.Label, 24, c.Accent, TextAlignmentOptions.TopLeft);
                UI.Stretch(num.rectTransform, new Vector2(0, 0), new Vector2(1, 0.42f), new Vector2(22, 0), new Vector2(-22, -14));
                num.characterSpacing = 6;
                var tt = UI.Text(b.Content, "Title", c.title.ToUpperInvariant(), UI.DisplayBold, 28, open ? Color.white : UI.TextDim, TextAlignmentOptions.TopLeft);
                UI.Stretch(tt.rectTransform, new Vector2(0, 0), new Vector2(1, 0.42f), new Vector2(22, 54), new Vector2(-22, -48));
                tt.enableAutoSizing = true; tt.fontSizeMin = 16; tt.fontSizeMax = 28;
                var best = BestGrade(c);
                string status = !open ? "🔒 LOCKED" : Save.Cleared(c.id) ? (best != null ? "✓ " + best : "✓ CLEARED") : "NEW";
                var st = UI.Text(b.Content, "Status", status.Replace("🔒", "◆"), UI.Label, 24, !open ? UI.TextDim : Save.Cleared(c.id) ? UI.Good : UI.Gold, TextAlignmentOptions.BottomLeft);
                UI.Stretch(st.rectTransform, new Vector2(0, 0), new Vector2(1, 0.42f), new Vector2(22, 18), new Vector2(-22, -10));
                st.characterSpacing = 4;
                b.LabelText.text = "";
                cards.Add((b, c));
            }

            // detail panel
            detail = UI.Panel(Root, "Detail", UI.Crimson);
            UI.At(detail, new Vector2(0.5f, 0), new Vector2(160, 60), new Vector2(1320, 220), new Vector2(0.5f, 0));
            dTitle = UI.Text(detail, "Title", "", UI.Display, 44, Color.white, TextAlignmentOptions.TopLeft);
            UI.At(dTitle.rectTransform, new Vector2(0, 1), new Vector2(40, -30), new Vector2(760, 60), new Vector2(0, 1));
            dTitle.enableAutoSizing = true; dTitle.fontSizeMin = 26; dTitle.fontSizeMax = 44;
            dSong = UI.Text(detail, "Song", "", UI.Body, 30, UI.TextDim, TextAlignmentOptions.TopLeft);
            UI.At(dSong.rectTransform, new Vector2(0, 1), new Vector2(40, -94), new Vector2(760, 44), new Vector2(0, 1));
            dBest = UI.Text(detail, "Best", "", UI.Label, 26, UI.Fg, TextAlignmentOptions.TopLeft);
            UI.At(dBest.rectTransform, new Vector2(0, 1), new Vector2(40, -146), new Vector2(760, 40), new Vector2(0, 1));
            dBest.characterSpacing = 3;
            playStory = UI.Button(detail, "PLAY STORY ▶", UI.Crimson, () => { chapterId = selected.id; mode = "story"; }, 32, UI.DisplayBold);
            UI.At((RectTransform)playStory.transform, new Vector2(1, 1), new Vector2(-40, -26), new Vector2(420, 80), new Vector2(1, 1));
            playSong = UI.Button(detail, "FREE PLAY ♪", UI.Coral, () => { chapterId = selected.id; mode = "free"; }, 28);
            UI.At((RectTransform)playSong.transform, new Vector2(1, 0), new Vector2(-40, 26), new Vector2(420, 74), new Vector2(1, 0));

            // captain + bubble
            var face = UI.Img(Root, "Captain", Content.Portrait("happy"), Color.white);
            UI.At(face.rectTransform, new Vector2(0, 0), new Vector2(-10, -70), new Vector2(330, 495), new Vector2(0, 0));
            face.preserveAspect = true;
            var bub = UI.Panel(Root, "Bubble", UI.Crimson, 16, 0.45f);
            UI.At(bub, new Vector2(0, 0), new Vector2(286, 290), new Vector2(400, 62), new Vector2(0, 0));
            var tail = UI.Img(bub, "Tail", Sprites.Diamond, UI.Glass);
            UI.At(tail.rectTransform, new Vector2(0, 0), new Vector2(22, 4), new Vector2(22, 22));
            tail.transform.SetAsFirstSibling();
            string hint = next == null ? "Every rhythm restored! Try Hard mode."
                : next.index == 1 ? "Let's warm up with First Beat!"
                : $"Next stop: {next.title}!";
            bubble = UI.Text(bub, "Text", hint, UI.Body, 26, UI.Fg, TextAlignmentOptions.MidlineLeft);
            UI.Fill(bubble.rectTransform, 22, 4, 16, 4);
            bubble.textWrappingMode = TextWrappingModes.NoWrap;
            bubble.enableAutoSizing = true; bubble.fontSizeMin = 18; bubble.fontSizeMax = 27;
            var bubbleGroup = UI.Group(bub, 0);
            StartCoroutine(BubbleIn(bubbleGroup, bub));
            Select(next ?? chapters[chapters.Length - 1]);
        }

        IEnumerator BubbleIn(CanvasGroup g, RectTransform rt)
        {
            yield return GH.Wait(0.7f);
            var p = rt.anchoredPosition;
            yield return GH.Tween(0.45f, k => { g.alpha = k; rt.anchoredPosition = p + new Vector2(0, -18 * (1 - k)); rt.localScale = Vector3.one * (0.92f + 0.08f * GH.EaseOutBack(k)); });
        }

        static string ThumbArt(Chapter c) => c.id switch
        {
            "tutorial" => "control_center", "cold" => "cold_outside", "exercise" => "exercise_outside", "sickness" => "sick_inside",
            "sleep" => "sleep_outside", _ => "finale",
        };

        public static IEnumerable<string> ThumbKeys()
        {
            foreach (var c in Content.Story.chapters) yield return ThumbArt(c);
            yield return "hub_bodymap";
        }

        static string BestGrade(Chapter c)
        {
            string best = null; int bestScore = -1;
            foreach (var d in SongCard.Diffs)
            {
                var b = Save.Best(c.song, d);
                if (b != null && b.score > bestScore) { bestScore = b.score; best = b.grade; }
            }
            return best;
        }

        void Select(Chapter c)
        {
            selected = c;
            bool open = Unlocked(c);
            foreach (var (b, cc) in cards) b.Highlighted = cc == c;
            dTitle.text = (c.index >= 6 ? "FINALE · " : $"CHAPTER {c.index} · ") + c.title.ToUpperInvariant();
            dSong.text = open ? $"{c.subtitle}   ·   ♪ {c.songTitle}" : "Locked — restore the previous rhythm first.";
            var bests = new List<string>();
            foreach (var d in SongCard.Diffs)
            {
                var b = Save.Best(c.song, d);
                if (b != null) bests.Add($"{d.ToUpperInvariant()} {b.grade}");
            }
            dBest.text = bests.Count > 0 ? "BEST  ·  " + string.Join("   ", bests) : open ? "NOT YET PLAYED" : "";
            playStory.interactable = open;
            playStory.LabelText.text = Save.Cleared(c.id) ? "REPLAY STORY ▶" : "PLAY STORY ▶";
            playStory.SetAccent(c.Color);
            playSong.interactable = Save.Cleared(c.id);
            playSong.gameObject.SetActive(true);
            detail.Find("Border").GetComponent<Image>().color = c.Color.WithA(0.55f);
            AudioDirector.I.Sfx("ui_hover", 0.3f);
            if (open) Focus(playStory);
        }

        public IEnumerator Run(System.Action<string, string> done)
        {
            AudioDirector.I.PlayBgm("control_center", 1.5f, 0.85f);
            yield return FadeIn(0.5f);
            mode = null;
            string qa = GH.Query("hub");
            float t = 0;
            while (mode == null)
            {
                t += Time.unscaledDeltaTime;
                if (qa != null && t > 1.5f) { var parts = qa.Split(':'); chapterId = parts[0]; mode = parts.Length > 1 ? parts[1] : "story"; }
                if (Input.GetKeyDown(KeyCode.Escape)) mode = "title";
                yield return null;
            }
            done(chapterId, mode);
        }
    }

    /// <summary>Settings modal with sliders, toggles and audio-offset calibration.</summary>
    public class SettingsScreen : GHScreen
    {
        bool done;

        public static SettingsScreen Make()
        {
            var s = Create<SettingsScreen>(GameRoot.I.ModalLayer, "Settings");
            s.Build();
            return s;
        }

        void Build()
        {
            var dim = UI.Img(Root, "Dim", Sprites.White, new Color(0.01f, 0, 0.01f, 0.93f), true);
            UI.Fill(dim.rectTransform);
            var p = UI.Panel(Root, "Panel", UI.Crimson);
            UI.At(p, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 940));
            p.Find("Glass").GetComponent<Image>().color = new Color(0.05f, 0.025f, 0.05f, 0.97f);
            var sub = UI.Text(p, "Sub", "Sound, timing and comfort", UI.Label, 26, UI.TextDim, TextAlignmentOptions.Left);
            UI.At(sub.rectTransform, new Vector2(0, 1), new Vector2(62, -106), new Vector2(600, 36), new Vector2(0, 1));
            sub.characterSpacing = 4;
            var title = UI.Text(p, "Title", "SETTINGS", UI.Display, 54, Color.white, TextAlignmentOptions.Left);
            UI.At(title.rectTransform, new Vector2(0, 1), new Vector2(60, -40), new Vector2(600, 70), new Vector2(0, 1));
            UI.Glowing(title, UI.Crimson, 0.3f);
            var s = Save.S;
            float y = -170;
            Slider("MUSIC", s.music, 0, 1, v => s.music = v, v => $"{v * 100:0}%", p, ref y);
            Slider("VOICE", s.voice, 0, 1, v => s.voice = v, v => $"{v * 100:0}%", p, ref y);
            Slider("EFFECTS", s.sfx, 0, 1, v => s.sfx = v, v => $"{v * 100:0}%", p, ref y);
            Slider("HIT SOUNDS", s.hitsound, 0, 1, v => s.hitsound = v, v => $"{v * 100:0}%", p, ref y);
            Slider("NOTE SPEED", s.noteSpeed, 1, 10, v => s.noteSpeed = Mathf.Round(v * 2) / 2, v => (Mathf.Round(v * 2) / 2).ToString("0.0"), p, ref y);
            var offsetSlider = Slider("AUDIO OFFSET", s.offsetMs, -200, 300, v => s.offsetMs = Mathf.RoundToInt(v / 5) * 5, v => $"{Mathf.RoundToInt(v / 5) * 5:+0;-0;0} ms", p, ref y);
            var cal = UI.Button(p, "CALIBRATE", UI.Teal, () => StartCoroutine(Calibrate(offsetSlider)), 24);
            UI.At((RectTransform)cal.transform, new Vector2(0, 1), new Vector2(1036, y + 74 + 2), new Vector2(204, 56), new Vector2(0, 1));
            y -= 22;
            var sep = UI.Img(p, "Sep", Sprites.White, new Color(1, 1, 1, 0.08f));
            UI.At(sep.rectTransform, new Vector2(0, 1), new Vector2(60, y + 6), new Vector2(1180, 2), new Vector2(0, 1));
            Toggle("AUTO-ADVANCE DIALOGUE", s.autoAdvance, v => s.autoAdvance = v, p, 0, ref y);
            float y2 = y + 74;
            Toggle("SHOW LYRICS", s.lyrics, v => s.lyrics = v, p, 1, ref y2);
            Toggle("REDUCE FLASHING", s.reduceFlash, v => s.reduceFlash = v, p, 0, ref y);
            y2 = y + 74;
            Toggle("HIGH QUALITY GRAPHICS", s.highQuality, v => s.highQuality = v, p, 1, ref y2);
            var ok = UI.Button(p, "DONE", UI.Crimson, () => done = true, 34, UI.DisplayBold);
            UI.At((RectTransform)ok.transform, new Vector2(1, 0), new Vector2(-60, 44), new Vector2(280, 84), new Vector2(1, 0));
            Focus(ok);
        }

        UnityEngine.UI.Slider Slider(string label, float value, float min, float max, System.Action<float> set, System.Func<float, string> fmt, RectTransform p, ref float y)
        {
            var l = UI.Text(p, label, label, UI.Label, 28, UI.TextDim, TextAlignmentOptions.Left);
            UI.At(l.rectTransform, new Vector2(0, 1), new Vector2(60, y), new Vector2(270, 50), new Vector2(0, 1));
            l.characterSpacing = 6;
            var holder = UI.Node(label + " Slider", p);
            UI.At(holder, new Vector2(0, 1), new Vector2(340, y - 10), new Vector2(520, 30), new Vector2(0, 1));
            var track = UI.Img(holder, "Track", Sprites.Round(6), new Color(1, 1, 1, 0.1f), true);
            UI.Fill(track.rectTransform, 0, 8, 0, 8);
            var fillArea = UI.Node("Fill Area", holder); UI.Fill(fillArea, 0, 8, 0, 8);
            var fill = UI.Img(fillArea, "Fill", Sprites.Round(6), UI.Crimson);
            fill.material = UI.Add2;
            UI.Fill(fill.rectTransform);
            var handleArea = UI.Node("Handle Area", holder); UI.Fill(handleArea, 14, 0, 14, 0);
            var handle = UI.Img(handleArea, "Handle", Sprites.Circle, Color.white, true);
            handle.rectTransform.sizeDelta = new Vector2(30, 30);
            var sl = holder.gameObject.AddComponent<UnityEngine.UI.Slider>();
            sl.fillRect = fill.rectTransform;
            sl.handleRect = handle.rectTransform;
            sl.targetGraphic = handle;
            sl.minValue = min; sl.maxValue = max;
            sl.value = value;
            var val = UI.Text(p, label + " Value", fmt(value), UI.DisplayBold, 28, Color.white, TextAlignmentOptions.Right);
            UI.At(val.rectTransform, new Vector2(0, 1), new Vector2(870, y), new Vector2(140, 50), new Vector2(0, 1));
            sl.onValueChanged.AddListener(v => { set(v); val.text = fmt(v); Save.NotifySettings(); });
            y -= 72;
            return sl;
        }

        void Toggle(string label, bool value, System.Action<bool> set, RectTransform p, int col, ref float y)
        {
            bool state = value;
            GHButton b = null;
            b = UI.Button(p, "", state ? UI.Good : UI.TextDim, () =>
            {
                state = !state; set(state); Save.NotifySettings();
                b.LabelText.text = (state ? "● " : "○ ") + label;
                b.SetAccent(state ? UI.Good : UI.TextDim);
            }, 24);
            b.LabelText.text = (state ? "● " : "○ ") + label;
            b.LabelText.alignment = TextAlignmentOptions.Left;
            UI.At((RectTransform)b.transform, new Vector2(0, 1), new Vector2(60 + col * 600, y), new Vector2(580, 62), new Vector2(0, 1));
            y -= 74;
        }

        IEnumerator Calibrate(UnityEngine.UI.Slider target)
        {
            var dim = UI.Img(Root, "CalDim", Sprites.White, new Color(0, 0, 0, 0.9f), true);
            UI.Fill(dim.rectTransform);
            var t = UI.Text(Root, "CalText", "", UI.Body, 40, Color.white, TextAlignmentOptions.Center);
            UI.At(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 200));
            var heart = UI.Glow(Root, "CalHeart", Sprites.Heart, UI.Crimson, UI.Add2);
            UI.At(heart.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(160, 160));
            t.text = "Tap SPACE, click, or touch on every heartbeat you <b>hear</b>.\n8 beats after the count-in.";
            yield return GH.Wait(2.2f);
            AudioDirector.I.SetBgmLevel(0.15f);
            float interval = 0.6f;
            var beats = new List<float>();
            var taps = new List<float>();
            float start = Time.unscaledTime + 0.5f;
            int total = 12;
            int next = 0;
            while (Time.unscaledTime < start + interval * (total + 0.8f))
            {
                float now = Time.unscaledTime;
                if (next < total && now >= start + next * interval)
                {
                    AudioDirector.I.Sfx(next < 4 ? "metro_hi" : "metro", 1f);
                    beats.Add(start + next * interval);
                    next++;
                    heart.rectTransform.localScale = Vector3.one * 1.3f;
                }
                heart.rectTransform.localScale = Vector3.Lerp(heart.rectTransform.localScale, Vector3.one, Time.unscaledDeltaTime * 8);
                if (next > 4 && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))) taps.Add(now);
                t.text = next <= 4 ? $"Listen…  {Mathf.Max(0, 4 - next + 1)}" : $"Tap!   {taps.Count} / 8";
                yield return null;
            }
            AudioDirector.I.SetBgmLevel(1f);
            var deltas = new List<float>();
            foreach (var tap in taps)
            {
                float best = float.MaxValue;
                for (int i = 4; i < beats.Count; i++) if (Mathf.Abs(tap - beats[i]) < Mathf.Abs(best)) best = tap - beats[i];
                if (Mathf.Abs(best) < 0.3f) deltas.Add(best);
            }
            if (deltas.Count >= 4)
            {
                deltas.Sort();
                float med = deltas[deltas.Count / 2];
                int ms = Mathf.RoundToInt(med * 1000 / 5) * 5;
                Save.S.offsetMs = Mathf.Clamp(ms, -200, 300);
                target.value = Save.S.offsetMs;
                Save.NotifySettings();
                t.text = $"Offset set to <b>{Save.S.offsetMs:+0;-0;0} ms</b>";
            }
            else t.text = "Not enough taps — offset unchanged.";
            yield return GH.Wait(1.6f);
            Destroy(dim.gameObject); Destroy(t.gameObject); Destroy(heart.gameObject);
        }

        void Update() { if (Input.GetKeyDown(KeyCode.Escape)) done = true; }

        public IEnumerator Run()
        {
            done = false;
            yield return FadeIn(0.2f);
            while (!done) yield return null;
            Save.Write();
            yield return FadeOut(0.2f);
        }
    }

    public class CreditsScreen : GHScreen
    {
        public static CreditsScreen Make() => Create<CreditsScreen>(GameRoot.I.ScreenLayer, "Credits");

        public IEnumerator Run(bool ending)
        {
            var black = UI.Img(Root, "Black", Sprites.White, Color.black, true);
            UI.Fill(black.rectTransform);
            var art = Content.Art(ending ? "ending" : "key_art");
            var bg = UI.Raw(Root, "Bg", art, new Color(0.35f, 0.3f, 0.35f));
            UI.Fill(bg.rectTransform);
            var kb = bg.gameObject.AddComponent<KenBurns>(); kb.Target = bg; kb.Restart("pull", 60);
            if (art != null) kb.ImageAspect = art.width / (float)art.height;
            UIParticles.Create(Root, ending ? "confetti" : "sparks", 0.5f);
            var scroll = UI.Node("Scroll", Root);
            UI.At(scroll, new Vector2(0.5f, 0), new Vector2(0, -100), new Vector2(1300, 3000), new Vector2(0.5f, 1));
            string txt =
                "<size=150%><b>GAME HEART</b></size>\n<color=#FF8A80>RHYTHM OF LIFE</color>\n\n\n" +
                "<color=#FF8A80>A SPARC LEARNING ADVENTURE</color>\nRemastered edition\n\n\n" +
                "<color=#FF8A80>ORIGINAL GAME HEART</color>\nSPARC project team\nStory, lessons and Captain B from the original SPARC Game Heart\nThe original was built on the open-source CyanStars engine (IPOL Studio)\n\n" +
                "<color=#FF8A80>REMASTER</color>\nNew rhythm engine, stage, cutscenes and interface built in Unity 6\nAnatomical heart model from the original project\n\n" +
                "<color=#FF8A80>MUSIC</color>\nFirst Beat · Frostbite Pulse · Sprint Circuit · Fever Front\nNight Tide · Symphony of the Heart · Rhythm of Life · Control Center\nOriginal lyrics, AI-generated songs (YuE2)\n\n" +
                "<color=#FF8A80>VOICE</color>\nCaptain B — AI text-to-speech (Qwen3-TTS)\n\n" +
                "<color=#FF8A80>ART</color>\nAI-generated illustrations (gpt-image-2.5, Qwen-Image)\n\n" +
                "<color=#FF8A80>FONTS</color>\nOrbitron · Rajdhani · Inter (SIL Open Font License)\n\n\n" +
                "<color=#FF8A80>YOUR HEART BEATS ABOUT 100,000 TIMES A DAY.</color>\nThank you for keeping the rhythm.\n\n\n\n";
            var t = UI.Text(scroll, "Text", txt, UI.Body, 38, Color.white, TextAlignmentOptions.Top);
            UI.Fill(t.rectTransform);
            t.lineSpacing = 12;
            AudioDirector.I.PlayBgm("title_theme", 2f);
            yield return FadeIn(1f);
            float y = -100, speed = 70;
            bool skip = false;
            while (y < 3100 && !skip)
            {
                y += Time.unscaledDeltaTime * speed * (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || Input.touchCount > 0 ? 5 : 1);
                scroll.anchoredPosition = new Vector2(0, y);
                if (Input.GetKeyDown(KeyCode.Escape) || (GH.QA && y > 300)) skip = true;
                yield return null;
            }
            yield return FadeOut(1f);
        }
    }
}
