using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameHeart
{
    /// <summary>Top-most layer: fades, loading pulse, chapter title cards, toasts, flash.</summary>
    public class Overlay : MonoBehaviour
    {
        Image fader, flash;
        RectTransform loading, toastRoot;
        Image loadFill, loadHeart;
        TextMeshProUGUI loadText, toastText;
        CanvasGroup loadGroup, toastGroup;
        float loadTarget;
        Coroutine toastCo, loadCo;
        RectTransform root;

        public void Build(RectTransform layer)
        {
            root = layer;
            fader = UI.Img(layer, "Fader", Sprites.White, Color.black);
            UI.Fill(fader.rectTransform);
            fader.raycastTarget = true;
            flash = UI.Img(layer, "Flash", Sprites.White, new Color(1, 1, 1, 0));
            flash.material = UI.Add1;
            UI.Fill(flash.rectTransform);

            loading = UI.Node("Loading", layer);
            UI.At(loading, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(620, 200));
            loadGroup = UI.Group(loading, 0);
            loadHeart = UI.Glow(loading, "Heart", Sprites.Heart, UI.Crimson, UI.Add2);
            UI.At(loadHeart.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 6), new Vector2(76, 76));
            loadText = UI.Text(loading, "Text", "SYNCING HEARTBEAT", UI.Label, 30, UI.Fg, TextAlignmentOptions.Center);
            UI.At(loadText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(620, 44));
            loadText.characterSpacing = 10;
            var track = UI.Img(loading, "Track", Sprites.Round(4), new Color(1, 1, 1, 0.12f));
            UI.At(track.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(420, 8));
            loadFill = UI.Img(track.rectTransform, "Fill", Sprites.Round(4), UI.Crimson);
            loadFill.material = UI.Add2;
            UI.Stretch(loadFill.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);

            toastRoot = UI.Node("Toast", layer);
            UI.At(toastRoot, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(760, 70));
            toastGroup = UI.Group(toastRoot, 0);
            UI.Fill(UI.Panel(toastRoot, "Panel", UI.Crimson));
            toastText = UI.Text(toastRoot, "Text", "", UI.Label, 30, UI.Fg, TextAlignmentOptions.Center);
            UI.Fill(toastText.rectTransform, 20, 0, 20, 0);
            toastText.characterSpacing = 4;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (loadGroup.alpha > 0.01f)
            {
                float beat = EcgLine.Pqrst(Time.unscaledTime * 1.2f);
                loadHeart.rectTransform.localScale = Vector3.one * (1 + Mathf.Max(0, beat) * 0.25f);
                var a = loadFill.rectTransform.anchorMax;
                a.x = GH.Damp(a.x, loadTarget, 8, dt);
                loadFill.rectTransform.anchorMax = a;
            }
            if (flash.color.a > 0) flash.color = flash.color.WithA(Mathf.Max(0, flash.color.a - dt * 3f));
        }

        public IEnumerator FadeTo(float a, float dur = 0.4f)
        {
            float from = fader.color.a;
            fader.raycastTarget = a > 0.5f;
            yield return GH.Tween(dur, k => fader.color = Color.black.WithA(Mathf.Lerp(from, a, k)), GH.Smooth01);
        }

        public void SetFade(float a) { fader.color = Color.black.WithA(a); fader.raycastTarget = a > 0.5f; }

        public void Flash(Color c, float a = 0.5f)
        {
            if (Save.S.reduceFlash) a *= 0.3f;
            flash.color = c.WithA(a);
        }

        public void Loading(bool on, string text = "SYNCING HEARTBEAT")
        {
            loadText.text = text;
            if (on) { loadTarget = 0; var am = loadFill.rectTransform.anchorMax; am.x = 0; loadFill.rectTransform.anchorMax = am; }
            if (loadCo != null) StopCoroutine(loadCo);
            float from = loadGroup.alpha, to = on ? 1f : 0f;
            loadCo = StartCoroutine(GH.Tween(0.25f, k => loadGroup.alpha = Mathf.Lerp(from, to, k)));
        }

        public void LoadProgress(float p) => loadTarget = Mathf.Clamp01(p);

        public void Toast(string text, Color accent, float seconds = 2.4f)
        {
            if (toastCo != null) StopCoroutine(toastCo);
            toastCo = StartCoroutine(ToastCo(text, seconds));
        }

        IEnumerator ToastCo(string text, float seconds)
        {
            toastText.text = text;
            yield return GH.Tween(0.25f, k => { toastGroup.alpha = k; toastRoot.anchoredPosition = new Vector2(0, -60 + 30 * (1 - k)); });
            yield return GH.Wait(seconds);
            yield return GH.Tween(0.3f, k => toastGroup.alpha = 1 - k);
        }

        /// <summary>Cinematic chapter title: number, huge title, ECG sweep, subtitle.</summary>
        public IEnumerator ChapterCard(Chapter ch)
        {
            var card = UI.Node("ChapterCard", root);
            UI.Fill(card);
            card.SetSiblingIndex(1);
            var g = UI.Group(card, 0);
            var bg = UI.Img(card, "Bg", Sprites.White, new Color(0.02f, 0.01f, 0.02f, 0.9f));
            UI.Fill(bg.rectTransform);
            var halo = UI.Glow(card, "Halo", Sprites.Glow, ch.Color.WithA(0.35f), UI.Add2);
            UI.At(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1600, 700));
            var num = UI.Text(card, "Num", ch.index >= 6 ? "FINALE" : $"CHAPTER {GH.Pad2(ch.index)}", UI.Label, 40, ch.Accent, TextAlignmentOptions.Center);
            UI.At(num.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1400, 60));
            num.characterSpacing = 30;
            var title = UI.Text(card, "Title", ch.title.ToUpperInvariant(), UI.Display, 128, Color.white, TextAlignmentOptions.Center);
            UI.At(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1800, 170));
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true; title.fontSizeMin = 60; title.fontSizeMax = 128;
            UI.Glowing(title, ch.Color, 0.45f);
            var ecgRt = UI.Node("ECG", card);
            UI.At(ecgRt, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(1500, 90));
            var ecg = ecgRt.gameObject.AddComponent<EcgLine>();
            ecg.material = UI.Add2;
            ecg.color = ch.Color;
            ecg.Thickness = 5;
            var sub = UI.Text(card, "Sub", ch.subtitle + "   ·   ♪ " + ch.songTitle, UI.Label, 34, UI.TextDim, TextAlignmentOptions.Center);
            UI.At(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(1400, 50));
            sub.characterSpacing = 6;
            AudioDirector.I.Sfx("stinger", 0.9f);
            yield return GH.Tween(0.35f, k => g.alpha = k);
            float t = 0;
            float dur = GH.QA && GH.Flag("fast") ? 0.6f : 2.8f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 1.3f);
                title.characterSpacing = Mathf.Lerp(40, 6, GH.EaseOutExpo(k));
                title.alpha = GH.EaseOutCubic(k * 1.5f);
                num.characterSpacing = Mathf.Lerp(60, 30, GH.EaseOutExpo(k));
                ecg.Points.Clear();
                int n = 160;
                for (int i = 0; i < n; i++)
                {
                    float x = i / (float)(n - 1);
                    if (x > k * 1.05f) break;
                    ecg.Points.Add(new Vector2(x, 0.35f + 0.6f * EcgLine.Pqrst(x * 4.5f)));
                }
                ecg.AlphaAlong = u => Mathf.Clamp01(u * 3f) * Mathf.Clamp01((1 - u) * 6f + 0.2f);
                ecg.SetDirty();
                yield return null;
            }
            yield return GH.Tween(0.4f, k => g.alpha = 1 - k);
            Destroy(card.gameObject);
        }
    }
}
