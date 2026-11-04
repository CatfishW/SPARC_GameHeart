using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameHeart
{
    /// <summary>Procedurally drawn, anti-aliased sprites so the UI needs no authored textures.</summary>
    public static class Sprites
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Sprite Make(string key, int w, int h, Func<float, float, float> alpha, Vector4 border = default, float ppu = 100)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            cache[key] = s;
            return s;
        }

        static float RoundRectSdf(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2) - (w / 2 - r);
            float qy = Mathf.Abs(y - h / 2) - (h / 2 - r);
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        /// <summary>9-sliced filled rounded rectangle.</summary>
        public static Sprite Round(int r = 16)
        {
            int n = r * 2 + 8;
            return Make("round" + r, n, n, (x, y) => 0.5f - RoundRectSdf(x, y, n, n, r), new Vector4(r + 3, r + 3, r + 3, r + 3));
        }

        /// <summary>9-sliced rounded outline.</summary>
        public static Sprite Outline(int r = 16, float t = 2f)
        {
            int n = r * 2 + 8;
            return Make($"outline{r}_{t}", n, n, (x, y) =>
            {
                float d = RoundRectSdf(x, y, n, n, r);
                return Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(t + 0.5f + d);
            }, new Vector4(r + 3, r + 3, r + 3, r + 3));
        }

        /// <summary>Soft rounded shadow/glow, 9-sliced so it hugs any panel.</summary>
        public static Sprite SoftRound(int r = 24, int blur = 24)
        {
            int n = (r + blur) * 2 + 8;
            return Make($"soft{r}_{blur}", n, n, (x, y) =>
            {
                float d = RoundRectSdf(x, y, n, n, r + blur / 2f);
                float k = Mathf.Clamp01(-d / blur + 0.5f);
                return k * k * (3 - 2 * k);
            }, new Vector4(r + blur + 3, r + blur + 3, r + blur + 3, r + blur + 3));
        }

        public static Sprite Glow => Make("glow", 128, 128, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) / 64f;
            float a = Mathf.Clamp01(1 - d);
            return a * a * a;
        });

        public static Sprite Circle => Make("circle", 128, 128, (x, y) => 62f - Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) + 0.5f);

        public static Sprite Ring(float thickness = 6f) => Make("ring" + thickness, 256, 256, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128));
            return Mathf.Clamp01(124 - d + 0.5f) * Mathf.Clamp01(d - (124 - thickness) + 0.5f);
        });

        public static Sprite SoftRing => Make("softring", 256, 256, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128)) / 128f;
            return Mathf.Exp(-Mathf.Pow((d - 0.82f) * 9f, 2));
        });

        public static Sprite LineH => Make("lineh", 256, 32, (x, y) =>
        {
            float vy = 1 - Mathf.Abs(y - 16) / 16f;
            float vx = Mathf.Clamp01(Mathf.Min(x, 256 - x) / 40f);
            return vy * vy * vx;
        });

        public static Sprite GradientV => Make("gradv", 4, 64, (x, y) => y / 64f);
        public static Sprite GradientH => Make("gradh", 64, 4, (x, y) => x / 64f);
        public static Sprite White => Make("white", 4, 4, (x, y) => 1);

        public static Sprite Vignette => Make("vignette", 256, 256, (x, y) =>
        {
            float dx = (x - 128) / 128f, dy = (y - 128) / 128f;
            float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
            return Mathf.Clamp01((d - 0.55f) / 0.6f);
        });

        public static Sprite Heart => Make("heart", 256, 256, (x, y) =>
        {
            float u = (x - 128) / 100f, v = (y - 118) / 100f;
            float a = u * u + v * v - 1;
            float f = a * a * a - u * u * v * v * v;
            return f < 0 ? Mathf.Clamp01(-f * 40f + 0.5f) : Mathf.Clamp01(0.5f - f * 40f);
        });

        public static Sprite Diamond => Make("diamond", 128, 128, (x, y) => 60 - (Mathf.Abs(x - 64) + Mathf.Abs(y - 64)) + 0.5f);

        public static Sprite Chevron => Make("chevron", 128, 128, (x, y) =>
        {
            float u = x - 64, v = y - 64;
            float d = Mathf.Abs(v - (-Mathf.Abs(u) * 0.9f + 22));
            return Mathf.Clamp01(12 - d) * Mathf.Clamp01(50 - Mathf.Abs(u));
        });

        public static Sprite Spark => Make("spark", 128, 128, (x, y) =>
        {
            float u = Mathf.Abs(x - 64) / 64f, v = Mathf.Abs(y - 64) / 64f;
            float star = Mathf.Max(Mathf.Clamp01(1 - u * 8) * Mathf.Clamp01(1 - v), Mathf.Clamp01(1 - v * 8) * Mathf.Clamp01(1 - u));
            float core = Mathf.Clamp01(1 - Mathf.Sqrt(u * u + v * v) * 2.2f);
            return Mathf.Max(star * star, core * core);
        });

        public static Sprite Snow => Make("snow", 64, 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 32f;
            return Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d) * 1.4f;
        });

        public static Sprite Germ => Make("germ", 128, 128, (x, y) =>
        {
            float u = x - 64, v = y - 64;
            float r = Mathf.Sqrt(u * u + v * v);
            float ang = Mathf.Atan2(v, u);
            float edge = 34 + 9 * Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 5)), 6);
            return Mathf.Clamp01(edge - r + 0.5f) * (0.55f + 0.45f * Mathf.Clamp01(r / 34f));
        });
    }

    /// <summary>Fonts, colours and widget builders.</summary>
    public static class UI
    {
        public static TMP_FontAsset Display, DisplayBold, Label, Body, BodyLight, BodyBold;
        public static Material Add1, Add2, Add4;

        public static readonly Color Ink = new Color(0.035f, 0.02f, 0.04f, 1f);
        public static readonly Color Glass = new Color(0.06f, 0.03f, 0.06f, 0.78f);
        public static readonly Color GlassHi = new Color(0.12f, 0.06f, 0.1f, 0.88f);
        public static readonly Color Fg = new Color(0.97f, 0.94f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.78f, 0.7f, 0.74f, 1f);
        public static readonly Color Crimson = GH.Hex("#FF3D5E");
        public static readonly Color Coral = GH.Hex("#FF8A80");
        public static readonly Color Teal = GH.Hex("#3FE0D0");
        public static readonly Color Gold = GH.Hex("#FFD36E");
        public static readonly Color Good = GH.Hex("#6BFF9E");
        public static readonly Color Bad = GH.Hex("#FF4D4D");

        public static Color Accent = Crimson;

        public static void Init()
        {
            Display = Resources.Load<TMP_FontAsset>("Fonts/Display");
            DisplayBold = Resources.Load<TMP_FontAsset>("Fonts/DisplayBold");
            Label = Resources.Load<TMP_FontAsset>("Fonts/Label");
            Body = Resources.Load<TMP_FontAsset>("Fonts/Body");
            BodyLight = Resources.Load<TMP_FontAsset>("Fonts/BodyLight");
            BodyBold = Resources.Load<TMP_FontAsset>("Fonts/BodyBold");
            var sh = Shader.Find("GameHeart/UIAdditive");
            Add1 = new Material(sh) { name = "UIAdd1" }; Add1.SetFloat("_Intensity", 1f);
            Add2 = new Material(sh) { name = "UIAdd2" }; Add2.SetFloat("_Intensity", 2.2f);
            Add4 = new Material(sh) { name = "UIAdd4" }; Add4.SetFloat("_Intensity", 4.5f);
        }

        // ------------------------------------------------------------------ layout
        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Fill(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform At(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        // ------------------------------------------------------------------ primitives
        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Glow(Transform parent, string name, Sprite sprite, Color color, Material mat = null)
        {
            var img = Img(parent, name, sprite, color);
            img.material = mat ?? Add2;
            return img;
        }

        public static RawImage Raw(Transform parent, string name, Texture tex, Color color)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font ?? Body;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static readonly Dictionary<string, Material> glowPresets = new Dictionary<string, Material>();

        public static TextMeshProUGUI Glowing(TextMeshProUGUI t, Color glow, float power = 0.35f)
        {
            if (t.font == null) return t;
            string key = t.font.name + " Glow";
            if (!glowPresets.TryGetValue(key, out var preset)) glowPresets[key] = preset = Resources.Load<Material>("Fonts/" + key);
            if (preset == null) return t;
            t.fontSharedMaterial = preset;
            var m = t.fontMaterial;
            m.SetColor(ShaderUtilities.ID_GlowColor, glow);
            m.SetFloat(ShaderUtilities.ID_GlowPower, power);
            m.SetFloat(ShaderUtilities.ID_GlowOuter, 0.6f);
            return t;
        }

        public static CanvasGroup Group(Component c, float alpha = 1)
        {
            var g = c.GetComponent<CanvasGroup>() ?? c.gameObject.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            return g;
        }

        /// <summary>Dark glass panel with soft shadow and thin accent border.</summary>
        public static RectTransform Panel(Transform parent, string name, Color accent, float radius = 18, float borderAlpha = 0.55f)
        {
            var rt = Node(name, parent);
            var shadow = Img(rt, "Shadow", Sprites.SoftRound(24, 32), new Color(0, 0, 0, 0.55f));
            Fill(shadow.rectTransform, -34, -40, -34, -28);
            var bg = Img(rt, "Glass", Sprites.Round((int)radius), Glass);
            Fill(bg.rectTransform);
            var sheen = Img(rt, "Sheen", Sprites.GradientV, new Color(1, 1, 1, 0.045f));
            Fill(sheen.rectTransform, radius * 0.6f, 0, radius * 0.6f, 2);
            sheen.rectTransform.anchorMin = new Vector2(0, 0.55f);
            var border = Img(rt, "Border", Sprites.Outline((int)radius, 2f), accent.WithA(borderAlpha));
            Fill(border.rectTransform);
            return rt;
        }

        public static GHButton Button(Transform parent, string label, Color accent, Action onClick, float fontSize = 30, TMP_FontAsset font = null)
        {
            var rt = Node("Button " + label, parent);
            var b = rt.gameObject.AddComponent<GHButton>();
            b.Build(label, accent, fontSize, font ?? Label);
            b.OnClick = onClick;
            return b;
        }

        public static void Kill(Component c) { if (c != null) UnityEngine.Object.Destroy(c.gameObject); }
    }

    /// <summary>Animated glass button with hover glow, keyboard focus and click sound.</summary>
    public class GHButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public Action OnClick;
        public TextMeshProUGUI LabelText;
        public Image Bg, Border, Halo;
        public RectTransform Content;
        Color accent;
        float hover, press, punch;
        public bool Highlighted;
        public string HoverSfx = "ui_hover", ClickSfx = "ui_click";

        public void Build(string label, Color accent, float fontSize, TMP_FontAsset font)
        {
            this.accent = accent;
            var rt = (RectTransform)transform;
            Halo = UI.Glow(rt, "Halo", Sprites.SoftRound(20, 26), accent.WithA(0f), UI.Add2);
            UI.Fill(Halo.rectTransform, -30, -30, -30, -30);
            Bg = UI.Img(rt, "Bg", Sprites.Round(14), UI.GlassHi, true);
            UI.Fill(Bg.rectTransform);
            Border = UI.Img(rt, "Border", Sprites.Outline(14, 2f), accent.WithA(0.7f));
            UI.Fill(Border.rectTransform);
            Content = UI.Node("Content", rt);
            UI.Fill(Content);
            LabelText = UI.Text(Content, "Label", label, font, fontSize, UI.Fg, TextAlignmentOptions.Center);
            UI.Fill(LabelText.rectTransform, 16, 4, 16, 4);
            LabelText.characterSpacing = 4;
            targetGraphic = Bg;
            transition = Transition.None;
            var nav = navigation; nav.mode = Navigation.Mode.Automatic; navigation = nav;
        }

        public void SetAccent(Color c) { accent = c; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool sel = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            float target = Highlighted ? 1 : (IsHighlightedOrSelected() || sel) && IsInteractable() ? 1 : 0;
            hover = GH.Damp(hover, target, 14, dt);
            press = GH.Damp(press, IsPressed() ? 1 : 0, 25, dt);
            punch = GH.Damp(punch, 0, 9, dt);
            float s = 1 + hover * 0.035f - press * 0.04f + punch * 0.06f;
            Content.localScale = Vector3.one * s;
            Bg.rectTransform.localScale = Vector3.one * s;
            Border.rectTransform.localScale = Vector3.one * s;
            Border.color = accent.WithA((IsInteractable() || Highlighted ? 0.55f : 0.2f) + hover * 0.45f);
            Bg.color = Color.Lerp(UI.GlassHi, Color.Lerp(UI.GlassHi, accent * 0.45f, 0.6f).WithA(0.92f), hover);
            Halo.color = accent.WithA(hover * 0.35f + punch * 0.4f);
            LabelText.color = IsInteractable() || Highlighted ? Color.Lerp(UI.TextDim, Color.white, 0.4f + hover * 0.6f) : UI.TextDim.WithA(0.5f);
        }

        bool IsHighlightedOrSelected() => currentSelectionState == SelectionState.Highlighted || currentSelectionState == SelectionState.Selected;

        public override void OnPointerEnter(PointerEventData e)
        {
            base.OnPointerEnter(e);
            if (IsInteractable() && AudioDirector.I != null) AudioDirector.I.Sfx(HoverSfx, 0.35f);
        }

        public override void OnSelect(BaseEventData e)
        {
            base.OnSelect(e);
            if (e is AxisEventData && AudioDirector.I != null) AudioDirector.I.Sfx(HoverSfx, 0.35f);
        }

        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Fire(); }
        public void OnSubmit(BaseEventData e) => Fire();

        public void Fire()
        {
            if (!IsActive() || !IsInteractable()) return;
            punch = 1;
            if (AudioDirector.I != null) AudioDirector.I.Sfx(ClickSfx, 0.7f);
            OnClick?.Invoke();
        }
    }

    /// <summary>Base for full-screen UI screens with fade in/out.</summary>
    public abstract class GHScreen : MonoBehaviour
    {
        public RectTransform Root;
        public CanvasGroup Group;

        public static T Create<T>(Transform canvas, string name) where T : GHScreen
        {
            var rt = UI.Node(name, canvas);
            UI.Fill(rt);
            var s = rt.gameObject.AddComponent<T>();
            s.Root = rt;
            s.Group = UI.Group(rt, 0);
            return s;
        }

        public IEnumerator FadeIn(float d = 0.35f)
        {
            Group.blocksRaycasts = true;
            yield return GH.Tween(d, k => Group.alpha = k);
        }

        public IEnumerator FadeOut(float d = 0.3f, bool destroy = true)
        {
            Group.blocksRaycasts = false;
            float a = Group.alpha;
            yield return GH.Tween(d, k => Group.alpha = a * (1 - k));
            if (destroy) Destroy(gameObject);
        }

        public static void Focus(Selectable s)
        {
            if (s != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }
    }
}
