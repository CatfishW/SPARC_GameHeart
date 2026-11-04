using System.Collections.Generic;
using UnityEngine;

namespace GameHeart
{
    /// <summary>Procedural meshes: note bars, quads, the vessel tube and red blood cells.</summary>
    public static class Meshes
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static Mesh Quad(bool horizontal = false)
        {
            string key = "quad" + horizontal;
            if (cache.TryGetValue(key, out var m)) return m;
            m = new Mesh { name = key };
            if (horizontal)
                m.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f) };
            else
                m.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = horizontal ? new[] { 0, 2, 1, 2, 3, 1 } : new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        /// <summary>Extruded rounded rectangle lying in XZ (width along X, depth along Z), centred, height along +Y.</summary>
        public static Mesh RoundedBar(float width, float depth, float height, float radius, int seg = 6)
        {
            string key = $"bar{width}_{depth}_{height}_{radius}";
            if (cache.TryGetValue(key, out var m)) return m;
            radius = Mathf.Min(radius, Mathf.Min(width, depth) * 0.5f - 0.001f);
            var outline = new List<Vector2>();
            Vector2[] corners = { new Vector2(width / 2 - radius, depth / 2 - radius), new Vector2(-width / 2 + radius, depth / 2 - radius),
                                  new Vector2(-width / 2 + radius, -depth / 2 + radius), new Vector2(width / 2 - radius, -depth / 2 + radius) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90 + 90f * i / seg) * Mathf.Deg2Rad;
                    outline.Add(corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            // top fan
            verts.Add(new Vector3(0, height, 0)); norms.Add(Vector3.up); uvs.Add(new Vector2(.5f, .5f));
            foreach (var p in outline) { verts.Add(new Vector3(p.x, height, p.y)); norms.Add(Vector3.up); uvs.Add(new Vector2(p.x / width + .5f, p.y / depth + .5f)); }
            int n = outline.Count;
            for (int i = 0; i < n; i++) { tris.Add(0); tris.Add(1 + (i + 1) % n); tris.Add(1 + i); }
            // sides with a soft bevel normal
            int b = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var p = outline[i];
                var prev = outline[(i - 1 + n) % n]; var next = outline[(i + 1) % n];
                var t = (next - prev).normalized; var nrm = new Vector3(t.y, 0, -t.x);
                verts.Add(new Vector3(p.x, height, p.y)); norms.Add((nrm + Vector3.up * 0.6f).normalized); uvs.Add(new Vector2(p.x / width + .5f, 1));
                verts.Add(new Vector3(p.x, 0, p.y)); norms.Add(nrm); uvs.Add(new Vector2(p.x / width + .5f, 0));
            }
            for (int i = 0; i < n; i++)
            {
                int a0 = b + i * 2, a1 = a0 + 1, c0 = b + ((i + 1) % n) * 2, c1 = c0 + 1;
                tris.Add(a0); tris.Add(c0); tris.Add(a1);
                tris.Add(c0); tris.Add(c1); tris.Add(a1);
            }
            m = new Mesh { name = key };
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        /// <summary>Open cylinder along +Z with inward-facing geometry; uv.x = angle 0..1 (0.5 = bottom), uv.y = z 0..1.</summary>
        public static Mesh Tube(float radius, float length, int around = 64, int along = 48)
        {
            string key = $"tube{radius}_{length}";
            if (cache.TryGetValue(key, out var m)) return m;
            var v = new Vector3[(around + 1) * (along + 1)];
            var uv = new Vector2[v.Length];
            var nrm = new Vector3[v.Length];
            for (int j = 0; j <= along; j++)
                for (int i = 0; i <= around; i++)
                {
                    float a = (i / (float)around) * Mathf.PI * 2 - Mathf.PI / 2 - Mathf.PI; // bottom at uv 0.5
                    float z = length * j / along;
                    var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    v[j * (around + 1) + i] = dir * radius + new Vector3(0, 0, z);
                    nrm[j * (around + 1) + i] = -dir;
                    uv[j * (around + 1) + i] = new Vector2(i / (float)around, j / (float)along);
                }
            var t = new int[around * along * 6];
            int k = 0;
            for (int j = 0; j < along; j++)
                for (int i = 0; i < around; i++)
                {
                    int a0 = j * (around + 1) + i, a1 = a0 + 1, b0 = a0 + around + 1, b1 = b0 + 1;
                    t[k++] = a0; t[k++] = b0; t[k++] = a1;
                    t[k++] = a1; t[k++] = b0; t[k++] = b1;
                }
            m = new Mesh { name = key, vertices = v, uv = uv, normals = nrm, triangles = t };
            m.RecalculateBounds();
            cache[key] = m;
            return m;
        }

        /// <summary>Biconcave disc (red blood cell) of radius 1.</summary>
        public static Mesh BloodCell()
        {
            if (cache.TryGetValue("rbc", out var m)) return m;
            int rings = 14, seg = 28;
            var verts = new List<Vector3>(); var tris = new List<int>();
            // profile: thickness h(r) of a biconcave disc
            for (int side = 0; side < 2; side++)
            {
                int start = verts.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float x = r / (float)rings;
                    float h = 0.5f * Mathf.Sqrt(Mathf.Max(0, 1 - x * x)) * (0.14f + 2.0f * x * x - 1.12f * x * x * x * x);
                    h = Mathf.Max(h, 0.0f) + 0.02f;
                    for (int s = 0; s < seg; s++)
                    {
                        float a = s / (float)seg * Mathf.PI * 2;
                        verts.Add(new Vector3(Mathf.Cos(a) * x, side == 0 ? h : -h, Mathf.Sin(a) * x));
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < seg; s++)
                    {
                        int a0 = start + r * seg + s, a1 = start + r * seg + (s + 1) % seg;
                        int b0 = a0 + seg, b1 = a1 + seg;
                        if (side == 0) { tris.Add(a0); tris.Add(b0); tris.Add(a1); tris.Add(a1); tris.Add(b0); tris.Add(b1); }
                        else { tris.Add(a0); tris.Add(a1); tris.Add(b0); tris.Add(a1); tris.Add(b1); tris.Add(b0); }
                    }
            }
            // stitch rims
            int top = rings * seg, bot = (rings + 1) * seg + rings * seg;
            for (int s = 0; s < seg; s++)
            {
                int a0 = top + s, a1 = top + (s + 1) % seg, b0 = bot + s, b1 = bot + (s + 1) % seg;
                tris.Add(a0); tris.Add(a1); tris.Add(b0); tris.Add(a1); tris.Add(b1); tris.Add(b0);
            }
            m = new Mesh { name = "rbc" };
            m.SetVertices(verts); m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            cache["rbc"] = m;
            return m;
        }

        /// <summary>Spiky sphere for germs.</summary>
        public static Mesh Germ()
        {
            if (cache.TryGetValue("germ", out var m)) return m;
            int lat = 24, lon = 32;
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int a = 0; a <= lat; a++)
                for (int b = 0; b <= lon; b++)
                {
                    float th = Mathf.PI * a / lat, ph = 2 * Mathf.PI * b / lon;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    float spike = Mathf.Pow(Mathf.Abs(Mathf.Sin(d.x * 9) * Mathf.Sin(d.y * 9) * Mathf.Sin(d.z * 9)), 3) * 0.35f;
                    v.Add(d * (0.5f + spike));
                }
            for (int a = 0; a < lat; a++)
                for (int b = 0; b < lon; b++)
                {
                    int i0 = a * (lon + 1) + b, i1 = i0 + 1, j0 = i0 + lon + 1, j1 = j0 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(j0);
                    tris.Add(i1); tris.Add(j1); tris.Add(j0);
                }
            m = new Mesh { name = "germ" };
            m.SetVertices(v); m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            cache["germ"] = m;
            return m;
        }
    }

    /// <summary>Runtime textures for the 3D stage.</summary>
    public static class StageTex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Texture2D Make(string key, int w, int h, System.Func<float, float, Color> f, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            if (cache.TryGetValue(key, out var t)) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = wrap, name = key };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = f((x + .5f) / w, (y + .5f) / h);
            t.SetPixels(px);
            t.Apply(true, true);
            cache[key] = t;
            return t;
        }

        public static Texture2D Glow => Make("tglow", 128, 128, (u, v) =>
        {
            float d = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2);
            return new Color(1, 1, 1, d * d * d);
        });

        public static Texture2D Ring => Make("tring", 256, 256, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2;
            float a = Mathf.Exp(-Mathf.Pow((d - 0.86f) * 16f, 2)) + 0.25f * Mathf.Exp(-Mathf.Pow((d - 0.86f) * 5f, 2));
            return new Color(1, 1, 1, Mathf.Clamp01(a) * (d < 1 ? 1 : 0));
        });

        public static Texture2D Column => Make("tcolumn", 64, 256, (u, v) =>
        {
            float x = 1 - Mathf.Abs(u - .5f) * 2;
            float a = Mathf.Pow(x, 2.5f) * Mathf.Pow(1 - v, 1.6f);
            return new Color(1, 1, 1, a);
        });

        public static Texture2D Line => Make("tline", 256, 32, (u, v) =>
        {
            float y = 1 - Mathf.Abs(v - .5f) * 2;
            float x = Mathf.Clamp01(Mathf.Min(u, 1 - u) * 12);
            return new Color(1, 1, 1, Mathf.Pow(y, 3) * x);
        });

        public static Texture2D ReceptorFrame => Make("treceptor", 128, 64, (u, v) =>
        {
            float px = Mathf.Min(u, 1 - u) * 128, py = Mathf.Min(v, 1 - v) * 64;
            float edge = Mathf.Min(px, py);
            float a = Mathf.Exp(-Mathf.Pow((edge - 4) / 2.2f, 2)) + 0.12f * Mathf.Clamp01(1 - edge / 30f);
            return new Color(1, 1, 1, Mathf.Clamp01(a));
        });

        public static Texture2D Flow => Make("tflow", 64, 256, (u, v) =>
        {
            float x = 1 - Mathf.Abs(u - .5f) * 2;
            float stripe = 0.55f + 0.45f * Mathf.Sin(v * Mathf.PI * 2 * 6);
            float core = Mathf.Pow(x, 0.8f);
            return new Color(1, 1, 1, Mathf.Clamp01(core * (0.35f + 0.4f * stripe) + Mathf.Pow(x, 6) * 0.4f));
        }, TextureWrapMode.Repeat);

        public static Texture2D Spark => Make("tspark", 128, 128, (u, v) =>
        {
            float x = Mathf.Abs(u - .5f) * 2, y = Mathf.Abs(v - .5f) * 2;
            float star = Mathf.Max(Mathf.Clamp01(1 - x * 9) * Mathf.Clamp01(1 - y), Mathf.Clamp01(1 - y * 9) * Mathf.Clamp01(1 - x));
            float core = Mathf.Clamp01(1 - Mathf.Sqrt(x * x + y * y) * 1.6f);
            return new Color(1, 1, 1, Mathf.Clamp01(star * star + core * core * core));
        });

        public static Texture2D Heart => Make("theart", 128, 128, (u, v) =>
        {
            float x = (u - .5f) * 2.6f, y = (v - .45f) * 2.6f;
            float a = x * x + y * y - 1;
            float f = a * a * a - x * x * y * y * y;
            float k = Mathf.Clamp01(-f * 6);
            return new Color(1, 1, 1, Mathf.Clamp01(k));
        });

        public static Texture2D Snow => Make("tsnow", 64, 64, (u, v) =>
        {
            float d = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2);
            return new Color(1, 1, 1, d * d);
        });
    }
}
