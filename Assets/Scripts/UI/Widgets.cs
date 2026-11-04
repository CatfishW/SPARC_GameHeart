using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameHeart
{
    /// <summary>Glowing polyline (ECG trace). Points are normalised 0..1 in the rect.</summary>
    public class EcgLine : MaskableGraphic
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        public float Thickness = 4f;
        public float HeadGlow = 1f;
        public Func<float, float> AlphaAlong; // 0..1 along the line -> alpha

        public override Texture mainTexture => Sprites.LineH.texture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Points.Count < 2) return;
            var r = rectTransform.rect;
            Vector2 P(int i) => new Vector2(r.xMin + Points[i].x * r.width, r.yMin + Points[i].y * r.height);
            float half = Thickness * 0.5f;
            int n = Points.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = P(i);
                Vector2 dir = i == 0 ? P(1) - P(0) : i == n - 1 ? P(n - 1) - P(n - 2) : P(i + 1) - P(i - 1);
                dir = dir.sqrMagnitude < 1e-6f ? Vector2.right : dir.normalized;
                var nrm = new Vector2(-dir.y, dir.x) * half;
                float a = AlphaAlong != null ? AlphaAlong(i / (float)(n - 1)) : 1f;
                var c = color; c.a *= a;
                float u = i / (float)(n - 1);
                vh.AddVert(p - nrm, c, new Vector2(0.5f, 0));
                vh.AddVert(p + nrm, c, new Vector2(0.5f, 1));
                if (i > 0)
                {
                    int b = i * 2;
                    vh.AddTriangle(b - 2, b - 1, b + 1);
                    vh.AddTriangle(b - 2, b + 1, b);
                }
            }
        }

        public void SetDirty() => SetVerticesDirty();

        /// <summary>Classic PQRST complex sampled at x in [0,1) of one beat.</summary>
        public static float Pqrst(float x)
        {
            x = x - Mathf.Floor(x);
            float y = 0;
            y += 0.10f * Mathf.Exp(-Mathf.Pow((x - 0.18f) / 0.035f, 2));   // P
            y -= 0.12f * Mathf.Exp(-Mathf.Pow((x - 0.29f) / 0.010f, 2));   // Q
            y += 1.00f * Mathf.Exp(-Mathf.Pow((x - 0.31f) / 0.012f, 2));   // R
            y -= 0.28f * Mathf.Exp(-Mathf.Pow((x - 0.335f) / 0.012f, 2));  // S
            y += 0.22f * Mathf.Exp(-Mathf.Pow((x - 0.52f) / 0.05f, 2));    // T
            return y;
        }
    }

    /// <summary>Slow camera moves over a still (RawImage uvRect), Ken Burns style.</summary>
    public class KenBurns : MonoBehaviour
    {
        public RawImage Target;
        public string Move = "drift";
        public float Duration = 12f;
        float t;
        public float ImageAspect = 16f / 9f;

        public void Restart(string move, float duration) { Move = move; Duration = Mathf.Max(4, duration); t = 0; }

        void Update()
        {
            if (Target == null) return;
            t += Time.unscaledDeltaTime;
            float k = GH.EaseInOutCubic(Mathf.Clamp01(t / Duration)) * 0.85f + Mathf.Clamp01(t / Duration) * 0.15f;
            float zoom = 1.06f, ox = 0, oy = 0;
            switch (Move)
            {
                case "push": zoom = Mathf.Lerp(1.02f, 1.16f, k); break;
                case "pull": zoom = Mathf.Lerp(1.16f, 1.03f, k); break;
                case "panL": zoom = 1.12f; ox = Mathf.Lerp(0.045f, -0.045f, k); break;
                case "panR": zoom = 1.12f; ox = Mathf.Lerp(-0.045f, 0.045f, k); break;
                case "rise": zoom = Mathf.Lerp(1.14f, 1.08f, k); oy = Mathf.Lerp(-0.04f, 0.04f, k); break;
                case "sink": zoom = Mathf.Lerp(1.06f, 1.14f, k); oy = Mathf.Lerp(0.035f, -0.03f, k); break;
                default: zoom = 1.07f + 0.02f * Mathf.Sin(t * 0.25f); ox = 0.012f * Mathf.Sin(t * 0.17f); oy = 0.01f * Mathf.Cos(t * 0.13f); break;
            }
            // cover-fit for the screen aspect
            var rt = Target.rectTransform.rect;
            float screenAspect = rt.height > 0 ? rt.width / rt.height : 16f / 9f;
            float w = 1f / zoom, h = 1f / zoom;
            if (screenAspect > ImageAspect) h *= ImageAspect / screenAspect; else w *= screenAspect / ImageAspect;
            float cx = 0.5f + ox, cy = 0.5f + oy;
            cx = Mathf.Clamp(cx, w / 2, 1 - w / 2);
            cy = Mathf.Clamp(cy, h / 2, 1 - h / 2);
            Target.uvRect = new Rect(cx - w / 2, cy - h / 2, w, h);
        }
    }

    /// <summary>Lightweight 2D particle layer for cutscenes and menus.</summary>
    public class UIParticles : MonoBehaviour
    {
        class P { public RectTransform rt; public Image img; public Vector2 pos, vel; public float life, max, size, spin, phase; }
        readonly List<P> ps = new List<P>();
        string mode;
        RectTransform area;
        float density = 1f;

        public static UIParticles Create(Transform parent, string mode, float density = 1f)
        {
            var rt = UI.Node("FX " + mode, parent);
            UI.Fill(rt);
            var p = rt.gameObject.AddComponent<UIParticles>();
            p.area = rt;
            p.density = density;
            p.SetMode(mode);
            return p;
        }

        public void SetMode(string m)
        {
            mode = m ?? "";
            foreach (var p in ps) Destroy(p.rt.gameObject);
            ps.Clear();
            int n = mode switch { "snow" => 90, "embers" => 60, "germs" => 18, "stars" => 70, "cells" => 22, "sparks" => 40, "confetti" => 90, _ => 0 };
            n = Mathf.RoundToInt(n * density * (Save.S.highQuality ? 1f : 0.6f));
            for (int i = 0; i < n; i++) ps.Add(Spawn(true));
        }

        P Spawn(bool anywhere)
        {
            var p = new P();
            Sprite s; Material mat = UI.Add2;
            Color c;
            switch (mode)
            {
                case "snow": s = Sprites.Snow; c = new Color(0.9f, 0.95f, 1f, 0.8f); p.size = UnityEngine.Random.Range(6f, 22f); break;
                case "embers": s = Sprites.Glow; c = new Color(1f, 0.55f, 0.2f, 0.9f); p.size = UnityEngine.Random.Range(6f, 16f); break;
                case "germs": s = Sprites.Germ; c = new Color(0.45f, 1f, 0.4f, 0.55f); p.size = UnityEngine.Random.Range(40f, 110f); mat = UI.Add1; break;
                case "stars": s = Sprites.Spark; c = new Color(0.85f, 0.8f, 1f, 0.9f); p.size = UnityEngine.Random.Range(8f, 26f); break;
                case "cells": s = Sprites.Circle; c = new Color(0.9f, 0.15f, 0.2f, 0.35f); p.size = UnityEngine.Random.Range(40f, 120f); mat = null; break;
                case "confetti": s = Sprites.White; c = Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 1f); p.size = UnityEngine.Random.Range(10f, 18f); mat = null; break;
                default: s = Sprites.Spark; c = new Color(1f, 0.8f, 0.85f, 0.85f); p.size = UnityEngine.Random.Range(10f, 30f); break;
            }
            p.img = UI.Img(area, "p", s, c);
            if (mat != null) p.img.material = mat;
            p.rt = p.img.rectTransform;
            p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
            p.rt.sizeDelta = mode == "confetti" ? new Vector2(p.size, p.size * 0.5f) : new Vector2(p.size, p.size);
            var r = area.rect;
            float W = Mathf.Max(r.width, 1920), H = Mathf.Max(r.height, 1080);
            p.max = UnityEngine.Random.Range(5f, 11f);
            p.life = anywhere ? UnityEngine.Random.Range(0, p.max) : 0;
            p.phase = UnityEngine.Random.value * 10;
            p.spin = UnityEngine.Random.Range(-120f, 120f);
            switch (mode)
            {
                case "snow": p.pos = new Vector2(UnityEngine.Random.Range(-W, W) * 0.6f, anywhere ? UnityEngine.Random.Range(-H, H) * 0.5f : H * 0.55f); p.vel = new Vector2(UnityEngine.Random.Range(-60, 20), -UnityEngine.Random.Range(60, 160)); break;
                case "embers": p.pos = new Vector2(UnityEngine.Random.Range(-W, W) * 0.55f, anywhere ? UnityEngine.Random.Range(-H, H) * 0.5f : -H * 0.55f); p.vel = new Vector2(UnityEngine.Random.Range(-30, 30), UnityEngine.Random.Range(60, 180)); break;
                case "confetti": p.pos = new Vector2(UnityEngine.Random.Range(-W, W) * 0.55f, anywhere ? UnityEngine.Random.Range(0, H) * 0.5f : H * 0.55f); p.vel = new Vector2(UnityEngine.Random.Range(-60, 60), -UnityEngine.Random.Range(120, 260)); break;
                default: p.pos = new Vector2(UnityEngine.Random.Range(-W, W) * 0.5f, UnityEngine.Random.Range(-H, H) * 0.5f); p.vel = UnityEngine.Random.insideUnitCircle * 25f; break;
            }
            p.rt.anchoredPosition = p.pos;
            return p;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var r = area.rect;
            float W = Mathf.Max(r.width, 1920) * 0.6f, H = Mathf.Max(r.height, 1080) * 0.6f;
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                p.life += dt;
                p.phase += dt;
                var v = p.vel;
                if (mode == "snow" || mode == "confetti") v.x += Mathf.Sin(p.phase * 1.3f) * 40f;
                p.pos += v * dt;
                p.rt.anchoredPosition = p.pos;
                p.rt.localRotation = Quaternion.Euler(0, 0, p.spin * p.life);
                float fade = Mathf.Clamp01(p.life / 0.8f) * Mathf.Clamp01((p.max - p.life) / 1.2f);
                if (mode == "stars") fade *= 0.5f + 0.5f * Mathf.Sin(p.phase * 3f);
                var c = p.img.color; c.a = Mathf.Lerp(c.a, fade * (mode == "germs" ? 0.55f : mode == "cells" ? 0.35f : 0.9f), 0.2f);
                p.img.color = c;
                if (p.life > p.max || Mathf.Abs(p.pos.x) > W || Mathf.Abs(p.pos.y) > H)
                {
                    Destroy(p.rt.gameObject);
                    ps[i] = Spawn(false);
                }
            }
        }
    }
}
