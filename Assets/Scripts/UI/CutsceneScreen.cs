using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameHeart
{
    /// <summary>
    /// Plays a sequence of painted shots with camera moves, letterboxing, weather particles,
    /// Captain B's portrait and voiced, typewritten dialogue. Click / Space / Enter advances; SKIP ends.
    /// </summary>
    public class CutsceneScreen : GHScreen
    {
        RawImage stillA, stillB;
        KenBurns kbA, kbB;
        bool aFront = true;
        RectTransform barTop, barBot, dialog, portraitRt, captionRt;
        Image portrait, portraitGlow, nameBar, nextIcon;
        TextMeshProUGUI nameText, lineText, captionText;
        CanvasGroup dialogGroup, portraitGroup, captionGroup;
        UIParticles fx;
        GHButton skip;
        Color accent = UI.Crimson;
        bool advance, skipped;
        float portraitBounce, talk;
        Vector2 dialogBase;
        string mood = "";

        public static CutsceneScreen Make()
        {
            var s = Create<CutsceneScreen>(GameRoot.I.ScreenLayer, "Cutscene");
            s.Build();
            return s;
        }

        void Build()
        {
            var bg = UI.Img(Root, "Black", Sprites.White, Color.black, true);
            UI.Fill(bg.rectTransform);
            stillB = UI.Raw(Root, "StillB", null, new Color(1, 1, 1, 0));
            UI.Fill(stillB.rectTransform);
            kbB = stillB.gameObject.AddComponent<KenBurns>(); kbB.Target = stillB;
            stillA = UI.Raw(Root, "StillA", null, new Color(1, 1, 1, 0));
            UI.Fill(stillA.rectTransform);
            kbA = stillA.gameObject.AddComponent<KenBurns>(); kbA.Target = stillA;
            fx = UIParticles.Create(Root, "");
            var vig = UI.Img(Root, "Vignette", Sprites.Vignette, new Color(0, 0, 0, 0.75f));
            UI.Fill(vig.rectTransform);
            var grad = UI.Img(Root, "BottomShade", Sprites.GradientV, new Color(0, 0, 0, 0.85f));
            UI.Stretch(grad.rectTransform, Vector2.zero, new Vector2(1, 0.45f), Vector2.zero, Vector2.zero);
            grad.rectTransform.localScale = new Vector3(1, -1, 1);
            barTop = UI.Img(Root, "BarTop", Sprites.White, Color.black).rectTransform;
            UI.Stretch(barTop, new Vector2(0, 1), Vector2.one, new Vector2(0, -0), new Vector2(0, 0));
            barBot = UI.Img(Root, "BarBottom", Sprites.White, Color.black).rectTransform;
            UI.Stretch(barBot, Vector2.zero, new Vector2(1, 0), Vector2.zero, Vector2.zero);

            // caption card for narration
            captionRt = UI.Node("Caption", Root);
            UI.At(captionRt, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1400, 160));
            captionGroup = UI.Group(captionRt, 0);
            captionText = UI.Text(captionRt, "Text", "", UI.BodyLight, 52, UI.Fg, TextAlignmentOptions.Center);
            UI.Fill(captionText.rectTransform);
            captionText.fontStyle = FontStyles.Italic;
            captionText.characterSpacing = 3;

            // portrait
            portraitRt = UI.Node("Portrait", Root);
            UI.At(portraitRt, new Vector2(0, 0), new Vector2(40, 0), new Vector2(520, 780), new Vector2(0, 0));
            portraitGroup = UI.Group(portraitRt, 0);
            portraitGlow = UI.Glow(portraitRt, "Glow", Sprites.Glow, accent.WithA(0.35f), UI.Add2);
            UI.At(portraitGlow.rectTransform, new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(700, 700));
            portrait = UI.Img(portraitRt, "Image", null, Color.white);
            UI.Fill(portrait.rectTransform);
            portrait.preserveAspect = true;

            // dialogue box
            dialog = UI.Node("Dialog", Root);
            UI.Stretch(dialog, new Vector2(0, 0), new Vector2(1, 0), new Vector2(470, 56), new Vector2(-90, 300));
            dialogGroup = UI.Group(dialog, 0);
            dialogBase = dialog.anchoredPosition;
            UI.Fill(UI.Panel(dialog, "Panel", accent, 22, 0.5f));
            nameBar = UI.Img(dialog, "NameBar", Sprites.Round(10), accent);
            UI.At(nameBar.rectTransform, new Vector2(0, 1), new Vector2(36, 0), new Vector2(250, 52), new Vector2(0, 0.5f));
            nameText = UI.Text(nameBar.rectTransform, "Name", "CAPTAIN B", UI.Label, 32, Color.white, TextAlignmentOptions.Center);
            UI.Fill(nameText.rectTransform);
            nameText.characterSpacing = 8;
            lineText = UI.Text(dialog, "Line", "", UI.Body, 40, UI.Fg, TextAlignmentOptions.TopLeft);
            UI.Fill(lineText.rectTransform, 44, 36, 70, 50);
            lineText.lineSpacing = 6;
            nextIcon = UI.Img(dialog, "Next", Sprites.Chevron, accent);
            nextIcon.material = UI.Add2;
            UI.At(nextIcon.rectTransform, new Vector2(1, 0), new Vector2(-46, 34), new Vector2(34, 34));
            nextIcon.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);

            skip = UI.Button(Root, "SKIP  ▶▶", UI.TextDim, () => skipped = true, 26);
            UI.At((RectTransform)skip.transform, new Vector2(1, 1), new Vector2(-40, -36), new Vector2(200, 60), new Vector2(1, 1));
        }

        public void SetAccent(Color c)
        {
            accent = c;
            nameBar.color = c;
            nextIcon.color = c;
            portraitGlow.color = c.WithA(0.35f);
            dialog.Find("Panel/Border").GetComponent<Image>().color = c.WithA(0.5f);
        }

        void Update()
        {
            if (WantsAdvance()) advance = true;
            float dt = Time.unscaledDeltaTime;
            float lvl = AudioDirector.I.VoiceLevel();
            talk = GH.Damp(talk, lvl, 18, dt);
            portraitBounce = Mathf.Max(0, portraitBounce - dt * 4);
            if (portrait != null)
            {
                float breathe = Mathf.Sin(Time.unscaledTime * 1.6f) * 0.006f;
                float s = 1 + breathe + talk * 0.018f + GH.EaseOutBack(1 - portraitBounce) * 0 + portraitBounce * 0.04f;
                portrait.rectTransform.localScale = new Vector3(s, s, 1);
                portrait.rectTransform.anchoredPosition = new Vector2(0, talk * 6f);
                portraitGlow.color = accent.WithA(0.25f + talk * 0.35f);
            }
            if (nextIcon != null) nextIcon.rectTransform.anchoredPosition = new Vector2(-46, 34 + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 8);
        }

        static bool WantsAdvance()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Z)) return true;
            bool overUi = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && false;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId) && IsButtonUnder(t.position))) return true;
            }
            else if (Input.GetMouseButtonDown(0) && !IsButtonUnder(Input.mousePosition)) return !overUi;
            return false;
        }

        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        static bool IsButtonUnder(Vector2 pos)
        {
            if (EventSystem.current == null) return false;
            var pe = new PointerEventData(EventSystem.current) { position = pos };
            hits.Clear();
            EventSystem.current.RaycastAll(pe, hits);
            foreach (var h in hits) if (h.gameObject.GetComponentInParent<GHButton>() != null) return true;
            return false;
        }

        public IEnumerator Play(Shot[] shots, Chapter ch)
        {
            if (shots == null || shots.Length == 0) yield break;
            if (ch != null) SetAccent(ch.Color);
            Focus(null);
            skipped = false;
            // preload all stills in this sequence
            yield return Content.LoadAll(Content.ArtFor(shots));
            StartCoroutine(FadeIn(0.5f));
            StartCoroutine(Letterbox(true));
            bool first = true;
            foreach (var shot in shots)
            {
                if (skipped) break;
                float est = 2f;
                foreach (var ln in shot.lines) est += ln.voice != null && AudioDirector.I.VoiceMeta.TryGetValue(ln.voice, out var vm) ? vm.duration + 1.2f : 3.5f;
                yield return ShowStill(shot, est, first);
                first = false;
                foreach (var ln in shot.lines)
                {
                    if (skipped) break;
                    if (ln.speaker == "N") yield return Caption(ln.text);
                    else yield return Say(ln);
                }
            }
            AudioDirector.I.StopVoice();
            StartCoroutine(HideDialog());
            yield return Letterbox(false);
        }

        IEnumerator ShowStill(Shot shot, float duration, bool first)
        {
            var tex = Content.Art(shot.art);
            var front = aFront ? stillB : stillA;  // the one that will become front
            var back = aFront ? stillA : stillB;
            var kb = aFront ? kbB : kbA;
            aFront = !aFront;
            front.texture = tex;
            if (tex != null) kb.ImageAspect = tex.width / (float)tex.height;
            kb.Restart(shot.move, duration);
            front.transform.SetAsLastSibling();
            front.transform.SetSiblingIndex(back.transform.GetSiblingIndex() + 1);
            Color tint = string.IsNullOrEmpty(shot.tint) ? Color.white : GH.Hex(shot.tint);
            fx.SetMode(shot.fx);
            GH.Log("shot", shot.art);
            yield return GH.Tween(first ? 0.9f : 0.7f, k => { front.color = tint.WithA(k); }, GH.Smooth01);
            back.color = back.color.WithA(0);
        }

        IEnumerator Letterbox(bool on)
        {
            float h = 96;
            yield return GH.Tween(0.6f, k =>
            {
                float v = on ? k : 1 - k;
                barTop.offsetMin = new Vector2(0, -h * v);
                barBot.offsetMax = new Vector2(0, h * v * 0.6f);
            }, GH.EaseInOutCubic);
        }

        IEnumerator Caption(string text)
        {
            yield return HideDialog();
            captionText.text = text;
            yield return GH.Tween(0.6f, k => captionGroup.alpha = k);
            advance = false;
            float t = 0, max = GH.QA && GH.Flag("fast") ? 0.4f : 3.2f;
            while (!advance && !skipped && t < max) { t += Time.unscaledDeltaTime; yield return null; }
            yield return GH.Tween(0.4f, k => captionGroup.alpha = 1 - k);
        }

        IEnumerator HideDialog()
        {
            if (dialogGroup.alpha <= 0.01f) yield break;
            float a = dialogGroup.alpha, p = portraitGroup.alpha;
            yield return GH.Tween(0.25f, k => { dialogGroup.alpha = a * (1 - k); portraitGroup.alpha = p * (1 - k); });
        }

        IEnumerator Say(StoryLine ln)
        {
            if (dialogGroup.alpha < 0.99f)
            {
                StartCoroutine(GH.Tween(0.35f, k =>
                {
                    dialogGroup.alpha = k;
                    portraitGroup.alpha = k;
                    portraitRt.anchoredPosition = new Vector2(40 - 80 * (1 - k), 0);
                    dialog.anchoredPosition = dialogBase + new Vector2(0, -30 * (1 - k));
                }, GH.EaseOutCubic));
            }
            if (ln.mood != mood)
            {
                mood = ln.mood;
                portrait.sprite = Content.Portrait(ln.mood);
                portraitBounce = 1;
            }
            nameText.text = "CAPTAIN B";
            lineText.text = ln.text;
            lineText.maxVisibleCharacters = 0;
            nextIcon.enabled = false;
            float dur = AudioDirector.I.PlayVoice(ln.voice);
            GH.Log("line", ln.voice + " " + ln.text);
            int chars = ln.text.Length;
            float typeDur = dur > 0 ? Mathf.Min(dur * 0.85f, chars * 0.045f) : chars * 0.03f;
            if (GH.QA && GH.Flag("fast")) typeDur = 0.1f;
            advance = false;
            float t = 0;
            while (t < typeDur && !advance && !skipped)
            {
                t += Time.unscaledDeltaTime;
                lineText.maxVisibleCharacters = Mathf.CeilToInt(chars * Mathf.Clamp01(t / typeDur));
                yield return null;
            }
            lineText.maxVisibleCharacters = chars;
            nextIcon.enabled = true;
            advance = false;
            float extra = 0;
            float tail = Save.S.autoAdvance ? 1.1f : float.MaxValue;
            if (GH.QA && GH.Flag("fast")) tail = 0.2f;
            while (!advance && !skipped)
            {
                if (!AudioDirector.I.VoicePlaying || (GH.QA && GH.Flag("fast")))
                {
                    extra += Time.unscaledDeltaTime;
                    if (extra > tail) break;
                }
                yield return null;
            }
            AudioDirector.I.Sfx("ui_tick", 0.4f);
        }
    }
}
