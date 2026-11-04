using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameHeart
{
    /// <summary>
    /// The whole game as one coroutine: title → control center → chapters
    /// (card → intro → decision → song → results → outcome → Pulse Check) → finale → credits.
    /// QA hooks: ?jump=title|hub|prologue|credits|&lt;chapter&gt;[:card|decision|song|results|win|struggle|quiz] &amp;auto=1 &amp;qa=1&amp;fast=1
    /// </summary>
    public class GameFlow
    {
        static GameRoot R => GameRoot.I;
        static Overlay Ov => GameRoot.I.Overlay;
        bool skipTitlePress;

        public IEnumerator Run()
        {
            Ov.SetFade(1);
            Ov.Loading(true, "WARMING UP THE HEART");
            var keys = new List<string> { "key_art" };
            keys.AddRange(HubScreen.ThumbKeys());
            int done = 0;
            foreach (var k in keys)
            {
                yield return Content.LoadArt(k);
                Content.Pin(k);
                Ov.LoadProgress(++done / (float)(keys.Count + 2));
            }
            yield return AudioDirector.I.LoadMusic("title_theme", p => Ov.LoadProgress((done + p) / (keys.Count + 2f)));
            Ov.LoadProgress(1);
            Ov.Loading(false);
            GH.Log("ready");

            string jump = GH.Query("jump");
            if (!string.IsNullOrEmpty(jump)) yield return Jump(jump);
            while (true)
            {
                string next = null;
                yield return Title(c => next = c);
                if (next == "hub") yield return HubLoop();
            }
        }

        // ------------------------------------------------------------------ title
        IEnumerator Title(System.Action<string> result)
        {
            while (true)
            {
                var t = TitleScreen.Make();
                Ov.StartCoroutine(Ov.FadeTo(0, 0.8f));
                string choice = null;
                yield return t.Run(c => choice = c, skipTitlePress);
                skipTitlePress = true;
                GH.Log("title_choice", choice);
                if (choice == "settings")
                {
                    yield return SettingsScreen.Make().Run();
                    yield return t.FadeOut(0.3f);
                    continue;
                }
                if (choice == "credits")
                {
                    yield return Ov.FadeTo(1, 0.5f);
                    Object.Destroy(t.gameObject);
                    Ov.SetFade(0);
                    yield return CreditsScreen.Make().Run(false);
                    continue;
                }
                if (choice == "new")
                {
                    Save.P = new Progress();
                    Save.Write();
                }
                yield return Ov.FadeTo(1, 0.6f);
                Object.Destroy(t.gameObject);
                if (!Save.P.prologueSeen)
                {
                    yield return Prologue();
                    yield return PlayChapter(Content.Story.Get("tutorial"));
                }
                result("hub");
                yield break;
            }
        }

        IEnumerator Prologue()
        {
            AudioDirector.I.PlayBgm("control_center", 2f, 0.9f);
            var cs = CutsceneScreen.Make();
            Ov.StartCoroutine(Ov.FadeTo(0, 0.6f));
            yield return cs.Play(Content.Story.prologue.shots, null);
            Save.P.prologueSeen = true;
            Save.Write();
            yield return Ov.FadeTo(1, 0.6f);
            Object.Destroy(cs.gameObject);
        }

        // ------------------------------------------------------------------ hub
        IEnumerator HubLoop()
        {
            while (true)
            {
                var hub = HubScreen.Make();
                Ov.StartCoroutine(Ov.FadeTo(0, 0.6f));
                string id = null, mode = null;
                yield return hub.Run((c, m) => { id = c; mode = m; });
                GH.Log("hub_choice", $"{id} {mode}");
                if (mode == "settings")
                {
                    yield return SettingsScreen.Make().Run();
                    Object.Destroy(hub.gameObject);
                    continue;
                }
                yield return Ov.FadeTo(1, 0.5f);
                Object.Destroy(hub.gameObject);
                if (mode == "title") yield break;
                var ch = Content.Story.Get(id);
                if (ch == null) continue;
                if (mode == "free") yield return FreePlay(ch);
                else yield return PlayChapter(ch);
            }
        }

        // ------------------------------------------------------------------ chapter
        static IEnumerable<string> ChapterArt(Chapter ch)
        {
            foreach (var k in Content.ArtFor(ch.intro)) yield return k;
            if (ch.HasDecision) foreach (var f in ch.decision.feedback) foreach (var k in Content.ArtFor(f.shots)) yield return k;
            foreach (var k in Content.ArtFor(ch.win)) yield return k;
            foreach (var k in Content.ArtFor(ch.struggle)) yield return k;
            yield return ch.stage;
        }

        IEnumerator Prepare(Chapter ch)
        {
            Ov.Loading(true, "ENTERING  " + ch.title.ToUpperInvariant());
            var keys = ChapterArt(ch).Distinct().ToList();
            int n = 0;
            foreach (var k in keys) { yield return Content.LoadArt(k); Ov.LoadProgress(++n / (keys.Count + 3f) * 0.5f); }
            yield return AudioDirector.I.LoadMusic(ch.song, p => Ov.LoadProgress(0.5f + p * 0.35f));
            yield return AudioDirector.I.LoadMusic(ch.song + "_inst", p => Ov.LoadProgress(0.85f + p * 0.15f));
            Ov.LoadProgress(1);
            yield return GH.Wait(0.2f);
            Ov.Loading(false);
        }

        void CutsceneBgm(Chapter ch, float vol = 0.75f)
        {
            string key = AudioDirector.I.HasMusic(ch.song + "_inst") ? ch.song + "_inst" : "control_center";
            AudioDirector.I.PlayBgm(key, 1.5f, vol);
        }

        IEnumerator PlayChapter(Chapter ch, string startAt = null)
        {
            GH.Log("chapter_begin", ch.id);
            UI.Accent = ch.Color;
            yield return Prepare(ch);
            CutsceneBgm(ch);
            Ov.SetFade(0);
            if (startAt == null || startAt == "card") yield return Ov.ChapterCard(ch);

            // intro
            if (startAt == null || startAt == "card" || startAt == "intro")
            {
                var cs = CutsceneScreen.Make();
                yield return cs.Play(ch.intro, ch);
                yield return cs.FadeOut(0.4f);
            }

            // decision
            if (ch.HasDecision && (startAt == null || startAt == "card" || startAt == "intro" || startAt == "decision"))
                yield return Decide(ch);

            // song
            SongResult result = null;
            if (startAt == null || startAt == "card" || startAt == "intro" || startAt == "decision" || startAt == "song")
            {
                while (true)
                {
                    SongResult r = null;
                    bool quit = false;
                    yield return PlaySong(ch, true, x => r = x, () => quit = true);
                    if (quit) { yield return Ov.FadeTo(1, 0.4f); yield break; }
                    result = r;
                    break;
                }
            }
            else if (startAt == "results" || startAt == "win" || startAt == "struggle" || startAt == "quiz")
            {
                result = new SongResult { song = ch.song, diff = "normal", score = 912345, acc = 0.94f, gauge = startAt == "struggle" ? 0.5f : 0.88f, perfect = 300, great = 40, good = 8, miss = 3, maxCombo = 211, units = 351, grade = "A" };
                result.cleared = result.gauge >= RhythmGame.ClearLine;
                if (startAt == "results")
                {
                    R.Stage.SetTheme(ch, Content.Art(ch.stage));
                    R.Stage.Show(true);
                    yield return ShowResults(ch, result, true, _ => { });
                    R.Stage.Show(false);
                }
            }

            // outcome
            if (startAt != "quiz")
            {
                CutsceneBgm(ch);
                var cs2 = CutsceneScreen.Make();
                Ov.StartCoroutine(Ov.FadeTo(0, 0.5f));
                yield return cs2.Play(result != null && result.cleared ? ch.win : ch.struggle, ch);
                yield return cs2.FadeOut(0.4f);
            }

            // quiz
            if (ch.quiz != null && ch.quiz.Length > 0)
            {
                var q = QuizScreen.Make();
                yield return q.Run(ch, Content.Art(ch.intro[ch.intro.Length - 1].art));
                yield return q.FadeOut(0.4f);
            }

            Save.MarkCleared(ch.id, result != null && result.cleared);
            GH.Log("chapter_end", $"{ch.id} cleared={result?.cleared}");
            if (ch.id == "finale")
            {
                Save.P.finaleSeen = true; Save.Write();
                yield return CreditsScreen.Make().Run(true);
            }
            else
            {
                var next = Content.Story.chapters.FirstOrDefault(c => c.index == ch.index + 1);
                if (next != null && HubScreen.Unlocked(next)) Ov.Toast("NEW RHYTHM UNLOCKED  ·  " + next.title.ToUpperInvariant(), next.Color);
            }
            yield return Ov.FadeTo(1, 0.5f);
        }

        IEnumerator Decide(Chapter ch)
        {
            var d = ch.decision;
            Texture2D bg = Content.Art(ch.intro[ch.intro.Length - 1].art);
            bool first = true;
            while (true)
            {
                var ds = DecisionScreen.Make(ch, bg);
                string choice = null;
                yield return ds.Ask(c => choice = c, first);
                first = false;
                if (choice == d.correct)
                {
                    AudioDirector.I.Sfx("quiz_right", 0.8f);
                    yield return ds.Respond(d.right[0], true, ch);
                    yield return ds.FadeOut(0.4f);
                    yield break;
                }
                AudioDirector.I.Sfx("wrong_choice", 0.9f);
                var fb = d.feedback.FirstOrDefault(f => f.choice == choice) ?? d.feedback[0];
                yield return ds.FadeOut(0.3f);
                var cs = CutsceneScreen.Make();
                yield return cs.Play(fb.shots, ch);
                yield return cs.FadeOut(0.3f);
                if (GH.Query("choose") != null) GH.QueryOverride("choose", d.correct);
            }
        }

        // ------------------------------------------------------------------ song
        IEnumerator PlaySong(Chapter ch, bool story, System.Action<SongResult> done, System.Action quit)
        {
            var chart = Content.Chart(ch.song);
            if (!AudioDirector.I.HasMusic(ch.song))
            {
                Ov.Loading(true);
                yield return AudioDirector.I.LoadMusic(ch.song, Ov.LoadProgress);
                Ov.Loading(false);
            }
            if (Content.Art(ch.stage) == null) yield return Content.LoadArt(ch.stage);
            var clip = AudioDirector.I.Music(ch.song);
            R.Stage.SetTheme(ch, Content.Art(ch.stage));
            R.Stage.Show(true);
            Ov.StartCoroutine(Ov.FadeTo(0, 0.5f));
            while (true)
            {
                AudioDirector.I.PlayBgm(ch.song, 1.2f, 0.55f, chart.previewStart);
                var card = SongCard.Make(ch, chart);
                string res = null, diff = "normal";
                yield return card.Ask((r, d) => { res = r; diff = d; });
                yield return card.FadeOut(0.3f);
                if (res == "back")
                {
                    if (story)
                    {
                        // in story mode BACK leaves the chapter to the hub
                        AudioDirector.I.StopBgm(0.5f);
                        R.Stage.Show(false);
                        quit();
                        yield break;
                    }
                    AudioDirector.I.StopBgm(0.5f);
                    R.Stage.Show(false);
                    quit();
                    yield break;
                }
                AudioDirector.I.StopBgm(0.4f);
                yield return GH.Wait(0.35f);

                bool retry;
                do
                {
                    retry = false;
                    SongResult result = null;
                    string pauseResult = null;
                    yield return RunSong(ch, chart, clip, diff, r => result = r, p => pauseResult = p);
                    if (pauseResult == "quit") { retry = false; result = null; break; }
                    if (pauseResult == "restart") { retry = true; continue; }
                    if (result == null) break;
                    string next = null;
                    yield return ShowResults(ch, result, story, c => next = c);
                    if (next == "retry") { retry = true; continue; }
                    R.Stage.Show(false);
                    done(result);
                    yield break;
                } while (retry);
                // quit from pause: back to the song card
            }
        }

        IEnumerator RunSong(Chapter ch, Chart chart, AudioClip clip, string diff, System.Action<SongResult> done, System.Action<string> paused)
        {
            var game = R.Game;
            game.Autoplay = GH.Flag("auto");
            float seek = 0;
            if (GH.Query("seek") != null) float.TryParse(GH.Query("seek"), out seek);
            SongResult result = null;
            void OnFinish(SongResult r) => result = r;
            void OnJudge(int lane, Judgment j, float d, NoteType t) => R.Stage.OnJudge(j, t);
            void OnBeat(int i, bool down) => R.Stage.OnBeat(down);
            game.Finished += OnFinish;
            game.Judged += OnJudge;
            game.Beat += OnBeat;
            bool pauseReq = false;
            game.Begin(chart, diff, clip, ch, R.Stage.Cam, seek);
            var hud = Hud.Make(ch, game);
            hud.OnPause = () => pauseReq = true;
            R.StartCoroutine(hud.Countdown());
            string pr = null;
            while (game.Playing && result == null)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) pauseReq = true;
                if (pauseReq)
                {
                    pauseReq = false;
                    game.SetPaused(true);
                    hud.SetDim(true);
                    var pm = PauseMenu.Make(ch);
                    while (pm.Result == null) yield return null;
                    string choice = pm.Result;
                    if (choice == "settings")
                    {
                        Object.Destroy(pm.gameObject);
                        yield return SettingsScreen.Make().Run();
                        pauseReq = true;
                        continue;
                    }
                    yield return pm.FadeOut(0.15f);
                    if (choice == "resume")
                    {
                        hud.SetDim(false);
                        yield return Resume(hud);
                        game.SetPaused(false);
                        continue;
                    }
                    pr = choice;
                    game.Stop();
                    break;
                }
                yield return null;
            }
            game.Finished -= OnFinish;
            game.Judged -= OnJudge;
            game.Beat -= OnBeat;
            yield return hud.Hide();
            if (pr != null) { paused(pr); yield break; }
            if (result != null)
            {
                result.isNewBest = Save.Submit(result.song, result.diff, result.score, result.acc, result.grade, result.fc, result.ap, result.maxCombo);
            }
            done(result);
        }

        IEnumerator Resume(Hud hud)
        {
            var t = UI.Text(GameRoot.I.ModalLayer, "Resume", "", UI.Display, 120, Color.white, TMPro.TextAlignmentOptions.Center);
            UI.At(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(400, 200));
            for (int i = 3; i >= 1; i--)
            {
                t.text = i.ToString();
                AudioDirector.I.Sfx("metro", 0.6f);
                yield return GH.Tween(0.5f, k => { t.alpha = 1 - k * 0.6f; t.rectTransform.localScale = Vector3.one * (1.2f - 0.2f * k); });
            }
            Object.Destroy(t.gameObject);
        }

        IEnumerator ShowResults(Chapter ch, SongResult r, bool story, System.Action<string> done)
        {
            var rs = ResultsScreen.Make();
            string c = null;
            yield return rs.Show(ch, r, story, x => c = x);
            yield return rs.FadeOut(0.3f);
            done(c);
        }

        IEnumerator FreePlay(Chapter ch)
        {
            Ov.Loading(true, "LOADING  " + ch.songTitle.ToUpperInvariant());
            yield return Content.LoadArt(ch.stage);
            yield return AudioDirector.I.LoadMusic(ch.song, Ov.LoadProgress);
            Ov.Loading(false);
            yield return PlaySong(ch, false, _ => { }, () => { });
            yield return Ov.FadeTo(1, 0.4f);
        }

        // ------------------------------------------------------------------ QA jumps
        IEnumerator Jump(string jump)
        {
            GH.Log("jump", jump);
            var parts = jump.Split(':');
            switch (parts[0])
            {
                case "title": yield break;
                case "hub": yield return HubLoop(); yield break;
                case "prologue": yield return Prologue(); yield break;
                case "credits": Ov.SetFade(0); yield return CreditsScreen.Make().Run(true); yield break;
            }
            var ch = Content.Story.Get(parts[0]);
            if (ch == null) yield break;
            if (parts.Length > 1 && parts[1] == "free") { yield return FreePlay(ch); yield break; }
            yield return PlayChapter(ch, parts.Length > 1 ? parts[1] : null);
        }
    }
}
